using System;
using System.Threading.Tasks;
using AdvancedBot.Core.Commands.Preconditions;
using Discord;
using Discord.Commands;

namespace AdvancedBot.Core.Commands.Modules
{
    [Group("automoderation")]
    [Alias("am")]
    [Summary("Category to manage the automoderation system in your server.")]
    [RequireCustomPermission(GuildPermission.ManageGuild)]
    public class AutoModerationModule : TopModule
    {
        [Command("")]
        [Summary("Shows an overview of the current automoderation settings.")]
        public async Task ShowAutoModOverviewAsync()
        {
            await ReplyAsync($"Not implemented yet");
        }

        [Command("modchannel")]
        [Summary("Gets the automod log channel.")]
        public async Task GetAutoModLogChannel()
        {
            var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);
            var channel = Context.Client.GetChannel(guild.AutoMod.LogChannelId) as ITextChannel;

            if (channel is null)
                throw new Exception($"The channel is invalid, please enter a new one.");

            await ReplyAsync($"Automod log channel is set to {channel.Mention}.");
        }

        [Command("modchannel")]
        [Summary("Sets the automod log channel.")]
        public async Task SetAutoModLogChannel(ITextChannel channel)
        {
            var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);
            
            if (Context.Guild.Id != channel.GuildId)
                throw new Exception($"Channel needs to be in the same server");

