using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using backend.Models;
using Supabase;
using Supabase.Postgrest;

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
            All prices, including transportation, ticket_price, and hotel prices, MUST be convert into Vietnamese đồng (VND), even if the destination is outside Vietnam.
            Must have 4 type of transportation: flight, train, bus, self-drive. But if it not possbile to travel from Ho Chi Minh to {request.Destination}, fill detail field not possible.
            Write all text in Vietnamese. Keep each 'details' field 1–2 sentences long.

            OUTPUT FORMAT (MUST FOLLOW EXACTLY):
            {{
            ""transportation"": [
                {{
                    ""type"": ""flight"",
                    ""ammountoftime"": ""string"",
                    ""price"": number,
                    ""bookingurl"": ""string or empty"",
                    ""ispossible"": boolean,
                }},
                {{
                    ""type"": ""train"",
                    ""ammountoftime"": ""string"",
                    ""price"": number,
                    ""bookingurl"": ""string or empty"",
                    ""ispossible"": boolean,
                }},
                    ""type"": ""bus"",
                    ""ammountoftime"": ""string"",
                    ""price"": number,
                    ""bookingurl"": ""string or empty"",
                    ""ispossible"": boolean,
                }},
                    ""type"": ""self-drive"",
                    ""ammountoftime"": ""string"",
                    ""price"": number,
                    ""bookingurl"": Empty,
                    ""ispossible"": boolean,
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
                    Time = item.GetProperty("ammountoftime").GetString(),
                    Price = item.GetProperty("price").GetInt64(),
                    BookingUrl = item.GetProperty("bookingurl").GetString(),
                    IsPossible = item.GetProperty("ispossible").GetBoolean(),
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
                    DayVisit = ""
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
                var bookingUrl = await FetchBookingUrlAsync(placeName);
                placesToStayList.Add(new PlaceToStay
                {
                    TourId = insertTourId,
                    Name = placeName,
                    ImageUrl = imageUrl,
                    BookingUrl = bookingUrl,
                    Detail = item.GetProperty("details").GetString(),
                    Price = item.GetProperty("price").GetInt64(),
                    Rating = (float?)item.GetProperty("rating").GetDouble(),
                    TotalRating = item.GetProperty("totaluserrating").GetInt64(),
                    IsSelected = false
                });
            }
            await _supabaseService.GetClient().From<PlaceToStay>().Insert(placesToStayList);

            return Ok(insertTour.Content);
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

        private async Task<string?> FetchBookingUrlAsync(string placeName)
        {
            try
            {
                var url = $"https://image-search-production-8ec2.up.railway.app/hotel/{Uri.EscapeDataString(placeName)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                return doc.RootElement.TryGetProperty("booking_url", out var hotelProp) ? hotelProp.GetString() : null;
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
        public async Task<IActionResult> UpdateTourTransportation([FromBody] List<UpdateTourTransportationDto> updates)
        {
            var client = _supabaseService.GetClient();

            foreach (var update in updates)
            {
                var transportId = Guid.Parse(update.Id);
                var transport = await client.From<Transportation>().Where(transport => transport.Id == transportId).Single();

                if (transport?.IsSelected == null) continue;

                transport.IsSelected = update.IsSelected;
                await transport.Update<Transportation>();
            }

            return Ok("Update Transportation Successful");
        }

        [HttpPost("share/{tourId}")]
        public async Task<IActionResult> CreateShare(string tourId)
        {
            var client = _supabaseService.GetClient();

            if (!Guid.TryParse(tourId, out Guid tourGuid))
                return BadRequest("Invalid tour ID");

            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized("Missing Authorization header");

            string jwt = authHeader["Bearer ".Length..].Trim();

            var user = await client.Auth.GetUser(jwt);
            if (user == null)
                return Unauthorized("Invalid token");

            Guid userGuid = Guid.Parse(user.Id);
            var existingShare = await client.From<Share>()
                .Where(s => s.TourId == tourGuid)
                .Get();

            if (existingShare.Models.Count > 0)
            {
                var existing = existingShare.Models[0];
                return Ok(new { code = existing.Code });
            }

            string code = Share.GenerateShareCode(tourGuid);

            var share = new Share
            {
                TourId = tourGuid,
                UserId = userGuid, 
                Code = code
            };

            await client.From<Share>().Insert(share);

            return Ok(new { code });
        }

        [HttpGet("lookup/{code}")]
        public async Task<IActionResult> LookupShare(string code)
        {
            var cd = code.ToUpper();

            var client = _supabaseService.GetClient();
            
            var tourShare = await client
                .From<Share>()
                .Filter("code", Constants.Operator.Equals, cd)
                .Get();
        
            if (tourShare.Models.Count == 0)
                return NotFound("Invalid share code");

            var share = tourShare.Models[0];

            var tourResult = await client.From<Tour>()
                .Where(t => t.Id == share.TourId)
                .Get();

            if (tourResult.Models.Count == 0)
                return NotFound("Tour not found");

            return Ok(tourResult.Content);
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinTour([FromBody] JoinTourRequest request)
        {
            var client = _supabaseService.GetClient();

            string authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized("Missing token");
            string jwt = authHeader["Bearer ".Length..].Trim();

            var user = await client.Auth.GetUser(jwt);
            if (user == null)
                return Unauthorized("Invalid token");

            Guid guestId = Guid.Parse(user.Id);

            var shareResult = await client
                .From<Share>()
                .Where(s => s.Code == request.Code)
                .Get();

            if (shareResult.Models.Count == 0)
                return NotFound("Invalid share code");

            var ownerShare = shareResult.Models[0];
            var existing = await client
                .From<Share>()
                .Where(s => s.TourId == ownerShare.TourId && s.UserId == guestId)
                .Get();

            if (existing.Models.Count > 0)
                return Ok(new { message = "Already joined" });

            var joinRow = new Share
            {
                TourId = ownerShare.TourId,
                UserId = guestId,
                Code = null
            };

            await client.From<Share>().Insert(joinRow);

            return Ok(new { message = "Joined successfully", tourId = ownerShare.TourId });
        }

        [HttpGet("getSharedTours/{userId}")]
        public async Task<IActionResult> GetSharedTours(string userId)
        {
            var client = _supabaseService.GetClient();
            if (!Guid.TryParse(userId, out Guid userGuid))
                return BadRequest("Invalid user ID");

            var sharesResult = await client
                .From<Share>()
                .Where(t => t.UserId == userGuid)
                .Get();

            // Console.WriteLine("share:", sharesResult.Content);
            if (sharesResult.Models.Count == 0)
                return Ok(new List<object>()); 

            var tourIds = sharesResult.Models
                .Select(s => s.TourId)
                .Distinct()
                .ToList();
            
            var tours = new List<Tour>();

            foreach (var tid in tourIds)
            {
                var tourResult = await client
                    .From<Tour>()
                    .Filter("id", Constants.Operator.Equals, tid.ToString())
                    .Get();

                if (tourResult.Models.Count > 0)
                {
                    tours.Add(tourResult.Models[0]);
                }
            }
            
            var final = tours.Select(t => new ShareTour
            {
                Id = t.Id,
                Destination = t.Destination,
                ImageUrl = t.ImageUrl,
                CheckInDate = t.CheckInDate,
                CheckOutDate = t.CheckOutDate,
                TravelType = t.TravelType,
                MinBudget = t.MinBudget,
                MaxBudget = t.MaxBudget,
                CreatedAt = t.CreatedAt
                
            }).ToList();

            return Ok(final);
        }

    }

    public class JoinTourRequest
    {
        public string Code { get; set; } = string.Empty;
    }
    public class ShareTour
    {
        public Guid Id { get; set; }
        public string Destination { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string CheckInDate { get; set; } = string.Empty;
        public string CheckOutDate { get; set; } = string.Empty;
        public long? MinBudget { get; set; }
        public long? MaxBudget { get; set; }
        public string TravelType { get; set; } = string.Empty;
        public DateTime? CreatedAt {get; set; }
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
        public string DayVisit { get; set; } = string.Empty;
    }

    public class UpdateTourPlacesToStayDto {
        public string Id { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class UpdateTourTransportationDto {
        public string Id { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}