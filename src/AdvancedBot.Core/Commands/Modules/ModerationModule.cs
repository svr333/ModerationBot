using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AdvancedBot.Core.Commands.Preconditions;
using AdvancedBot.Core.Entities.Enums;
using AdvancedBot.Core.Services.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;

namespace AdvancedBot.Core.Commands.Modules
{
    [Name("moderation")]
    public class ModerationModule : TopModule
    {
        private ModerationService _moderation;

        public ModerationModule(ModerationService moderation)
        {
            _moderation = moderation;
        }

        [Command("case")]
        [Summary("Shows information about that mod case.")]
        public async Task GetCaseAsync(uint id)
        {
            var infraction = _moderation.GetInfraction(Context.Guild.Id, id);
            var infractioner = Context.Client.GetUser(infraction.InfractionerId);
            var moderator = Context.Client.GetUser(infraction.ModeratorId);

            var embed = new EmbedBuilder()
            {
                Title = $"#{infraction.Id} | {infraction.Type.Humanize()}",
                Description = $"**Reason:** {infraction.Reason}"
            }
            .WithFooter($"Id: {infraction.InfractionerId} | ModId: {moderator.Id}")
            .AddField($"User", $"{infractioner.Mention}", true)
            .AddField($"Moderator", $"{moderator.Mention}", true)
            .WithColor(GetColorOnInfractionType(infraction.Type));

            if (infraction.FinishesAt != null)
            {
                embed.AddField($"Ends At", $"{infraction.FinishesAt.Humanize()}, true");
            }

            await ReplyAsync($"", false, embed.Build());
        }

        [Command("modlogs")]
        [Summary("Shows the modlogs related to the person.")]
        public async Task GetModLogsForUserAsync(IGuildUser user)
        {
            var infractions = _moderation.GetAllUserInfractions(Context.Guild.Id, user.Id);

            var embed = new EmbedBuilder()
            {
                Title = $"Modlogs for {user.Username}:",
                Color = Color.DarkBlue
            }
            .WithFooter($"Requested by {Context.User.Username} | {Context.User.Id}");

            for (int i = 0; i < infractions.Length; i++)
            {
                var mod = Context.Client.GetUser(infractions[i].ModeratorId);
                embed.AddField($"#{infractions[i].Id} | {infractions[i].Type.Humanize()} by {mod.Username}",
                $"{infractions[i].Date.ToShortDateString()} | {infractions[i].Reason}\n\u200b");
            }

            await ReplyAsync("", false, embed.Build());
        }

        [Command("reason")]
        [Summary("Change the reason of an existing case.")]
        public async Task ChangeReasonAsync(uint caseId, [Remainder] string newReason)
        {
            var oldReason = _moderation.ChangeInfractionReason(Context.Guild.Id, caseId, newReason);

            await ReplyAsync($"Updated the reason for case: {caseId}.", false, new EmbedBuilder()
            {
                Title = $"#{caseId} | Reason change",
                Color = Color.DarkBlue
            }
            .AddField($"Old Reason", oldReason)
            .AddField($"New Reason", newReason)
            .Build());
        }

        [Command("duration")]
        [Summary("Change the duration of an existing case.")]
        public async Task ChangeDurationAsync(uint caseId, [Remainder] string rawNewTime)
        {
            var newTime = ParseTimeSpanFromString(ref rawNewTime);
            var oldTime = _moderation.UpdateDurationOnInfraction(Context.Guild.Id, caseId, newTime);

            await ReplyAsync($"Updated the reason for case: {caseId}.", false, new EmbedBuilder()
            {
                Title = $"#{caseId} | Duration change",
                Color = Color.DarkBlue
            }
            .AddField($"Old Time", oldTime.Humanize())
            .AddField($"New Time", newTime.Humanize())
            .Build());
        }

