using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;

namespace AdvancedBot.Core.Commands.Preconditions
{
    public class RequireHigherHierarchyPrecondition : ParameterPreconditionAttribute
    {
        public async override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services)
        {
            var ctx = context as SocketCommandContext;
            var guild = await ctx.Client.Rest.GetGuildAsync(ctx.Guild.Id);
            var user = await guild.GetUserAsync((value as IUser).Id);
            var bot = await guild.GetUserAsync(context.Client.CurrentUser.Id);

            var userHierarchy = GetHierarchyOfUser(guild, user);

            if (guild.Roles.OrderByDescending(r => r.Position).FirstOrDefault().Position <= userHierarchy)
            {
                return PreconditionResult.FromError("The bot must be ranked higher than the mentioned user.");
            }

            return ((context.User as SocketGuildUser).Hierarchy > userHierarchy)
                    ? PreconditionResult.FromSuccess()
                    : PreconditionResult.FromError("You must be ranked higher than the mentioned user.");
        }

        private int GetHierarchyOfUser(RestGuild guild, RestGuildUser user)
        {
            var orderedRoles = guild.Roles.OrderByDescending(r => r.Position).ToArray();
            var orderedRoleIds = orderedRoles.Select(x => x.Id).ToArray();

            for (int i = 0; i < orderedRoleIds.Length; i++)
            {
                var test = user.RoleIds.FirstOrDefault(x => x == orderedRoleIds[i]);

                if (test != 0)
                    return orderedRoles[i].Position;
            }

            return 0;
        }
    }
}
