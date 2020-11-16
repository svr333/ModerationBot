using System.Threading.Tasks;
using AdvancedBot.Core.Entities;
using Discord;
using Discord.WebSocket;

namespace AdvancedBot.Core.Services.Commands
{
    public class ModerationService
    {
        public async Task BanUserFromGuildAsync(GuildAccount guild, SocketGuildUser user, int pruneDays = 0, string reason = "No reason provided.")
        {
            await user.BanAsync(pruneDays, reason);
        }
    }
}
