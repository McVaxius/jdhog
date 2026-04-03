using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jdhog.Models;

namespace Jdhog.Services;

public sealed class OpenAiCompatibleChatEngine : IChatEngine, IDisposable
{
    private readonly HttpClient httpClient = new();

    public string ProviderKey => "openai-compatible";
    public string DisplayName => "OpenAI-compatible";
    public string Summary => "Single seam for local-host and remote API-key backends that expose an OpenAI-style chat endpoint.";

    public async Task<ProviderHealthSnapshot> CheckHealthAsync(Configuration configuration, CancellationToken cancellationToken = default)
    {
        if (!TryGetBaseUri(configuration.ProviderBaseUrl, out var baseUri))
        {
            return new ProviderHealthSnapshot
            {
                ProviderName = DisplayName,
                Status = "Not configured",
                Detail = "Provider base URL is missing or invalid.",
                IsConfigured = false,
                IsReachable = false,
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "models"));
        ApplyAuthorization(request, configuration.ProviderApiKey);

        try
        {
            using var response = await SendAsync(request, configuration.ProviderTimeoutSeconds, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = BuildHealthDetail(response.StatusCode, body);

            return new ProviderHealthSnapshot
            {
                ProviderName = DisplayName,
                Status = response.IsSuccessStatusCode ? "Reachable" : $"HTTP {(int)response.StatusCode}",
                Detail = detail,
                IsConfigured = !string.IsNullOrWhiteSpace(configuration.ProviderModel),
                IsReachable = response.IsSuccessStatusCode,
            };
        }
        catch (OperationCanceledException)
        {
            return new ProviderHealthSnapshot
            {
                ProviderName = DisplayName,
                Status = "Timeout",
                Detail = "Provider health check timed out.",
                IsConfigured = !string.IsNullOrWhiteSpace(configuration.ProviderModel),
                IsReachable = false,
            };
        }
        catch (Exception ex)
        {
            return new ProviderHealthSnapshot
            {
                ProviderName = DisplayName,
                Status = "Unavailable",
                Detail = ex.Message,
                IsConfigured = !string.IsNullOrWhiteSpace(configuration.ProviderModel),
                IsReachable = false,
            };
        }
    }

    public async Task<ChatEngineResult> GenerateAsync(
        Configuration configuration,
        ChatEngineRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        if (!TryGetBaseUri(configuration.ProviderBaseUrl, out var baseUri))
            return BuildFailure(ChatEngineOutcome.Unavailable, configuration, "Provider base URL is missing or invalid.", startedAt);

        if (string.IsNullOrWhiteSpace(configuration.ProviderModel))
            return BuildFailure(ChatEngineOutcome.Unavailable, configuration, "Provider model is not configured yet.", startedAt);

        var payload = BuildRequestPayload(configuration.ProviderModel, request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "chat/completions"));
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        ApplyAuthorization(httpRequest, configuration.ProviderApiKey);

        try
        {
            using var response = await SendAsync(httpRequest, configuration.ProviderTimeoutSeconds, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return BuildHttpFailure(configuration, response.StatusCode, body, startedAt);

            return ParseCompletionResponse(configuration.ProviderModel, body, startedAt);
        }
        catch (OperationCanceledException)
        {
            return BuildFailure(ChatEngineOutcome.Timeout, configuration, "Provider request timed out.", startedAt);
        }
        catch (Exception ex)
        {
            return BuildFailure(ChatEngineOutcome.Error, configuration, ex.Message, startedAt);
        }
    }

    public void Dispose()
        => httpClient.Dispose();

    private static string BuildRequestPayload(string model, ChatEngineRequest request)
    {
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = request.SystemPolicy,
            },
        };

        foreach (var turn in request.History)
        {
            messages.Add(new
            {
                role = turn.Role,
                content = turn.Content,
            });
        }

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Primary mode: {NullSafe(request.PrimaryMode)}");
        userPrompt.AppendLine($"Target notes: {NullSafe(request.TargetNotes)}");
        userPrompt.AppendLine($"Allow command suggestions: {request.AllowCommandSuggestions}");
        userPrompt.AppendLine($"Allow emote suggestions: {request.AllowEmoteSuggestions}");
        userPrompt.AppendLine("User message:");
        userPrompt.AppendLine(request.UserMessage.Trim());

        messages.Add(new
        {
            role = "user",
            content = userPrompt.ToString().Trim(),
        });

