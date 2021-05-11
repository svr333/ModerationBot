using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvancedBot.Core.Entities;
using AdvancedBot.Core.Entities.Enums;
using AdvancedBot.Core.Services.DataStorage;
using Discord;
using Discord.WebSocket;
using Humanizer;

namespace AdvancedBot.Core.Services.Commands
{
    public class AutoModerationService
    {
        private DiscordSocketClient _client;
        private GuildAccountService _guilds;
        private ModerationService _moderation;
        private Dictionary<ulong, Queue<DateTimeOffset>> _recentMessages = new Dictionary<ulong, Queue<DateTimeOffset>>();

        public AutoModerationService(DiscordSocketClient client, GuildAccountService guilds, ModerationService moderation)
        {
            _client = client;
            _guilds = guilds;
            _moderation = moderation;
        }

        public async Task HandleMessageReceivedChecksAsync(GuildAccount guild, IMessage message)
        {
            if (message.Author.IsBot)
                return;

            if (!UserCanBypass(message.Author as IGuildUser, message.Channel.Id, guild.AutoMod.BlacklistedWordsSettings.WhitelistedRoles, guild.AutoMod.BlacklistedWordsSettings.WhitelistedChannels)
            && MessageContainsBlacklistedWords(guild.AutoMod, message.Content.ToLower(), out string trigger))
            {
                await AddAutoModInfractionToGuild(guild, message.Author.Id, AutoModInfractionType.BlacklistedWords, trigger);
                await message.DeleteAsync();
            }
            else if (!UserCanBypass(message.Author as IGuildUser, message.Channel.Id, guild.AutoMod.SpamSettings.WhitelistedRoles, guild.AutoMod.SpamSettings.WhitelistedChannels)
            && UserIsSpamming(guild, message.Author.Id, message.Timestamp))
            {
                await AddAutoModInfractionToGuild(guild, message.Author.Id, AutoModInfractionType.Spam, $"<#{message.Channel.Id}>");
                var messages = await message.Channel.GetMessagesAsync(10).FlattenAsync();
                messages = messages.Where(x => x.Author.Id == message.Author.Id).Take(5);

                await ((ITextChannel) message.Channel).DeleteMessagesAsync(messages);

                var violations = guild.AutoModInfractions.Count(x => x.InfractionerId == message.Author.Id && x.Type == AutoModInfractionType.Spam);
                if (violations >= 3)
                {
                    _guilds.SaveGuildAccount(guild);
                    _moderation.MuteUser(message.Author as IGuildUser, _client.CurrentUser.Id, new TimeSpan(1, 0, 0), $"Spam Automod violation #{violations}");
                    return;
                }
            }
            else if (message.MentionedUserIds.Count >= 5)
            {
                await AddAutoModInfractionToGuild(guild, message.Author.Id, AutoModInfractionType.MassMention, $"<#{message.Channel.Id}>");
                _guilds.SaveGuildAccount(guild);
                _moderation.MuteUser(message.Author as IGuildUser, _client.CurrentUser.Id, new TimeSpan(1, 30, 0), "Mass mention Automod violation.");
                return;
            }

            _guilds.SaveGuildAccount(guild);
        }

        public void SetAutoModLogChannel(ulong guildId, ITextChannel channel)
        {
            if (channel.GuildId != guildId)
                throw new Exception($"Channel needs to be in the same guild.");
            
            var guild = _guilds.GetOrCreateGuildAccount(guildId);
            guild.AutoMod.LogChannelId = channel.Id;

            _guilds.SaveGuildAccount(guild);
        }

        private async Task<AutoModInfraction> AddAutoModInfractionToGuild(GuildAccount guild, ulong userId, AutoModInfractionType type, string trigger = "")
        {
            var infraction = guild.AddAutoModInfractionToGuild(userId, type, trigger);

            if (guild.AutoMod.LogChannelId != 0)
            {
                var channel = _client.GetChannel(guild.AutoMod.LogChannelId) as ITextChannel;
                var embed = await GetMessageEmbedForLogAsync(infraction);

                await channel.SendMessageAsync("", false, embed);
            }

            return infraction;
        }

        private async Task<Embed> GetMessageEmbedForLogAsync(AutoModInfraction infraction)
        {
            var infractioner = await _client.Rest.GetUserAsync(infraction.InfractionerId);

            var embed = new EmbedBuilder()
            {
                Title = $"Automod Violation | {infraction.Type.Humanize()}",
                Color = Color.Purple
            }
            .AddField("Case Id", infraction.Id, true)
            .AddField("User", infractioner.Mention, true);

            if (!string.IsNullOrEmpty(infraction.Trigger))
                embed.AddField($"Trigger", infraction.Trigger, true);

            return embed.Build();
        }

        private bool MessageContainsBlacklistedWords(AutoModSettings settings, string message, out string trigger)
        {
            trigger = "";
            
            for (int i = 0; i < settings.BlacklistedWordsSettings.BlacklistedWords.Count; i++)
            {
                if (message.Contains(settings.BlacklistedWordsSettings.BlacklistedWords[i]))
                {
                    trigger = settings.BlacklistedWordsSettings.BlacklistedWords[i];
                    return true;
                }
            }

            return false;
        }
    
        private bool UserIsSpamming(GuildAccount guild, ulong userId, DateTimeOffset timestamp)
        {
            if (guild.AutoMod.SpamSettings.MaxMessages == 0)
                return false;

            if (_recentMessages.TryGetValue(userId, out Queue<DateTimeOffset> timeStamps))
            {
                if (timeStamps.Count < guild.AutoMod.SpamSettings.MaxMessages)
                {
                    timeStamps.Enqueue(timestamp);
                    return false;
                }
                
                if ((timeStamps.Last() - timeStamps.First()).TotalMilliseconds < guild.AutoMod.SpamSettings.Seconds * 1000)
                {
                    timeStamps.Clear();
                    return true;
                }

                timeStamps.Dequeue();
                timeStamps.Enqueue(timestamp);
                return false;
            }
            else
            {
                var queue = new Queue<DateTimeOffset>(5);
                queue.Enqueue(timestamp);

                _recentMessages.Add(userId, queue);
                return false;
            }
        }

        private bool UserCanBypass(IGuildUser user, ulong channelId, List<ulong> allowedRoles, List<ulong> allowedChannels)
        {
            if (allowedChannels.Contains(channelId))
                return true;

            for (int i = 0; i < allowedRoles.Count; i++)
            {
                if (user.RoleIds.Contains(allowedRoles[i]))
                    return true;
            }

            return false;
        }
    }
}
