using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace AdvancedBot.Core.Commands.Preconditions
{
    public class EnsureNotSelf : ParameterPreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services)
        {
            var user = value as IUser;

            if (context.User.Id == user.Id)
            {
                return Task.FromResult(PreconditionResult.FromError("You cannot run this command on yourself."));
            }

            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
