using Supabase;
using Microsoft.Extensions.Configuration;

namespace backend
{
    public class SupabaseClientService
    {
        private readonly Supabase.Client _client;

        public SupabaseClientService(IConfiguration configuration)
        {
            var url = configuration["Supabase:Url"];
            var key = configuration["Supabase:Key"];

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
                throw new Exception("Supabase URL or Key is not set in environment variables.");

            _client = new Client(url, key);
        }

        public Supabase.Client GetClient()
        {
            return _client;
        }
    }
}