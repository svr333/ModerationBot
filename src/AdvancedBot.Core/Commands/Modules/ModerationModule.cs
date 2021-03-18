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

        [Command("reason")]
        public async Task ChangeReasonAsync(uint caseId, [Remainder] string newReason)
        {
            var oldReason = _moderation.ChangeInfractionReasonInGuild(Context.Guild.Id, caseId, newReason);

            await ReplyAsync($"Successfully updated the reason for case: {caseId}.", false, new EmbedBuilder()
            {
                Title = $"#{caseId} | Reason change",
                Color = Color.DarkBlue
            }
            .AddField($"Old Reason", oldReason)
            .AddField($"New Reason", newReason)
            .Build());
        }

        [Command("Duration")]
        public async Task ChangeDurationAsync(uint caseId, [Remainder] string newTime)
        {

        }

        [Command("warn")]
        public async Task WarnUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason)
        {
            var warning = _moderation.WarnUserInGuild(user, (SocketGuildUser) Context.User, reason);
            var dmChannel = await user.GetOrCreateDMChannelAsync();

            await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
            {
                Title = $"**Galaxy Life Reborn:** You were warned",
                Color = GetColorOnInfractionType(warning.Type)
            }
            .AddField($"Reason", warning.Reason)
            .Build());

            await ReplyAsync($"#{warning.Id} | {user.Mention} successfully warned.");
        }

        [Command("delwarn")]
        public async Task RemoveWarnAsync(uint caseId)
        {
            var warning = _moderation.RemoveWarningFromGuild(Context.Guild.Id, Context.User.Id, caseId);
            var user = Context.Client.GetUser(warning.Id);
            var dmChannel = await user.GetOrCreateDMChannelAsync();

            await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
            {
                Title = $"**Galaxy Life Reborn:** Warning Redacted",
                Description = $"Warning with the below reason has been redacted by a moderator.\n\u200b",
                Color = GetColorOnInfractionType(warning.Type)
            }
            .AddField($"Reason", warning.Reason)
            .Build());

            await ReplyAsync($"Successfully removed warning #{caseId} from {user.Username}.");
        }

        [Command("kick")]
        public async Task KickUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var infraction = await _moderation.KickUserFromGuildAsync(user, Context.User.Id, reason);
            var dmChannel = await user.GetOrCreateDMChannelAsync();

            await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
            {
                Title = $"**Galaxy Life Reborn:** You were kicked",
                Color = GetColorOnInfractionType(infraction.Type)
            }
            .AddField($"Reason", infraction.Reason)
            .Build());

            await ReplyAsync($"Successfully kicked {user.Mention}");
        }

        [Command("ban")][Alias("tempban")]
        public async Task BanUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var time = ParseTimeSpanFromString(ref reason);
            var infraction = await _moderation.BanUserFromGuildAsync(user, Context.User.Id, reason, time, 0);
            var dmChannel = await user.GetOrCreateDMChannelAsync();

            await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
            {
                Title = $"**Galaxy Life Reborn:** You were banned",
                Color = GetColorOnInfractionType(infraction.Type)
            }
            .AddField($"Duration", time.Humanize(), true)
            .AddField($"Reason", infraction.Reason, true)
            .Build());

            if (time.TotalMilliseconds < 1000)
            {
                await ReplyAsync($"Successfully banned {user.Mention}.");
            }
            else
            {
                await ReplyAsync($"Successfully banned {user.Mention} for {time.Humanize()}");
            }
        }

        [Command("unban")]
        public async Task UnbanUserAsync([EnsureNotSelf] IGuildUser user)
        {
            var infraction = _moderation.UnbanUserFromGuild(Context.User.Id, user);
            await ReplyAsync($"Successfully unbanned {user.Mention}.");
        }

        [Command("mute")]
        public async Task MuteUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var time = ParseTimeSpanFromString(ref reason);
            if (time.TotalMilliseconds < 1000)
                time = new TimeSpan(14, 0, 0, 0);

            _moderation.MuteUser(user, Context.User.Id, time, reason);
            await ReplyAsync($"Successfully muted {user.Mention} for {time.Humanize()}.");
        }

        [Command("unmute")]
        public async Task UnmuteUserAsync([EnsureNotSelf] SocketGuildUser user)
        {
            _moderation.UnmuteUser(user, Context.User.Id);
            await ReplyAsync($"Successfully unmuted {user.Mention}.");
        }

        [Command("muterole")]
        public async Task GetMuteRole()
        {
            var role = Context.Guild.GetRole(_moderation.GetMutedRoleId(Context.Guild.Id));
            await ReplyAsync("", false, new EmbedBuilder()
            {
                Title = $"Muted Role",
                Description = $"{role.Mention} ({role.Id})",
                Color = Color.DarkBlue
            }
            .Build());
        }
        
        [Command("muterole")]
        public async Task SetMuteRole(IRole role)
        {
            _moderation.CreateOrSetMutedRole(Context.Guild, role);
            await ReplyAsync($"Successfully set the muterole to {role.Mention}.");
        }

        [Command("muterole create")]
        public async Task CreateAndSetMuteRole()
        {
            _moderation.CreateOrSetMutedRole(Context.Guild);
            var role = Context.Guild.GetRole(_moderation.GetMutedRoleId(Context.Guild.Id));

            await ReplyAsync($"Successfully created the mutedrole {role.Mention}.");
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
                    regex[i].Replace(input, "");
                    results[i] = int.Parse(match.Captures[i].Value);
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
