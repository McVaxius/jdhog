using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Jdhog.Windows;
using Jdhog.Services;

namespace Jdhog;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public ConfigManager ConfigManager { get; }
    public ConversationStateService ConversationStateService { get; }
    public OutboundActionPolicy OutboundActionPolicy { get; }
    public OfflineModelHost OfflineModelHost { get; }
    public WindowSystem WindowSystem { get; } = new(PluginInfo.InternalName);
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private IDtrBarEntry? dtrEntry;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigManager = new ConfigManager(PluginInterface, Log);
        ConversationStateService = new ConversationStateService();
        OutboundActionPolicy = new OutboundActionPolicy();
        OfflineModelHost = new OfflineModelHost(Configuration, ConversationStateService, OutboundActionPolicy);
        if (!string.IsNullOrWhiteSpace(Configuration.LastAccountId)) ConfigManager.CurrentAccountId = Configuration.LastAccountId;
        ClientState.Login += OnLogin;
        mainWindow = new MainWindow(this);
        configWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configWindow);
        CommandManager.AddHandler(PluginInfo.Command, new CommandInfo(OnCommand) { HelpMessage = $"Open {PluginInfo.DisplayName}. Use {PluginInfo.Command} or {PluginInfo.AliasCommand}, plus {PluginInfo.Command} config for settings." });
        CommandManager.AddHandler(PluginInfo.AliasCommand, new CommandInfo(OnCommand) { HelpMessage = $"Alias for {PluginInfo.Command}." });
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;
        SetupDtrBar();
        UpdateDtrBar();
        Log.Information("[JabberDhog] Plugin loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        ClientState.Login -= OnLogin;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        CommandManager.RemoveHandler(PluginInfo.AliasCommand);
        CommandManager.RemoveHandler(PluginInfo.Command);
        WindowSystem.RemoveAllWindows();
        dtrEntry?.Remove();
        OfflineModelHost.Dispose();
    }

    public void ToggleMainUi() => mainWindow.Toggle();
    public void ToggleConfigUi() => configWindow.Toggle();
    public void PrintStatus(string m) => ChatGui.Print($"[{PluginInfo.DisplayName}] {m}");

    private void OnCommand(string command, string arguments)
    {
        var a = arguments.Trim(); if (a.Equals("config", StringComparison.OrdinalIgnoreCase)) { ToggleConfigUi(); return; } if (a.Equals("on", StringComparison.OrdinalIgnoreCase)) { Configuration.PluginEnabled = true; Configuration.Save(); UpdateDtrBar(); return; } if (a.Equals("off", StringComparison.OrdinalIgnoreCase)) { Configuration.PluginEnabled = false; Configuration.Save(); UpdateDtrBar(); return; } ToggleMainUi();
    }

    private void SetupDtrBar()
    {
        dtrEntry = DtrBar.Get(PluginInfo.DisplayName);
        dtrEntry.OnClick = _ => { Configuration.PluginEnabled = !Configuration.PluginEnabled; Configuration.Save(); UpdateDtrBar(); };
    }

    public void UpdateDtrBar()
    {
        if (dtrEntry == null) return; dtrEntry.Shown = Configuration.DtrBarEnabled; if (!Configuration.DtrBarEnabled) return; var g = Configuration.PluginEnabled ? Configuration.DtrIconEnabled : Configuration.DtrIconDisabled; var s = Configuration.PluginEnabled ? "On" : "Off"; dtrEntry.Text = Configuration.DtrBarMode switch { 1 => new SeString(new TextPayload($"{g} JD")), 2 => new SeString(new TextPayload(g)), _ => new SeString(new TextPayload("JD: " + s)), }; dtrEntry.Tooltip = new SeString(new TextPayload($"{PluginInfo.DisplayName} {s}. Click to toggle."));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        UpdateDtrBar();
        if (ClientState.IsLoggedIn && ObjectTable.LocalPlayer != null)
        {
            var p = ObjectTable.LocalPlayer;
            ConfigManager.EnsureAccountSelected(PlayerState.ContentId, p.Name.ToString());
            ConfigManager.EnsureCharacterExists(p.Name.ToString(), p.HomeWorld.Value.Name.ToString());
            Configuration.LastAccountId = ConfigManager.CurrentAccountId;
            Configuration.Save();
        }
    }

    private void OnLogin() => UpdateDtrBar();
}
