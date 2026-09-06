using System.Text.Json;

namespace DeckSoundboard;

public sealed class ConfigStore
{
    public string BaseDirectory { get; }
    public string ConfigPath { get; }
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public ConfigStore()
    {
        BaseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeckSoundboard");
        Directory.CreateDirectory(BaseDirectory);
        ConfigPath = Path.Combine(BaseDirectory, "settings.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), _json) ?? new AppConfig();
        }
        catch
        {
            try { File.Copy(ConfigPath, ConfigPath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, _json));
        File.Move(tmp, ConfigPath, true);
    }
}
