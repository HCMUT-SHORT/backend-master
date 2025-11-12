using Google.GenAI;

public class GeminiService
{
    private readonly Client _client;

    public GeminiService(IConfiguration configuration)
    {
        var apiKey = configuration["Gemini:apiKey"];
        _client = new Client(apiKey: apiKey);
    }
    public async Task<string> GenerateContentAsync(string prompt)
    {
        var response = await _client.Models.GenerateContentAsync(
            model: "gemini-2.0-flash",
            contents: prompt
        );

        return response?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "No content returned from Gemini API";
    }
}