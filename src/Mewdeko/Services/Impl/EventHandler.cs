﻿namespace Mewdeko.Services.Impl;

public class EventHandler
{
    // Delegates
    public delegate Task AsyncEventHandler<in TEventArgs>(TEventArgs args);

    public delegate Task AsyncEventHandler<in TEventArgs, in TArgs>(TEventArgs args, TArgs arsg2);

    public delegate Task AsyncEventHandler<in TEventArgs, in TArgs, in TEvent>(TEventArgs args, TArgs args2,
        TEvent args3);

    public delegate Task AsyncEventHandler<in TEventArgs, in TArgs, in TEvent, in TArgs2>(TEventArgs args, TArgs args2,
        TEvent args3, TArgs2 args4);

    // Actual events
    public event AsyncEventHandler<SocketMessage>? MessageReceived;
    public event AsyncEventHandler<SocketGuildEvent>? EventCreated;
    public event AsyncEventHandler<SocketRole>? RoleCreated;
    public event AsyncEventHandler<SocketGuild, SocketGuild>? GuildUpdated;
    public event AsyncEventHandler<IGuildUser>? UserJoined;
    public event AsyncEventHandler<SocketRole, SocketRole>? RoleUpdated;
    public event AsyncEventHandler<IGuild, IUser>? UserLeft;
    public event AsyncEventHandler<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>>? MessageDeleted;
    public event AsyncEventHandler<Cacheable<SocketGuildUser, ulong>, SocketGuildUser>? GuildMemberUpdated;
    public event AsyncEventHandler<Cacheable<IMessage, ulong>, SocketMessage, ISocketMessageChannel>? MessageUpdated;

    public event AsyncEventHandler<IReadOnlyCollection<Cacheable<IMessage, ulong>>, Cacheable<IMessageChannel, ulong>>?
        MessagesBulkDeleted;

    public event AsyncEventHandler<SocketUser, SocketGuild>? UserBanned;
    public event AsyncEventHandler<SocketUser, SocketGuild>? UserUnbanned;
    public event AsyncEventHandler<SocketUser, SocketUser>? UserUpdated;
    public event AsyncEventHandler<SocketUser, SocketVoiceState, SocketVoiceState>? UserVoiceStateUpdated;
    public event AsyncEventHandler<SocketChannel>? ChannelCreated;
    public event AsyncEventHandler<SocketChannel>? ChannelDestroyed;
    public event AsyncEventHandler<SocketChannel, SocketChannel>? ChannelUpdated;
    public event AsyncEventHandler<SocketRole>? RoleDeleted;

    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction>?
        ReactionAdded;

    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>, SocketReaction>?
        ReactionRemoved;

    public event AsyncEventHandler<Cacheable<IUserMessage, ulong>, Cacheable<IMessageChannel, ulong>>? ReactionsCleared;
    public event AsyncEventHandler<SocketInteraction>? InteractionCreated;
    public event AsyncEventHandler<Cacheable<IUser, ulong>, Cacheable<IMessageChannel, ulong>>? UserIsTyping;
    public event AsyncEventHandler<SocketUser, SocketPresence, SocketPresence>? PresenceUpdated;
    public event AsyncEventHandler<IGuild>? JoinedGuild;
    public event AsyncEventHandler<SocketThreadChannel>? ThreadCreated;
    public event AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>, SocketThreadChannel>? ThreadUpdated;
    public event AsyncEventHandler<Cacheable<SocketThreadChannel, ulong>>? ThreadDeleted;
    public event AsyncEventHandler<SocketThreadUser>? ThreadMemberJoined;
    public event AsyncEventHandler<SocketThreadUser>? ThreadMemberLeft;
    public event AsyncEventHandler<SocketAuditLogEntry, SocketGuild>? AuditLogCreated;
    public event AsyncEventHandler<DiscordSocketClient>? Ready;

    private readonly DiscordSocketClient client;


    public EventHandler(DiscordSocketClient client)
    {
        this.client = client;
        client.MessageReceived += ClientOnMessageReceived;
        client.UserJoined += ClientOnUserJoined;
        client.UserLeft += ClientOnUserLeft;
        client.MessageDeleted += ClientOnMessageDeleted;
        client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
        client.MessageUpdated += ClientOnMessageUpdated;
        client.MessagesBulkDeleted += ClientOnMessagesBulkDeleted;
        client.UserBanned += ClientOnUserBanned;
        client.UserUnbanned += ClientOnUserUnbanned;
        client.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;
        client.UserUpdated += ClientOnUserUpdated;
        client.ChannelCreated += ClientOnChannelCreated;
        client.ChannelDestroyed += ClientOnChannelDestroyed;
        client.ChannelUpdated += ClientOnChannelUpdated;
        client.RoleDeleted += ClientOnRoleDeleted;
        client.ReactionAdded += ClientOnReactionAdded;
        client.ReactionRemoved += ClientOnReactionRemoved;
        client.ReactionsCleared += ClientOnReactionsCleared;
        client.InteractionCreated += ClientOnInteractionCreated;
        client.UserIsTyping += ClientOnUserIsTyping;
        client.PresenceUpdated += ClientOnPresenceUpdated;
        client.JoinedGuild += ClientOnJoinedGuild;
        client.GuildScheduledEventCreated += ClientOnEventCreated;
        client.RoleUpdated += ClientOnRoleUpdated;
        client.GuildUpdated += ClientOnGuildUpdated;
        client.RoleCreated += ClientOnRoleCreated;
        client.ThreadCreated += ClientOnThreadCreated;
        client.ThreadUpdated += ClientOnThreadUpdated;
        client.ThreadDeleted += ClientOnThreadDeleted;
        client.ThreadMemberJoined += ClientOnThreadMemberJoined;
        client.ThreadMemberLeft += ClientOnThreadMemberLeft;
        client.AuditLogCreated += ClientOnAuditLogCreated;
        client.Ready += ClientOnReady;
    }

