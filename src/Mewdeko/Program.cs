using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using Discord.Commands;
using Discord.Interactions;
using Discord.Rest;
using Fergun.Interactive;
using Lavalink4NET.Extensions;
using MartineApiNet;
using Mewdeko.AuthHandlers;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Currency.Services.Impl;
using Mewdeko.Modules.Nsfw;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Mewdeko.Services.Strings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NekosBestApiNet;
using Npgsql;
using Serilog;
using ZiggyCreatures.Caching.Fusion;
using RunMode = Discord.Commands.RunMode;

namespace Mewdeko;

/// <summary>
///     The main entry point class for the Mewdeko application.
/// </summary>
public class Program
{
    private static IDataCache Cache { get; set; }

    /// <summary>
    ///     The entry point of the application.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation of running the application.</returns>
    public static async Task Main(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var log = LogSetup.SetupLogger("Api");
        var credentials = new BotCredentials();
        DependencyInstaller.CheckAndInstallDependencies(credentials.PsqlConnectionString);
        Cache = new RedisCache(credentials);

        if (!Uri.TryCreate(credentials.LavalinkUrl, UriKind.Absolute, out _))
        {
            Log.Error("The Lavalink URL is invalid! Please check the Lavalink URL in the configuration");
            Helpers.ReadErrorAndExit(5);
        }

        var migrationService = new MigrationService(
            null,
            credentials.Token,
            credentials.PsqlConnectionString,
            credentials.MigrateToPsql);

        await migrationService.ApplyMigrations(
            new MewdekoPostgresContext(new DbContextOptions<MewdekoPostgresContext>()));

        Log.Information("Waiting 5 seconds for migrations, if any...");
        await Task.Delay(5000);

        // Set up the Host or WebApplication based on IsApiEnabled

        if (credentials.IsApiEnabled)
        {
            var builder = WebApplication.CreateBuilder(args);
            var services = builder.Services;

            // Configure logging
            builder.Logging.ClearProviders();


            // Configure services
            ConfigureServices(services, credentials, Cache);

            // Configure web host settings
            builder.WebHost.UseUrls($"http://localhost:{credentials.ApiPort}");
            services.AddTransient<IApiKeyValidation, ApiKeyValidation>();
            services.AddAuthorization();

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(x =>
            {
                x.AddSecurityDefinition("ApiKeyHeader", new OpenApiSecurityScheme
                {
                    Name = "x-api-key",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Description = "Authorization by x-api-key inside request's header"
                });
                x.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme, Id = "ApiKeyHeader"
                            }
                        },
                        new string[]
                        {
                        }
                    }
                });
            });

            var auth = services.AddAuthentication(options =>
            {
                options.AddScheme<AuthHandler>(AuthHandler.SchemeName, AuthHandler.SchemeName);
            });

            auth.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiKeyPolicy", policy =>
                    policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ApiKey"));
                options.AddPolicy("TopggPolicy",
                    policy => policy.RequireClaim(AuthHandler.TopggClaim)
                        .AddAuthenticationSchemes(AuthHandler.SchemeName));
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("BotInstancePolicy", policy =>
                {
                    policy
                        .AllowAnyOrigin() // Allow any origin since dashboard could be accessed from anywhere
                        .AllowAnyMethod() // Allow GET, POST, etc.
                        .AllowAnyHeader(); // Allow any headers including custom auth headers
                });
            });


            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing request: {Method} {Path}",
                        context.Request.Method,
                        context.Request.Path);
                    throw;
                }
            });
            app.UseCors("BotInstancePolicy");
            app.UseSerilogRequestLogging(options =>
            {
                options.IncludeQueryInRequestPath = true;
                options.MessageTemplate =
                    "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms\n{RequestBody}";
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    try
                    {
                        var requestBody = string.Empty;

                        if (httpContext.Request.ContentLength > 0)
                        {
                            // Enable buffering for multiple reads
                            httpContext.Request.EnableBuffering();

                            // Create a StreamReader that leaves the stream open
                            using var reader = new StreamReader(
                                httpContext.Request.Body,
                                Encoding.UTF8,
                                false,
                                -1,
                                true);
                            requestBody = reader.ReadToEndAsync().Result;
                            // Reset the stream position back to the beginning
                            httpContext.Request.Body.Position = 0;
                        }

                        diagnosticContext.Set("RequestBody", requestBody);
                        diagnosticContext.Set("QueryString", httpContext.Request.QueryString);
                    }
                    catch (Exception ex)
                    {
                        // Log any errors but don't throw them to avoid breaking the request pipeline
                        Log.Error(ex, "Error reading request body for logging");
                        diagnosticContext.Set("RequestBody", "Error reading request body");
                    }
                };
            });

            // Configure the HTTP request pipeline.
            if (builder.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();
            app.MapControllers();

            foreach (var address in app.Urls)
            {
                Log.Information("Listening on {Address}", address);
            }

            // Start the app and the bot
            await app.RunAsync();
        }
        else
        {
            // Create a generic host when IsApiEnabled is false
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSerilog(log);
                })
                .ConfigureServices((context, services) =>
                {
                    // Configure services without web-specific services
                    ConfigureServices(services, credentials, Cache);
                })
                .Build();

            // Start the bot without hosting any servers
            await host.RunAsync();
        }
    }

    private static void ConfigureServices(IServiceCollection services, BotCredentials credentials, IDataCache cache)
    {

        var discordRestClient = new DiscordRestClient();
        discordRestClient.LoginAsync(TokenType.Bot, credentials.Token).GetAwaiter().GetResult();
        var botGatewayInfo = discordRestClient.GetBotGatewayAsync().GetAwaiter().GetResult();
        discordRestClient.LogoutAsync().GetAwaiter().GetResult();

        Log.Information("Discord recommends {0} shards with {1} max concurrency",
            botGatewayInfo.Shards,
            botGatewayInfo.SessionStartLimit.MaxConcurrency);

        var client = new DiscordShardedClient(new DiscordSocketConfig
        {
            MessageCacheSize = 15,
            LogLevel = LogSeverity.Info,
            ConnectionTimeout = int.MaxValue,
            AlwaysDownloadUsers = true,
            GatewayIntents = GatewayIntents.All,
            FormatUsersInBidirectionalUnicode = false,
            LogGatewayIntentWarnings = false,
            DefaultRetryMode = RetryMode.RetryRatelimit
        });

        services.AddSerilog(LogSetup.SetupLogger("Mewdeko"));
        services.AddSingleton(client);
        services.AddSingleton(credentials);
        services.AddSingleton(cache);
        services.AddSingleton(cache.Redis);

        services
            .AddSingleton<FontProvider>()
            .AddSingleton<IBotCredentials>(credentials);
        //services.AddDbContext<MewdekoPostgresContext>(); # used only for migrations
        services.AddPooledDbContextFactory<MewdekoContext>(dbContextOptionsBuilder =>
            {
                var connString = new NpgsqlConnectionStringBuilder(credentials.PsqlConnectionString)
                {
                    Pooling = true,
                    MinPoolSize = 20,
                    MaxPoolSize = 100,
                    ConnectionIdleLifetime = 300,
                    ConnectionPruningInterval = 10
                }.ToString();

                dbContextOptionsBuilder
                    .UseNpgsql(connString, npgsqlOptions =>
                    {
                        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        npgsqlOptions.MaxBatchSize(1000);
                        npgsqlOptions.EnableRetryOnFailure(
                            3,
                            TimeSpan.FromSeconds(3),
                            null);
                    })
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging();
            })
            .AddSingleton<DbContextProvider>()
            .AddSingleton<EventHandler>()
            .AddSingleton(new CommandService(new CommandServiceConfig
            {
                CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async
            }))
            .AddSingleton(new MartineApi())
            .AddTransient<ISeria, JsonSeria>()
            .AddTransient<IPubSub, RedisPubSub>()
            .AddTransient<IConfigSeria, YamlSeria>()
            .AddSingleton(new InteractiveService(client, new InteractiveConfig
            {
                ReturnAfterSendingPaginator = true
            }))
            .AddSingleton(new NekosBestApi("Mewdeko"))
            .AddSingleton(p => new InteractionService(p.GetRequiredService<DiscordShardedClient>()))
            .AddSingleton<Localization>()
            .AddSingleton<GeneratedBotStrings>()
            .AddSingleton<BotConfigService>()
            .AddSingleton<BotConfig>()
            .AddConfigServices()
            .AddBotStringsServices(credentials.TotalShards)
            .AddMemoryCache()
            .AddLavalink()
            .ConfigureLavalink(x =>
            {
                x.Passphrase = "Hope4a11";
                x.BaseAddress = new Uri(credentials.LavalinkUrl);
            })
            .AddSingleton<ISearchImagesService, SearchImagesService>()
            .AddSingleton<ToneTagService>()
            .AddTransient<GuildSettingsService>();

        services.AddFusionCache().TryWithAutoSetup();

        if (credentials.UseGlobalCurrency)
        {
            services.AddTransient<ICurrencyService, GlobalCurrencyService>();
        }
        else
        {
            services.AddTransient<ICurrencyService, GuildCurrencyService>();
        }

        services.AddHttpClient();
        services.AddHttpClient("memelist").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.Scan(scan => scan.FromAssemblyOf<IReadyExecutor>()
            .AddClasses(classes => classes.AssignableToAny(
                typeof(INService),
                typeof(IEarlyBehavior),
                typeof(ILateBlocker),
                typeof(IInputTransformer),
                typeof(ILateExecutor)))
            .AsSelfWithInterfaces()
            .WithSingletonLifetime()
        );

        services.AddSingleton<Mewdeko>()
            .AddHostedService<MewdekoService>();
    }
}