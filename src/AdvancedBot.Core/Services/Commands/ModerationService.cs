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
            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public async Task<Infraction> BanUserFromGuildAsync(SocketGuildUser user, ulong modId, string reason, TimeSpan time, int pruneDays = 0)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var infraction = guild.AddInfractionToGuild(user.Id, modId, InfractionType.Ban, DateTime.UtcNow.Add(time), reason);

            await user.BanAsync(pruneDays, reason);
            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction UnbanUserFromGuild(ulong modId, IGuildUser user)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var infraction = guild.AddInfractionToGuild(user.Id, modId, InfractionType.Unban, null, "");

            user.Guild.RemoveBanAsync(user).GetAwaiter().GetResult();
            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction MuteUser(IGuildUser user, ulong modId, TimeSpan time, string reason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var mutedRole = user.Guild.GetRole(guild.MutedRoleId);
            var endsAt = DateTime.UtcNow.Add(time);

            user.AddRoleAsync(mutedRole);
            guild.CurrentMutes.Add(user.Id, endsAt);
            var infraction = guild.AddInfractionToGuild(user.Id, modId, InfractionType.Mute, endsAt, reason);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction UnmuteUser(IGuildUser user, ulong modId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);

            var infraction = guild.AddInfractionToGuild(user.Id, modId, InfractionType.Unmute, null, "");
            user.RemoveRoleAsync(user.Guild.GetRole(guild.MutedRoleId));

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public void CreateOrSetMutedRole(IGuild guild, IRole role = null)
        {
            var gld = _guilds.GetOrCreateGuildAccount(guild.Id);

            if (role is null)
            {
                var perms = new GuildPermissions(addReactions: false, sendMessages: false);
                role = guild.CreateRoleAsync("Muted", perms, null, false, null).GetAwaiter().GetResult();
            }

            gld.MutedRoleId = role.Id;
            _guilds.SaveGuildAccount(gld);
        }

        public ulong GetMutedRoleId(ulong guildId)
            => _guilds.GetOrCreateGuildAccount(guildId).MutedRoleId;
    }
}
