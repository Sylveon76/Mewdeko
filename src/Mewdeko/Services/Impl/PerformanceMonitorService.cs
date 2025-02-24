using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Mewdeko.Common.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Service for monitoring and reporting CPU-intensive operations in the bot to Prometheus/Grafana.
    /// Can be enabled/disabled via BotCredentials.
    /// </summary>
    public class PerformanceMonitorService : BackgroundService, INService
    {
        private readonly IServiceProvider services;
        private readonly BotCredentials credentials;
        private readonly NonBlocking.ConcurrentDictionary<string, PerformanceEntry> performanceEntries = new();
        private readonly NonBlocking.ConcurrentDictionary<string, Stopwatch> activeOperations = new();
        private readonly TimeSpan reportingInterval = TimeSpan.FromSeconds(15);
        private readonly TimeSpan operationThreshold = TimeSpan.FromMilliseconds(100);
        private readonly KestrelMetricServer metricsServer;

        // Prometheus metrics
        private readonly Counter operationCounter;
        private readonly Histogram operationDuration;
        private readonly Gauge cpuUsage;
        private readonly Gauge memoryUsage;
        private readonly Gauge guildCount;
        private readonly Gauge userCount;
        private readonly Gauge shardCount;
        private readonly Gauge uptime;
        private readonly Gauge commandsProcessed;
        private readonly Gauge messagesReceived;

        /// <summary>
        /// Initializes a new instance of the PerformanceMonitorService class.
        /// </summary>
        public PerformanceMonitorService(
            IServiceProvider services,
            BotCredentials credentials)
        {
            this.services = services;
            this.credentials = credentials;
            var metricPort1 = credentials.MetricsPort > 0 ? credentials.MetricsPort : 9090;

            if (!credentials.EnableMetrics)
            {
                Log.Information("Performance metrics are disabled. Enable them in credentials to use Prometheus/Grafana monitoring.");
                return;
            }

            // Initialize Prometheus metrics
            operationCounter = Metrics.CreateCounter(
                "mewdeko_operations_total",
                "Total number of operations executed",
                new CounterConfiguration
                {
                    LabelNames = new[] { "category", "operation" }
                });

            operationDuration = Metrics.CreateHistogram(
                "mewdeko_operation_duration_milliseconds",
                "Duration of operations in milliseconds",
                new HistogramConfiguration
                {
                    LabelNames = ["category", "operation"],
                    Buckets = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
                });

            cpuUsage = Metrics.CreateGauge(
                "mewdeko_cpu_usage_percent",
                "CPU usage of the Mewdeko process");

            memoryUsage = Metrics.CreateGauge(
                "mewdeko_memory_usage_megabytes",
                "Memory usage of the Mewdeko process in MB");

            guildCount = Metrics.CreateGauge(
                "mewdeko_guild_count",
                "Number of guilds the bot is connected to");

            userCount = Metrics.CreateGauge(
                "mewdeko_user_count",
                "Number of users the bot can see");

            shardCount = Metrics.CreateGauge(
                "mewdeko_shard_count",
                "Number of shards the bot is using");

            uptime = Metrics.CreateGauge(
                "mewdeko_uptime_seconds",
                "Bot uptime in seconds");

            commandsProcessed = Metrics.CreateGauge(
                "mewdeko_commands_processed_total",
                "Total number of commands processed");

            messagesReceived = Metrics.CreateGauge(
                "mewdeko_messages_received_total",
                "Total number of messages received");

            // Start the Prometheus metrics server
            try
            {
                metricsServer = new KestrelMetricServer(port: metricPort1);
                metricsServer.Start();
                Log.Information("Prometheus metrics server started on port {MetricPort}", metricPort1);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start metrics server on port {MetricPort}. Metrics will not be available.", metricPort1);
            }
        }

        /// <summary>
        /// Begins tracking an operation's performance.
        /// </summary>
        public IDisposable TrackOperation(string operationName, string category = "General")
        {
            if (!credentials.EnableMetrics)
                return new NullTracker();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var fullOperationKey = $"{category}:{operationName}";
            activeOperations[fullOperationKey] = stopwatch;

            return new OperationTracker(this, fullOperationKey, category, operationName);
        }

        /// <summary>
        /// Increments the command processed counter.
        /// </summary>
        public void IncrementCommandsProcessed()
        {
            if (!credentials.EnableMetrics)
                return;

            commandsProcessed.Inc();
        }

        /// <summary>
        /// Increments the messages received counter.
        /// </summary>
        public void IncrementMessagesReceived()
        {
            if (!credentials.EnableMetrics)
                return;

            messagesReceived.Inc();
        }

        /// <summary>
        /// Gets the current top CPU intensive operations.
        /// </summary>
        public List<PerformanceEntry> GetTopOperations(int count = 10)
        {
            return performanceEntries.Values
                .OrderByDescending(x => x.AverageExecutionTimeMs)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Calculates CPU usage for the current process.
        /// </summary>
        public float GetCurrentCpuUsage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;

                Thread.Sleep(100);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                return (float)(cpuUsageTotal * 100);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calculating CPU usage");
                return 0;
            }
        }

        /// <summary>
        /// Implements the background service execution logic.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!credentials.EnableMetrics)
            {
                // If metrics are disabled, keep the service alive but don't do anything
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                return;
            }

            try
            {
                // Wait for the client to be ready
                var mewdeko = services.GetRequiredService<Mewdeko>();
                await mewdeko.Ready.Task;

                var client = services.GetRequiredService<DiscordShardedClient>();
                var startTime = Process.GetCurrentProcess().StartTime;

                // Setup message received counter
                client.MessageReceived += _ => {
                    IncrementMessagesReceived();
                    return Task.CompletedTask;
                };

                // Main monitoring loop
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Update general system metrics
                        var cpuUsage = GetCurrentCpuUsage();
                        this.cpuUsage.Set(cpuUsage);

                        var memoryUsageMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
                        memoryUsage.Set(memoryUsageMb);

                        guildCount.Set(client.Guilds.Count);
                        userCount.Set(client.Guilds.Sum(g => g.MemberCount));
                        shardCount.Set(client.Shards.Count);

                        var uptimeSeconds = (DateTime.UtcNow - startTime.ToUniversalTime()).TotalSeconds;
                        uptime.Set(uptimeSeconds);

                        // Log stats at longer intervals
                        Log.Debug("Performance Stats: CPU {CpuUsage:F1}%, Memory {MemoryMb}MB, Guilds {GuildCount}, Shards {ShardCount}",
                            cpuUsage, memoryUsageMb, client.Guilds.Count, client.Shards.Count);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in performance monitoring loop");
                    }

                    await Task.Delay(reportingInterval, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fatal error in PerformanceMonitorService");
            }
        }

        /// <summary>
        /// Ends tracking an operation and records its execution time.
        /// </summary>
        internal void EndOperation(string operationKey, string category, string operationName)
        {
            if (!credentials.EnableMetrics)
                return;

            if (!activeOperations.TryRemove(operationKey, out var stopwatch)) return;
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Record in Prometheus
            operationCounter.WithLabels(category, operationName).Inc();
            operationDuration.WithLabels(category, operationName).Observe(elapsedMs);

            // Only track operations that exceed the threshold in memory
            if (elapsedMs >= operationThreshold.TotalMilliseconds)
            {
                RecordOperationTime(operationKey, elapsedMs);
            }
        }

        /// <summary>
        /// Records the execution time of an operation for statistical purposes.
        /// </summary>
        private void RecordOperationTime(string operationKey, long elapsedMs)
        {
            performanceEntries.AddOrUpdate(
                operationKey,
                key => new PerformanceEntry
                {
                    OperationKey = key,
                    CallCount = 1,
                    TotalExecutionTimeMs = elapsedMs,
                    MaxExecutionTimeMs = elapsedMs,
                    MinExecutionTimeMs = elapsedMs,
                    LastExecutionTimeMs = elapsedMs,
                    LastExecutionTime = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.CallCount++;
                    existing.TotalExecutionTimeMs += elapsedMs;
                    existing.MaxExecutionTimeMs = Math.Max(existing.MaxExecutionTimeMs, elapsedMs);
                    existing.MinExecutionTimeMs = Math.Min(existing.MinExecutionTimeMs, elapsedMs);
                    existing.LastExecutionTimeMs = elapsedMs;
                    existing.LastExecutionTime = DateTime.UtcNow;
                    return existing;
                });
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (metricsServer != null)
            {
                await metricsServer.StopAsync();
                Log.Information("Prometheus metrics server stopped");
            }

            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Class for tracking active operation performance.
        /// </summary>
        private class OperationTracker(
            PerformanceMonitorService service,
            string operationKey,
            string category,
            string operationName)
            : IDisposable
        {
            public void Dispose()
            {
                service.EndOperation(operationKey, category, operationName);
            }
        }

        /// <summary>
        /// A no-op tracker used when metrics are disabled
        /// </summary>
        private class NullTracker : IDisposable
        {
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Class representing performance data for a tracked operation.
    /// </summary>
    public class PerformanceEntry
    {
        /// <summary>
        /// Gets or sets the unique key identifying the operation.
        /// </summary>
        public string OperationKey { get; set; }

        /// <summary>
        /// Gets or sets the number of times this operation has been called.
        /// </summary>
        public long CallCount { get; set; }

        /// <summary>
        /// Gets or sets the total execution time in milliseconds across all calls.
        /// </summary>
        public long TotalExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time in milliseconds observed.
        /// </summary>
        public long MaxExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum execution time in milliseconds observed.
        /// </summary>
        public long MinExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the execution time of the most recent call in milliseconds.
        /// </summary>
        public long LastExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the most recent call.
        /// </summary>
        public DateTime LastExecutionTime { get; set; }

        /// <summary>
        /// Gets the average execution time in milliseconds.
        /// </summary>
        public double AverageExecutionTimeMs => CallCount > 0 ? (double)TotalExecutionTimeMs / CallCount : 0;
    }
}