using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Jdhog.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly string[] DtrModes = { "Text only", "Icon + text", "Icon only" };
    private static readonly string[] SuggestedEmotes =
    {
        "/wave",
        "/smile",
        "/nod",
        "/cheer",
        "/clap",
        "/thumbsup",
        "/blush",
        "/think",
        "/doze",
        "/dance",
    };

    private readonly Plugin plugin;
    private int selectedEmoteIndex;
    private string customCommand = string.Empty;

    public ConfigWindow(Plugin plugin) : base($"{PluginInfo.DisplayName} Settings##Config")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(720f, 520f), MaximumSize = new Vector2(1500f, 1300f) };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("JdhogConfigTabs"))
        {
            if (ImGui.BeginTabItem("Getting Started"))
            {
                DrawGettingStartedTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("General"))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Provider"))
            {
                DrawProviderTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Profile"))
            {
                DrawProfileTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawGettingStartedTab()
    {
        var cfg = plugin.Configuration;

        ImGui.TextWrapped("JabberDhog does not ship a model by itself. It talks to an optional provider, and the easiest first setup is a local OpenAI-compatible host so your testing can stay on your own machine.");
        ImGui.Separator();

        ImGui.TextUnformatted("Recommended first path");
        ImGui.BulletText("Install a local host such as LM Studio.");
        ImGui.BulletText("Download or load a chat model in that host.");
        ImGui.BulletText("Start the host's OpenAI-compatible local server.");
        ImGui.BulletText("Switch to the Provider tab and enter the local Base URL plus the exact model name exposed by the host.");
        ImGui.BulletText("Leave API key blank for a purely local host unless the host explicitly requires one.");
        ImGui.BulletText("Open the main JabberDhog window and use Check provider health, then Run seam preview.");

        if (ImGui.SmallButton("Open LM Studio"))
            Process.Start(new ProcessStartInfo { FileName = "https://lmstudio.ai/", UseShellExecute = true });
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open LM Studio in your browser. This is the recommended first local-host path.");

        ImGui.SameLine();
        if (ImGui.SmallButton("Open OpenAI-compatible docs"))
            Process.Start(new ProcessStartInfo { FileName = "https://platform.openai.com/docs/overview", UseShellExecute = true });
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open general API docs for providers that expose an OpenAI-compatible endpoint.");

        ImGui.Separator();
        ImGui.TextUnformatted("What to enter in Provider");
        ImGui.BulletText($"Base URL: {cfg.ProviderBaseUrl}");
        ImGui.BulletText("Model name: the exact model ID reported by your local or remote provider.");
        ImGui.BulletText("API key: optional for local hosts, usually required for paid remote providers.");
        ImGui.BulletText("Timeout: leave the default unless your provider is especially slow.");

        ImGui.Separator();
        ImGui.TextUnformatted("Safety model");
        ImGui.BulletText("Providers are optional, and JabberDhog can swap between them later.");
        ImGui.BulletText("The model only proposes text, commands, or emotes.");
        ImGui.BulletText("JabberDhog remains the final authority and blocks anything outside your allow-lists.");
        ImGui.BulletText("Refusal, quota, timeout, malformed output, and unavailable-provider states are expected seam outcomes.");
    }

    private void DrawGeneralTab()
    {
        var cfg = plugin.Configuration;

        var enabled = cfg.PluginEnabled;
        if (ImGui.Checkbox("Plugin enabled", ref enabled))
        {
            cfg.PluginEnabled = enabled;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var dtr = cfg.DtrBarEnabled;
        if (ImGui.Checkbox("Show DTR bar entry", ref dtr))
        {
            cfg.DtrBarEnabled = dtr;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var mode = cfg.DtrBarMode;
        if (ImGui.Combo("DTR mode", ref mode, DtrModes, DtrModes.Length))
        {
            cfg.DtrBarMode = mode;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var onIcon = cfg.DtrIconEnabled;
        if (ImGui.InputText("DTR enabled glyph", ref onIcon, 8))
        {
            cfg.DtrIconEnabled = onIcon.Length <= 3 ? onIcon : onIcon[..3];
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        var offIcon = cfg.DtrIconDisabled;
        if (ImGui.InputText("DTR disabled glyph", ref offIcon, 8))
        {
            cfg.DtrIconDisabled = offIcon.Length <= 3 ? offIcon : offIcon[..3];
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Seam overview");
        ImGui.BulletText(plugin.OfflineModelHost.Summary);
        ImGui.BulletText(plugin.ConversationStateService.Summary);
        ImGui.BulletText(plugin.OutboundActionPolicy.Summary);
        ImGui.Spacing();
        ImGui.TextWrapped("Recommended first setup: follow the Getting Started tab, then use the Provider tab to point JabberDhog at a local OpenAI-compatible host before trying the seam preview.");
    }

    private void DrawProviderTab()
    {
        var cfg = plugin.Configuration;
        var providers = plugin.OfflineModelHost.GetProviders();
        var providerLabels = providers.Select(provider => provider.DisplayName).ToArray();
        var selectedProviderIndex = 0;
        for (var i = 0; i < providers.Count; i++)
        {
            if (!providers[i].ProviderKey.Equals(cfg.PreferredProviderKey, StringComparison.OrdinalIgnoreCase))
                continue;

            selectedProviderIndex = i;
            break;
        }

        if (ImGui.Combo("Active provider", ref selectedProviderIndex, providerLabels, providerLabels.Length))
        {
            cfg.PreferredProviderKey = providers[selectedProviderIndex].ProviderKey;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("The seam treats every backend as optional. This first path targets any OpenAI-compatible local or remote endpoint.");

        var baseUrl = cfg.ProviderBaseUrl;
        if (ImGui.InputText("Base URL", ref baseUrl, 256))
        {
            cfg.ProviderBaseUrl = baseUrl;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Examples: http://127.0.0.1:1234/v1 for LM Studio, or a remote OpenAI-compatible gateway.");

        var model = cfg.ProviderModel;
        if (ImGui.InputText("Model name", ref model, 128))
        {
            cfg.ProviderModel = model;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("This must match the model ID exposed by the provider. Leave everything else the same when swapping local or remote backends.");

        var apiKey = cfg.ProviderApiKey;
        if (ImGui.InputText("API key", ref apiKey, 256, ImGuiInputTextFlags.Password))
        {
            cfg.ProviderApiKey = apiKey;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Optional for local-host flows. Stored in the plugin config so the seam can reuse it for remote providers.");

        var timeout = cfg.ProviderTimeoutSeconds;
        ImGui.SetNextItemWidth(100f);
        if (ImGui.InputInt("Timeout seconds", ref timeout))
        {
            cfg.ProviderTimeoutSeconds = Math.Clamp(timeout, 5, 300);
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Shared request timeout for health checks and seam preview calls.");

        ImGui.Separator();
        ImGui.TextUnformatted("Contract expectations");
        ImGui.BulletText("The model must return strict JSON for the seam.");
        ImGui.BulletText("Refusal, quota, timeout, malformed output, and policy blocks are first-class result states.");
        ImGui.BulletText("The plugin remains the final authority for any emote or command proposal.");
    }

    private void DrawProfileTab()
    {
        var account = plugin.ConfigManager.GetCurrentAccount();
        ImGui.Text($"Current account: {account?.AccountAlias ?? "(waiting for login)"}");

        var currentProfileLabel = string.IsNullOrWhiteSpace(plugin.ConfigManager.SelectedCharacterKey)
            ? "(Account default)"
            : plugin.ConfigManager.SelectedCharacterKey;
        if (ImGui.BeginCombo("Character profile", currentProfileLabel))
        {
            if (ImGui.Selectable("(Account default)", string.IsNullOrWhiteSpace(plugin.ConfigManager.SelectedCharacterKey)))
                plugin.ConfigManager.SelectedCharacterKey = string.Empty;

            foreach (var key in plugin.ConfigManager.GetSortedCharacterKeys())
            {
                if (ImGui.Selectable(key, key == plugin.ConfigManager.SelectedCharacterKey))
                    plugin.ConfigManager.SelectedCharacterKey = key;
            }

            ImGui.EndCombo();
        }

        var pcfg = plugin.ConfigManager.GetActiveConfig();

        var profileEnabled = pcfg.Enabled;
        if (ImGui.Checkbox("Profile enabled", ref profileEnabled))
        {
            pcfg.Enabled = profileEnabled;
            plugin.ConfigManager.SaveCurrentAccount();
        }

        var primaryMode = pcfg.PrimaryMode;
        if (ImGui.InputText("Primary mode", ref primaryMode, 128))
        {
            pcfg.PrimaryMode = primaryMode;
            plugin.ConfigManager.SaveCurrentAccount();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Short description of how this character should behave, for example 'quiet greeter' or 'playful venue helper'.");

        var notes = pcfg.TargetNotes;
        if (ImGui.InputTextMultiline("Target notes", ref notes, 2048, new Vector2(-1f, 110f)))
        {
            pcfg.TargetNotes = notes;
            plugin.ConfigManager.SaveCurrentAccount();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Durable notes that become part of the local seam policy prompt.");

        var turnLimit = pcfg.ConversationTurnLimit;
        ImGui.SetNextItemWidth(100f);
        if (ImGui.InputInt("Conversation turn limit", ref turnLimit))
        {
            pcfg.ConversationTurnLimit = Math.Clamp(turnLimit, 1, 40);
            plugin.ConfigManager.SaveCurrentAccount();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("How many recent turns the seam keeps in memory for this character profile.");

        var allowCommands = pcfg.AllowModelCommandSuggestions;
        if (ImGui.Checkbox("Allow command suggestions", ref allowCommands))
        {
            pcfg.AllowModelCommandSuggestions = allowCommands;
            plugin.ConfigManager.SaveCurrentAccount();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Even when enabled, proposed slash commands still have to match the explicit allow-list below.");

        var allowEmotes = pcfg.AllowModelEmoteSuggestions;
        if (ImGui.Checkbox("Allow emote suggestions", ref allowEmotes))
        {
            pcfg.AllowModelEmoteSuggestions = allowEmotes;
            plugin.ConfigManager.SaveCurrentAccount();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Even when enabled, proposed emotes still have to match the explicit allow-list below.");

        ImGui.Separator();
        ImGui.TextUnformatted("Allowed emotes");
        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("##AllowedEmotePicker", ref selectedEmoteIndex, SuggestedEmotes, SuggestedEmotes.Length);
        ImGui.SameLine();
        if (ImGui.Button("Add emote"))
            AddUniqueEntry(pcfg.AllowedEmotes, SuggestedEmotes[selectedEmoteIndex]);

        DrawEditableList("AllowedEmotes", pcfg.AllowedEmotes);

        ImGui.Spacing();
        ImGui.TextUnformatted("Allowed commands");
        ImGui.SetNextItemWidth(-110f);
        if (ImGui.InputText("##AllowedCommandInput", ref customCommand, 256, ImGuiInputTextFlags.EnterReturnsTrue))
            AddUniqueEntry(pcfg.AllowedCommands, customCommand);

        ImGui.SameLine();
        if (ImGui.Button("Add command"))
            AddUniqueEntry(pcfg.AllowedCommands, customCommand);

        DrawEditableList("AllowedCommands", pcfg.AllowedCommands);
    }

    private void AddUniqueEntry(List<string> entries, string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || entries.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            return;

        entries.Add(trimmed);
        customCommand = string.Empty;
        plugin.ConfigManager.SaveCurrentAccount();
    }

    private void DrawEditableList(string id, List<string> entries)
    {
        if (entries.Count == 0)
        {
            ImGui.TextDisabled("None configured yet.");
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            ImGui.PushID($"{id}_{i}");
            if (ImGui.SmallButton("-"))
            {
                entries.RemoveAt(i);
                plugin.ConfigManager.SaveCurrentAccount();
                ImGui.PopID();
                break;
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(entry);
            ImGui.PopID();
        }
    }
}
