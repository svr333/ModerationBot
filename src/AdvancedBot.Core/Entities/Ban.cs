using System;

namespace AdvancedBot.Core.Entities
{
    public class Ban
    {
        public int Id { get; set; }
        public ulong ModeratorId { get; set; }
        public ulong BannedUserId { get; set; }
        public string Reason { get; set; }
        public DateTime FinishesAt { get; set; }
    }
}
