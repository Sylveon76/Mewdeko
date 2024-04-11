﻿using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Fergun.Interactive;
using Figgle;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using MartineApiNet;
using Mewdeko.Common.Collections;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Interactions;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Currency.Services.Impl;
using Mewdeko.Modules.Nsfw;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using NekosBestApiNet;
using Newtonsoft.Json;
using NsfwSpyNS;
using Serilog;
using StackExchange.Redis;
using RunMode = Discord.Commands.RunMode;
using TypeReader = Discord.Commands.TypeReader;

namespace Mewdeko;

/// <summary>
/// The main class for Mewdeko, responsible for initializing services, handling events, and managing the bot's lifecycle.
/// </summary>
public class Mewdeko
{
    private readonly DbService db;

    /// <summary>
    /// Gets the collection of all guild configurations. Is somehow better than redis.
    /// </summary>
    public ConcurrentHashSet<GuildConfig> AllGuildConfigs;

    /// <summary>
    /// Initializes a new instance of the Mewdeko bot with a specific shard ID.
    /// </summary>
    /// <param name="shardId">The ID of the shard this instance will operate on. If set to nothing it will act as if its unsharded.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the shard ID is negative.</exception>
    public Mewdeko(int shardId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);

        Credentials = new BotCredentials();
        Cache = new RedisCache(Credentials, shardId);
        db = new DbService(Credentials.TotalShards, Credentials.Token, Credentials.UsePsql,
            Credentials.PsqlConnectionString);


        if (shardId == 0)
            db.Setup();


