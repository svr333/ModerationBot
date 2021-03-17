using System;

namespace AdvancedBot.Core.Entities
{
    public class Mute
    {
        public int Id { get; set; }
        public ulong ModeratorId { get; set; }
        public ulong MuteeId { get; set; }
        public string Reason { get; set; }
        public DateTime FinishesAt { get; set; }
    }
}
