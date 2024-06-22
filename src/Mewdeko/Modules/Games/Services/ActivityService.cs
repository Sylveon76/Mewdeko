namespace Mewdeko.Modules.Games.Services
{
    /// <summary>
    /// Service responsible for the activity command, lol.
    /// </summary>
    public class ActivityService : INService
    {
        private readonly MewdekoContext dbContext;
        private readonly GuildSettingsService guildSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityService"/> class.
        /// </summary>
        /// <param name="db">The database service.</param>
        /// <param name="guildSettings">The guild settings service.</param>
        public ActivityService(MewdekoContext dbContext, GuildSettingsService guildSettings)
        {
            this.dbContext = dbContext;
            this.guildSettings = guildSettings;
        }

        /// <summary>
        /// Gets the ID of the game master role for the specified guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild.</param>
        /// <returns>The ID of the game master role.</returns>
        public async Task<ulong> GetGameMasterRole(ulong guildId) =>
            (await guildSettings.GetGuildConfig(guildId)).GameMasterRole;

        /// <summary>
        /// Sets the game master role for the specified guild.
        /// </summary>
        /// <param name="guildid">The ID of the guild.</param>
        /// <param name="role">The ID of the role to set as the game master role.</param>
        public async Task GameMasterRoleSet(ulong guildid, ulong role)
        {

            var gc = await dbContext.ForGuildId(guildid, set => set);
            gc.GameMasterRole = role;
            await guildSettings.UpdateGuildConfig(guildid, gc);
        }
    }
}