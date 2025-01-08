using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class SlashUtility
{
    /// <summary>
    ///     Commands for configuring and managing AI functionality.
    /// </summary>
    [Group("ai", "Configure and manage AI settings")]
    public class AiSlashCommands : MewdekoSlashSubmodule<AiService>
    {

        /// <summary>
        ///     Handles the button interaction for setting an AI API key, displaying a modal for secure input.
        /// </summary>
        /// <returns>A task representing the modal response operation.</returns>
        [ComponentInteraction("setaikey", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public Task AiKeyButton()
        {
            return RespondWithModalAsync<AiKeyModal>("aikeymodal");
        }

        /// <summary>
        ///     Processes the submitted AI API key from the modal and updates the configuration.
        /// </summary>
        /// <param name="modal">The modal containing the submitted API key.</param>
        /// <returns>A task representing the asynchronous configuration update operation.</returns>
        [ModalInteraction("aikeymodal", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKeyModal(AiKeyModal modal)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            config.ApiKey = modal.ApiKey;
            await Service.UpdateConfig(config);

            await ctx.Interaction.SendConfirmAsync(Strings.AiApiKeyUpdated(ctx.Guild.Id, config.Provider));
        }

        /// <summary>
        ///     Configures AI functionality for a specific channel.
        /// </summary>
        /// <param name="channel">The channel to configure AI for. Defaults to current channel if not specified.</param>
        /// <param name="enabled">Whether to enable or disable AI in the channel.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("channel", "Configure AI for a channel")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiChannel(ITextChannel? channel = null, bool enabled = true)
        {
            channel ??= (ITextChannel)ctx.Channel;
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            config.Enabled = enabled;
            config.ChannelId = channel.Id;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiConfigUpdated(ctx.Guild.Id, channel.Mention))
                .Build());
        }

        /// <summary>
        ///     Sets or lists available AI models for a provider.
        /// </summary>
        /// <param name="provider">The AI provider to use.</param>
        /// <param name="model">The model ID to set. If null, lists available models.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("model", "Set the AI provider and model")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiModel(
            AiService.AiProvider provider,
            [Autocomplete(typeof(AiModelAutoCompleter))]
            string? model = null)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                await ctx.Interaction.SendErrorAsync(Strings.AiNoApiKey(ctx.Guild.Id, provider), Config);
                return;
            }
            var models = await Service.GetSupportedModels(provider, config.ApiKey);

            if (model == null)
            {
                var modelList = string.Join("\n", models.Select(m => $"• {m.Name} (`{m.Id}`)"));
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(Strings.AiModelList(ctx.Guild.Id, provider.ToString(), modelList))
                    .Build());
                return;
            }

            if (!models.Any(m => m.Id.Equals(model, StringComparison.OrdinalIgnoreCase)))
            {
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(Strings.AiInvalidModel(ctx.Guild.Id, model, provider.ToString()))
                    .Build());
                return;
            }

            config.Provider = provider;
            config.Model = model;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiModelChanged(ctx.Guild.Id, model))
                .Build());
        }

        /// <summary>
        ///     Sets the API key for the configured AI provider.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("key", "Set the API key for the AI service")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKey()
        {
            var component = new ComponentBuilder()
                .WithButton(Strings.AiKeyClickToSet(ctx.Guild.Id), "setaikey")
                .Build();
            await ctx.Interaction.RespondAsync("_ _", components: component);
        }

        /// <summary>
        ///     Sets the system prompt used for AI conversations.
        /// </summary>
        /// <param name="prompt">The system prompt to set.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("prompt", "Set the system prompt for the AI")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiPrompt(string prompt)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            config.SystemPrompt = prompt;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiSystemPromptUpdated(ctx.Guild.Id))
                .Build());
        }

        /// <summary>
        ///     Shows the current AI configuration for the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("config", "Show current AI configuration")]
        [RequireContext(ContextType.Guild)]
        public async Task AiConfig()
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithTitle(Strings.AiConfigTitle(ctx.Guild.Id))
                .WithDescription(Strings.AiConfigDescription(
                    ctx.Guild.Id,
                    config.Enabled,
                    config.ChannelId,
                    config.Provider,
                    config.Model ?? "Not Set",
                    config.TokensUsed))
                .WithOkColor()
                .Build());
        }
    }
}