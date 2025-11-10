using Microsoft.AspNetCore.Mvc;
using Supabase.Gotrue;
using System.Text.Json;

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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginAuthRequest request)
        {
            try
            {
                var response = await _supabaseService.GetClient().Auth.SignIn(request.Email, request.Password);

                if (response == null || response.User == null || response.User.Id == null)
                    return BadRequest("There is an error when login into account");

                string? fullName = null;
                if (response.User.UserMetadata != null && response.User.UserMetadata.TryGetValue("full_name", out var nameValue))
                {
                    fullName = nameValue?.ToString();
                }

                return Ok(new
                {
                    user = new
                    {
                        response.User.Id,
                        fullName
                    },
                    token = response.AccessToken
                });
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException ex)
            {
                string msg = "An error occurred";
                using var jsonDoc = JsonDocument.Parse(ex.Message);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("msg", out var msgProp))
                    msg = msgProp.GetString() ?? "An error occurred";

                return BadRequest(msg);
            }
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
        {
            try
            {
                var options = new SignUpOptions
                {
                    Data = new Dictionary<string, object>
                    {
                        { "full_name", request.FullName }
                    }
                };

                var response = await _supabaseService.GetClient().Auth.SignUp(request.Email, request.Password, options);

                if (response == null || response.User == null || response.User.Id == null)
                    return BadRequest("There is an error when creating account");

                string? fullName = null;
                if (response.User.UserMetadata != null && response.User.UserMetadata.TryGetValue("full_name", out var nameValue))
                {
                    fullName = nameValue?.ToString();
                }

                return Ok(new
                {
                    user = new
                    {
                        response.User.Id,
                        fullName
                    },
                    token = response.AccessToken
                });
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException ex)
            {
                string msg = "An error occurred";
                using var jsonDoc = JsonDocument.Parse(ex.Message);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("msg", out var msgProp))
                    msg = msgProp.GetString() ?? "An error occurred";

                return BadRequest(msg);
            }
        }

        [HttpGet("profile")]
        public async Task<IActionResult> Profile([FromHeader(Name = "Authorization")] string? authHeader)
        {
            if (string.IsNullOrEmpty(authHeader))
                return Unauthorized("Missing Authorization header");

            var token = authHeader.Replace("Bearer ", "").Trim();

            try
            {
                var user = await _supabaseService.GetClient().Auth.GetUser(token);

                if (user == null || user.Id == null)
                    return BadRequest("Invalid or expired token");

                string? fullName = null;
                if (user.UserMetadata != null && user.UserMetadata.TryGetValue("full_name", out var nameValue))
                {
                    fullName = nameValue?.ToString();
                }

                return Ok(new
                {
                    user = new
                    {
                        user.Id,
                        fullName
                    }
                });
            }
            catch (Supabase.Gotrue.Exceptions.GotrueException ex)
            {
                string msg = "An error occurred";
                try
                {
                    using var jsonDoc = JsonDocument.Parse(ex.Message);
                    if (jsonDoc.RootElement.TryGetProperty("msg", out var msgProp))
                        msg = msgProp.GetString() ?? msg;
                }
                catch { }

                return BadRequest(msg);
            }
        }
    }

    public class LoginAuthRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    public class SignUpRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FullName { get; set; }
    }
}