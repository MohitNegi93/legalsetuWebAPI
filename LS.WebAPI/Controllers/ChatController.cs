using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace LsWebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(IConfiguration config, IHttpClientFactory httpClientFactory) : ControllerBase
{
    // Fetches the API key from appsettings.json or environment variables
    private readonly string _apiKey = config["GeminiApiKey"] ?? throw new ArgumentNullException("GeminiApiKey is missing");

    [HttpPost]
    public async Task<IActionResult> GenerateResponse([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { Error = "Prompt is required." });
        }

        var httpClient = httpClientFactory.CreateClient();
        // Using gemini-2.5-flash for the fastest, most cost-effective text interactions
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

        // --- LEGAL SETU CHAT RULES (SYSTEM INSTRUCTION) ---
        var systemPrompt = @"You are an AI legal assistant for Legal Setu (https://legal-n39tw062h-dcsmohitnegi-3714s-projects.vercel.app/). 
Your role is to help Indian citizens understand their rights, answer legal questions in plain language (no legalese), and assist with legal documents. 
You must always clarify that your responses are for informational purposes and not a substitute for professional legal advice. 
For serious matters, advise the user to book a verified lawyer through Legal Setu. 
Strictly decline to answer non-legal questions (like technology, sports, cooking, etc.) and politely redirect the conversation to legal assistance.";

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = request.Prompt } } }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new { Error = "Failed to communicate with Gemini API", Details = responseString });
        }

        try
        {
            // Parse the JSON response from Gemini
            using var jsonDoc = JsonDocument.Parse(responseString);
            var replyText = jsonDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString();

            return Ok(new { Reply = replyText });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "Failed to parse the response.", Details = ex.Message });
        }
    }
}

public class ChatRequest
{
    public string Prompt { get; set; } = string.Empty;
}