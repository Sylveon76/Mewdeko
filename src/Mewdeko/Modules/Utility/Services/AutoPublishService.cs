using LinqToDB.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Provides functionality to automatically publish messages in Discord news channels
/// according to specific rules, including channel, user, and word blacklists.
/// </summary>
public class AutoPublishService : INService
{
    private readonly DbService dbService;
    private readonly DiscordSocketClient client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoPublishService"/> class.
    /// </summary>
    /// <param name="service">The database service for accessing and storing configuration.</param>
    /// <param name="handler">The event handler for subscribing to message received events.</param>
    /// <param name="client">The Discord client for interacting with the Discord API.</param>
    public AutoPublishService(DbService service, EventHandler handler, DiscordSocketClient client)
    {
        this.client = client;
        dbService = service;
        handler.MessageReceived += AutoPublish;
    }

    /// <summary>
    /// Automatically publishes messages in news channels if they meet certain criteria,
    /// checking against user and word blacklists.
    /// </summary>
    /// <param name="args">The message event arguments.</param>
    private async Task AutoPublish(SocketMessage args)
    {
        if (args.Channel is not INewsChannel channel)
            return;

        if (args is not IUserMessage msg)
            return;

        var curuser = await channel.GetUserAsync(client.CurrentUser.Id);

        var perms = curuser.GetPermissions(channel);

        if (!perms.Has(ChannelPermission.ManageMessages) &&
            curuser.GuildPermissions.Has(GuildPermission.ManageMessages))
            return;


        await using var uow = dbService.GetDbContext();
        var autoPublish = await uow.AutoPublish.FirstAsyncEF(x => x.ChannelId == channel.Id);
        if (autoPublish is null)
            return;

        var blacklistedWords = uow.PublishWordBlacklists.Where(x => x.ChannelId == channel.Id);
        if (blacklistedWords.Any())
        {
            if (blacklistedWords.Any(i => args.Content.ToLower().Contains(i.Word)))
            {
                return;
            }
        }

        var userBlacklists = uow.PublishUserBlacklists.Where(x => x.ChannelId == channel.Id);

        if (userBlacklists.Any())
        {
            if (userBlacklists.Any(i => args.Author.Id == i.User))
            {
                return;
            }
        }

        try
        {
            await msg.CrosspostAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, "Unable to publish message:");
        }
    }

    /// <summary>
    /// Adds a channel to the list of auto-publish channels.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the channel resides.</param>
    /// <param name="channelId">The ID of the channel to auto-publish.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> AddAutoPublish(ulong guildId, ulong channelId)
    {
        await using var uow = dbService.GetDbContext();
        var autoPublish = await uow.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId);
        if (autoPublish != null)
        {
            // AutoPublish already exists for the channel
            return false;
        }

        autoPublish = new AutoPublish
        {
            GuildId = guildId, ChannelId = channelId
        };
        uow.AutoPublish.Add(autoPublish);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Retrieves the auto-publish configurations for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve configurations for.</param>
    /// <returns>A list of configurations for auto-publishing.</returns>
    public async Task<List<(AutoPublish?, List<PublishUserBlacklist?>, List<PublishWordBlacklist?>)>>
        GetAutoPublishes(ulong guildId)
    {
        await using var uow = dbService.GetDbContext();
        var autoPublishes = uow.AutoPublish.Where(x => x.GuildId == guildId).ToHashSet();

        return !autoPublishes.Any()
            ? [(null, null, null)]
            : (from i in autoPublishes
                let userBlacklists = uow.PublishUserBlacklists.Where(x => x.ChannelId == i.ChannelId).ToList()
                let wordBlacklists = uow.PublishWordBlacklists.Where(x => x.ChannelId == i.ChannelId).ToList()
                select (i, userBlacklists, wordBlacklists)).ToList();
    }

    /// <summary>
    /// Checks if the bot has the necessary permissions to auto-publish in a channel.
    /// </summary>
    /// <param name="channel">The news channel to check permissions for.</param>
    /// <returns>True if the bot has the necessary permissions, false otherwise.</returns>
    public async Task<bool> PermCheck(INewsChannel channel)
    {
        var curuser = await channel.GetUserAsync(client.CurrentUser.Id);

        var perms = curuser.GetPermissions(channel);

        return perms.Has(ChannelPermission.ManageMessages) ||
               !curuser.GuildPermissions.Has(GuildPermission.ManageMessages);
    }

    /// <summary>
    /// Removes a channel from the list of auto-publish channels.
    /// </summary>
    /// <param name="guildId">The ID of the guild where the channel resides.</param>
    /// <param name="channelId">The ID of the channel to stop auto-publishing.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> RemoveAutoPublish(ulong guildId, ulong channelId)
    {
        await using var uow = dbService.GetDbContext();
        var autoPublish = await uow.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId);
        if (autoPublish == null)
        {
            return false;
        }

        uow.AutoPublish.Remove(autoPublish);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Adds a user to the blacklist, preventing their messages from being auto-published.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="userId">The ID of the user to blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> AddUserToBlacklist(ulong channelId, ulong userId)
    {
        await using var uow = dbService.GetDbContext();
        var userBlacklist =
            await uow.PublishUserBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.User == userId);
        if (userBlacklist != null)
        {
            // User is already blacklisted in the channel
            return false;
        }

        userBlacklist = new PublishUserBlacklist
        {
            ChannelId = channelId, User = userId
        };
        uow.PublishUserBlacklists.Add(userBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Removes a user from the blacklist, allowing their messages to be auto-published again.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="userId">The ID of the user to remove from the blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> RemoveUserFromBlacklist(ulong channelId, ulong userId)
    {
        await using var uow = dbService.GetDbContext();
        var userBlacklist =
            await uow.PublishUserBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.User == userId);
        if (userBlacklist == null)
        {
            // User is not blacklisted in the channel
            return false;
        }

        uow.PublishUserBlacklists.Remove(userBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Adds a word to the blacklist, preventing messages containing it from being auto-published.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="word">The word to blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> AddWordToBlacklist(ulong channelId, string word)
    {
        await using var uow = dbService.GetDbContext();
        var wordBlacklist =
            await uow.PublishWordBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.Word == word);
        if (wordBlacklist != null)
        {
            // Word is already blacklisted in the channel
            return false;
        }

        wordBlacklist = new PublishWordBlacklist
        {
            ChannelId = channelId, Word = word
        };
        uow.PublishWordBlacklists.Add(wordBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Removes a word from the blacklist, allowing messages containing it to be auto-published again.
    /// </summary>
    /// <param name="channelId">The ID of the channel where the blacklist applies.</param>
    /// <param name="word">The word to remove from the blacklist.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> RemoveWordFromBlacklist(ulong channelId, string word)
    {
        await using var uow = dbService.GetDbContext();
        var wordBlacklist =
            await uow.PublishWordBlacklists.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId && x.Word == word);
        if (wordBlacklist == null)
        {
            // Word is not blacklisted in the channel
            return false;
        }

        uow.PublishWordBlacklists.Remove(wordBlacklist);
        await uow.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if an auto-publish configuration exists for a given channel.
    /// </summary>
    /// <param name="channelId">The ID of the channel to check.</param>
    /// <returns>True if an auto-publish configuration exists, false otherwise.</returns>
    public async Task<bool> CheckIfExists(ulong channelId)
    {
        await using var uow = dbService.GetDbContext();
        return (await uow.AutoPublish.FirstOrDefaultAsyncEF(x => x.ChannelId == channelId)) is null;
    }
}