        Client = new DiscordSocketClient(new DiscordSocketConfig
        {
            MessageCacheSize = 15,
            LogLevel = LogSeverity.Info,
            ConnectionTimeout = int.MaxValue,
            TotalShards = Credentials.TotalShards,
            ShardId = shardId,
            AlwaysDownloadUsers = true,
            GatewayIntents = GatewayIntents.All,
            FormatUsersInBidirectionalUnicode = false,
            LogGatewayIntentWarnings = false,
            DefaultRetryMode = RetryMode.RetryRatelimit
        });
        CommandService = new CommandService(new CommandServiceConfig
        {
            CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async
        });
    }

    /// <summary>
    /// Gets the credentials used by the bot.
    /// </summary>
    public BotCredentials Credentials { get; }

    /// <summary>
    /// Gets the Discord client used by the bot.
    /// </summary>
    public DiscordSocketClient Client { get; }

    private CommandService CommandService { get; }

    /// <summary>
    /// Gets the color used for successful operations.
    /// </summary>
    public static Color OkColor { get; set; }

    /// <summary>
    /// Gets the color used for error operations.
    /// </summary>
    public static Color ErrorColor { get; set; }

    /// <summary>
    /// Used to tell other services in the bot if its done initializing.
    /// </summary>
    public TaskCompletionSource<bool> Ready { get; } = new();

    private IServiceProvider Services { get; set; }
    private IDataCache Cache { get; }

    /// <summary>
    /// Occurs when the bot joins a guild.
    /// </summary>
    public event Func<GuildConfig, Task> JoinedGuild = delegate { return Task.CompletedTask; };


    private async Task AddServices()
    {
        if (!Uri.TryCreate(Credentials.LavalinkUrl, UriKind.Absolute, out _))
        {
            Log.Error("The Lavalink URL is invalid! Please check the Lavalink URL in the configuration");
            Helpers.ReadErrorAndExit(5);
        }

        var sw = Stopwatch.StartNew();
        var gs2 = Stopwatch.StartNew();
        var bot = Client.CurrentUser;
        await using var uow = db.GetDbContext();
        AllGuildConfigs =
            new ConcurrentHashSet<GuildConfig>(
                uow.GuildConfigs.GetAllGuildConfigs(Client.Guilds.Select(x => x.Id).ToList()));
        await uow.EnsureUserCreated(bot.Id, bot.Username, bot.Discriminator, bot.AvatarId);
        gs2.Stop();
        Log.Information("Guild Configs cached in {ElapsedTotalSeconds}s", gs2.Elapsed.TotalSeconds);

        var s = new ServiceCollection()
            .AddScoped<INsfwSpy, NsfwSpy>()
            .AddSingleton<FontProvider>()
            .AddSingleton<IBotCredentials>(Credentials)
            .AddSingleton(db)
            .AddSingleton(Client)
            .AddSingleton(new EventHandler(Client))
            .AddSingleton(CommandService)
            .AddSingleton(this)
            .AddSingleton(Cache)
            .AddSingleton(new MartineApi())
            .AddSingleton(Cache.Redis)
            .AddTransient<ISeria, JsonSeria>()
            .AddTransient<IPubSub, RedisPubSub>()
            .AddTransient<IConfigSeria, YamlSeria>()
            .AddSingleton(new InteractiveService(Client, new InteractiveConfig
            {
                ReturnAfterSendingPaginator = true
            }))
            .AddSingleton(new NekosBestApi())
            .AddSingleton<InteractionService>()
            .AddSingleton<Localization>()
            .AddSingleton<BotConfigService>()
            .AddSingleton<BotConfig>()
            .AddConfigServices()
            .AddBotStringsServices(Credentials.TotalShards)
            .AddMemoryCache()
            .AddLavalink()
            .ConfigureLavalink(x =>
            {
                x.Passphrase = "Hope4a11";
                x.BaseAddress = new Uri(Credentials.LavalinkUrl);
            })
            .AddScoped<ISearchImagesService, SearchImagesService>()
            .AddSingleton<ToneTagService>()
            .AddSingleton<GuildSettingsService>();
        if (Credentials.UseGlobalCurrency)
        {
            s.AddTransient<ICurrencyService, GlobalCurrencyService>();
        }
        else
        {
            s.AddTransient<ICurrencyService, GuildCurrencyService>();
        }


        Log.Information("Passed Singletons");

        s.AddHttpClient();
        s.AddHttpClient("memelist").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        if (Credentials.TotalShards <= 1 &&
            Environment.GetEnvironmentVariable($"{Client.CurrentUser.Id}_IS_COORDINATED") != "1")
        {
            s.AddSingleton<ICoordinator, SingleProcessCoordinator>();
        }
        else
        {
            s.AddSingleton<RemoteGrpcCoordinator>()
                .AddSingleton<ICoordinator>(x => x.GetRequiredService<RemoteGrpcCoordinator>())
                .AddSingleton<IReadyExecutor>(x => x.GetRequiredService<RemoteGrpcCoordinator>());
        }

        Log.Information("Passed Coord");

        s.Scan(scan => scan.FromAssemblyOf<IReadyExecutor>()
            .AddClasses(classes => classes.AssignableToAny(
                // services
                typeof(INService),
                // behaviours
                typeof(IEarlyBehavior),
                typeof(ILateBlocker),
                typeof(IInputTransformer),
                typeof(ILateExecutor)))
            .AsSelfWithInterfaces()
            .WithSingletonLifetime()
        );

        Log.Information("Passed Interface Scanner");
        //initialize Services
        Services = s.BuildServiceProvider();
        var commandHandler = Services.GetService<CommandHandler>();
        commandHandler.AddServices(s);
        _ = Task.Run(() => LoadTypeReaders(typeof(Mewdeko).Assembly));
        var cache = Services.GetService<IDataCache>();
        var audioService = Services.GetService<IAudioService>();
        try
        {
            await audioService.StartAsync();
        }
        catch (Exception e)
        {
            Log.Error("Unable to start audio service: {Message}", e.Message);
        }

        var sub = cache.Redis.GetSubscriber();
        await sub.SubscribeAsync($"{Credentials.RedisKey()}_configsupdate", async (_, _) =>
        {
            await GuildConfigsUpdated();
        });

        sw.Stop();
        Log.Information($"All services loaded in {sw.Elapsed.TotalSeconds:F2}s");
    }

    private IEnumerable<object> LoadTypeReaders(Assembly assembly)
    {
        var sw = new Stopwatch();
        sw.Start();
        var interactionService = Services.GetService<InteractionService>();
        Type[] allTypes;
        try
        {
            allTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Log.Warning(ex.LoaderExceptions[0], "Error getting types");
            return Enumerable.Empty<object>();
        }

        var filteredTypes = allTypes
            .Where(x => x.IsSubclassOf(typeof(TypeReader))
                        && x.BaseType.GetGenericArguments().Length > 0
                        && !x.IsAbstract);

        var toReturn = new List<object>();
        foreach (var ft in filteredTypes)
        {
            var x = (TypeReader)ActivatorUtilities.CreateInstance(Services, ft);
            var baseType = ft.BaseType;
            var typeArgs = baseType?.GetGenericArguments();
            if (typeArgs != null)
                CommandService.AddTypeReader(typeArgs[0], x);
            toReturn.Add(x);
        }

        CommandService.AddTypeReaders<IEmote>(
            new TryParseTypeReader<Emote>(Emote.TryParse),
            new TryParseTypeReader<Emoji>(Emoji.TryParse));

        interactionService.AddTypeConverter<TimeSpan>(new TimeSpanConverter());
        interactionService.AddTypeConverter(typeof(IRole[]), new RoleArrayConverter());
        interactionService.AddTypeConverter(typeof(IUser[]), new UserArrayConverter());
        interactionService.AddTypeConverter<StatusRolesTable>(new StatusRolesTypeConverter());

        sw.Stop();
        Log.Information("TypeReaders loaded in {ElapsedTotalSeconds}s", sw.Elapsed.TotalSeconds);
        return toReturn;
    }

    private async Task LoginAsync(string token)
    {
        Client.Log += Client_Log;
        var clientReady = new TaskCompletionSource<bool>();

        Task SetClientReady()
        {
            _ = Task.Run(() => clientReady.TrySetResult(true));
            return Task.CompletedTask;
        }

        //connect
        Log.Information("Shard {0} logging in ...", Client.ShardId);
        try
        {
            await Client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await Client.StartAsync().ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            LoginErrorHandler.Handle(ex);
            Helpers.ReadErrorAndExit(3);
        }
        catch (Exception ex)
        {
            LoginErrorHandler.Handle(ex);
            Helpers.ReadErrorAndExit(4);
        }

        Client.Ready += SetClientReady;
        await clientReady.Task.ConfigureAwait(false);
        Client.Ready -= SetClientReady;
        Client.JoinedGuild += Client_JoinedGuild;
        Client.LeftGuild += Client_LeftGuild;
        Log.Information("Shard {0} logged in", Client.ShardId);
        Log.Information("Logged in as:");
        Console.WriteLine(FiggleFonts.Digital.Render(Client.CurrentUser.Username));

#if !DEBUG
        Client.Log -= Client_Log;
#endif
    }

    private Task Client_LeftGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var chan = await Client.Rest.GetChannelAsync(Credentials.GuildJoinsChannelId).ConfigureAwait(false);
                await ((RestTextChannel)chan).SendErrorAsync($"Left server: {arg.Name} [{arg.Id}]", new BotConfig(),
                    fields:

                    [
                        new EmbedFieldBuilder().WithName("Total Guilds")
                            .WithValue(Services.GetRequiredService<ICoordinator>()
                                .GetGuildCount().ToString())
                    ]).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }

            Log.Information("Left server: {0} [{1}]", arg.Name, arg.Id);
        });
        return Task.CompletedTask;
    }

    private Task Client_JoinedGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            await arg.DownloadUsersAsync().ConfigureAwait(false);
            Log.Information("Joined server: {0} [{1}]", arg.Name, arg.Id);

            GuildConfig gc;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                gc = await uow.ForGuildId(arg.Id);
            }

            await JoinedGuild.Invoke(gc).ConfigureAwait(false);
            var chan =
                await Client.Rest.GetChannelAsync(Credentials.GuildJoinsChannelId).ConfigureAwait(false) as
                    RestTextChannel;
            var eb = new EmbedBuilder();
            eb.WithTitle($"Joined {Format.Bold(arg.Name)} {arg.Id}");
            eb.AddField("Members", arg.MemberCount);
            eb.AddField("Boosts", arg.PremiumSubscriptionCount);
            eb.AddField("Owner", $"Name: {arg.Owner}\nID: {arg.OwnerId}");
            eb.AddField("Text Channels", arg.TextChannels.Count);
            eb.AddField("Voice Channels", arg.VoiceChannels.Count);
            eb.AddField("Total Guilds", Services.GetRequiredService<ICoordinator>().GetGuildCount());
            eb.WithThumbnailUrl(arg.IconUrl);
            eb.WithColor(OkColor);
            await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        var sw = Stopwatch.StartNew();

        await LoginAsync(Credentials.Token).ConfigureAwait(false);

        Log.Information("Shard {ShardId} loading services...", Client.ShardId);
        try
        {
            await AddServices();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding services");
            Helpers.ReadErrorAndExit(9);
        }

        sw.Stop();
        Log.Information("Shard {ShardId} connected in {Elapsed:F2}s", Client.ShardId, sw.Elapsed.TotalSeconds);
        var commandService = Services.GetService<CommandService>();
        var interactionService = Services.GetRequiredService<InteractionService>();
        await commandService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services)
            .ConfigureAwait(false);
        await interactionService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services)
            .ConfigureAwait(false);
