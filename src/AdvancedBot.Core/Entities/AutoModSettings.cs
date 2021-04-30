using System.Collections.Generic;

namespace AdvancedBot.Core.Entities
{
    public class AutoModSettings
    {
        public ulong LogChannelId { get; set; }
        public BlacklistedWordsSettings BlacklistedWordsSettings { get; set; } = new BlacklistedWordsSettings();
        public SpamSettings SpamSettings { get; set; } = new SpamSettings();
        public List<string> BlacklistedLinks { get; set; }
    }

    public class BlacklistedWordsSettings
    {
        public List<string> BlacklistedWords { get; set; } = new List<string>();
        public List<ulong> WhitelistedChannels { get; set; } = new List<ulong>();
        public List<ulong> WhitelistedRoles { get; set; } = new List<ulong>();
    }

    public class SpamSettings
    {
        public int MaxMessagesPerFiveSeconds { get; set; }
        public List<ulong> WhitelistedChannels { get; set; } = new List<ulong>();
        public List<ulong> WhitelistedRoles { get; set; } = new List<ulong>();
    }
}
