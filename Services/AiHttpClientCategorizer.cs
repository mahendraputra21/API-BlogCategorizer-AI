using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BlogCategorizer.Services;

public interface IAiHttpClientCategorizer
{
    Task<string> CategorizeAsync(string content, string modelId = "openai/gpt-4o-mini", CancellationToken cancellationToken = default);
}

public class AiHttpClientCategorizer : IAiHttpClientCategorizer
{
    private readonly HttpClient _http;
    private readonly string _pat;
    private readonly string? _orgName;
    private static readonly string[] Allowed = ["Tech", "AI", "Cloud", ".NET", "Architecture", "Tutorials", "Other"];

    public AiHttpClientCategorizer(HttpClient http, IConfiguration config)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));

        // IMPORTANT: use the GitHub Models host (not api.github.com)
        // This is the correct base for inference endpoints:
        var baseAddress = config["ModelBaseAddress"] ??
            throw new InvalidOperationException("ModelBaseAddress is not configured in appSettings.");
        
        _http.BaseAddress = new Uri(baseAddress);

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BlogCategorizer/1.0");

        _pat = config["GitHub:Pat"] ?? throw new InvalidOperationException("GitHub:Pat is not configured in User Secrets or environment.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _pat);

        // Optional: if you want org-attributed calls, set GitHub:Org in config
        _orgName = config["GitHub:Org"];
    }

    /// <summary>
    /// Categorize the provided content. Returns one of the fixed categories or "Other".
    /// </summary>
    public async Task<string> CategorizeAsync(string content, string modelId = "openai/gpt-4o-mini", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Other";

        // Truncate long content to keep request size reasonable (adjust as needed)
        const int maxChars = 32000;
        if (content.Length > maxChars) content = content.Substring(0, maxChars);

        var systemPrompt = @"You are a blog post categorizer.
        Allowed categories (exactly): Technology, AI, Cloud, Programming, Architecture, Business, Other.
        Return EXACTLY ONE category word from the list above, and nothing else. If unsure, return Other.";

        var payload = new
        {
            model = modelId,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = content }
            },
            temperature = 0.0
        };

        var json = JsonSerializer.Serialize(payload);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");

        // choose endpoint: org-scoped or public
        var endpoint = _orgName is not null && !string.IsNullOrWhiteSpace(_orgName)
            ? $"orgs/{_orgName}/inference/chat/completions"
            : "inference/chat/completions";

        using var resp = await _http.PostAsync(endpoint, body, cancellationToken);
        var respText = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"AI call failed: {resp.StatusCode} - {respText}");

        // parse response robustly (supports common shapes)
        try
        {
            using var doc = JsonDocument.Parse(respText);
            // try common path: choices[0].message.content
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
                {
                    var raw = contentEl.GetString() ?? string.Empty;
                    return NormalizeAndValidate(raw);
                }

                // some providers return choices[0].text
                if (first.TryGetProperty("text", out var textEl))
                {
                    var raw = textEl.GetString() ?? string.Empty;
                    return NormalizeAndValidate(raw);
                }
            }

            // fallback: top-level 'output' or other fields
            if (doc.RootElement.TryGetProperty("output", out var outputEl))
            {
                var raw = outputEl.GetString() ?? string.Empty;
                return NormalizeAndValidate(raw);
            }
        }
        catch (JsonException)
        {
            // fall through to fallback handling below
        }

        // If we couldn't parse a category, return Other
        return "Other";
    }

    private static string NormalizeAndValidate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Other";

        // keep only first line/word in case model returned extra text
        var candidate = raw.Trim();
        // remove surrounding quotes or punctuation
        candidate = candidate.Trim(new char[] { '"', '\'', '.', '\n', '\r', ' ' });

        // sometimes model returns sentences like "Category: .NET" — take last token that matches allowed
        var tokens = candidate.Split(new[] { '\n', '\r', ':', '-' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(t => t.Trim())
                              .Where(t => t.Length > 0)
                              .ToArray();

        // check tokens (try exact matches, then case-insensitive)
        foreach (var t in tokens)
        {
            var match = Allowed.FirstOrDefault(a => string.Equals(a, t, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        // as a last resort, try to match any allowed by simple contains (e.g., ".NET" inside text)
        foreach (var a in Allowed)
        {
            if (candidate.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)
                return a;
        }

        return "Other";
    }
}
