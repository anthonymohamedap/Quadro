using System;
using System.ComponentModel.DataAnnotations;

namespace QuadroApp.Model.DB
{
    public enum VoorraadAlertType
    {
        LowStock = 0,
        BelowMinimum = 1,
        OpenShortage = 2,
        OrderOverdue = 3,
        PartialReceiptPending = 4
    }

    public enum VoorraadAlertStatus
    {
        Open = 0,
        Dismissed = 1,
        Resolved = 2
    }

    public class VoorraadAlert
    {
        public int Id { get; set; }

        public int? TypeLijstId { get; set; }
        public TypeLijst? TypeLijst { get; set; }

        public VoorraadAlertType AlertType { get; set; }
        public VoorraadAlertStatus Status { get; set; } = VoorraadAlertStatus.Open;
        public DateTime AangemaaktOp { get; set; } = DateTime.UtcNow;
        public DateTime? LaatstHerinnerdOp { get; set; }
        public DateTime? VolgendeHerinneringOp { get; set; }

        [MaxLength(120)]
        public string? BronReferentie { get; set; }

        [MaxLength(500)]
        public string Bericht { get; set; } = string.Empty;
    }
}
