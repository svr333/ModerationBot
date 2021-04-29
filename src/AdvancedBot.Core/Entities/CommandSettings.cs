using System.Collections.Generic;

namespace AdvancedBot.Core.Entities
{
    public class CommandSettings
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public bool ChannelListIsBlacklist { get; set; }
        public bool RolesListIsBlacklist { get; set; }
        public List<ulong> WhitelistedChannels { get; set; } = new List<ulong>();
        public List<ulong> WhitelistedRoles { get; set; } = new List<ulong>();
    }
}
