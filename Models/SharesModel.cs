using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Security.Cryptography;
using System.Text;

namespace backend.Models
{
    [Table("shares")]
    public class Share : BaseModel
    {
        [Column("tourId")]
        public Guid TourId { get; set; }

        [Column("userId")]
        public Guid UserId { get; set; }

        [Column("code")]
        public string? Code { get; set; }


        public static string GenerateShareCode(Guid tourId)
        {
            byte[] guidBytes = tourId.ToByteArray();
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(guidBytes);
                
                const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                var result = new StringBuilder();
                
                for (int i = 0; i < 10; i++)
                {
                    result.Append(chars[hash[i] % 36]);
                }
                return result.ToString();
            }
        }

    }
    
}
