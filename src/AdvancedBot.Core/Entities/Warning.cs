using Discord.WebSocket;

namespace AdvancedBot.Core.Entities
{
    public class Warning
    {
        public Warning(uint id, SocketGuildUser warnedUser, SocketGuildUser moderator, string reason)
        {
            WarnedUser = warnedUser;
            Moderator = moderator;
            Reason = reason;
        }

        public uint Id { get; }
        public SocketGuildUser WarnedUser { get; }
        public SocketGuildUser Moderator { get; }
        public string Reason { get; set; }
    }
}
