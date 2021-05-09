using System.Linq;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace AdvancedBot.Core.Extensions
{
    public static class GuildUserExt
    {
        public static Color GetUserTopColour(this SocketGuildUser user)
        {
            var hierarchyOrderedRoleList = user.Roles.OrderByDescending(x => x.Position).ToList();

            var color = hierarchyOrderedRoleList.FirstOrDefault(x => x.Color != Color.Default).Color;

            return color == Color.Default ? new Color(5198940) : color;
        }

        public static Color GetUserTopColour(this RestGuildUser user, DiscordSocketRestClient client, ulong guildId)
        {
            var guild = client.GetGuildAsync(guildId).GetAwaiter().GetResult();
            var userRoleIds = user.RoleIds.ToArray();

            for (int i = 0; i < userRoleIds.Length; i++)
            {
                var role = guild.Roles.FirstOrDefault(x => x.Id == userRoleIds[i]);
                
                if (role.Color == Color.Default)
                    continue;

                return role.Color;
            }

            return new Color(5198940);
        }
    }
}
