using System.Text.Json.Serialization;

namespace DeckSoundboard;

public sealed class AppConfig
{
    public bool PttEnabled { get; set; } = true;
    public string PttKey { get; set; } = "V";
    public int PreDelayMs { get; set; } = 75;
    public int PostDelayMs { get; set; } = 100;
    public int OutputDeviceNumber { get; set; } = -1;
    public string OutputDeviceName { get; set; } = "Default";
    public float MasterVolume { get; set; } = 1.0f;
    public bool StartMinimized { get; set; }
    public bool StartWithWindows { get; set; }
    public bool StopCurrentWhenNewStarts { get; set; } = true;
    public List<SoundClipConfig> Sounds { get; set; } = new();
    public List<D6Binding> Bindings { get; set; } = new();
}

public sealed class SoundClipConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Sound";
    public string FilePath { get; set; } = "";
    public float Volume { get; set; } = 1.0f;
    public bool Loop { get; set; }
    [JsonIgnore] public string Display => string.IsNullOrWhiteSpace(Name) ? Path.GetFileNameWithoutExtension(FilePath) : Name;
}

public sealed class D6Binding
{
    public string Context { get; set; } = "";
    public string SoundId { get; set; } = "";
    public string FriendlyName { get; set; } = "D6 Key";
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}