#if !DEBUG
        if (Client.ShardId == 0)
            await interactionService.RegisterCommandsGloballyAsync().ConfigureAwait(false);
#endif
#if DEBUG
        if (Client.Guilds.Select(x => x.Id).Contains(Credentials.DebugGuildId))
            await interactionService.RegisterCommandsToGuildAsync(Credentials.DebugGuildId);
#endif


        _ = Task.Run(HandleStatusChanges);
        _ = Task.Run(ExecuteReadySubscriptions);
        Ready.TrySetResult(true);
        Log.Information("Shard {ShardId} ready", Client.ShardId);
    }

    private Task ExecuteReadySubscriptions()
    {
        var readyExecutors = Services.GetServices<IReadyExecutor>();
        var tasks = readyExecutors.Select(async toExec =>
        {
            try
            {
                await toExec.OnReadyAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed running OnReadyAsync method on {Type} type: {Message}", toExec.GetType().Name,
                    ex.Message);
            }
        });

        return tasks.WhenAll();
    }

    private static Task Client_Log(LogMessage arg)
    {
        if (arg.Exception != null)
            Log.Warning(arg.Exception, arg.Source + " | " + arg.Message);
        else
            Log.Information(arg.Source + " | " + arg.Message);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the bot and blocks the calling thread until the bot is stopped.
    /// </summary>
    public async Task RunAndBlockAsync()
    {
        await RunAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }

    private void HandleStatusChanges()
    {
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        // ReSharper disable once AsyncVoidLambda
        sub.Subscribe($"{Client.CurrentUser.Id}_status.game_set", async (_, game) =>
        {
            try
            {
                var obj = new
                {
                    Name = default(string), Activity = ActivityType.Playing
                };
                obj = JsonConvert.DeserializeAnonymousType(game, obj);
                await Client.SetGameAsync(obj.Name, type: obj.Activity).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting game");
            }
        }, CommandFlags.FireAndForget);

        // ReSharper disable once AsyncVoidLambda
        sub.Subscribe($"{Client.CurrentUser.Id}_status.stream_set", async (_, streamData) =>
        {
            try
            {
                var obj = new
                {
                    Name = "", Url = ""
                };
                obj = JsonConvert.DeserializeAnonymousType(streamData, obj);
                await Client.SetGameAsync(obj?.Name, obj!.Url, ActivityType.Streaming).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting stream");
            }
        }, CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Sets the bot's status to the specified game.
    /// </summary>
    /// <param name="game">The name of the game to set.</param>
    /// <param name="type">The type of activity.</param>
    public async Task SetGameAsync(string? game, ActivityType type)
    {
        var obj = new
        {
            Name = game, Activity = type
        };
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        await sub.PublishAsync($"{Client.CurrentUser.Id}_status.game_set", JsonConvert.SerializeObject(obj))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates te guild configs hashset to whatever the db has right now. Will probably used by the api.
    /// </summary>
    private async Task GuildConfigsUpdated()
    {
        await using var uow = db.GetDbContext();
        AllGuildConfigs =
            new ConcurrentHashSet<GuildConfig>(
                uow.GuildConfigs.GetAllGuildConfigs(Client.Guilds.Select(x => x.Id).ToList()));
    }
}