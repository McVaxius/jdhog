namespace Jdhog;

internal static class PluginInfo
{
    public const string DisplayName = "Jabberdhoggy";
    public const string InternalName = "jdhog";
    public const string Command = "/jdhog";
    public const string Visibility = "Public";
    public const string Summary = "Offline-first chatbot scaffold with per-account and per-character policy.";
    public const string SupportUrl = "https://ko-fi.com/mcvaxius";
    public static readonly string[] Concept = new[]
    {
        "Separate account and character policy.",
        "Keep inference offline-first and bounded.",
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
        "Model evaluation",
        "Channel listeners",
        "Action permissions",
        "Polish"
    };
    public static readonly string[] Tests = new[]
    {
        "Load plugin and open UI",
        "Confirm account and character profile creation",
        "Save profile notes"
    };
}
