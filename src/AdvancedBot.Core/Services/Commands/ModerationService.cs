using System;
using System.Threading.Tasks;
using AdvancedBot.Core.Entities;
using AdvancedBot.Core.Services.DataStorage;
using Discord;
using Discord.WebSocket;

namespace AdvancedBot.Core.Services.Commands
{
    public class ModerationService
    {
        private GuildAccountService _guilds;

        public ModerationService(GuildAccountService guilds)
        {
            _guilds = guilds;
        }

        public Warning WarnUserInGuild(SocketGuildUser user, SocketGuildUser moderator, string reason = "")
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);

            var warning = guild.AddWarningToUser(user, moderator, reason);

            _guilds.SaveGuildAccount(guild);
            return warning;
        }

        public string ChangeWarningReasonInGuild(ulong guildId, uint warningId, string newReason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            
            var oldReason = guild.ChangeWarningReason(warningId, newReason);

            _guilds.SaveGuildAccount(guild);
            return oldReason;
        }

        public Warning RemoveWarningFromGuild(ulong guildId, uint warningId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            
            var warning = guild.RemoveWarningById(warningId);

            _guilds.SaveGuildAccount(guild);
            return warning;
        }

        public async Task BanUserFromGuildAsync(SocketGuildUser user, int pruneDays = 0, string reason = "No reason provided.")
            => await user.BanAsync(pruneDays, reason);

        public async Task KickUserFromGuildAsync(SocketGuildUser user, string reason = "No reason provided.")
            => await user.KickAsync(reason);
    }
}
