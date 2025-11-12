using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using backend.Models;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TourController : ControllerBase
    {
        private readonly SupabaseClientService _supabaseService;
        private readonly GeminiService _geminiService;
        private readonly HttpClient _httpClient;

        public TourController(SupabaseClientService supabaseService, GeminiService geminiService, HttpClient httpClient)
        {
            _supabaseService = supabaseService;
            _geminiService = geminiService;
            _httpClient = httpClient;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTour([FromBody] CreateTourRequest request)
        {
            var prompt = $@"
            You are a JSON generator for a travel itinerary system.
            Return ONLY a valid JSON object that can be parsed directly with System.Text.Json — no markdown, no explanations, no extra text.

            TASK:
            Generate a detailed tour plan from Ho Chi Minh City to {request.Destination}
            from {request.CheckInDate} to {request.CheckOutDate}.
            The plan must be suitable for {request.TravelType}, with a budget between {request.MinBugget} and {request.MaxBugget} VND.
            All prices, including transportation, ticket_price, and hotel prices, MUST be in Vietnamese đồng (VND), even if the destination is outside Vietnam.
            Write all text in Vietnamese. Keep each 'details' field 1–2 sentences long.

            OUTPUT FORMAT (MUST FOLLOW EXACTLY):
            {{
            ""transportation"": {{
                ""flight"": {{
                ""details"": ""string"",
                ""price"": number,
                ""booking_url"": ""string or empty"",
                ""isSelectedTransport"": false
                }},
                ""train"": {{
                ""details"": ""string"",
                ""price"": number,
                ""booking_url"": ""string or empty"",
                ""isSelectedTransport"": false
                }},
                ""bus"": {{
                ""details"": ""string"",
                ""price"": number,
                ""booking_url"": ""string or empty"",
                ""isSelectedTransport"": false
                }},
                ""self-drive"": {{
                ""details"": ""string"",
                ""price"": number,
                ""isSelectedTransport"": false
                }}
            }},
            ""places_to_visit"": [
                {{
                ""placeName"": ""string"",
                ""details"": ""string"",
                ""image_url"": ""string"",
                ""ticket_price"": number,
                ""best_time_to_visit"": ""string"",
                ""day_visit"": """",
                ""rating"": ""string"",
                ""total_user_rating"": ""string""
                }}
                // total 10 items
            ],
            ""places_to_stay"": [
                {{
                ""placeName"": ""string"",
                ""details"": ""string"",
                ""image_url"": ""string"",
                ""price"": number,
                ""rating"": ""string"",
                ""total_user_rating"": ""string""
                }}
                // total 4 items
            ]
            }}

            RULES:
            - Return only valid JSON (no comments like this in the actual output).
            - No markdown fences, no extra explanation.
            - All fields must match the given format and keys exactly.
            - If transportation not available, set booking_url to """".
            - day_visit must be empty string.
            - Vietnamese only.

            Now, generate the JSON output for the requested destination:
            ";

            var result = await _geminiService.GenerateContentAsync(prompt);
            if (result.StartsWith("```"))
            {
                int firstLineEnd = result.IndexOf('\n');
                int lastFence = result.LastIndexOf("```");
                if (lastFence > firstLineEnd)
                {
                    result = result.Substring(firstLineEnd + 1, lastFence - firstLineEnd - 1);
                }
            }

            var itineraryDoc = JsonDocument.Parse(result);
            var root = itineraryDoc.RootElement;

            var placesToVisit = root.GetProperty("places_to_visit").EnumerateArray().ToList();
            var placesToStay = root.GetProperty("places_to_stay").EnumerateArray().ToList();

            var updatedPlacesToVisit = new List<Dictionary<string, object>>();
            foreach (var place in placesToVisit)
            {
                var dict = place.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone());

                var placeName = dict["placeName"]?.ToString();
                if (!string.IsNullOrWhiteSpace(placeName))
                {
                    var imageUrl = await FetchImageUrlAsync(placeName);
                    dict["image_url"] = imageUrl ?? dict["image_url"];
                }

                updatedPlacesToVisit.Add(dict);
            }

            var updatedPlacesToStay = new List<Dictionary<string, object>>();
            foreach (var place in placesToStay)
            {
                var dict = place.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value.Clone());

                var placeName = dict["placeName"]?.ToString();
                if (!string.IsNullOrWhiteSpace(placeName))
                {
                    var imageUrl = await FetchImageUrlAsync(placeName);
                    dict["image_url"] = imageUrl ?? dict["image_url"];
                }

                updatedPlacesToStay.Add(dict);
            }

            var updatedData = new Dictionary<string, object>
            {
                ["transportation"] = root.GetProperty("transportation").Clone(),
                ["places_to_visit"] = updatedPlacesToVisit,
                ["places_to_stay"] = updatedPlacesToStay
            };

            var tour = new Tour
            {
                Destination = request.Destination,
                CheckInDate = request.CheckInDate,
                CheckOutDate = request.CheckOutDate,
                MinBugget = request.MinBugget,
                MaxBugget = request.MaxBugget,
                TravelType = request.TravelType,
                CreatedBy = Guid.Parse(request.UserId),
                Information = JsonSerializer.Serialize(updatedData)
            };

            await _supabaseService.GetClient().From<Tour>().Insert(tour);

            return Ok(new { itinerary = updatedData });
        } 

        private async Task<string?> FetchImageUrlAsync(string placeName)
        {
            try
            {
                var url = $"https://image-search-production-8ec2.up.railway.app/image/{Uri.EscapeDataString(placeName)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                return doc.RootElement.TryGetProperty("image_url", out var imageProp) ? imageProp.GetString() : null;
            }
            catch
            {
                return null;
            }
        }  
    }

    public class CreateTourRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string TravelType { get; set; } = string.Empty;
        public string CheckInDate { get; set; } = string.Empty;
        public string CheckOutDate { get; set; } = string.Empty;
        public int MinBugget { get; set; }
        public int MaxBugget { get; set; }
    }
}