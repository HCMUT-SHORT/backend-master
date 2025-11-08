using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SupabaseClientService _supabaseService;

        public AuthController(SupabaseClientService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "Supabase service is working!" });
        }
    }
}