using NAudio.Wave;

namespace DeckSoundboard;

public sealed class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly ConfigStore _store;
    private readonly AudioEngine _audio;
    private readonly SoundboardController _controller;
    private readonly D6Server _server;
    private readonly NotifyIcon _tray;
    private readonly DataGridView _sounds = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly DataGridView _bindings = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
    private readonly ComboBox _output = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly CheckBox _pttEnabled = new() { Text = "Enable push-to-talk", AutoSize = true };
    private readonly TextBox _ptt = new() { Width = 100 };
    private readonly NumericUpDown _pre = new() { Minimum = 0, Maximum = 5000, Width = 90 };
    private readonly NumericUpDown _post = new() { Minimum = 0, Maximum = 5000, Width = 90 };
    private readonly TrackBar _master = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 220 };
    private readonly CheckBox _startup = new() { Text = "Start with Windows" };
    private readonly CheckBox _startMin = new() { Text = "Start minimized to tray" };
    private readonly Label _status = new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
    private string? _latestContext;

    public MainForm(AppConfig config, ConfigStore store, AudioEngine audio, SoundboardController controller, D6Server server)
    {
        _config = config; _store = store; _audio = audio; _controller = controller; _server = server;
        Text = "DeckSoundboard"; Width = 980; Height = 650; MinimumSize = new Size(850, 550); StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnClosing;

        _tray = new NotifyIcon { Text = "DeckSoundboard", Visible = true, Icon = SystemIcons.Application };
        _tray.DoubleClick += (_, _) => ShowFromTray();
        _tray.ContextMenuStrip = new ContextMenuStrip();
        _tray.ContextMenuStrip.Items.Add("Open", null, (_, _) => ShowFromTray());
        _tray.ContextMenuStrip.Items.Add("Emergency stop / release PTT", null, async (_, _) => await _controller.EmergencyStopAsync());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => { _allowExit = true; Close(); });

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildSoundsTab()); tabs.TabPages.Add(BuildBindingsTab()); tabs.TabPages.Add(BuildSettingsTab()); tabs.TabPages.Add(BuildStatusTab());
        Controls.Add(tabs);

        _server.ContextSeen += context => BeginInvoke(() => { _latestContext = context; RefreshBindings(); tabs.SelectedIndex = 1; });
        _controller.StateChanged += () => BeginInvoke(UpdateStatus);
        PopulateSettings(); RefreshSounds(); RefreshBindings(); UpdateStatus();
    }

    private TabPage BuildSoundsTab()
    {
        var tab = new TabPage("Sounds");
        _sounds.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 220 });
        _sounds.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Audio file", DataPropertyName = "FilePath", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _sounds.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Volume", DataPropertyName = "VolumeText", Width = 80 });
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        bar.Controls.Add(MakeButton("Add sound", AddSound)); bar.Controls.Add(MakeButton("Edit", EditSound)); bar.Controls.Add(MakeButton("Remove", RemoveSound)); bar.Controls.Add(MakeButton("Test play", TestSound)); bar.Controls.Add(MakeButton("Stop", async (_, _) => await _controller.EmergencyStopAsync()));
        tab.Controls.Add(_sounds); tab.Controls.Add(bar); return tab;
    }

    private TabPage BuildBindingsTab()
    {
        var tab = new TabPage("D6 Buttons");
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Button", DataPropertyName = "FriendlyName", Width = 180 });
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Assigned sound", DataPropertyName = "SoundName", Width = 260 });
        _bindings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "D6 context", DataPropertyName = "Context", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        var info = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), Text = "Drag the DeckSoundboard 'Sound Clip' action onto a D6 key, then press that key once. It will appear here. Select it and click Assign sound." };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        bar.Controls.Add(MakeButton("Assign sound", AssignSound)); bar.Controls.Add(MakeButton("Rename button", RenameBinding)); bar.Controls.Add(MakeButton("Unassign", UnassignBinding)); bar.Controls.Add(MakeButton("Forget button", ForgetBinding));
        tab.Controls.Add(_bindings); tab.Controls.Add(bar); tab.Controls.Add(info); return tab;
    }

    private TabPage BuildSettingsTab()
    {
        var tab = new TabPage("Settings"); var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(14), AutoScroll = true };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(panel, "Push-to-talk", _pttEnabled); AddRow(panel, "Push-to-talk key", _ptt); AddRow(panel, "Audio output", _output); AddRow(panel, "PTT lead-in (ms)", _pre); AddRow(panel, "PTT tail (ms)", _post); AddRow(panel, "Master volume", _master); AddRow(panel, "Startup", _startup); AddRow(panel, "Window", _startMin);
        var help = new Label { AutoSize = true, MaximumSize = new Size(700, 0), Text = "PTT examples: V, F8, Space, Mouse4/XButton1, Mouse5/XButton2. For the first test, use the exact single key your game uses for push-to-talk." };
        AddRow(panel, "PTT formats", help);
        var save = MakeButton("Save settings", (_, _) => SaveSettings()); var emergency = MakeButton("EMERGENCY STOP + RELEASE PTT", async (_, _) => await _controller.EmergencyStopAsync());
        var buttons = new FlowLayoutPanel { AutoSize = true }; buttons.Controls.Add(save); buttons.Controls.Add(emergency); AddRow(panel, "", buttons);
        tab.Controls.Add(panel); return tab;
    }

    private TabPage BuildStatusTab()
    {
        var tab = new TabPage("Status"); var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(18), AutoScroll = true };
        p.Controls.Add(_status); p.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(800, 0), Text = "Local D6 bridge: http://127.0.0.1:42761\nSettings are automatically stored in %APPDATA%\\DeckSoundboard\\settings.json\n\nIf a sound ever stops unexpectedly or a game keeps transmitting, use Emergency Stop to force-release PTT." });
        tab.Controls.Add(p); return tab;
    }

    private static Button MakeButton(string text, EventHandler handler) { var b = new Button { Text = text, AutoSize = true, Margin = new Padding(4) }; b.Click += handler; return b; }
    private static void AddRow(TableLayoutPanel p, string label, Control c) { p.RowStyles.Add(new RowStyle(SizeType.AutoSize)); p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 9, 12, 4) }); p.Controls.Add(c); }

    private void PopulateSettings()
    {
        _pttEnabled.Checked = _config.PttEnabled; _ptt.Text = _config.PttKey; _pre.Value = _config.PreDelayMs; _post.Value = _config.PostDelayMs; _master.Value = (int)(_config.MasterVolume * 100); _startup.Checked = _config.StartWithWindows; _startMin.Checked = _config.StartMinimized;
        _output.Items.Clear(); foreach (var d in _audio.GetOutputDevices()) _output.Items.Add(new DeviceItem(d.Number, d.Name));
        var match = _output.Items.Cast<DeviceItem>().FirstOrDefault(x => x.Number == _config.OutputDeviceNumber) ?? _output.Items.Cast<DeviceItem>().First(); _output.SelectedItem = match;
    }

    private void SaveSettings()
    {
        _config.PttEnabled = _pttEnabled.Checked; _config.PttKey = _ptt.Text.Trim(); _config.PreDelayMs = (int)_pre.Value; _config.PostDelayMs = (int)_post.Value; _config.MasterVolume = _master.Value / 100f; _config.StartWithWindows = _startup.Checked; _config.StartMinimized = _startMin.Checked;
        if (_output.SelectedItem is DeviceItem d) { _config.OutputDeviceNumber = d.Number; _config.OutputDeviceName = d.Name; }
        _store.Save(_config); try { StartupManager.SetEnabled(_config.StartWithWindows); } catch (Exception ex) { MessageBox.Show("Could not update Windows startup setting:\n" + ex.Message); }
        MessageBox.Show("Settings saved.", "DeckSoundboard");
    }

    private void AddSound(object? s, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Audio files|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aac|All files|*.*", Multiselect = false };
        if (ofd.ShowDialog() != DialogResult.OK) return;
        var clip = new SoundClipConfig { Name = Path.GetFileNameWithoutExtension(ofd.FileName), FilePath = ofd.FileName, Volume = 1f };
        _config.Sounds.Add(clip); _store.Save(_config); RefreshSounds();
    }

    private SoundClipConfig? SelectedSound() => _sounds.SelectedRows.Count == 0 ? null : _sounds.SelectedRows[0].Tag as SoundClipConfig;
    private D6Binding? SelectedBinding() => _bindings.SelectedRows.Count == 0 ? null : _bindings.SelectedRows[0].Tag as D6Binding;

    private void EditSound(object? s, EventArgs e)
    {
        var clip = SelectedSound(); if (clip is null) return;
        using var form = new Form { Text = "Edit sound", Width = 520, Height = 240, StartPosition = FormStartPosition.CenterParent };
        var name = new TextBox { Text = clip.Name, Width = 320 }; var file = new TextBox { Text = clip.FilePath, Width = 320 }; var vol = new NumericUpDown { Minimum = 0, Maximum = 100, Value = (decimal)(clip.Volume * 100), Width = 80 };
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoSize = true }; AddRow(t, "Name", name); AddRow(t, "File", file); AddRow(t, "Volume %", vol);
        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true }; AddRow(t, "", ok); form.Controls.Add(t); form.AcceptButton = ok;
        if (form.ShowDialog(this) == DialogResult.OK) { clip.Name = name.Text.Trim(); clip.FilePath = file.Text.Trim(); clip.Volume = (float)vol.Value / 100f; _store.Save(_config); RefreshSounds(); RefreshBindings(); }
    }

    private void RemoveSound(object? s, EventArgs e)
    {
        var clip = SelectedSound(); if (clip is null) return;
        if (MessageBox.Show($"Remove '{clip.Display}'?", "DeckSoundboard", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        _config.Sounds.Remove(clip); foreach (var b in _config.Bindings.Where(b => b.SoundId == clip.Id)) b.SoundId = ""; _store.Save(_config); RefreshSounds(); RefreshBindings();
    }

    private async void TestSound(object? s, EventArgs e) { var clip = SelectedSound(); if (clip is not null) await _controller.ToggleAsync(clip.Id); }

    private void AssignSound(object? s, EventArgs e)
    {
        var b = SelectedBinding(); if (b is null) return; if (_config.Sounds.Count == 0) { MessageBox.Show("Add at least one sound first."); return; }
        using var chooser = new Form { Text = "Assign sound", Width = 420, Height = 160, StartPosition = FormStartPosition.CenterParent };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 }; foreach (var sound in _config.Sounds) combo.Items.Add(new SoundItem(sound)); combo.SelectedIndex = 0;
        var ok = new Button { Text = "Assign", DialogResult = DialogResult.OK }; var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) }; p.Controls.Add(combo); p.Controls.Add(ok); chooser.Controls.Add(p); chooser.AcceptButton = ok;
        if (chooser.ShowDialog(this) == DialogResult.OK && combo.SelectedItem is SoundItem item) { b.SoundId = item.Sound.Id; _store.Save(_config); RefreshBindings(); }
    }

    private void RenameBinding(object? s, EventArgs e)
    {
        var b = SelectedBinding(); if (b is null) return; var value = Microsoft.VisualBasic.Interaction.InputBox("Button name:", "Rename D6 button", b.FriendlyName); if (string.IsNullOrWhiteSpace(value)) return; b.FriendlyName = value.Trim(); _store.Save(_config); RefreshBindings();
    }
    private void UnassignBinding(object? s, EventArgs e) { var b = SelectedBinding(); if (b is null) return; b.SoundId = ""; _store.Save(_config); RefreshBindings(); }
    private void ForgetBinding(object? s, EventArgs e) { var b = SelectedBinding(); if (b is null) return; _config.Bindings.Remove(b); _store.Save(_config); RefreshBindings(); }

    private void RefreshSounds()
    {
        _sounds.Rows.Clear(); foreach (var clip in _config.Sounds) { var row = _sounds.Rows[_sounds.Rows.Add(clip.Display, clip.FilePath, $"{clip.Volume * 100:0}%")]; row.Tag = clip; }
    }
    private void RefreshBindings()
    {
        _bindings.Rows.Clear(); foreach (var b in _config.Bindings.OrderByDescending(x => x.Context == _latestContext).ThenBy(x => x.FriendlyName)) { var sound = _config.Sounds.FirstOrDefault(s => s.Id == b.SoundId)?.Display ?? "(unassigned)"; var row = _bindings.Rows[_bindings.Rows.Add(b.FriendlyName + (b.Context == _latestContext ? "  ← last pressed" : ""), sound, b.Context)]; row.Tag = b; if (b.Context == _latestContext) row.Selected = true; }
    }
    private void UpdateStatus() { var playing = _config.Sounds.FirstOrDefault(s => s.Id == _controller.CurrentSoundId)?.Display ?? "Nothing"; var ptt = !_config.PttEnabled ? "DISABLED" : (_controller.PttHeld ? $"HELD ({_config.PttKey})" : $"Released ({_config.PttKey})"); _status.Text = $"D6 bridge: RUNNING\nPTT: {ptt}\nPlaying: {playing}"; }

    private bool _allowExit;
    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        // Closing the window now means EXIT. Start-minimized still works, but
        // once the user clicks X/Exit we tear down playback/PTT/server state.
        _allowExit = true;
        _tray.Visible = false;
        try { _controller.EmergencyStopAsync().GetAwaiter().GetResult(); } catch { }
    }
    private void ShowFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private sealed record DeviceItem(int Number, string Name) { public override string ToString() => Name; }
    private sealed record SoundItem(SoundClipConfig Sound) { public override string ToString() => Sound.Display; }
}
