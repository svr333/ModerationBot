using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using AdvancedBot.Core.Entities;
using AdvancedBot.Core.Entities.Enums;
using AdvancedBot.Core.Services.DataStorage;
using Discord;
using Discord.WebSocket;
using Humanizer;
using Humanizer.Localisation;

namespace AdvancedBot.Core.Services.Commands
{
    public class ModerationService
    {
        private GuildAccountService _guilds;
        private DiscordSocketClient _client;
        private Timer _infractionChecker = new Timer(1000 * 60 * 1);

        public ModerationService(GuildAccountService guilds, DiscordSocketClient client)
        {
            _guilds = guilds;
            _client = client;

            _infractionChecker.Elapsed += CheckInfractions;

            _infractionChecker.Start();
        }

        private void CheckInfractions(object sender, ElapsedEventArgs e)
        {
            var guilds = _client.Guilds.ToArray();

            for (int i = 0; i < guilds.Length; i++)
            {
                var guild = _guilds.GetOrCreateGuildAccount(guilds[i].Id);

                if (!guild.TimedInfractions.Any())
                    continue;

                var infractionsToRemove = new List<Infraction>();

                for (int j = 0; j < guild.TimedInfractions.ToArray().Length; j++)
                {
                    if (guild.TimedInfractions[j].FinishesAt > DateTime.Now)
                        continue;

                    infractionsToRemove.Add(guild.TimedInfractions[j]);

                    switch (guild.TimedInfractions[j].Type)
                    {
                        case InfractionType.Mute:
                            var user = guilds[i].GetUser(guild.TimedInfractions[j].InfractionerId);
                            UnmuteUser(user, _client.CurrentUser.Id);
                            break;
                        case InfractionType.Ban:
                            UnbanUserFromGuild(_client.CurrentUser.Id, guild.TimedInfractions[j].InfractionerId, guilds[i].Id);
                            break;
                        default:
                            break;
                    }
                }

                for (int j = 0; j < infractionsToRemove.Count; j++)
                {
                    guild.TimedInfractions.Remove(infractionsToRemove[j]);
                }

                _guilds.SaveGuildAccount(guild);
            }

            Console.WriteLine($"Successfully checked all guilds for muted/banned people.");
        }

        public Infraction GetInfraction(ulong guildId, uint id)
        {
            var infraction = _guilds.GetOrCreateGuildAccount(guildId).Infractions.FirstOrDefault(x => x.Id == id);
            if (infraction == null)
                throw new Exception($"There is no case with id `{id}`.");
            
            return infraction;
        }

        public Infraction WarnUserInGuild(IUser user, IGuildUser moderator, string reason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(moderator.Guild.Id);

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

            return guild.Infractions.Where(x => x.InfractionerId == userId).OrderByDescending(x => x.Date).ToArray();
        }

        public Infraction RemoveWarningFromGuild(ulong guildId, ulong modId, uint warningId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            var infraction = guild.RemoveWarningById(warningId);

            AddInfractionToGuild(infraction.InfractionerId, modId, InfractionType.Delwarn, null, "", guild);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public async Task<Infraction> KickUserFromGuildAsync(IGuildUser user, ulong modId, string reason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.GuildId);
            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Kick, null, reason, guild);

            await user.KickAsync(reason);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public async Task<Infraction> BanUserFromGuildAsync(IUser user, ulong guildId, ulong modId, string reason, TimeSpan time, int pruneDays = 0)
        {
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            DateTime? endsAt = null;

            if (time.TotalMilliseconds >= 1000)
            {
                endsAt = DateTime.UtcNow.Add(time);
            }

            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Ban, endsAt, reason, guild);

            if (endsAt != null)
            {
                guild.TimedInfractions.Add(infraction);
            }

            await _client.GetGuild(guildId).AddBanAsync(user, pruneDays, reason);
            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction UnbanUserFromGuild(ulong modId, ulong infractionerId, ulong guildId)
        {
            var user = _client.Rest.GetUserAsync(infractionerId).GetAwaiter().GetResult();
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Unban, null, "", guild);

            try
            {
                _client.GetGuild(guildId).RemoveBanAsync(user).GetAwaiter().GetResult();
            }
            catch
            {
                throw new Exception($"User isn't banned.");
            }
            