        [Command("warn")]
        [Summary("Warns a user.")]
        public async Task WarnUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason)
        {
            var warning = _moderation.WarnUserInGuild(user, (SocketGuildUser)Context.User, reason);
            
            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"**Galaxy Life Reborn:** You were warned",
                    Color = GetColorOnInfractionType(warning.Type)
                }
                .AddField($"Reason", warning.Reason)
                .Build());
            }
            catch (Exception)
            {
                await ReplyAsync($"Could not dm user to notify him.");
            }

            await ReplyAsync($"#{warning.Id} | Warned {user.Mention}.");
        }

        [Command("delwarn")]
        [Summary("Deletes an existing warning by case id.")]
        public async Task RemoveWarnAsync(uint caseId)
        {
            var warning = _moderation.RemoveWarningFromGuild(Context.Guild.Id, Context.User.Id, caseId);
            var user = Context.Client.GetUser(warning.Id);

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"**Galaxy Life Reborn:** Warning Redacted",
                    Description = $"Warning with the below reason has been redacted by a moderator.\n\u200b",
                    Color = GetColorOnInfractionType(warning.Type)
                }
                .AddField($"Reason", warning.Reason)
                .Build());
            }
            catch (Exception)
            {
                await ReplyAsync($"Could not dm user to notify him.");
            }

            await ReplyAsync($"#{warning.Id} | Removed warning #{caseId} from {user.Username}.");
        }

        [Command("clearwarn")]
        [Summary("Clears all warns related to a certain user.")]
        public async Task RemoveAllUserWarns(IGuildUser user)
        {
            _moderation.ClearAllInfractionsForUser(user, Context.User.Id);
            await ReplyAsync($"Cleared all warns for {user.Mention}.");
        }

        [Command("kick")]
        [Summary("Kicks a user.")]
        public async Task KickUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"You were kicked from **Galaxy Life Reborn**",
                    Color = GetColorOnInfractionType(InfractionType.Kick)
                }
                .AddField($"Reason", reason)
                .Build());
            }
            catch (Exception)
            {
                await ReplyAsync($"Could not dm user to notify him.");
            }

            var infraction = await _moderation.KickUserFromGuildAsync(user, Context.User.Id, reason);
            await ReplyAsync($"#{infraction.Id} | Kicked {user.Mention}.");
        }

        [Command("ban")]
        [Alias("tempban")]
        [Summary("Bans a user.")]
        public async Task BanUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var time = ParseTimeSpanFromString(ref reason);

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"You were banned from **Galaxy Life Reborn**",
                    Color = GetColorOnInfractionType(InfractionType.Ban)
                }
                .AddField($"Duration", time.Humanize(), true)
                .AddField($"Reason", reason, true)
                .Build());
            }
            catch (Exception)
            {
                await ReplyAsync($"Could not dm user to notify him.");
            }

            var infraction = await _moderation.BanUserFromGuildAsync(user, Context.User.Id, reason, time, 7);

            if (time.TotalMilliseconds < 1000)
            {
                await ReplyAsync($"#{infraction.Id} | Banned {user.Mention}.");
            }
            else
            {
                await ReplyAsync($"#{infraction.Id} | Banned {user.Mention} for {time.Humanize()}");
            }
        }

        [Command("unban")]
        [Summary("Unbans a user.")]
        public async Task UnbanUserAsync([EnsureNotSelf] IGuildUser user)
        {
            var infraction = _moderation.UnbanUserFromGuild(Context.User.Id, user);
            await ReplyAsync($"#{infraction.Id} | Unbanned {user.Mention}.");
        }

        [Command("mute")]
        [Summary("Mutes a user.")]
        public async Task MuteUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var time = ParseTimeSpanFromString(ref reason);
            if (time.TotalMilliseconds < 1000)
                time = new TimeSpan(14, 0, 0, 0);

            var infraction = _moderation.MuteUser(user, Context.User.Id, time, reason);

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"You were muted in **Galaxy Life Reborn**",
                    Color = GetColorOnInfractionType(InfractionType.Mute)
                }
                .AddField($"Duration", time.Humanize(), true)
                .AddField($"Reason", reason, true)
                .Build());
            }
            catch (Exception)
            {
                await ReplyAsync($"Could not dm user to notify him.");
            }
            
            await ReplyAsync($"#{infraction.Id} | Muted {user.Mention} for {time.Humanize()}.");
        }

        [Command("unmute")]
        [Summary("Unmutes a user.")]
        public async Task UnmuteUserAsync([EnsureNotSelf] SocketGuildUser user)
        {
            var infraction = _moderation.UnmuteUser(user, Context.User.Id);

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"Your mute was lifted in **Galaxy Life Reborn**",
                    Color = GetColorOnInfractionType(InfractionType.Unmute)
                }
                .Build());
            }
            catch (Exception)
            {
                await ReplyAsync($"Could not dm user to notify him.");
            }

            await ReplyAsync($"#{infraction.Id} | Unmuted {user.Mention}.");
        }

        [Command("muterole")]
        [Summary("Shows you the currently active mute role.")]
        public async Task GetMuteRole()
        {
            var roleId = _moderation.GetMutedRoleId(Context.Guild.Id);
            if (roleId == 0)
            {
                await ReplyAsync($"There is currently no muted role active.");
                return;
            }

            var role = Context.Guild.GetRole(roleId);
            await ReplyAsync("", false, new EmbedBuilder()
            {
                Title = $"Muted Role",
                Description = $"{role.Mention} ({role.Id})",
                Color = Color.DarkBlue
            }
            .Build());
        }

        [Command("muterole")]
        [Summary("Sets the mute role to the provided role.")]
        public async Task SetMuteRole(IRole role)
        {
            _moderation.CreateOrSetMutedRole(Context.Guild, role);
            await ReplyAsync($"Successfully set the muterole to {role.Mention}.");
        }

        [Command("muterole create")]
        [Summary("Creates a new role to add as mute role.")]
        public async Task CreateAndSetMuteRole()
        {
            _moderation.CreateOrSetMutedRole(Context.Guild);
            var role = Context.Guild.GetRole(_moderation.GetMutedRoleId(Context.Guild.Id));

            await ReplyAsync($"Successfully created the mutedrole {role.Mention}.");
        }

        [Command("modchannel")]
        [Summary("Set the modlogs channel.")]
        public async Task SetModLogChannel(ITextChannel channel)
        {
            if (Context.Guild.Id != channel.GuildId)
                throw new Exception($"Channel needs to be in the same guild.");

            _moderation.SetModLogsChannel(channel);
            await ReplyAsync($"Set modlogs channel to {channel.Mention}.");
        }

        private Color GetColorOnInfractionType(InfractionType type)
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

        private TimeSpan ParseTimeSpanFromString(ref string input)
        {
            var regex = new Regex[] { new Regex("([0-9]+)[yY]"), new Regex("([0-9]+)[wW]"), new Regex("([0-9]+)[dD]"), new Regex("([0-9]+)[hH]"), new Regex("([0-9]+)[mM]"), new Regex("([0-9]+)[sS]") };
            var results = new int[regex.Length];

            for (int i = 0; i < regex.Length; i++)
            {
                var match = regex[i].Match(input);

                if (match.Success)
                {
                    input = regex[i].Replace(input, "");
                    results[i] = int.Parse(match.Captures[0].Value.ToLower().Replace("y", "").Replace("w", "").Replace("d", "").Replace("h", "").Replace("m", "").Replace("s", ""));
                }
                else
                {
                    results[i] = 0;
                }
            }

            var days = results[0] * 365 + results[1] * 7 + results[2];

            input = input.Trim();
            return new TimeSpan(days, results[3], results[4], results[5]);
        }
    }
}
