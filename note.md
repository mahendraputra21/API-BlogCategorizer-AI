📌 Overview
-----------

The **Blog Categorizer** is a .NET 10 Minimal API that:
1.  Accepts blog content or a URL
    
2.  Extracts the main article text (SmartReader + HtmlAgilityPack fallback)
    
3.  Sends it to **GitHub Models** for classification
    
4.  Returns a single category from:  
    **Tech, AI, Cloud, .NET, Architecture, Tutorials, Other**
    
No Playwright is used in this version.

* * *

🧩 Architecture Diagram (Mermaid)
=================================
:::mermaid
flowchart LR
  A[Client] --> B[.NET 10 Minimal API POST /categorize]

  B --> C{Is input a URL?}
  C -->|Yes| D[HtmlExtractor Service]
  C -->|No| E[Direct Text Input]

  subgraph Extraction["HTML Extraction (No Playwright)"]
    D --> F[HttpClient Fetch browser-like headers]
    F --> G{Fetch 200 OK?}
    G -->|Yes| H[SmartReader Readability extraction]
    G -->|If SmartReader fails| I[HtmlAgilityPack when fallback DOM parser]
    H --> J[Clean & Normalize Text]
    I --> J
  end

  E --> J

  J --> K[AiHttpClientCategorizer ]
  K --> L[GitHub Models API models.github.ai/inference/chat/completions]
  L --> M[Model - openai/gpt-4o-mini]
  M --> K

  K --> N[Normalize & Validate Category]
  N --> O[Return JSON]
:::


* * *

🔄 Request Flow (Step-by-Step)
==============================

### **1. Client sends request**

    POST /categorize
    {
      "input": "<URL or article text>"
    }
    

### **2. API determines input type**

*   If `input` is a valid URL → go to HTML extraction.
    
*   Otherwise → treat the string as plain text.
    

* * *

🏗 3. HTML Extraction Workflow (URL path)
-----------------------------------------

### **3.1 HttpClient fetch**

*   Sends GET request with:
    *   Modern browser-like User-Agent
        
    *   Accept / Accept-Language
        
    *   Referrer headers
        
    *   Automatic decompression
        
    *   Auto-redirects enabled
        

### **3.2 SmartReader Extraction**

If the fetch returns **200 OK**:
*   Use SmartReader to extract the main article body (readability algorithm).
    
*   Produces clean, content-focused text.
    

### **3.3 HtmlAgilityPack fallback**

If SmartReader fails or produces empty text:
*   Parse HTML manually with HtmlAgilityPack.
    
*   Remove `<script>`, `<style>`, `<noscript>`.
    
*   Extract body text.
    
*   Normalize whitespace.
    

### **3.4 Text Cleaning**

*   Collapse whitespace
    
*   Trim leading/trailing junk
    
*   Optional length truncation for token control
    

* * *

📝 4. Direct Text Mode
----------------------

If the user passed raw text → skip extraction and normalize it.

* * *

🤖 5. AI Categorization
=======================

### **5.1 AiHttpClientCategorizer**

*   Uses typed HttpClient
    
*   Base URL: `https://models.github.ai/`
    
*   Endpoint: `inference/chat/completions`
    
*   Auth: `Bearer <GitHub PAT>`
    
*   Temperature: `0.0`
    
*   System prompt forces a **single-word** category.
    

### **5.2 GitHub Models**

The model (e.g., `openai/gpt-4o-mini`) analyzes the content and returns one category.

### **5.3 Response Parsing**

The service extracts:

    choices[0].message.content
    

or fallback fields, then normalizes the result.

### **5.4 Category Validation**

Ensures the result matches one of:

    Tech, AI, Cloud, .NET, Architecture, Tutorials, Other
    

If not → return **Other**.

* * *

📤 6. API Response
==================

    200 OK
    {
      "category": "AI"
    }
    

* * *

❗ Error Handling
================

### **HTML Extraction Errors**

| Error | Cause | Handling |
| --- | --- | --- |
| 403 Forbidden | Anti-bot rules | Retry w/ headers, else return 500 |
| Timeout | Slow website | Retry w/ backoff |
| Invalid URL | Bad input | 400 Bad Request |

### **AI Errors**

| Error | Cause | Handling |
| --- | --- | --- |
| 404 | Wrong model or wrong endpoint | Fix base URL to models.github.ai |
| 401 | Invalid PAT | Fix token or scope |
| 429 | Rate-limited | Retry-after support (future) |
| 500 | Model service error | Retry + log |

* * *

🛠 Implementation Notes
=======================

### **HttpClient Best Practices**

*   Use `AddHttpClient<T>` for extraction and AI categorization.
    
*   Avoid manually creating HttpClient instances (socket exhaustion risk).
    

### **Secrets**

*   Dev: User Secrets (`GitHub:Pat`)
    
*   Prod: Key Vault / Secret Manager
    

### **Optional caching**

*   Cache extracted article text (key: URL hash)
    
*   Cache classification results (key: article text hash)
    

### **Prompt design**

*   Use strict system prompt
    
*   Use `temperature = 0`
    
*   Still post-process output
    

* * *

🚀 Extensibility
================

### You can easily add:

*   Azure/AWS/GCP deployment
    
*   Logging (Serilog, ELK, Seq, AppInsights)
    
*   Rate limiting (ASP.NET Core middleware)
    
*   Polly policies for retry, timeout, circuit breaker
    
*   Batch classification endpoint
    
*   Database storage (for caching or analytics)
        


### Flow Chart
https://www.figma.com/board/Kbw4NKDFwDa8kWjuwAfRid/BlogCategorizer-System?node-id=0-1&t=kGFrrgoFzPIELs58-1