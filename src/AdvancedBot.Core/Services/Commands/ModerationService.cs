using System;
using System.Linq;
using System.Threading.Tasks;
using AdvancedBot.Core.Entities;
using AdvancedBot.Core.Entities.Enums;
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

        public Infraction GetInfraction(ulong guildId, uint id)
        {
            var infraction = _guilds.GetOrCreateGuildAccount(guildId).Infractions.FirstOrDefault(x => x.Id == id);
            if (infraction == null)
                throw new Exception($"There is no case with id `{id}`.");
            
            return infraction;
        }

        public Infraction WarnUserInGuild(IGuildUser user, IGuildUser moderator, string reason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);

            var infraction = guild.AddInfractionToGuild(user.Id, moderator.Id, InfractionType.Warning, null, reason);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public string ChangeInfractionReasonInGuild(ulong guildId, uint warningId, string newReason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            
            var oldReason = guild.ChangeInfractionReason(warningId, newReason);

            _guilds.SaveGuildAccount(guild);
            return oldReason;
        }

        public Infraction RemoveWarningFromGuild(ulong guildId, ulong modId, uint warningId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            var infraction = guild.RemoveWarningById(warningId);

            guild.AddInfractionToGuild(infraction.InfractionerId, modId, InfractionType.Delwarn, null, "");

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public async Task<Infraction> KickUserFromGuildAsync(SocketGuildUser user, ulong modId, string reason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var infraction = guild.AddInfractionToGuild(user.Id, modId, InfractionType.Kick, null, reason);

            await user.KickAsync(reason);
            return infraction;
        }

        public async Task<Infraction> BanUserFromGuildAsync(SocketGuildUser user, ulong modId, string reason, DateTime? endsAt, int pruneDays = 0)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var infraction = guild.AddInfractionToGuild(user.Id, modId, InfractionType.Ban, endsAt, reason);

            await user.BanAsync(pruneDays, reason);
            return infraction;
        }

        public Infraction UnbanUserFromGuild(ulong modId, IGuildUser user)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var infraction = guild.AddInfractionToGuild(user.Id, modId, InfractionType.Unban, null, "");

            user.Guild.RemoveBanAsync(user).GetAwaiter().GetResult();
            return infraction;
        }
    }
}
