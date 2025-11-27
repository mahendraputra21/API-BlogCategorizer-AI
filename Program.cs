using BlogCategorizer.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// config (User Secrets already loaded into builder.Configuration)
builder.Services.AddHttpClient<IHtmlExtractor, HtmlExtractor>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
    });

builder.Services.AddHttpClient<AiHttpClientCategorizer>();

// Optional: Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/categorize", async (CategoryRequest req, IHtmlExtractor extractor, AiHttpClientCategorizer ai) =>
{
    if (req?.Input is null || string.IsNullOrWhiteSpace(req.Input))
        return Results.BadRequest(new { error = "Input required (URL or article text)." });

    string text;
    if (Uri.IsWellFormedUriString(req.Input, UriKind.Absolute))
    {
        try
        {
            text = await extractor.ExtractMainContentAsync(req.Input);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: $"Failed to fetch/extract URL: {ex.Message}");
        }
    }
    else
    {
        text = req.Input;
    }

    // Truncate to a safe length to control cost / tokens. Adjust as needed.
    if (text.Length > 30_000) text = text[..30_000];

    string category;
    try
    {
        category = await ai.CategorizeAsync(text);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: $"AI classification failed: {ex.Message}");
    }

    return Results.Ok(new { category });
});

app.Run();

public record CategoryRequest(string Input);
