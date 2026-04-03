using System;
using System.Collections.Generic;
using System.Linq;
using Jdhog.Models;

namespace Jdhog.Services;

public sealed class ConversationStateService
{
    private readonly Dictionary<string, List<ConversationTurn>> conversations = new(StringComparer.OrdinalIgnoreCase);

    public string Summary => $"Tracks {conversations.Count} live seam conversation(s).";

    public IReadOnlyList<ConversationTurn> GetRecentTurns(string conversationKey, int maxTurns)
    {
        if (!conversations.TryGetValue(conversationKey, out var turns) || turns.Count == 0)
            return Array.Empty<ConversationTurn>();

        var safeCount = Math.Max(1, maxTurns);
        return turns.Count <= safeCount
            ? turns.ToArray()
            : turns.Skip(Math.Max(0, turns.Count - safeCount)).ToArray();
    }

    public void RecordUserTurn(string conversationKey, string content)
        => Append(conversationKey, "user", content);

    public void RecordAssistantTurn(string conversationKey, string content)
        => Append(conversationKey, "assistant", content);

    public void ClearConversation(string conversationKey)
    {
        if (!string.IsNullOrWhiteSpace(conversationKey))
            conversations.Remove(conversationKey);
    }

    private void Append(string conversationKey, string role, string content)
    {
        if (string.IsNullOrWhiteSpace(conversationKey) || string.IsNullOrWhiteSpace(content))
            return;

        if (!conversations.TryGetValue(conversationKey, out var turns))
        {
            turns = new List<ConversationTurn>();
            conversations[conversationKey] = turns;
        }

        turns.Add(new ConversationTurn(role, content.Trim(), DateTimeOffset.UtcNow));
    }
}
