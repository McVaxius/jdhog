using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jdhog.Models;

namespace Jdhog.Services;

public sealed class OfflineModelHost : IDisposable
{
    private readonly Configuration configuration;
    private readonly ConversationStateService conversationStateService;
    private readonly OutboundActionPolicy outboundActionPolicy;
    private readonly Dictionary<string, IChatEngine> providers;

    public OfflineModelHost(
        Configuration configuration,
        ConversationStateService conversationStateService,
        OutboundActionPolicy outboundActionPolicy)
    {
        this.configuration = configuration;
        this.conversationStateService = conversationStateService;
        this.outboundActionPolicy = outboundActionPolicy;

        providers = new Dictionary<string, IChatEngine>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai-compatible"] = new OpenAiCompatibleChatEngine(),
        };
    }

    public string Summary
        => $"Provider seam with {providers.Count} optional backend(s). Active: {GetActiveProvider()?.DisplayName ?? "None"}";

    public IReadOnlyList<IChatEngine> GetProviders()
        => providers.Values.ToArray();

    public IChatEngine? GetActiveProvider()
        => providers.GetValueOrDefault(configuration.PreferredProviderKey);

    public string BuildConversationKey(string selectedCharacterKey)
        => string.IsNullOrWhiteSpace(selectedCharacterKey)
            ? "account-default"
            : selectedCharacterKey.Trim();

    public Task<ProviderHealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var provider = GetActiveProvider();
        if (provider == null)
        {
            return Task.FromResult(new ProviderHealthSnapshot
            {
                ProviderName = configuration.PreferredProviderKey,
                Status = "Disabled",
                Detail = "No provider is selected.",
                IsConfigured = false,
                IsReachable = false,
            });
        }

        return provider.CheckHealthAsync(configuration, cancellationToken);
    }

    public async Task<ChatEngineResult> RunPreviewAsync(
        string conversationKey,
        CharacterConfig characterConfig,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var trimmedMessage = userMessage.Trim();
        if (string.IsNullOrWhiteSpace(trimmedMessage))
        {
            return new ChatEngineResult
            {
                Outcome = ChatEngineOutcome.Error,
                ProviderName = GetActiveProvider()?.DisplayName ?? "None",
                Detail = "Preview message is empty.",
            };
        }

        var provider = GetActiveProvider();
        if (provider == null)
        {
            return new ChatEngineResult
            {
                Outcome = ChatEngineOutcome.Unavailable,
                ProviderName = "None",
                Detail = "No provider is selected.",
            };
        }

        var maxTurns = Math.Clamp(characterConfig.ConversationTurnLimit, 1, 40);
        var request = new ChatEngineRequest
        {
            ConversationKey = conversationKey,
            UserMessage = trimmedMessage,
            PrimaryMode = characterConfig.PrimaryMode,
            TargetNotes = characterConfig.TargetNotes,
            SystemPolicy = outboundActionPolicy.BuildSystemPolicy(characterConfig),
            AllowCommandSuggestions = characterConfig.AllowModelCommandSuggestions,
            AllowEmoteSuggestions = characterConfig.AllowModelEmoteSuggestions,
            History = conversationStateService.GetRecentTurns(conversationKey, maxTurns),
        };

        var result = await provider.GenerateAsync(configuration, request, cancellationToken);
        var reviews = outboundActionPolicy.ReviewProposals(result.ProposedActions, characterConfig);
        var finalOutcome = result.Outcome == ChatEngineOutcome.Ok && reviews.Any(review => !review.Allowed)
            ? ChatEngineOutcome.PolicyBlocked
            : result.Outcome;

        if (result.Outcome == ChatEngineOutcome.Ok)
        {
            conversationStateService.RecordUserTurn(conversationKey, trimmedMessage);
            if (!string.IsNullOrWhiteSpace(result.AssistantText))
                conversationStateService.RecordAssistantTurn(conversationKey, result.AssistantText);
        }

        return new ChatEngineResult
        {
            Outcome = finalOutcome,
            ProviderName = result.ProviderName,
            ModelName = result.ModelName,
            AssistantText = result.AssistantText,
            Detail = result.Detail,
            RawResponseText = result.RawResponseText,
            ProposedActions = result.ProposedActions,
            PolicyReviews = reviews,
            Duration = result.Duration,
        };
    }

    public void ClearConversation(string conversationKey)
        => conversationStateService.ClearConversation(conversationKey);

    public void Dispose()
    {
        foreach (var provider in providers.Values.OfType<IDisposable>())
            provider.Dispose();
    }
}
