using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace backend.Models
{
    [Table("placetostay")]
    public class PlaceToStay : BaseModel
    {
        [PrimaryKey("id", false)]
        public Guid Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("detail")]
        public string? Detail { get; set; }

        [Column("imageUrl")]
        public string? ImageUrl { get; set; }

        [Column("bookingUrl")]
        public string? BookingUrl { get; set; }

        [Column("price")]
        public long? Price { get; set; }

        [Column("rating")]
        public float? Rating { get; set; }

        [Column("totalRating")]
        public long? TotalRating { get; set; }

        [Column("isSelected")]
        public bool? IsSelected { get; set; }

        [Column("tourId")]
        public Guid TourId { get; set; } 
    }
}