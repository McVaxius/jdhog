namespace Jdhog.Models;

public sealed class CharacterConfig
{
    public bool Enabled { get; set; } = false;
    public string PrimaryMode { get; set; } = string.Empty;
    public string TargetNotes { get; set; } = string.Empty;
    public CharacterConfig Clone() => (CharacterConfig)MemberwiseClone();
}
