using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace LS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ChatController(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        [HttpPost]
        public async Task Post([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                Response.StatusCode = 400;
                await Response.WriteAsJsonAsync(new { error = "Prompt is required" });
                return;
            }

            var apiKey = _configuration["GeminiApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                Response.StatusCode = 500;
                await Response.WriteAsJsonAsync(new { error = "API key not configured." });
                return;
            }

            // 1. Prepare HTTP SSE headers for React streaming
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var modelId = !string.IsNullOrEmpty(request.ModelId) ? request.ModelId : "gemini-3.6-flash";

            // Call the Gemini live streaming endpoint directly
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:streamGenerateContent?alt=sse&key={apiKey}";

            var systemInstruction = !string.IsNullOrEmpty(request.SystemPrompt)
                ? request.SystemPrompt
                : "You are a specialized AI assistant. You are strictly restricted to talking about technology, programming, and software development.";

            // 2. Build the Gemini payload
            var payload = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = request.Prompt } } }
                },
                systemInstruction = new
                {
                    role = "system",
                    parts = new[] { new { text = systemInstruction } }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = content;

                // Use ResponseHeadersRead to immediately start processing the stream
                using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    var errorData = JsonSerializer.Serialize(new { type = "error", message = $"API Error: {response.StatusCode}" });
                    await Response.WriteAsync($"data: {errorData}\n\n");
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                // 3. Read the stream chunk-by-chunk
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Gemini sends chunks starting with "data: "
                    if (line.StartsWith("data: "))
                    {
                        var dataStr = line.Substring(6).Trim();
                        if (dataStr != "[DONE]")
                        {
                            using var doc = JsonDocument.Parse(dataStr);

                            // Extract the text fragment from the Gemini JSON chunk
                            var textPart = doc.RootElement
                                .GetProperty("candidates")[0]
                                .GetProperty("content")
                                .GetProperty("parts")[0]
                                .GetProperty("text").GetString();

                            if (!string.IsNullOrEmpty(textPart))
                            {
                                // Format it exactly as your React code expects: data: {"type": "text", "chunk": "..."}
                                var chunkData = JsonSerializer.Serialize(new { type = "text", chunk = textPart });
                                await Response.WriteAsync($"data: {chunkData}\n\n");
                                await Response.Body.FlushAsync();
                            }
                        }
                    }
                }

                // 4. Signal completion to your React frontend
                await Response.WriteAsync("data: [DONE]\n\n");
                await Response.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                var errorData = JsonSerializer.Serialize(new { type = "error", message = ex.Message });
                await Response.WriteAsync($"data: {errorData}\n\n");
                await Response.Body.FlushAsync();
            }
        }
    }

    // Model class matching your React frontend fetch body
    public class ChatRequest
    {
        public string Prompt { get; set; }
        public string ModelId { get; set; }
        public bool ThinkingEnabled { get; set; }
        public bool WebSearchEnabled { get; set; }
        public string SystemPrompt { get; set; }
    }
}