        return JsonSerializer.Serialize(new
        {
            model,
            temperature = 0.2,
            max_tokens = 350,
            messages,
        });
    }

    private ChatEngineResult ParseCompletionResponse(string modelName, string body, long startedAt)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            if (!TryParseAssistantEnvelope(content, out var status, out var assistantText, out var detail, out var proposals))
            {
                if (LooksLikeRefusal(content))
                    return BuildFailure(ChatEngineOutcome.Refused, modelName, "Provider refused without returning seam JSON.", startedAt, content);

                return BuildFailure(ChatEngineOutcome.Malformed, modelName, "Provider response was not valid seam JSON.", startedAt, content);
            }

            var outcome = status.Equals("refused", StringComparison.OrdinalIgnoreCase)
                ? ChatEngineOutcome.Refused
                : ChatEngineOutcome.Ok;

            return new ChatEngineResult
            {
                Outcome = outcome,
                ProviderName = DisplayName,
                ModelName = modelName,
                AssistantText = assistantText,
                Detail = detail,
                RawResponseText = content,
                ProposedActions = proposals,
                Duration = Stopwatch.GetElapsedTime(startedAt),
            };
        }
        catch (Exception ex)
        {
            return new ChatEngineResult
            {
                Outcome = ChatEngineOutcome.Malformed,
                ProviderName = DisplayName,
                ModelName = modelName,
                Detail = ex.Message,
                RawResponseText = body,
                Duration = Stopwatch.GetElapsedTime(startedAt),
            };
        }
    }

    private static bool TryParseAssistantEnvelope(
        string rawContent,
        out string status,
        out string assistantText,
        out string detail,
        out IReadOnlyList<ActionProposal> proposals)
    {
        status = "ok";
        assistantText = string.Empty;
        detail = string.Empty;
        proposals = Array.Empty<ActionProposal>();

        using var document = JsonDocument.Parse(rawContent);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (root.TryGetProperty("status", out var statusElement))
            status = statusElement.GetString() ?? "ok";
        if (root.TryGetProperty("assistant_text", out var assistantTextElement))
            assistantText = assistantTextElement.GetString() ?? string.Empty;
        if (root.TryGetProperty("reason", out var reasonElement))
            detail = reasonElement.GetString() ?? string.Empty;

        var collected = new List<ActionProposal>();
        if (root.TryGetProperty("proposed_commands", out var commandArray) && commandArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in commandArray.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    collected.Add(new ActionProposal("command", value));
            }
        }

        if (root.TryGetProperty("proposed_emotes", out var emoteArray) && emoteArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in emoteArray.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    collected.Add(new ActionProposal("emote", value));
            }
        }

        proposals = collected;
        return true;
    }

    private static void ApplyAuthorization(HttpRequestMessage request, string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 300)));
        return await httpClient.SendAsync(request, timeoutCts.Token);
    }

    private static bool TryGetBaseUri(string rawBaseUrl, out Uri baseUri)
    {
        if (Uri.TryCreate(rawBaseUrl?.Trim().TrimEnd('/') + "/", UriKind.Absolute, out baseUri!))
            return true;

        baseUri = null!;
        return false;
    }

    private ChatEngineResult BuildHttpFailure(Configuration configuration, HttpStatusCode statusCode, string body, long startedAt)
    {
        var outcome = statusCode switch
        {
            HttpStatusCode.Unauthorized => ChatEngineOutcome.Unauthorized,
            HttpStatusCode.Forbidden => ChatEngineOutcome.Unauthorized,
            (HttpStatusCode)429 => body.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                                   body.Contains("credit", StringComparison.OrdinalIgnoreCase)
                ? ChatEngineOutcome.QuotaExceeded
                : ChatEngineOutcome.RateLimited,
            HttpStatusCode.BadGateway => ChatEngineOutcome.Unavailable,
            HttpStatusCode.ServiceUnavailable => ChatEngineOutcome.Unavailable,
            HttpStatusCode.GatewayTimeout => ChatEngineOutcome.Timeout,
            _ => ChatEngineOutcome.Unavailable,
        };

        return BuildFailure(outcome, configuration, BuildHealthDetail(statusCode, body), startedAt, body);
    }

    private ChatEngineResult BuildFailure(ChatEngineOutcome outcome, Configuration configuration, string detail, long startedAt, string rawResponseText = "")
        => BuildFailure(outcome, configuration.ProviderModel, detail, startedAt, rawResponseText);

    private ChatEngineResult BuildFailure(ChatEngineOutcome outcome, string modelName, string detail, long startedAt, string rawResponseText = "")
        => new()
        {
            Outcome = outcome,
            ProviderName = DisplayName,
            ModelName = modelName ?? string.Empty,
            Detail = detail,
            RawResponseText = rawResponseText,
            Duration = Stopwatch.GetElapsedTime(startedAt),
        };

    private static string BuildHealthDetail(HttpStatusCode statusCode, string body)
    {
        var trimmed = string.IsNullOrWhiteSpace(body)
            ? string.Empty
            : body.Trim().Replace("\r", " ").Replace("\n", " ");

        if (trimmed.Length > 220)
            trimmed = trimmed[..220] + "...";

        return string.IsNullOrWhiteSpace(trimmed)
            ? $"HTTP {(int)statusCode}"
            : $"HTTP {(int)statusCode}: {trimmed}";
    }

    private static bool LooksLikeRefusal(string content)
        => content.Contains("cannot help", StringComparison.OrdinalIgnoreCase) ||
           content.Contains("can't help", StringComparison.OrdinalIgnoreCase) ||
           content.Contains("not able to", StringComparison.OrdinalIgnoreCase) ||
           content.Contains("won't do that", StringComparison.OrdinalIgnoreCase) ||
           content.Contains("refuse", StringComparison.OrdinalIgnoreCase);

    private static string NullSafe(string value)
        => string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
}
