using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace backend.Models
{
    [Table("transportation")]
    public class Transportation : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("type")]
        public string? Type { get; set; }

        [Column("time")]
        public string? Time { get; set; }

        [Column("price")]
        public long? Price { get; set; }

        [Column("bookingUrl")]
        public string? BookingUrl { get; set; }

        [Column("isSelected")]
        public bool? IsSelected { get; set; }

        [Column("isPossible")]
        public bool? IsPossible { get; set; }

        [Column("tourId")]
        public Guid TourId { get; set; } 
    }
}