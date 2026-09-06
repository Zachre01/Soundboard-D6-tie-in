using System.Diagnostics;

namespace DeckSoundboard;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, "DeckSoundboard.Singleton", out var first);
        if (!first) { MessageBox.Show("DeckSoundboard is already running in the system tray."); return; }

        var store = new ConfigStore(); var config = store.Load(); using var audio = new AudioEngine(); var ptt = new PttController(); using var controller = new SoundboardController(config, audio, ptt); using var server = new D6Server(config, store, controller);
        try { server.Start(); }
        catch (Exception ex) { MessageBox.Show("Could not start the local D6 bridge on port 42761.\n\n" + ex.Message, "DeckSoundboard", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

        using var form = new MainForm(config, store, audio, controller, server);
        var tray = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)) || config.StartMinimized;
        if (tray) { form.Load += (_, _) => form.Hide(); }
        try
        {
            Application.Run(form);
        }
        finally
        {
            // Make a real exit deterministic: release audio/PTT and stop any
            // DeckSoundboard plugin helper that FIFINE launched.
            try { controller.EmergencyStopAsync().GetAwaiter().GetResult(); } catch { }
            try
            {
                foreach (var proc in Process.GetProcessesByName("DeckSoundboard.D6Plugin"))
                {
                    try { proc.Kill(true); proc.WaitForExit(1500); } catch { }
                    finally { proc.Dispose(); }
                }
            }
            catch { }
        }
    }
}
