using System;
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
            .AddField($"Moderator", $"{moderator.Mention}", true);

            if (infraction.FinishesAt != null)
            {
                embed.AddField($"Ends At", $"{infraction.FinishesAt.Humanize()}, true");
            }

            switch (infraction.Type)
            {
                case InfractionType.Warning:
                    embed.WithColor(Color.Orange);
                    break;
                case InfractionType.Mute:
                    embed.WithColor(Color.Red);
                    break;
                case InfractionType.Kick:
                    embed.WithColor(Color.DarkOrange);
                    break;
                case InfractionType.Ban:
                    embed.WithColor(Color.DarkRed);
                    break;
                default:
                    embed.WithColor(Color.Green);
                    break;
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
            await ReplyAsync($"#{warning.Id} | {user.Mention} successfully warned.");
        }

        [Command("delwarn")]
        public async Task RemoveWarnAsync(uint caseId)
        {
            var warning = _moderation.RemoveWarningFromGuild(Context.Guild.Id, Context.User.Id, caseId);
            await ReplyAsync($"Successfully removed warning #{caseId}.");
        }

        [Command("kick")]
        public async Task KickUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var infraction = await _moderation.KickUserFromGuildAsync(user, Context.User.Id, reason);
            await ReplyAsync($"Successfully kicked {user.Mention}");
        }

        [Command("ban")][Alias("tempban")]
        public async Task BanUserAsync([EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var infraction = await _moderation.BanUserFromGuildAsync(user, Context.User.Id, reason, null, 0);
            await ReplyAsync($"Successfully banned {user.Mention}.");
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
            
        }

        [Command("unmute")]
        public async Task UnmuteUserAsync([EnsureNotSelf] IUser user)
        {
            
        }
    }
}
