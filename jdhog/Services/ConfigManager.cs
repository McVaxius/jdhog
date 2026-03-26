using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Jdhog.Models;

namespace Jdhog.Services;

public sealed class ConfigManager
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly string configDirectory;
    private readonly Dictionary<string, AccountConfig> accounts = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public ConfigManager(IDalamudPluginInterface pluginInterface, IPluginLog log) { this.pluginInterface = pluginInterface; this.log = log; configDirectory = pluginInterface.GetPluginConfigDirectory(); Directory.CreateDirectory(configDirectory); LoadAllAccounts(); }
    public string CurrentAccountId { get; set; } = string.Empty;
    public string SelectedCharacterKey { get; set; } = string.Empty;
    public AccountConfig? GetCurrentAccount() => string.IsNullOrWhiteSpace(CurrentAccountId) ? null : accounts.GetValueOrDefault(CurrentAccountId);
    public CharacterConfig GetActiveConfig() { var a = GetCurrentAccount(); if (a == null) return new CharacterConfig(); if (string.IsNullOrWhiteSpace(SelectedCharacterKey)) return a.DefaultConfig; return a.Characters.TryGetValue(SelectedCharacterKey, out var c) ? c : a.DefaultConfig; }
    public IEnumerable<string> GetSortedCharacterKeys() => GetCurrentAccount()?.Characters.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase) ?? Enumerable.Empty<string>();
    public void EnsureAccountSelected(ulong contentId, string? aliasHint = null) { var id = contentId == 0 ? Guid.NewGuid().ToString("N")[..8] : contentId.ToString("X"); if (!accounts.ContainsKey(id)) accounts[id] = new AccountConfig { AccountId = id, AccountAlias = string.IsNullOrWhiteSpace(aliasHint) ? "Account" : aliasHint }; CurrentAccountId = id; SaveCurrentAccount(); }
    public void EnsureCharacterExists(string name, string world) { var a = GetCurrentAccount(); if (a == null || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world)) return; var key = $"{name}@{world}"; if (!a.Characters.ContainsKey(key)) a.Characters[key] = a.DefaultConfig.Clone(); SelectedCharacterKey = key; SaveCurrentAccount(); }
    public void SaveCurrentAccount() { if (!string.IsNullOrWhiteSpace(CurrentAccountId)) SaveAccount(CurrentAccountId); }
    private void LoadAllAccounts() { try { foreach (var p in Directory.GetFiles(configDirectory, "*_jdhog.json")) { var a = JsonSerializer.Deserialize<AccountConfig>(File.ReadAllText(p), JsonOptions); if (a != null && !string.IsNullOrWhiteSpace(a.AccountId)) accounts[a.AccountId] = a; } } catch (Exception ex) { log.Error(ex, "[Jabberdhoggy] Failed to load account configs."); } }
    private void SaveAccount(string id) { if (!accounts.TryGetValue(id, out var a)) return; try { File.WriteAllText(Path.Combine(configDirectory, $"{id}_jdhog.json"), JsonSerializer.Serialize(a, JsonOptions)); } catch (Exception ex) { log.Error(ex, "[Jabberdhoggy] Failed to save account config."); } }
}
