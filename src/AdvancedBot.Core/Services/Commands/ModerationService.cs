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
        private DiscordSocketClient _client;

        public ModerationService(GuildAccountService guilds, DiscordSocketClient client)
        {
            _guilds = guilds;
            _client = client;
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

            var infraction = AddInfractionToGuild(user.Id, moderator.Id, InfractionType.Warning, null, reason, guild);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public string ChangeInfractionReason(ulong guildId, uint warningId, string newReason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            
            var oldReason = guild.ChangeInfractionReason(warningId, newReason);

            _guilds.SaveGuildAccount(guild);
            return oldReason;
        }

        public TimeSpan UpdateDurationOnInfraction(ulong guildId, uint caseId, TimeSpan newTime)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);

            var oldTime = guild.UpdateInfractionDuration(caseId, newTime);

            _guilds.SaveGuildAccount(guild);
            return oldTime;
        }

        public Infraction[] GetAllUserInfractions(ulong guildId, ulong userId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);

            return guild.Infractions.Where(x => x.InfractionerId == userId).ToArray();
        }

        public Infraction RemoveWarningFromGuild(ulong guildId, ulong modId, uint warningId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            var infraction = guild.RemoveWarningById(warningId);

            AddInfractionToGuild(infraction.InfractionerId, modId, InfractionType.Delwarn, null, "", guild);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public async Task<Infraction> KickUserFromGuildAsync(SocketGuildUser user, ulong modId, string reason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Kick, null, reason, guild);

            await user.KickAsync(reason);
            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public async Task<Infraction> BanUserFromGuildAsync(SocketGuildUser user, ulong modId, string reason, TimeSpan time, int pruneDays = 0)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var endsAt = DateTime.UtcNow.Add(time);

            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Ban, endsAt, reason, guild);

            if (endsAt > DateTime.UtcNow)
            {
                guild.CurrentBans.Add(user.Id, endsAt);
            }

            await user.BanAsync(pruneDays, reason);
            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction UnbanUserFromGuild(ulong modId, IGuildUser user)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);
            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Unban, null, "", guild);

            user.Guild.RemoveBanAsync(user).GetAwaiter().GetResult();
            guild.CurrentBans.Remove(user.Id);

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
            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Mute, endsAt, reason, guild);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction UnmuteUser(IGuildUser user, ulong modId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);

            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Unmute, null, "", guild);
            user.RemoveRoleAsync(user.Guild.GetRole(guild.MutedRoleId));
            guild.CurrentMutes.Remove(user.Id);

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

        public void ClearAllInfractionsForUser(IGuildUser user, ulong modId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.GuildId);
            var warns = guild.Infractions.Where(x => x.InfractionerId == user.Id && x.Type == InfractionType.Warning).ToArray();

            for (int i = 0; i < warns.Length; i++)
            {
                RemoveWarningFromGuild(guild.Id, modId, warns[i].Id);
            }
        }

        public void SetModLogsChannel(ITextChannel channel)
        {
            var guild = _guilds.GetOrCreateGuildAccount(channel.GuildId);
            guild.ModLogsChannelId = channel.Id;

            _guilds.SaveGuildAccount(guild);
        }

        public ulong GetMutedRoleId(ulong guildId)
            => _guilds.GetOrCreateGuildAccount(guildId).MutedRoleId;
    
        private Infraction AddInfractionToGuild(ulong userId, ulong modId, InfractionType type, DateTime? endsAt, string reason, GuildAccount guild)
        {
            if (guild.ModLogsChannelId != 0)
            {
                (_client.GetChannel(guild.ModLogsChannelId) as ITextChannel).SendMessageAsync($"{userId}").GetAwaiter().GetResult();
            }
            
            return guild.AddInfractionToGuild(userId, modId, type, endsAt, reason);
        }
    }
}
