using System.Net.Http;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <inheritdoc />
    [Group]
    public class AiCommands : MewdekoSubmodule<AiService>
    {
        /// <summary>
        ///     Configures AI service for a channel
        /// </summary>
        /// <param name="channel">Channel to configure AI for</param>
        /// <param name="enabled">Whether to enable or disable AI</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AiChannel(ITextChannel? channel = null, bool enabled = true)
        {
            channel ??= (ITextChannel)ctx.Channel;
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            config.Enabled = enabled;
            config.ChannelId = channel.Id;
            await Service.UpdateConfig(config);

            await ctx.Channel.SendConfirmAsync(Strings.AiConfigUpdated(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Sets the AI provider and model
        /// </summary>
        /// <param name="provider">AI provider to use</param>
        /// <param name="model">Model name</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AiModel(AiService.AiProvider provider, string? model = null)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                await ctx.Channel.SendErrorAsync(Strings.AiNoApiKey(ctx.Guild.Id, provider), Config);
                return;
            }
            var models = await Service.GetSupportedModels(provider, config.ApiKey);

            if (model == null)
            {
                var modelList = string.Join("\n", models.Select(m => $"• {m.Name} (`{m.Id}`)"));
                await ctx.Channel.SendConfirmAsync(Strings.AiModelList(ctx.Guild.Id, provider.ToString(), modelList));
                return;
            }

            if (!models.Any(m => m.Id.Equals(model, StringComparison.OrdinalIgnoreCase)))
            {
                await ctx.Channel.SendErrorAsync(Strings.AiInvalidModel(ctx.Guild.Id, model, provider.ToString()), Config);
                return;
            }

            config.Provider = provider;
            config.Model = model;
            await Service.UpdateConfig(config);

            await ctx.Channel.SendConfirmAsync(Strings.AiModelChanged(ctx.Guild.Id, model));
        }

        /// <summary>
        ///     Sets the webhook for AI responses in this guild.
        /// </summary>
        /// <param name="name">The name of the webhook. If null, disables the webhook.</param>
        /// <param name="avatar">Optional URL for the webhook's avatar.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.ManageWebhooks)]
        public async Task AiWebhook(string? name = null, string? avatar = null)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            var channel = await ctx.Guild.GetTextChannelAsync(config.ChannelId);

            if (name is null)
            {
                await Service.SetWebhook(ctx.Guild.Id, null);
                await ctx.Channel.SendConfirmAsync(Strings.AiWebhookDisabled(ctx.Guild.Id));
                return;
            }

            if (channel is null)
            {
                await ctx.Channel.SendErrorAsync(Strings.AiNoChannelSet(ctx.Guild.Id, Config.Prefix), Config);
                return;
            }

            if (avatar is not null)
            {
                if (!Uri.IsWellFormedUriString(avatar, UriKind.Absolute))
                {
                    await ctx.Channel.SendErrorAsync(Strings.AiWebhookInvalidAvatar(ctx.Guild.Id), Config);
                    return;
                }

                var http = new HttpClient();
                using var sr = await http.GetAsync(avatar, HttpCompletionOption.ResponseHeadersRead);
                var imgData = await sr.Content.ReadAsByteArrayAsync();
                var imgStream = imgData.ToStream();
                await using var _ = imgStream;
                var webhook = await channel.CreateWebhookAsync(name, imgStream);
                await Service.SetWebhook(ctx.Guild.Id, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");
            }
            else
            {
                var webhook = await channel.CreateWebhookAsync(name);
                await Service.SetWebhook(ctx.Guild.Id, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");
            }

            await ctx.Channel.SendConfirmAsync(Strings.AiWebhookSet(ctx.Guild.Id));
        }


        /// <summary>
        ///     Sets or displays the custom embed template for AI responses.
        ///     Use %airesponse% to specify where the AI response should appear in the embed.
        /// </summary>
        /// <param name="embedTemplate">Optional embed template. If not provided, displays current template.</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task AiCustomEmbed([Remainder] string? embedTemplate = null)
        {
            if (embedTemplate is null)
            {
                var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
                if (string.IsNullOrEmpty(config.CustomEmbed))
                {
                    await ctx.Channel.SendErrorAsync(Strings.AiNoCustomEmbed(ctx.Guild.Id), Config);
                    return;
                }
                await ctx.Channel.SendConfirmAsync(config.CustomEmbed);
                return;
            }

            await Service.SetCustomEmbed(ctx.Guild.Id, embedTemplate);
            await ctx.Channel.SendConfirmAsync(Strings.AiCustomEmbedSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the API key for the AI service
        /// </summary>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AiKey()
        {
            var component = new ComponentBuilder()
                .WithButton(Strings.AiKeyClickToSet(ctx.Guild.Id), "setaikey")
                .Build();
            await ctx.Channel.SendMessageAsync("_ _", components: component);
        }

        /// <summary>
        ///     Sets the system prompt for the AI
        /// </summary>
        /// <param name="prompt">System prompt to use</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AiPrompt([Remainder] string prompt)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            config.SystemPrompt = prompt;
            await Service.UpdateConfig(config);

            await ctx.Channel.SendConfirmAsync(Strings.AiSystemPromptUpdated(ctx.Guild.Id));
        }

        /// <summary>
        ///     Shows current AI configuration
        /// </summary>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        public async Task AiConfig()
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.AiConfigTitle(ctx.Guild.Id))
                .WithDescription(Strings.AiConfigDescription(
                    ctx.Guild.Id,
                    config.Enabled,
                    config.ChannelId,
                    config.Provider,
                    config.Model ?? "Not Set",
                    config.TokensUsed))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
    }
}