    private Task ClientOnAuditLogCreated(SocketAuditLogEntry arg1, SocketGuild arg2)
    {
        if (AuditLogCreated is not null)
            _ = AuditLogCreated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnReady()
    {
        if (Ready is not null)
            _ = Ready(client);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadMemberLeft(SocketThreadUser arg)
    {
        if (ThreadMemberLeft is not null)
            _ = ThreadMemberLeft(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadMemberJoined(SocketThreadUser arg)
    {
        if (ThreadMemberJoined is not null)
            _ = ThreadMemberJoined(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
    {
        if (ThreadDeleted is not null)
            _ = ThreadDeleted(arg);
        return Task.CompletedTask;
    }


    private Task ClientOnThreadUpdated(Cacheable<SocketThreadChannel, ulong> arg1, SocketThreadChannel arg2)
    {
        if (ThreadUpdated is not null)
            _ = ThreadUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnThreadCreated(SocketThreadChannel arg)
    {
        if (ThreadCreated is not null)
            _ = ThreadCreated(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnJoinedGuild(SocketGuild arg)
    {
        if (JoinedGuild is not null)
            _ = JoinedGuild(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnPresenceUpdated(SocketUser arg1, SocketPresence arg2, SocketPresence arg3)
    {
        _ = PresenceUpdated?.Invoke(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnUserIsTyping(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (UserIsTyping is not null)
            _ = UserIsTyping(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnInteractionCreated(SocketInteraction arg)
    {
        if (InteractionCreated is not null)
            _ = InteractionCreated(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnReactionsCleared(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (ReactionsCleared is not null)
            _ = ReactionsCleared(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnReactionRemoved(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        if (ReactionRemoved is not null)
            _ = ReactionRemoved(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnReactionAdded(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2,
        SocketReaction arg3)
    {
        if (ReactionAdded is not null)
            _ = ReactionAdded(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnRoleDeleted(SocketRole arg)
    {
        if (RoleDeleted is not null)
            _ = RoleDeleted(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnChannelUpdated(SocketChannel arg1, SocketChannel arg2)
    {
        if (ChannelUpdated is not null)
            _ = ChannelUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnChannelDestroyed(SocketChannel arg)
    {
        if (ChannelDestroyed is not null)
            _ = ChannelDestroyed(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnChannelCreated(SocketChannel arg)
    {
        if (ChannelCreated is not null)
            _ = ChannelCreated(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnUserUpdated(SocketUser arg1, SocketUser arg2)
    {
        if (UserUpdated is not null)
            _ = UserUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
    {
        if (UserVoiceStateUpdated is not null)
            _ = UserVoiceStateUpdated(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnUserUnbanned(SocketUser arg1, SocketGuild arg2)
    {
        if (UserUnbanned is not null)
            _ = UserUnbanned(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserBanned(SocketUser arg1, SocketGuild arg2)
    {
        if (UserBanned is not null)
            _ = UserBanned(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnMessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1,
        Cacheable<IMessageChannel, ulong> arg2)
    {
        if (MessagesBulkDeleted is not null)
            _ = MessagesBulkDeleted(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnMessageUpdated(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
    {
        if (MessageUpdated is not null)
            _ = MessageUpdated(arg1, arg2, arg3);
        return Task.CompletedTask;
    }

    private Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
    {
        if (GuildMemberUpdated is not null)
            _ = GuildMemberUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (MessageDeleted is not null)
            _ = MessageDeleted(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserLeft(SocketGuild arg1, SocketUser arg2)
    {
        if (UserLeft is not null)
            _ = UserLeft(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnUserJoined(SocketGuildUser arg)
    {
        if (UserJoined is not null)
            _ = UserJoined(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnMessageReceived(SocketMessage arg)
    {
        if (MessageReceived is not null)
            _ = MessageReceived(arg);
        return Task.CompletedTask;
    }

    private Task ClientOnEventCreated(SocketGuildEvent args)
    {
        if (EventCreated is not null)
            _ = EventCreated(args);
        return Task.CompletedTask;
    }

    private Task ClientOnRoleUpdated(SocketRole arg1, SocketRole arg2)
    {
        if (RoleUpdated is not null)
            _ = RoleUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnGuildUpdated(SocketGuild arg1, SocketGuild arg2)
    {
        if (GuildUpdated is not null)
            _ = GuildUpdated(arg1, arg2);
        return Task.CompletedTask;
    }

    private Task ClientOnRoleCreated(SocketRole args)
    {
        if (RoleCreated is not null)
            _ = RoleCreated(args);
        return Task.CompletedTask;
    }
}