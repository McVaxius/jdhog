using Dalamud.Configuration;
using System;

namespace Jdhog;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool PluginEnabled { get; set; } = false;
    public bool DtrBarEnabled { get; set; } = true;
    public int DtrBarMode { get; set; } = 1;
    public string DtrIconEnabled { get; set; } = "\uE044";
    public string DtrIconDisabled { get; set; } = "\uE04C";
    public string LastAccountId { get; set; } = string.Empty;
    public string PreferredProviderKey { get; set; } = "openai-compatible";
    public string ProviderBaseUrl { get; set; } = "http://127.0.0.1:1234/v1";
    public string ProviderModel { get; set; } = string.Empty;
    public string ProviderApiKey { get; set; } = string.Empty;
    public int ProviderTimeoutSeconds { get; set; } = 45;
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
