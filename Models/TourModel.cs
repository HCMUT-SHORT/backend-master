using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace backend.Models
{
    [Table("tour")]
    public class Tour : BaseModel
    {
        [PrimaryKey("id", true)]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("destination")]
        public string? Destination { get; set; }

        [Column("imageUrl")]
        public string? ImageUrl { get; set; }

        [Column("checkindate")]
        public string? CheckInDate { get; set; }

        [Column("checkoutdate")]
        public string? CheckOutDate { get; set; }

        [Column("minBudget")]
        public long? MinBudget { get; set; }

        [Column("maxBudget")]
        public long? MaxBudget { get; set; }

        [Column("travelType")]
        public string? TravelType { get; set; }

        [Column("createdBy")]
        public Guid CreatedBy { get; set; }

        [Column("createdAt")]
        public DateTime? CreatedAt { get; set; }
    }
}