            var inf = guild.TimedInfractions.Find(x => x.InfractionerId == user.Id && x.Type == InfractionType.Ban);
            if (inf != null)
                guild.TimedInfractions.Remove(inf);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction MuteUser(IGuildUser user, ulong modId, TimeSpan time, string reason)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);

            var mutedRole = user.Guild.GetRole(guild.MutedRoleId);
            var endsAt = DateTime.UtcNow.Add(time);

            var inf = guild.TimedInfractions.Find(x => x.InfractionerId == user.Id && x.Type == InfractionType.Mute);
            if (inf != null)
                guild.TimedInfractions.Remove(inf);

            user.AddRoleAsync(mutedRole);
            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Mute, endsAt, reason, guild);
            guild.TimedInfractions.Add(infraction);

            _guilds.SaveGuildAccount(guild);
            return infraction;
        }

        public Infraction UnmuteUser(IGuildUser user, ulong modId)
        {
            var guild = _guilds.GetOrCreateGuildAccount(user.Guild.Id);

            var infraction = AddInfractionToGuild(user.Id, modId, InfractionType.Unmute, null, "", guild);
            user.RemoveRoleAsync(user.Guild.GetRole(guild.MutedRoleId));
            
            var inf = guild.TimedInfractions.Find(x => x.InfractionerId == user.Id && x.Type == InfractionType.Mute);
            if (inf != null)
                guild.TimedInfractions.Remove(inf);

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

        public Infraction[] GetCurrentTimedInfractions(ulong guildId)
            => _guilds.GetOrCreateGuildAccount(guildId).TimedInfractions.OrderBy(x => x.Type).ToArray();

        public void SetModLogsChannel(ITextChannel channel)
        {
            var guild = _guilds.GetOrCreateGuildAccount(channel.GuildId);
            guild.ModLogsChannelId = channel.Id;

            _guilds.SaveGuildAccount(guild);
        }

        public ulong GetMutedRoleId(ulong guildId)
            => _guilds.GetOrCreateGuildAccount(guildId).MutedRoleId;
    
        public Color GetColorFromInfractionType(InfractionType type)
        {
            switch (type)
            {
                case InfractionType.Warning:
                    return Color.Orange;
                case InfractionType.Mute:
                    return Color.Red;
                case InfractionType.Kick:
                    return Color.DarkOrange;
                case InfractionType.Ban:
                    return Color.DarkRed;
                default:
                    return Color.Green;
            }
        }

        private Infraction AddInfractionToGuild(ulong userId, ulong modId, InfractionType type, DateTime? endsAt, string reason, GuildAccount guild)
        {
            var infraction = guild.AddInfractionToGuild(userId, modId, type, endsAt, reason);

            if (guild.ModLogsChannelId != 0)
            {
                var channel = _client.GetChannel(guild.ModLogsChannelId) as ITextChannel;
                var embed = GetMessageEmbedForLog(infraction);

                channel.SendMessageAsync("", false, embed).GetAwaiter().GetResult();
            }
            
            return infraction;
        }

        private Embed GetMessageEmbedForLog(Infraction infraction)
        {
            var moderator = _client.Rest.GetUserAsync(infraction.ModeratorId).GetAwaiter().GetResult();
            var infractioner = _client.Rest.GetUserAsync(infraction.InfractionerId).GetAwaiter().GetResult();

            var embed = new EmbedBuilder()
            {
                Title = $"Case {infraction.Id} | {infraction.Type.Humanize()}",
                Color = GetColorFromInfractionType(infraction.Type)
            }
            .AddField("Moderator", moderator.Mention, true)
            .AddField("Infractioner", infractioner.Mention, true)
            .WithFooter($"Id: {infractioner.Id}")
            .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(infraction.Reason))
                embed.AddField("Reason", infraction.Reason, true);

            if (infraction.FinishesAt != null)
                embed.AddField("Duration", (DateTime.UtcNow - infraction.FinishesAt.Value.AddMilliseconds(500)).Humanize(4, minUnit: TimeUnit.Second), true);

            return embed.Build();
        }
    }
}
