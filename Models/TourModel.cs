using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Text.Json;

namespace backend.Models
{
    [Table("tour")]
    public class Tour : BaseModel
    {
        [Column("destination")]
        public string? Destination { get; set; }

        [Column("checkindate")]
        public string? CheckInDate { get; set; }

        [Column("checkoutdate")]
        public string? CheckOutDate { get; set; }

        [Column("minBugget")]
        public long? MinBugget { get; set; }

        [Column("maxBugget")]
        public long? MaxBugget { get; set; }

        [Column("travelType")]
        public string? TravelType { get; set; }

        [Column("createdBy")]
        public Guid? CreatedBy { get; set; }

        [Column("information")]
        public object? Information { get; set; }
    }
}