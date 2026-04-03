using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Jdhog.Models;

namespace Jdhog.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string previewPrompt = "Give me a safe in-character greeting idea for a nearby player.";
    private ProviderHealthSnapshot? lastHealthSnapshot;
    private ChatEngineResult? lastPreviewResult;
    private bool healthBusy;
    private bool previewBusy;
    private CancellationTokenSource? operationCts;

    public MainWindow(Plugin plugin) : base($"{PluginInfo.DisplayName}##Main")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(620f, 500f), MaximumSize = new Vector2(1400f, 1200f) };
    }

    public void Dispose()
    {
        operationCts?.Cancel();
        operationCts?.Dispose();
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        var characterConfig = plugin.ConfigManager.GetActiveConfig();
        var account = plugin.ConfigManager.GetCurrentAccount();
        var conversationKey = plugin.OfflineModelHost.BuildConversationKey(plugin.ConfigManager.SelectedCharacterKey);

        ImGui.Text($"{PluginInfo.DisplayName} v{version}");
        ImGui.SameLine(ImGui.GetWindowWidth() - 120f);
        if (ImGui.SmallButton("Ko-fi"))
            Process.Start(new ProcessStartInfo { FileName = PluginInfo.SupportUrl, UseShellExecute = true });

        ImGui.Separator();

        var enabled = cfg.PluginEnabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            cfg.PluginEnabled = enabled;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        ImGui.SameLine();
        var dtr = cfg.DtrBarEnabled;
        if (ImGui.Checkbox("DTR Bar", ref dtr))
        {
            cfg.DtrBarEnabled = dtr;
            cfg.Save();
            plugin.UpdateDtrBar();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Settings"))
            plugin.ToggleConfigUi();

        ImGui.SameLine();
        if (ImGui.SmallButton("Status to chat"))
            plugin.PrintStatus("Seam bootstrap is loaded.");

        ImGui.TextWrapped(PluginInfo.Summary);
        ImGui.Text($"Account: {account?.AccountAlias ?? "(waiting for login)"}");
        ImGui.Text($"Character profile: {(string.IsNullOrWhiteSpace(plugin.ConfigManager.SelectedCharacterKey) ? "(Account default)" : plugin.ConfigManager.SelectedCharacterKey)}");
        ImGui.Text($"Conversation key: {conversationKey}");
        ImGui.Text($"Profile enabled: {(characterConfig.Enabled ? "Yes" : "No")}");
        ImGui.Text($"Active provider: {plugin.OfflineModelHost.GetActiveProvider()?.DisplayName ?? "None"}");

        if (string.IsNullOrWhiteSpace(cfg.ProviderModel))
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Provider setup still needed");
            ImGui.TextWrapped("Open Settings and start with the Getting Started tab. The easiest first path is a local OpenAI-compatible host such as LM Studio, then a health check, then a seam preview.");

            if (ImGui.SmallButton("Open settings##Setup"))
                plugin.ToggleConfigUi();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open JabberDhog settings so you can follow the guided provider setup.");

            ImGui.SameLine();
            if (ImGui.SmallButton("Open LM Studio##Setup"))
                Process.Start(new ProcessStartInfo { FileName = "https://lmstudio.ai/", UseShellExecute = true });
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open LM Studio in your browser for the recommended first local-host setup path.");
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Provider seam");
        ImGui.BulletText(plugin.OfflineModelHost.Summary);
        ImGui.BulletText(plugin.ConversationStateService.Summary);
        ImGui.BulletText(plugin.OutboundActionPolicy.Summary);

        if (healthBusy)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Checking provider...");
            ImGui.EndDisabled();
        }
        else if (ImGui.Button("Check provider health"))
        {
            _ = RunHealthCheckAsync();
        }

        ImGui.SameLine();
        if (previewBusy)
        {
            ImGui.BeginDisabled();
            ImGui.Button("Running seam preview...");
            ImGui.EndDisabled();
        }
        else if (ImGui.Button("Run seam preview"))
        {
            _ = RunPreviewAsync(conversationKey);
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear seam history"))
        {
            plugin.OfflineModelHost.ClearConversation(conversationKey);
            lastPreviewResult = null;
        }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextMultiline("Preview prompt", ref previewPrompt, 2048, new Vector2(-1f, 90f));

        if (lastHealthSnapshot != null)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Latest health");
            ImGui.Text($"Status: {lastHealthSnapshot.Status}");
            ImGui.TextWrapped(lastHealthSnapshot.Detail);
        }

        if (lastPreviewResult != null)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Latest preview result");
            ImGui.Text($"Outcome: {lastPreviewResult.Outcome}");
            ImGui.Text($"Provider: {lastPreviewResult.ProviderName}");
            ImGui.Text($"Model: {lastPreviewResult.ModelName}");
            ImGui.Text($"Duration: {lastPreviewResult.Duration.TotalMilliseconds:F0} ms");
            if (!string.IsNullOrWhiteSpace(lastPreviewResult.Detail))
                ImGui.TextWrapped($"Detail: {lastPreviewResult.Detail}");

            if (!string.IsNullOrWhiteSpace(lastPreviewResult.AssistantText))
            {
                ImGui.Spacing();
                ImGui.TextUnformatted("Assistant text");
                ImGui.TextWrapped(lastPreviewResult.AssistantText);
            }

            if (lastPreviewResult.ProposedActions.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextUnformatted("Proposed actions");
                foreach (var proposal in lastPreviewResult.ProposedActions)
                    ImGui.BulletText($"{proposal.Kind}: {proposal.Value}");
            }

            if (lastPreviewResult.PolicyReviews.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextUnformatted("Policy review");
                foreach (var review in lastPreviewResult.PolicyReviews)
                    ImGui.BulletText($"{review.Kind}: {(review.Allowed ? "ALLOW" : "BLOCK")} | {review.NormalizedValue} | {review.Reason}");
            }

            if (!string.IsNullOrWhiteSpace(lastPreviewResult.RawResponseText))
            {
                ImGui.Spacing();
                ImGui.TextUnformatted("Raw response");
                ImGui.TextWrapped(lastPreviewResult.RawResponseText);
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Concept");
        foreach (var item in PluginInfo.Concept)
            ImGui.BulletText(item);

        ImGui.Separator();
        ImGui.TextUnformatted("Planned services");
        foreach (var item in PluginInfo.Services)
            ImGui.BulletText(item);
    }

    private async Task RunHealthCheckAsync()
    {
        if (healthBusy)
            return;

        ResetOperationCts();
        healthBusy = true;
        try
        {
            lastHealthSnapshot = await plugin.OfflineModelHost.CheckHealthAsync(operationCts!.Token);
        }
        catch (Exception ex)
        {
            lastHealthSnapshot = new ProviderHealthSnapshot
            {
                ProviderName = plugin.OfflineModelHost.GetActiveProvider()?.DisplayName ?? "None",
                Status = "Unavailable",
                Detail = ex.Message,
                IsConfigured = false,
                IsReachable = false,
            };
        }
        finally
        {
            healthBusy = false;
        }
    }

    private async Task RunPreviewAsync(string conversationKey)
    {
        if (previewBusy)
            return;

        ResetOperationCts();
        previewBusy = true;
        try
        {
            lastPreviewResult = await plugin.OfflineModelHost.RunPreviewAsync(
                conversationKey,
                plugin.ConfigManager.GetActiveConfig(),
                previewPrompt,
                operationCts!.Token);
        }
        catch (Exception ex)
        {
            lastPreviewResult = new ChatEngineResult
            {
                Outcome = ChatEngineOutcome.Error,
                ProviderName = plugin.OfflineModelHost.GetActiveProvider()?.DisplayName ?? "None",
                Detail = ex.Message,
            };
        }
        finally
        {
            previewBusy = false;
        }
    }

    private void ResetOperationCts()
    {
        operationCts?.Cancel();
        operationCts?.Dispose();
        operationCts = new CancellationTokenSource();
    }
}
