using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Jdhog.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly string[] DtrModes = { "Text only", "Icon + text", "Icon only" };
    private readonly Plugin plugin;
    public ConfigWindow(Plugin plugin) : base($"{PluginInfo.DisplayName} Settings##Config")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(620f, 460f), MaximumSize = new Vector2(1500f, 1300f) };
    }
    public void Dispose() { }
    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var enabled = cfg.PluginEnabled; if (ImGui.Checkbox("Plugin enabled", ref enabled)) { cfg.PluginEnabled = enabled; cfg.Save(); plugin.UpdateDtrBar(); }
        var dtr = cfg.DtrBarEnabled; if (ImGui.Checkbox("Show DTR bar entry", ref dtr)) { cfg.DtrBarEnabled = dtr; cfg.Save(); plugin.UpdateDtrBar(); }
        var mode = cfg.DtrBarMode; if (ImGui.Combo("DTR mode", ref mode, DtrModes, DtrModes.Length)) { cfg.DtrBarMode = mode; cfg.Save(); plugin.UpdateDtrBar(); }
        var onIcon = cfg.DtrIconEnabled; if (ImGui.InputText("DTR enabled glyph", ref onIcon, 8)) { cfg.DtrIconEnabled = onIcon.Length <= 3 ? onIcon : onIcon[..3]; cfg.Save(); plugin.UpdateDtrBar(); }
        var offIcon = cfg.DtrIconDisabled; if (ImGui.InputText("DTR disabled glyph", ref offIcon, 8)) { cfg.DtrIconDisabled = offIcon.Length <= 3 ? offIcon : offIcon[..3]; cfg.Save(); plugin.UpdateDtrBar(); }
        var account = plugin.ConfigManager.GetCurrentAccount();
        ImGui.Text($"Current account: {account?.AccountAlias ?? "(waiting for login)"}");
        var label = string.IsNullOrWhiteSpace(plugin.ConfigManager.SelectedCharacterKey) ? "(Account default)" : plugin.ConfigManager.SelectedCharacterKey;
        if (ImGui.BeginCombo("Character profile", label))
        {
            if (ImGui.Selectable("(Account default)", string.IsNullOrWhiteSpace(plugin.ConfigManager.SelectedCharacterKey))) plugin.ConfigManager.SelectedCharacterKey = string.Empty;
            foreach (var key in plugin.ConfigManager.GetSortedCharacterKeys()) if (ImGui.Selectable(key, key == plugin.ConfigManager.SelectedCharacterKey)) plugin.ConfigManager.SelectedCharacterKey = key;
            ImGui.EndCombo();
        }
        var pcfg = plugin.ConfigManager.GetActiveConfig(); var pEnabled = pcfg.Enabled; if (ImGui.Checkbox("Profile enabled", ref pEnabled)) { pcfg.Enabled = pEnabled; plugin.ConfigManager.SaveCurrentAccount(); }
        var primaryMode = pcfg.PrimaryMode; if (ImGui.InputText("Primary mode", ref primaryMode, 128)) { pcfg.PrimaryMode = primaryMode; plugin.ConfigManager.SaveCurrentAccount(); }
        var notes = pcfg.TargetNotes; if (ImGui.InputTextMultiline("Target notes", ref notes, 1024, new Vector2(-1f,90f))) { pcfg.TargetNotes = notes; plugin.ConfigManager.SaveCurrentAccount(); }
        ImGui.Separator(); ImGui.TextUnformatted("Rollout phases"); foreach (var x in PluginInfo.Phases) ImGui.BulletText(x);
        ImGui.Spacing(); ImGui.TextUnformatted("Concept recap"); foreach (var x in PluginInfo.Concept) ImGui.BulletText(x);
    }
}
