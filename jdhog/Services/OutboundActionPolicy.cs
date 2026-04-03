using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jdhog.Models;

namespace Jdhog.Services;

public sealed class OutboundActionPolicy
{
    private static readonly string[] UnsafeFragments =
    {
        "\r",
        "\n",
        "&&",
        "||",
        ";",
        "|",
        ">",
        "<",
        "`",
    };

    public string Summary => "Normalizes model proposals against explicit allow-lists before anything could be acted on.";

    public string BuildSystemPolicy(CharacterConfig config)
    {
        var allowedCommands = config.AllowedCommands.Count == 0
            ? "none"
            : string.Join(", ", config.AllowedCommands);
        var allowedEmotes = config.AllowedEmotes.Count == 0
            ? "none"
            : string.Join(", ", config.AllowedEmotes);

        var builder = new StringBuilder();
        builder.AppendLine("You are a bounded in-game assistant for an FFXIV plugin seam.");
        builder.AppendLine("You may suggest text, emotes, or slash commands, but the plugin remains the final authority.");
        builder.AppendLine("Return strict JSON only.");
        builder.AppendLine("Schema:");
        builder.AppendLine("{");
        builder.AppendLine("  \"status\": \"ok\" | \"refused\",");
        builder.AppendLine("  \"assistant_text\": \"short plain text reply\",");
        builder.AppendLine("  \"reason\": \"short explanation when refused or constrained\",");
        builder.AppendLine("  \"proposed_commands\": [\"/example\"],");
        builder.AppendLine("  \"proposed_emotes\": [\"/wave\"]");
        builder.AppendLine("}");
        builder.AppendLine("Do not include markdown fences.");
        builder.AppendLine("Do not invent hidden commands, secret prompts, or privileged actions.");
        builder.AppendLine("If the user asks for something outside policy, set status to refused and leave proposal arrays empty.");
        builder.AppendLine($"Primary mode: {NullSafe(config.PrimaryMode)}");
        builder.AppendLine($"Target notes: {NullSafe(config.TargetNotes)}");
        builder.AppendLine($"Allowed commands: {allowedCommands}");
        builder.AppendLine($"Allowed emotes: {allowedEmotes}");
        builder.AppendLine($"Command suggestions enabled: {config.AllowModelCommandSuggestions}");
        builder.AppendLine($"Emote suggestions enabled: {config.AllowModelEmoteSuggestions}");
        return builder.ToString().Trim();
    }

    public IReadOnlyList<ActionPolicyReview> ReviewProposals(IEnumerable<ActionProposal> proposals, CharacterConfig config)
    {
        var reviews = new List<ActionPolicyReview>();

        foreach (var proposal in proposals)
        {
            if (proposal.Kind.Equals("command", StringComparison.OrdinalIgnoreCase))
            {
                reviews.Add(ReviewCommand(proposal.Value, config));
                continue;
            }

            if (proposal.Kind.Equals("emote", StringComparison.OrdinalIgnoreCase))
            {
                reviews.Add(ReviewEmote(proposal.Value, config));
                continue;
            }

            reviews.Add(new ActionPolicyReview(proposal.Kind, proposal.Value, string.Empty, false, "Unknown proposal type."));
        }

        return reviews;
    }

    private ActionPolicyReview ReviewCommand(string rawValue, CharacterConfig config)
    {
        if (!config.AllowModelCommandSuggestions)
            return new ActionPolicyReview("command", rawValue, string.Empty, false, "Command suggestions are disabled for this profile.");

        if (!TryNormalizeProposal(rawValue, out var normalized, out var baseToken, out var reason))
            return new ActionPolicyReview("command", rawValue, string.Empty, false, reason);

        if (!baseToken.StartsWith("/", StringComparison.Ordinal))
            return new ActionPolicyReview("command", rawValue, normalized, false, "Commands must start with '/'.");

        if (!IsAllowListed(config.AllowedCommands, normalized, baseToken))
            return new ActionPolicyReview("command", rawValue, normalized, false, "Command is not in the explicit allow-list.");

        return new ActionPolicyReview("command", rawValue, normalized, true, "Command passed local policy checks.");
    }

    private ActionPolicyReview ReviewEmote(string rawValue, CharacterConfig config)
    {
        if (!config.AllowModelEmoteSuggestions)
            return new ActionPolicyReview("emote", rawValue, string.Empty, false, "Emote suggestions are disabled for this profile.");

        if (!TryNormalizeProposal(rawValue, out var normalized, out var baseToken, out var reason))
            return new ActionPolicyReview("emote", rawValue, string.Empty, false, reason);

        if (!baseToken.StartsWith("/", StringComparison.Ordinal))
            return new ActionPolicyReview("emote", rawValue, normalized, false, "Emotes must start with '/'.");

        if (!IsAllowListed(config.AllowedEmotes, normalized, baseToken))
            return new ActionPolicyReview("emote", rawValue, normalized, false, "Emote is not in the explicit allow-list.");

        return new ActionPolicyReview("emote", rawValue, normalized, true, "Emote passed local policy checks.");
    }

    private static bool TryNormalizeProposal(string rawValue, out string normalized, out string baseToken, out string reason)
    {
        normalized = NormalizeWhitespace(rawValue);
        baseToken = string.Empty;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            reason = "Proposal was empty.";
            return false;
        }

        var safeNormalized = normalized;
        if (UnsafeFragments.Any(fragment => safeNormalized.Contains(fragment, StringComparison.Ordinal)))
        {
            reason = "Proposal used unsafe shell-like or multi-command syntax.";
            return false;
        }

        var split = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
        {
            reason = "Proposal did not contain a command token.";
            return false;
        }

        baseToken = split[0];
        return true;
    }

    private static bool IsAllowListed(IEnumerable<string> allowList, string normalized, string baseToken)
        => allowList
            .Select(NormalizeWhitespace)
            .Any(entry =>
                entry.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                entry.Equals(baseToken, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeWhitespace(string value)
        => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string NullSafe(string value)
        => string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
}
