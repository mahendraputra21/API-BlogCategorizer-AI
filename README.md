# 🏷 Blog Categorizer – AI-Powered Blog Classification API (No Playwright)

The **Blog Categorizer** is a **.NET 10 Minimal API** service that classifies blog articles into a single, validated category using AI inference from GitHub Models—without headless browser automation such as Playwright.

## 🚀 Features

1. **Supports URL or raw text input**
2. **Extracts main article body**
   - Priority: SmartReader readability extraction
   - Fallback: HtmlAgilityPack DOM parsing
   - Uses HttpClient with modern, browser-like headers (no Playwright)
3. **Classifies content via GitHub Models inference API**
4. **Forces deterministic output**
   - `temperature = 0.0`
   - Always returns a **single-word category**
5. **Validates the result**, defaults to **Other** when uncertain

## ✅ Supported Categories
Tech, AI, Cloud, .NET, Architecture, Tutorials, Other

## 🧩 Architecture
<img width="1895" height="289" alt="image" src="https://github.com/user-attachments/assets/2793e742-e050-43f6-9178-aafedea222e4" />


## 📤 API Specification

Request
```json
POST /categorize
{
  "input": "<URL or blog article text>"
}
```
Response
```json
200 OK
{
  "category": "Cloud"
}
```

## 📦 Setup & Installation
### 1. Clone Repository
```bash
git clone <your-repo-url>
cd blog-categoriz er
```
### 2. Configure User Secrets (Dev)
```bash
dotnet user-secrets init
dotnet user-secrets set "GitHub:Pat" "your_github_personal_access_token"
```
### 3. Restore & Run
```bash
dotnet restore
dotnet run
```

### Runs on:
```bash
https://localhost:5001
or
http://localhost:5000
```

## 🔐 Authentication
Requires a GitHub Personal Access Token with ```read:models``` scope.
```bash
Authorization: Bearer <GitHub PAT>
```

## ❗ Error Handling Summary
| Status  | Cause                          | Handling                     |
| ------- | ------------------------------ | ---------------------------- |
| 400     | Invalid URL or request payload | Reject request               |
| 403     | Site blocks request            | Retry with headers, else 500 |
| Timeout | Slow domain                    | Retry with backoff           |
| 401     | PAT missing/invalid            | 401 Unauthorized             |
| 404     | Model/endpoint issue           | Fix GitHub Models Base URL   |
| 429     | Rate limited                   | Retry-After support (future) |
| 500     | Model inference failure        | Retry + log                  |

## 💡 HttpClient Best Practices

- Uses Typed clients via AddHttpClient<T>
- No manual instantiation, avoids socket exhaustion
- Auto-redirect + gzip decompression enabled

## 🗂 Optional Caching
You may cache:
- URL → extracted text (via URL hash)
- Article text → AI category (via content hash)

Good storage options:
MemoryCache, Redis, or SQL Server Table

## 🔮 Extensibility Roadmap
You can easily add:

- ☁ Azure / AWS / GCP deployment
- 📊 Logging with Serilog, Seq, AppInsights, ELK
- 🔁 Polly retry, timeout, circuit breaker policies
- ⚙ Rate limiting middleware
- 🧺 Batch classification endpoint
- 🗄 Persistent classification storage

## 🎯 Design Philosophy
Smart text extraction + deterministic AI classification + minimal dependencies — no Playwright. Fast, clean, validated output.

## ✨ Example cURL
```bash
curl -X POST "http://localhost:5000/categorize" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <GitHub PAT>" \
  -d '{"input":"This is a tutorial about .NET Minimal API design"}'
```

