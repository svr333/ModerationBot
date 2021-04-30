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

        public AutoModerationService(DiscordSocketClient client, GuildAccountService guilds)
        {
            _client = client;
            _guilds = guilds;
        }

        public async Task HandleMessageReceivedChecksAsync(GuildAccount guild, IMessage message)
        {
            if (message.Author.IsBot)
                return;

            if (MessageContainsBlacklistedWords(guild.AutoMod, message.Content.ToLower(), out string trigger)
            && !UserCanBypass(message.Author as IGuildUser, message.Channel.Id, guild.AutoMod.BlacklistedWordsSettings.WhitelistedRoles, guild.AutoMod.BlacklistedWordsSettings.WhitelistedChannels))
            {
                await AddAutoModInfractionToGuild(guild, message.Author.Id, AutoModInfractionType.BlacklistedWords, trigger);
                await message.DeleteAsync();
            }
            else if (UserIsSpamming()
            && !UserCanBypass(message.Author as IGuildUser, message.Channel.Id, guild.AutoMod.SpamSettings.WhitelistedRoles, guild.AutoMod.SpamSettings.WhitelistedChannels))

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
