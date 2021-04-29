using System;
using AdvancedBot.Core.Entities.Enums;

namespace AdvancedBot.Core.Entities
{
    public class AutoModInfraction
    {
        public AutoModInfraction(uint id, ulong userId, AutoModInfractionType type, string trigger = "")
        {
            Id = id;
            InfractionerId = userId;
            Type = type;
            Trigger = trigger;
            Date = DateTime.UtcNow;
        }

        public uint Id { get; set; }
        public ulong InfractionerId { get; set; }
        public AutoModInfractionType Type { get; set; }
        public string Trigger { get; set; }
        public DateTime Date { get; set; }
    }
}
