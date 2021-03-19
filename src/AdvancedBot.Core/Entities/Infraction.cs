using System;
using AdvancedBot.Core.Entities.Enums;

namespace AdvancedBot.Core.Entities
{
    public class Infraction
    {
        public Infraction() {} // LiteDB
        public Infraction(uint id, ulong infractionerId, ulong modId, InfractionType type, DateTime? endsAt = null, string reason)
        {
            Id = id;
            ModeratorId = modId;
            InfractionerId = infractionerId;
            Type = type;
            FinishesAt = endsAt;
            Reason = reason;
            Date = DateTime.UtcNow;
        }

        public uint Id { get; set; }
        public ulong ModeratorId { get; set; }
        public ulong InfractionerId { get; set; }
        public string Reason { get; set; }
        public DateTime? FinishesAt { get; set; }
        public InfractionType Type { get; set; }
        public DateTime Date { get; set; }
    }
}
