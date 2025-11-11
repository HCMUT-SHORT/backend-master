using Microsoft.AspNetCore.Mvc;
using Supabase.Gotrue;

namespace backend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TourController : ControllerBase
    {
        private readonly SupabaseClientService _supabaseService;
        private readonly GeminiService _geminiService;

        public TourController(SupabaseClientService supabaseService, GeminiService geminiService)
        {
            _supabaseService = supabaseService;
            _geminiService = geminiService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateTour([FromBody] CreateTourRequest request)
        {
            var prompt = $@"
            Generate a tour plan from Ho Chi Minh to {request.Destination} from {request.CheckInDate} to {request.CheckOutDate}, 
            suitable for {request.TravelType}, with a minimum budget of {request.MinBugget} VND and maximum budget of {request.MaxBugget} VND.
            I want 10 places to visit and 5 places to stay. Only return in string later can be parse to JSON. Detail of each thing only 1-2 sentence.
            Follow this sample output exactly and generate for the requested destination:
            {{
            ""transportation"": {{
                ""flight"": {{
                ""details"": ""Bay thẳng từ Sân bay Tân Sơn Nhất (SGN) đến Sân bay Liên Khương (DLI). Phương án nhanh nhất (khoảng 50 phút bay)."",
                ""price"": 1800000,
                ""booking_url"": ""https://www.vietnamairlines.com/"",
                ""isSelectedTransport"": false
                }},
                ""train"": {{
                ""details"": ""Không có tuyến tàu hỏa trực tiếp từ Sài Gòn đến Đà Lạt. Có thể đi tàu đến ga Tháp Chàm (Ninh Thuận) rồi bắt xe khách đi Đà Lạt, nhưng rất mất thời gian."",
                ""price"": 0,
                ""booking_url"": ""https://dsvn.vn/"",
                ""isSelectedTransport"": false
                }},
                ""bus"": {{
                ""details"": ""Xe khách giường nằm chất lượng cao (như Thành Bưởi, Phương Trang) từ Bến xe Miền Đông/Phạm Ngũ Lão đi Đà Lạt. Phương án tiết kiệm nhất (khoảng 6-7 tiếng di chuyển)."",
                ""price"": 450000,
                ""booking_url"": ""https://futabus.vn/"",
                ""isSelectedTransport"": false
                }},
                ""self-drive"": {{
                ""details"": ""Tự lái phương tiện cá nhân (ô tô/xe máy) theo đường QL20. Chủ động về thời gian, chi phí xăng và phí cầu đường ước tính khoảng 1,500,000 - 2,000,000 VND (khứ hồi, tùy loại xe)."",
                ""price"": 0,
                ""isSelectedTransport"": false
                }}
            }},
            ""places_to_visit"": [
                {{
                ""placeName"": ""Chợ đêm Đà Lạt & Hồ Xuân Hương"",
                ""details"": ""Trung tâm của thành phố, nơi giao thoa ẩm thực đường phố và mua sắm. Hồ Xuân Hương là biểu tượng thơ mộng, thích hợp để tản bộ."",
                ""image_url"": ""https://example.com/cho_dem_da_lat.jpg"",
                ""ticket_price"": 0,
                ""best_time_to_visit"": ""Buổi tối (18:00 - 22:00)"",
                ""day_visit"": ""None"",
                ""rating"": ""4.4"",
                ""total_user_rating"": ""65000""
                }},
                // repeat for remaining 9 places
            ],
            ""places_to_stay"": [
                {{
                ""placeName"": ""Khách sạn Sweet Lavender"",
                ""details"": ""Khách sạn 3 sao, vị trí trung tâm, phòng sạch sẽ và đánh giá tốt. Phù hợp cho ngân sách trung bình."",
                ""image_url"": ""https://example.com/sweet_lavender_hotel.jpg"",
                ""price"": 500000,
                ""rating"": ""4.2"",
                ""total_user_rating"": ""999""
                }},
                // repeat for remaining 4 stays
            ]
            }}
            Now generate a new itinerary for the requested destination following this structure and style exactly.";

            var result = await _geminiService.GenerateContentAsync(prompt);

            return Ok(new { itinerary = result });
        }   
    }

    public class CreateTourRequest
    {
        public string Destination { get; set; } = string.Empty;
        public string TravelType { get; set; } = string.Empty;
        public string CheckInDate { get; set; } = string.Empty;
        public string CheckOutDate { get; set; } = string.Empty;
        public int MinBugget { get; set; }
        public int MaxBugget { get; set; }
    }
}