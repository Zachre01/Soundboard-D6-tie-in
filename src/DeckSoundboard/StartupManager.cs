using Microsoft.Win32;

namespace DeckSoundboard;

public static class StartupManager
{
    private const string Name = "DeckSoundboard";
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)!;
        if (enabled) key.SetValue(Name, $"\"{Application.ExecutablePath}\" --tray"); else key.DeleteValue(Name, false);
    }
}
