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
            The plan must be suitable for {request.TravelType}, with a budget between {request.MinBudget} and {request.MaxBudget} VND.
            All prices, including transportation, ticket_price, and hotel prices, MUST be in Vietnamese đồng (VND), even if the destination is outside Vietnam.
            Must have 4 type of transportation: flight, train, bus, self-drive. But if it not possbile to travel from Ho Chi Minh to {request.Destination}, fill detail field not possible.
            Write all text in Vietnamese. Keep each 'details' field 1–2 sentences long.

            OUTPUT FORMAT (MUST FOLLOW EXACTLY):
            {{
            ""transportation"": [
                {{
                    ""type"": ""flight"",
                    ""details"": ""string"",
                    ""price"": number,
                    ""bookingurl"": ""string or empty"",
                }},
                {{
                    ""type"": ""train"",
                    ""details"": ""string"",
                    ""price"": number,
                    ""bookingurl"": ""string or empty"",
                }},
                    ""type"": ""bus"",
                    ""details"": ""string"",
                    ""price"": number,
                    ""bookingurl"": ""string or empty"",
                }},
                    ""type"": ""self-drive"",
                    ""details"": ""string"",
                    ""price"": number,
                    ""bookingurl"": ""Empty"",
                }}
            ],
            ""places_to_visit"": [
                {{
                    ""placename"": ""string"",
                    ""details"": ""string"",
                    ""imageurl"": ""string"",
                    ""besttimetovisit"": ""string"",
                    ""ticketprice"": number,
                    ""rating"": number,
                    ""totaluserrating"": number
                }}
                // total 9 items
            ],
            ""places_to_stay"": [
                {{
                    ""placename"": ""string"",
                    ""details"": ""string"",
                    ""imageurl"": ""string"",
                    ""price"": number,
                    ""rating"": number,
                    ""totaluserrating"": number
                }}
                // total 5 items
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

            var destinationImage = await FetchImageUrlAsync(request.Destination);
            var newTourId = await GenerateUniqueTourIdAsync();
            var vietnamTime = DateTime.UtcNow.AddHours(7);

            var newTour = new Tour
            {
                Id = newTourId,
                ImageUrl = destinationImage,
                Destination = request.Destination,
                CheckInDate = request.CheckInDate,
                CheckOutDate = request.CheckOutDate,
                MinBudget = request.MinBudget,
                MaxBudget = request.MaxBudget,
                TravelType = request.TravelType,
                CreatedAt = vietnamTime,
                CreatedBy = Guid.Parse(request.UserId)
            };

            var insertTour = await _supabaseService.GetClient().From<Tour>().Insert(newTour, new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation });
            var insertTourId = insertTour.Models.FirstOrDefault()?.Id ?? throw new Exception("Failed to insert tour.");

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

            var transportationJson = root.GetProperty("transportation").EnumerateArray().ToList();
            var transportationList = new List<Transportation>();
            foreach (var item in transportationJson)
            {
                transportationList.Add(new Transportation
                {
                    TourId = insertTourId,
                    Type = item.GetProperty("type").GetString(),
                    Detail = item.GetProperty("details").GetString(),
                    Price = item.GetProperty("price").GetInt64(),
                    BookingUrl = item.GetProperty("bookingurl").GetString(),
                    IsSelected = false
                });
            }
            await _supabaseService.GetClient().From<Transportation>().Insert(transportationList);

            var placesToVisitJson = root.GetProperty("places_to_visit").EnumerateArray().ToList();
            var placesToVisitList = new List<PlaceToVisit>();
            foreach (var item in placesToVisitJson)
            {
                var placeName = item.GetProperty("placename").GetString();
                if (string.IsNullOrWhiteSpace(placeName)) continue;

                var imageUrl = await FetchImageUrlAsync(placeName);
                placesToVisitList.Add(new PlaceToVisit
                {
                    TourId = insertTourId,
                    Name = placeName,
                    ImageUrl = imageUrl,
                    Detail = item.GetProperty("details").GetString(),
                    BestTimeToVisit = item.GetProperty("besttimetovisit").GetString(),
                    Price = item.GetProperty("ticketprice").GetInt64(),
                    Rating = (float?)item.GetProperty("rating").GetDouble(),
                    TotalRating = item.GetProperty("totaluserrating").GetInt64(),
                    DayVisit = 0
                });
            }
            await _supabaseService.GetClient().From<PlaceToVisit>().Insert(placesToVisitList);

            var placesToStayJson = root.GetProperty("places_to_stay").EnumerateArray().ToList();
            var placesToStayList = new List<PlaceToStay>();
            foreach (var item in placesToStayJson)
            {
                var placeName = item.GetProperty("placename").GetString();
                if (string.IsNullOrWhiteSpace(placeName)) continue;

                var imageUrl = await FetchImageUrlAsync(placeName);
                placesToStayList.Add(new PlaceToStay
                {
                    TourId = insertTourId,
                    Name = placeName,
                    ImageUrl = imageUrl,
                    Detail = item.GetProperty("details").GetString(),
                    Price = item.GetProperty("price").GetInt64(),
                    Rating = (float?)item.GetProperty("rating").GetDouble(),
                    TotalRating = item.GetProperty("totaluserrating").GetInt64(),
                    IsSelected = false
                });
            }
            await _supabaseService.GetClient().From<PlaceToStay>().Insert(placesToStayList);

            return Ok(new { insertTourId });
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

        private async Task<Guid> GenerateUniqueTourIdAsync()
        {
            var client = _supabaseService.GetClient();
            Guid newId;

            while (true)
            {
                newId = Guid.NewGuid();

                var existed = await client.From<Tour>().Where(t => t.Id == newId).Get();

                if (existed.Models.Count == 0)
                {
                    break;
                } 
            }
            return newId;
        }

        [HttpGet("getUserTours/{id}")]
        public async Task<IActionResult> GetUserTours(string id)
        {
            var userId = Guid.Parse(id);
            var tours = await _supabaseService.GetClient().From<Tour>().Where(t => t.CreatedBy == userId).Get();
            return Ok(tours.Content);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTour(string id)
        {
            var tourId = Guid.Parse(id);
            var tour = await _supabaseService.GetClient().From<Tour>().Where(t => t.Id == tourId).Get();
            return Ok(tour.Content);
        }

        [HttpGet("placestovisit/{id}")]
        public async Task<IActionResult> GetTourPlacesToVisit(string id)
        {
            var tourId = Guid.Parse(id);
            var placesToVisit = await _supabaseService.GetClient().From<PlaceToVisit>().Where(place => place.TourId == tourId).Get();
            return Ok(placesToVisit.Content);
        }

        [HttpGet("placestostay/{id}")]
        public async Task<IActionResult> GetTourPlacesToStay(string id)
        {
            var tourId = Guid.Parse(id);
            var placesToStay = await _supabaseService.GetClient().From<PlaceToStay>().Where(place => place.TourId == tourId).Get();
            return Ok(placesToStay.Content);
        }

        [HttpGet("transportation/{id}")]
        public async Task<IActionResult> GetTourTransporation(string id)
        {
            var tourId = Guid.Parse(id);
            var transportation = await _supabaseService.GetClient().From<Transportation>().Where(place => place.TourId == tourId).Get();
            return Ok(transportation.Content);
        }

        [HttpPut("placestovisit")]
        public async Task<IActionResult> UpdateTourPlacesToVisit([FromBody] List<UpdateTourPlacesToVisitDto> updates)
        {
            var client = _supabaseService.GetClient();

            foreach (var update in updates)
            {
                var placeId = Guid.Parse(update.Id);
                var place = await client.From<PlaceToVisit>().Where(place => place.Id == placeId).Single();

                if (place?.DayVisit == null) continue;

                place.DayVisit = update.DayVisit;
                await place.Update<PlaceToVisit>();
            }

            return Ok("Update Places To Visit Successful");
        }

        [HttpPut("placestostay")]
        public async Task<IActionResult> UpdateTourPlacesToStay([FromBody] List<UpdateTourPlacesToStayDto> updates)
        {
            var client = _supabaseService.GetClient();

            foreach (var update in updates)
            {
                var placeId = Guid.Parse(update.Id);
                var place = await client.From<PlaceToStay>().Where(place => place.Id == placeId).Single();

                if (place?.IsSelected == null) continue;

                place.IsSelected = update.IsSelected;
                await place.Update<PlaceToStay>();
            }

            return Ok("Update Places To Stay Successful");
        }

        [HttpPut("transportation")]
        public async Task<IActionResult> UpdateTourTransportation([FromBody] UpdateTourTransportationDto update)
        {
            var client = _supabaseService.GetClient();

            var OldTransportId = Guid.Parse(update.OldTransportId);
            var NewTransportId = Guid.Parse(update.NewTransportId);

            var oldTransport = await client.From<Transportation>().Where(transport => transport.Id == OldTransportId).Single();
            var newTransport = await client.From<Transportation>().Where(transport => transport.Id == NewTransportId).Single();

            if (oldTransport?.IsSelected == null || newTransport?.IsSelected == null) return BadRequest("Transportation does not exist");

            oldTransport.IsSelected = false;
            newTransport.IsSelected = true;

            await oldTransport.Update<Transportation>();
            await newTransport.Update<Transportation>();

            return Ok("Update Transportation Successful");
        }
    }

    public class CreateTourRequest {
        public string UserId { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string TravelType { get; set; } = string.Empty;
        public string CheckInDate { get; set; } = string.Empty;
        public string CheckOutDate { get; set; } = string.Empty;
        public int MinBudget { get; set; }
        public int MaxBudget { get; set; }
    }

    public class UpdateTourPlacesToVisitDto {
        public string Id { get; set; } = string.Empty;
        public int DayVisit { get; set; }
    }

    public class UpdateTourPlacesToStayDto {
        public string Id { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class UpdateTourTransportationDto {
        public string OldTransportId { get; set; } = string.Empty;
        public string NewTransportId { get; set; } = string.Empty;
    }
}