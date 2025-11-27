using HtmlAgilityPack;
using SmartReader;
using System.Net;
using System.Text.RegularExpressions;

namespace BlogCategorizer.Services;

public interface IHtmlExtractor
{
    Task<string> ExtractMainContentAsync(string url);
}

public class HtmlExtractor : IHtmlExtractor
{
    private readonly HttpClient _http;

    public HtmlExtractor(HttpClient http) => _http = http;

    public async Task<string> ExtractMainContentAsync(string url)
    {
        // Try SmartReader via its Reader (best extraction) but we must fetch HTML first
        string? html = null;

        // 1) Try polite browser-like GET with headers + retries
        var maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36");
                req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                req.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                req.Headers.Referrer = new Uri(url);
                // Optional: some sites require a Origin or Sec-Fetch-Site; adding minimal headers:
                req.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
                req.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");

                var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (resp.StatusCode == HttpStatusCode.Forbidden)
                {
                    // short delay before retry
                    await Task.Delay(500 * attempt);
                    continue;
                }

                resp.EnsureSuccessStatusCode();
                html = await resp.Content.ReadAsStringAsync();
                break;
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(500 * attempt);
                continue;
            }
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            // At this point many sites block simple requests (403). Suggest fallback.
            throw new InvalidOperationException("Failed to fetch HTML (403 or blocked). Consider using a headless browser (Playwright) or proxy.");
        }

        // 2) Try SmartReader extraction (if available)
        try
        {
            var reader = new Reader(url, html); // some Reader constructors accept both; if not, use Reader(url) then set html
            var article = await reader.GetArticleAsync();
            if (!string.IsNullOrWhiteSpace(article?.Content))
                return Clean(article.Content);
        }
        catch
        {
            // Ignore and fallback to HtmlAgilityPack
        }

        // 3) Fallback: HtmlAgilityPack basic extraction
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//noscript") ?? Enumerable.Empty<HtmlNode>();
        foreach (var n in nodesToRemove) n.Remove();

        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var text = body.InnerText ?? string.Empty;
        return Clean(text);
    }

    private static string Clean(string t) => Regex.Replace(t ?? string.Empty, @"\s+", " ").Trim();
}
