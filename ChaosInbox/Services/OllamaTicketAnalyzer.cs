using System.Net.Http.Json;
using System.Text.Json;
using ChaosInbox.Models;

namespace ChaosInbox.Services;

public class OllamaTicketAnalyzer(HttpClient httpClient, IConfiguration configuration, FallbackTicketAnalyzer fallback)
{
    private readonly string _model = configuration["Ollama:Model"] ?? "gemma3:4b";

    public async Task<TicketAnalysis> AnalyseAsync(EmailEnvelope email, CancellationToken cancellationToken = default)
    {
        var fallbackResult = fallback.Analyse(email, DateTime.UtcNow);

        try
        {
            var schema = new
            {
                type = "object",
                properties = new
                {
                    summary = new { type = "string" },
                    priority = new { type = "string", @enum = new[] { "Low", "Medium", "High", "Critical" } },
                    deadlineUtc = new { type = new[] { "string", "null" }, description = "ISO-8601 UTC datetime or null" },
                    confidence = new { type = "number", minimum = 0, maximum = 1 }
                },
                required = new[] { "summary", "priority", "deadlineUtc", "confidence" }
            };

            var prompt = $"""
You are a ticket-triage engine. Read the email and return ONLY data matching the supplied JSON schema.
Rules:
- Summary: 1-2 short sentences, focused on the requested action.
- Critical: outage, security incident, immediate/asap, or deadline within 24 hours.
- High: important action or deadline within 3 days.
- Medium: normal actionable work.
- Low: FYI/no-rush/non-actionable.
- deadlineUtc: infer only when the email gives a credible deadline; otherwise null.
- Current UTC time: {DateTime.UtcNow:O}

FROM: {email.Sender}
SUBJECT: {email.Subject}
BODY:
{email.Body}
""";

            var request = new
            {
                model = _model,
                stream = false,
                format = schema,
                messages = new[] { new { role = "user", content = prompt } },
                options = new { temperature = 0.1 }
            };

            using var response = await httpClient.PostAsJsonAsync("api/chat", request, cancellationToken);
            if (!response.IsSuccessStatusCode) return fallbackResult;

            var root = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(root?.Message?.Content)) return fallbackResult;

            var parsed = JsonSerializer.Deserialize<AiPayload>(root.Message.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Summary)) return fallbackResult;
            if (!Enum.TryParse<TicketPriority>(parsed.Priority, true, out var priority)) return fallbackResult;

            DateTime? deadline = null;
            if (!string.IsNullOrWhiteSpace(parsed.DeadlineUtc) &&
                DateTimeOffset.TryParse(parsed.DeadlineUtc, out var dto))
                deadline = dto.UtcDateTime;

            var confidence = Math.Clamp(parsed.Confidence, 0, 1);
            return new TicketAnalysis(parsed.Summary.Trim(), priority, deadline, confidence, true);
        }
        catch
        {
            return fallbackResult;
        }
    }

    private sealed class OllamaResponse
    {
        public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string Content { get; set; } = "";
    }

    private sealed class AiPayload
    {
        public string Summary { get; set; } = "";
        public string Priority { get; set; } = "Medium";
        public string? DeadlineUtc { get; set; }
        public double Confidence { get; set; } = 0.5;
    }
}