            guild.AutoMod.LogChannelId = channel.Id;
            Accounts.SaveGuildAccount(guild);
            await ReplyAsync($"Automod log channel set to {channel.Mention}.");
        }

        [Group("bannedwords")]
        [Alias("bw")]
        [Summary("Subcategory that holds all commands related to banned words.")]
        public class BannedWordsModule : AutoModerationModule
        {
            [Command("")]
            [Summary("Shows all the banned words.")]
            public async Task ShowBannedWordsAsync()
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (guild.AutoMod.BlacklistedWordsSettings.BlacklistedWords.Count == 0)
                {
                    await ReplyAsync($"There are no banned words in this server.");
                    return;
                }

                await ReplyAsync($"`{string.Join("`, `", guild.AutoMod.BlacklistedWordsSettings.BlacklistedWords)}`");
            }

            [Command("add")]
            [Summary("Add a word to the banned word list.")]
            public async Task AddBannedWordAsync([Remainder] string word)
            {
                word = word.ToLower().Trim();
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (guild.AutoMod.BlacklistedWordsSettings.BlacklistedWords.Contains(word))
                {
                    await ReplyAsync($"This word is already blacklisted.");
                    return;
                }

                guild.AutoMod.BlacklistedWordsSettings.BlacklistedWords.Add(word);
                Accounts.SaveGuildAccount(guild);

                await ReplyAsync($"Added `{word}` to the blacklist.");
            }

            [Command("remove")]
            [Summary("Removes a word from the banned word list.")]
            public async Task RemoveBannedWordAsync([Remainder] string word)
            {
                word = word.ToLower().Trim();
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (!guild.AutoMod.BlacklistedWordsSettings.BlacklistedWords.Contains(word))
                {
                    await ReplyAsync($"This word isn't blacklisted.");
                    return;
                }

                guild.AutoMod.BlacklistedWordsSettings.BlacklistedWords.Remove(word);
                Accounts.SaveGuildAccount(guild);

                await ReplyAsync($"Removed `{word}` from the blacklist.");
            }
        
            [Command("whitelist add")]
            [Alias("wl add")]
            [Summary("Adds a role to the whitelist.")]
            public async Task AddToWhitelistAsync(IRole role)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (guild.AutoMod.BlacklistedWordsSettings.WhitelistedRoles.Contains(role.Id))
                    throw new Exception($"Role is already whitelisted.");

                guild.AutoMod.BlacklistedWordsSettings.WhitelistedRoles.Add(role.Id);

                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Added {role.Mention} to the whitelist.");
            }

            [Command("whitelist add")]
            [Alias("wl add")]
            [Summary("Adds a channel to the whitelist.")]
            public async Task AddToWhitelistAsync(ITextChannel channel)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (guild.AutoMod.BlacklistedWordsSettings.WhitelistedChannels.Contains(channel.Id))
                    throw new Exception($"Channel is already whitelisted.");

                guild.AutoMod.BlacklistedWordsSettings.WhitelistedChannels.Add(channel.Id);
                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Added {channel.Mention} to the whitelist.");
            }

            [Command("whitelist remove")]
            [Alias("wl remove")]
            [Summary("Removes a role from the whitelist.")]
            public async Task RemoveFromWhitelistAsync(IRole role)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (!guild.AutoMod.BlacklistedWordsSettings.WhitelistedRoles.Contains(role.Id))
                    throw new Exception($"Role isn't on the whitelist.");

                guild.AutoMod.BlacklistedWordsSettings.WhitelistedRoles.Remove(role.Id);
                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Removed {role.Mention} from the whitelist.");
            }

            [Command("whitelist remove")]
            [Alias("wl remove")]
            [Summary("Removes a channel from the whitelist.")]
            public async Task RemoveFromWhitelistAsync(ITextChannel channel)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (!guild.AutoMod.BlacklistedWordsSettings.WhitelistedChannels.Contains(channel.Id))
                    throw new Exception($"Channel isn't on the whitelist.");

                guild.AutoMod.BlacklistedWordsSettings.WhitelistedChannels.Remove(channel.Id);
                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Removed {channel.Mention} from the whitelist.");
            }
        }
    
        [Group("antispam")]
        [Alias("as")]
        [Summary("Subcategory that holds all commands related to anti spam.")]
        public class AntiSpamModule : AutoModerationModule
        {
            [Command("")]
            [Summary("Shows the current antispam settings.")]
            public async Task ShowAntiSpamAsync()
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                await ReplyAsync($"Current limit is **{guild.AutoMod.SpamSettings.MaxMessages}** messages per **{guild.AutoMod.SpamSettings.Seconds}** seconds.\nSet messages to 0 to disable.");
            }

            [Command("set")]
            [Summary("Sets the antispam limit.")]
            public async Task SetAntiSpamAsync(uint messages, uint seconds)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (messages >= 10)
                    throw new Exception($"This number is bigger than Discord's built-in ratelimit.");
                else if (seconds > 10)
                    throw new Exception($"Cannot have more than 11 seconds of anti spam control.");

                guild.AutoMod.SpamSettings.MaxMessages = (int) messages;
                guild.AutoMod.SpamSettings.Seconds = (int) seconds;

                Accounts.SaveGuildAccount(guild);
                await ShowAntiSpamAsync();
            }

            [Command("whitelist add")]
            [Alias("wl add")]
            [Summary("Adds a role to the whitelist.")]
            public async Task AddToWhitelistAsync(IRole role)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (guild.AutoMod.SpamSettings.WhitelistedRoles.Contains(role.Id))
                    throw new Exception($"Role is already whitelisted.");

                guild.AutoMod.SpamSettings.WhitelistedRoles.Add(role.Id);

                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Added {role.Mention} to the whitelist.");
            }

            [Command("whitelist add")]
            [Alias("wl add")]
            [Summary("Adds a channel to the whitelist.")]
            public async Task AddToWhitelistAsync(ITextChannel channel)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (guild.AutoMod.SpamSettings.WhitelistedChannels.Contains(channel.Id))
                    throw new Exception($"Channel is already whitelisted.");

                guild.AutoMod.SpamSettings.WhitelistedChannels.Add(channel.Id);
                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Added {channel.Mention} to the whitelist.");
            }

            [Command("whitelist remove")]
            [Alias("wl remove")]
            [Summary("Removes a role from the whitelist.")]
            public async Task RemoveFromWhitelistAsync(IRole role)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (!guild.AutoMod.SpamSettings.WhitelistedRoles.Contains(role.Id))
                    throw new Exception($"Role isn't on the whitelist.");

                guild.AutoMod.SpamSettings.WhitelistedRoles.Remove(role.Id);
                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Removed {role.Mention} from the whitelist.");
            }

            [Command("whitelist remove")]
            [Alias("wl remove")]
            [Summary("Removes a channel from the whitelist.")]
            public async Task RemoveFromWhitelistAsync(ITextChannel channel)
            {
                var guild = Accounts.GetOrCreateGuildAccount(Context.Guild.Id);

                if (!guild.AutoMod.SpamSettings.WhitelistedChannels.Contains(channel.Id))
                    throw new Exception($"Channel isn't on the whitelist.");

                guild.AutoMod.SpamSettings.WhitelistedChannels.Remove(channel.Id);
                Accounts.SaveGuildAccount(guild);
                await ReplyAsync($"Removed {channel.Mention} from the whitelist.");
            }
        }
    }
}
