using System;
using System.Collections.Generic;

namespace Jdhog.Models;

public enum ChatEngineOutcome
{
    Ok,
    Refused,
    QuotaExceeded,
    RateLimited,
    Unavailable,
    Unauthorized,
    Timeout,
    Malformed,
    ToolNotAllowed,
    PolicyBlocked,
    Error,
}

public sealed record ConversationTurn(string Role, string Content, DateTimeOffset TimestampUtc);

public sealed record ActionProposal(string Kind, string Value, string Explanation = "");

public sealed record ActionPolicyReview(string Kind, string OriginalValue, string NormalizedValue, bool Allowed, string Reason);

public sealed class ChatEngineRequest
{
    public string ConversationKey { get; init; } = string.Empty;
    public string UserMessage { get; init; } = string.Empty;
    public string PrimaryMode { get; init; } = string.Empty;
    public string TargetNotes { get; init; } = string.Empty;
    public string SystemPolicy { get; init; } = string.Empty;
    public bool AllowCommandSuggestions { get; init; }
    public bool AllowEmoteSuggestions { get; init; }
    public IReadOnlyList<ConversationTurn> History { get; init; } = Array.Empty<ConversationTurn>();
}

public sealed class ProviderHealthSnapshot
{
    public bool IsConfigured { get; init; }
    public bool IsReachable { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public DateTimeOffset CheckedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ChatEngineResult
{
    public ChatEngineOutcome Outcome { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string AssistantText { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string RawResponseText { get; init; } = string.Empty;
    public IReadOnlyList<ActionProposal> ProposedActions { get; init; } = Array.Empty<ActionProposal>();
    public IReadOnlyList<ActionPolicyReview> PolicyReviews { get; init; } = Array.Empty<ActionPolicyReview>();
    public TimeSpan Duration { get; init; } = TimeSpan.Zero;
}
