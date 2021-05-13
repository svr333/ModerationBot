using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AdvancedBot.Core.Extensions;
using AdvancedBot.Core.Commands.Preconditions;
using AdvancedBot.Core.Entities.Enums;
using AdvancedBot.Core.Services.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;
using System.Linq;
using Humanizer.Localisation;

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

        [Command("moderations")]
        [Alias("infractions", "mutes", "bans")]
        [Summary("Shows all on-going timed infractions (mutes/bans)")]
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task ShowCurrentInfractionsAsync()
        {
            var timedInf = _moderation.GetCurrentTimedInfractions(Context.Guild.Id);
            var fields = new List<EmbedField>();

            if (timedInf.Length < 1)
            {
                await ReplyAsync($"There are currently no active infractions.");
                return;
            }

            var embed = new EmbedBuilder()
            {
                Title = $"Moderations for {Context.Guild.Name}",
                Color = Color.Blue
            };

            for (int i = 0; i < timedInf.Length; i++)
            {
                var mod = Context.Client.GetUser(timedInf[i].ModeratorId);
                var infractioner = Context.Client.GetUser(timedInf[i].InfractionerId);
                var infractionerName = $"{timedInf[i].InfractionerId}";

                if (infractioner != null)
                    infractionerName = $"{infractioner.Username}#{infractioner.DiscriminatorValue}";

                fields.Add(new EmbedFieldBuilder() 
                {
                    Name =$"Case {timedInf[i].Id} | {timedInf[i].Type} for {infractionerName}",
                    Value = $"{timedInf[i].Reason}"
                }
                .Build());
            }

            await SendPaginatedMessageAsync(fields, null, embed);
        }

        [Command("case")]
        [Summary("Shows information about that mod case.")]
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task GetCaseAsync(uint id)
        {
            var infraction = _moderation.GetInfraction(Context.Guild.Id, id);
            var infractioner = Context.Client.GetUser(infraction.InfractionerId);
            var moderator = Context.Client.GetUser(infraction.ModeratorId);

            var embed = new EmbedBuilder()
            {
                Title = $"#{infraction.Id} | {infraction.Type.Humanize()}",
                Description = $"**Reason:** {infraction.Reason}\n\u200b"
            }
            .WithFooter($"Id: {infraction.InfractionerId} | ModId: {moderator.Id}")
            .AddField($"User", $"{infractioner.Mention}", true)
            .AddField($"Moderator", $"{moderator.Mention}", true)
            .WithColor(_moderation.GetColorFromInfractionType(infraction.Type));

            if (infraction.FinishesAt != null)
            {
                embed.AddField($"Ends At", $"{(DateTime.UtcNow - infraction.FinishesAt.Value).Humanize(3, minUnit: TimeUnit.Second)}", true);
            }

            await ReplyAsync($"", false, embed.Build());
        }

        [Command("modlogs")]
        [Summary("Shows the modlogs related to the person.")]
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task GetModLogsForUserAsync(IUser user)
        {
            var infractions = _moderation.GetAllUserInfractions(Context.Guild.Id, user.Id);
            var fields = new List<EmbedField>();

            var embed = new EmbedBuilder()
            {
                Title = $"Modlogs for {user.Username}:",
                Color = Color.DarkBlue
            };

            for (int i = 0; i < infractions.Length; i++)
            {
                var mod = Context.Client.GetUser(infractions[i].ModeratorId);

                fields.Add(new EmbedFieldBuilder()
                {
                    Name = $"Case #{infractions[i].Id} | {infractions[i].Type.Humanize()} by {mod.Username}",
                    Value = $"{infractions[i].Reason} | {infractions[i].Date.ToShortDateString()}\n\u200b"
                }
                .Build());
            }

            await SendPaginatedMessageAsync(fields, null, embed);
        }

        [Command("warnings")]
        [Summary("Shows all warnings for that user.")]
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task GetWarningsForUserAsync(IUser user)
        {
            var warnings = _moderation.GetAllUserInfractions(Context.Guild.Id, user.Id);

            var embed = new EmbedBuilder()
            {
                Title = $"{user.Username}'s warnings",
                Color = Color.DarkBlue
            };

            var fields = new List<EmbedField>();

            for (int i = 0; i < warnings.Length; i++)
            {
                var mod = Context.Client.GetUser(warnings[i].ModeratorId);

                fields.Add(new EmbedFieldBuilder()
                {
                    Name = $"Case #{warnings[i].Id} | {warnings[i].Type.Humanize()} by {mod.Username}",
                    Value = $"{warnings[i].Reason} | {warnings[i].Date.ToShortDateString()}\n\u200b"
                }
                .Build());
            }

            await SendPaginatedMessageAsync(fields, null, embed);
        }

        [Command("reason")]
        [Summary("Change the reason of an existing case.")]
        [RequireCustomPermission(GuildPermission.KickMembers)]
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
        [RequireCustomPermission(GuildPermission.KickMembers)]
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
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task WarnUserAsync([RequireHigherHierarchyPrecondition][EnsureNotSelf] IUser user, [Remainder] string reason)
        {
            var warning = _moderation.WarnUserInGuild(user, (SocketGuildUser)Context.User, reason);
            
            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"**{Context.Guild.Name}:** You were warned",
                    Color = _moderation.GetColorFromInfractionType(warning.Type)
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
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task RemoveWarnAsync(uint caseId)
        {
            var warning = _moderation.RemoveWarningFromGuild(Context.Guild.Id, Context.User.Id, caseId);
            var user = Context.Client.GetUser(warning.Id);

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"**{Context.Guild.Name}:** Warning Redacted",
                    Description = $"Warning with the below reason has been redacted by a moderator.\n\u200b",
                    Color = _moderation.GetColorFromInfractionType(warning.Type)
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
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task RemoveAllUserWarns(IGuildUser user)
        {
            _moderation.ClearAllInfractionsForUser(user, Context.User.Id);
            await ReplyAsync($"Cleared all warns for {user.Mention}.");
        }

        [Command("kick")]
        [Summary("Kicks a user.")]
        [RequireBotPermission(GuildPermission.KickMembers)]
        [RequireCustomPermission(GuildPermission.KickMembers)]

        public async Task KickUserAsync([RequireHigherHierarchyPrecondition][EnsureNotSelf] IGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"You were kicked from **{Context.Guild.Name}**",
                    Color = _moderation.GetColorFromInfractionType(InfractionType.Kick)
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
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireCustomPermission(GuildPermission.BanMembers)]
        public async Task BanUserAsync([RequireHigherHierarchyPrecondition][EnsureNotSelf] IUser user, [Remainder] string reason = "No reason provided.")
        {
            var time = ParseTimeSpanFromString(ref reason);
            if (string.IsNullOrEmpty(reason)) reason = "No reason provided.";

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();

                var embed = new EmbedBuilder()
                {
                    Title = $"You were banned from **{Context.Guild.Name}**",
                    Color = _moderation.GetColorFromInfractionType(InfractionType.Ban)
                }
                .AddField($"Reason", reason, true);

                if (time.TotalMilliseconds > 1000)
                    embed.AddField("Duration", time.Humanize(3, minUnit: TimeUnit.Second));

                await dmChannel.SendMessageAsync("", false, embed.Build());
            }
            catch (Exception)
            {
                await ReplyAsync($"Could not dm user to notify him.");
            }

            var infraction = await _moderation.BanUserFromGuildAsync(user, Context.Guild.Id, Context.User.Id, reason, time, 7);

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
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireCustomPermission(GuildPermission.BanMembers)]
        public async Task UnbanUserAsync(IUser user, [Remainder] string reason = "No reason provided.")
        {
            var infraction = _moderation.UnbanUserFromGuild(Context.User.Id, user.Id, Context.Guild.Id, reason);
            await ReplyAsync($"#{infraction.Id} | Unbanned {user.Mention}.");
        }

        [Command("mute")]
        [Summary("Mutes a user.")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task MuteUserAsync([RequireHigherHierarchyPrecondition][EnsureNotSelf] IGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var time = ParseTimeSpanFromString(ref reason);
            if (string.IsNullOrEmpty(reason)) reason = "No reason provided.";

            if (time.TotalMilliseconds <= 1000)
                time = new TimeSpan(14, 0, 0, 0);

            var infraction = _moderation.MuteUser(user, Context.User.Id, time, reason);

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"You were muted in **{Context.Guild.Name}**",
                    Color = _moderation.GetColorFromInfractionType(InfractionType.Mute)
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
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task UnmuteUserAsync([RequireHigherHierarchyPrecondition][EnsureNotSelf] SocketGuildUser user, [Remainder] string reason = "No reason provided.")
        {
            var infraction = _moderation.UnmuteUser(user, Context.User.Id, reason);

            try
            {
                var dmChannel = await user.GetOrCreateDMChannelAsync();
                await dmChannel.SendMessageAsync("", false, new EmbedBuilder()
                {
                    Title = $"Your mute was lifted in **{Context.Guild.Name}**",
                    Color = _moderation.GetColorFromInfractionType(InfractionType.Unmute)
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
        [RequireCustomPermission(GuildPermission.KickMembers)]
        public async Task GetMuteRoleAsync()
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
        [RequireCustomPermission(GuildPermission.ManageGuild)]
        public async Task SetMuteRoleAsync(IRole role)
        {
            _moderation.CreateOrSetMutedRole(Context.Guild, role);
            await ReplyAsync($"Successfully set the muterole to {role.Mention}.");
        }

        [Command("muterole create")]
        [Summary("Creates a new role to add as mute role.")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireCustomPermission(GuildPermission.ManageGuild)]
        public async Task CreateAndSetMuteRoleAsync()
        {
            _moderation.CreateOrSetMutedRole(Context.Guild);
            var role = Context.Guild.GetRole(_moderation.GetMutedRoleId(Context.Guild.Id));

            await ReplyAsync($"Successfully created the mutedrole {role.Mention}.");
        }

        [Command("modchannel")]
        [Summary("Set the modlogs channel.")]
        [RequireCustomPermission(GuildPermission.ManageGuild)]
        public async Task SetModLogChannelAsync(ITextChannel channel)
        {
            _moderation.SetModLogsChannel(Context.Guild.Id, channel);
            await ReplyAsync($"Set modlogs channel to {channel.Mention}.");
        }

        [Command("whois")]
        [Summary("Shows info about the user.")]
        public async Task GetUserInfoAsync(IUser user = null)
        {
            user = user ?? Context.User;
            var gUser = (await (await Context.Client.Rest.GetGuildAsync(Context.Guild.Id)).GetUserAsync(user.Id));
            var nickNameText = string.IsNullOrEmpty(gUser.Nickname) ? "" : $"▫️**Nickname:** {gUser.Nickname}\n";

            var embed = new EmbedBuilder()
            {
                Title = $"Userinfo for {user.Username}",
                ThumbnailUrl = user.GetAvatarUrl(),
                Color = gUser.GetUserTopColour(Context.Client.Rest, Context.Guild.Id)
            }
            .AddField("📋 User Info", $"▫**Id:** {user.Id} ({user.Mention})\n▫**Username:** {user.Username}#{user.DiscriminatorValue}\n{nickNameText}▫️**Avatar:** [png]({user.GetAvatarUrl(ImageFormat.Png)}) | [jpg]({user.GetAvatarUrl(ImageFormat.Jpeg)}) | [gif]({user.GetAvatarUrl(ImageFormat.Gif)}) | [webp]({user.GetAvatarUrl(ImageFormat.WebP)})\n\u200b")
            .AddField("🕧 Important Dates", $"▫️**Created:** {user.CreatedAt.UtcDateTime.ToLongDateString()} {user.CreatedAt.UtcDateTime.ToLongTimeString()}\n▫️**Joined:** {gUser.JoinedAt.Value.UtcDateTime.ToLongDateString()} {gUser.JoinedAt.Value.UtcDateTime.ToLongTimeString()}\n\u200b")
            .AddField("Roles", string.Join(" ", gUser.RoleIds.Select(x => $"<@&{x}>").Skip(1)))
            .WithFooter($"Requested by {Context.User.Username} ({Context.User.Id})", Context.User.GetAvatarUrl());

            await ReplyAsync($"", false, embed.Build());
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
