namespace Jdhog;

internal static class PluginInfo
{
    public const string DisplayName = "JabberDhog";
    public const string InternalName = "jdhog";
    public const string Command = "/jdhog";
    public const string AliasCommand = "/jd";
    public const string Visibility = "Public";
    public const string Summary = "Offline-first chatbot seam with per-account policy, provider-agnostic inference, and explicit action review.";
    public const string SupportUrl = "https://ko-fi.com/mcvaxius";
    public static readonly string[] Concept = new[]
    {
        "Separate account and character policy.",
        "Keep inference offline-first and bounded.",
        "Treat the model as an adviser, never the final actor.",
        "Require explicit permission for emotes and commands."
    };
    public static readonly string[] Services = new[]
    {
        "ConfigManager",
        "OfflineModelHost",
        "ConversationStateService",
        "OutboundActionPolicy",
        "IChatEngine"
    };
    public static readonly string[] Phases = new[]
    {
        "Shell and docs",
        "Provider seam",
        "Channel listeners",
        "Action permissions",
        "Polish"
    };
    public static readonly string[] Tests = new[]
    {
        "Load plugin and open UI",
        "Confirm account and character profile creation",
        "Check provider health",
        "Run seam preview and inspect policy review"
    };
}
