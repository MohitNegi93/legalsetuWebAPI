using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace LS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public ChatController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            // Make sure you have "GeminiApiKey" in your appsettings.json or User Secrets
            _apiKey = configuration["GeminiApiKey"] ?? throw new ArgumentNullException("GeminiApiKey is missing");
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message is required" });
            }

            // UPDATE THIS LINE: Use gemini-3.6-flash instead of gemini-2.5-flash
            string modelId = "gemini-3.6-flash";
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={_apiKey}";

            // Legal Setu rules
            string systemInstruction = "You are an AI legal assistant for Legal Setu (https://legal-n39tw062h-dcsmohitnegi-3714s-projects.vercel.app/). Your role is to help Indian citizens understand their rights, answer legal questions in plain language (no legalese), and assist with legal documents. You must always clarify that your responses are for informational purposes and not a substitute for professional legal advice. For serious matters, advise the user to book a verified lawyer through Legal Setu. Strictly decline to answer non-legal questions (like technology, sports, cooking, etc.) and politely redirect the conversation to legal assistance.";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = request.Message }
                        }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = systemInstruction }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new { error = "Failed to communicate with Gemini API", details = responseString });
                }

                using var doc = JsonDocument.Parse(responseString);
                var reply = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return Ok(new { reply = reply });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred", details = ex.Message });
            }
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; }
    }
}