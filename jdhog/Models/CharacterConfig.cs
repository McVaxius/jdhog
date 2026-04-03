using System.Collections.Generic;

namespace Jdhog.Models;

public sealed class CharacterConfig
{
    public bool Enabled { get; set; } = false;
    public string PrimaryMode { get; set; } = string.Empty;
    public string TargetNotes { get; set; } = string.Empty;
    public int ConversationTurnLimit { get; set; } = 10;
    public bool AllowModelCommandSuggestions { get; set; } = true;
    public bool AllowModelEmoteSuggestions { get; set; } = true;
    public List<string> AllowedEmotes { get; set; } = new();
    public List<string> AllowedCommands { get; set; } = new();

    public CharacterConfig Clone()
        => new()
        {
            Enabled = Enabled,
            PrimaryMode = PrimaryMode,
            TargetNotes = TargetNotes,
            ConversationTurnLimit = ConversationTurnLimit,
            AllowModelCommandSuggestions = AllowModelCommandSuggestions,
            AllowModelEmoteSuggestions = AllowModelEmoteSuggestions,
            AllowedEmotes = new List<string>(AllowedEmotes),
            AllowedCommands = new List<string>(AllowedCommands),
        };
}
