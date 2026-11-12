using System;
using System.Drawing;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace RoomLink;

/// <summary>
/// Tray application shell: owns the bridges, the tray icon and its menu,
/// and the device-picker window.
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    private readonly Settings _settings;
    private readonly NotifyIcon _tray;
    private DevicePickerForm? _form;

    private Bridge? _bridgeA;   // headset mic → local speakers  ("remote → you")
    private Bridge? _bridgeB;   // local mic → headset speaker  ("you → remote")

    public bool IsRunning { get; private set; }
    public Settings Settings => _settings;

    public event EventHandler? StateChanged;

    public TrayContext()
    {
        _settings = Settings.Load();
        _tray = new NotifyIcon
        {
            Icon = IconFactory.Create(false),
            Text = "RoomLink — stopped",
            Visible = true,
        };
        BuildMenu();

        if (_settings.IsComplete && _settings.AutoStart)
        {
            try { Start(); }
            catch (Exception ex) { ShowError("Auto-start failed", ex); ShowConfig(); }
        }
    }

    // ------------------------------------------------------------------ menu

    private void BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("&Open RoomLink", null, (_, _) => ShowConfig());
        menu.Items.Add(new ToolStripSeparator());

        var startItem = new ToolStripMenuItem("&Start bridge");
        startItem.Click += (_, _) => Start();
        var stopItem = new ToolStripMenuItem("S&top bridge");
        stopItem.Click += (_, _) => Stop();

        var autoItem = new ToolStripMenuItem("Start bridge when RoomLink launches")
        { Checked = _settings.AutoStart, CheckOnClick = true };
        autoItem.Click += (_, _) =>
        {
            _settings.AutoStart = autoItem.Checked;
            _settings.Save();
        };

        menu.Items.AddRange(new ToolStripItem[] { startItem, stopItem, new ToolStripSeparator(), autoItem, new ToolStripSeparator() });
        menu.Items.Add("E&xit", null, (_, _) => ExitApp());

        // Enable/disable items depending on state
        EventHandler refresh = (_, _) =>
        {
            startItem.Enabled = !IsRunning;
            stopItem.Enabled = IsRunning;
            autoItem.Checked = _settings.AutoStart;
        };
        StateChanged += refresh;
        refresh(null, EventArgs.Empty);

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowConfig();
    }

    // ---------------------------------------------------------------- bridge

    public void Start()
    {
        if (IsRunning) return;
        if (!_settings.IsComplete)
            throw new InvalidOperationException("Some devices are not selected.");

        _bridgeA = new Bridge("remote → you", _settings.HeadsetMicId!, _settings.LocalSpeakerId!)
        { Gain = _settings.HeadsetToSpeakersGain };
        _bridgeB = new Bridge("you → remote", _settings.LocalMicId!, _settings.HeadsetSpeakerId!)
        { Gain = _settings.MicToHeadsetGain };

        try
        {
            _bridgeA.Start();
            _bridgeB.Start();
        }
        catch
        {
            _bridgeA.Dispose(); _bridgeA = null;
            _bridgeB.Dispose(); _bridgeB = null;
            throw;
        }

        IsRunning = true;
        var oldIcon = _tray.Icon;
        _tray.Icon = IconFactory.Create(true);
        oldIcon?.Dispose();
        _tray.Text = "RoomLink — bridging";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _bridgeA?.Dispose(); _bridgeA = null;
        _bridgeB?.Dispose(); _bridgeB = null;
        IsRunning = false;
        var oldIcon = _tray.Icon;
        _tray.Icon = IconFactory.Create(false);
        oldIcon?.Dispose();
        _tray.Text = "RoomLink — stopped";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetGains(float headsetToSpeakers, float micToHeadset)
    {
        _bridgeA?.SetGain(headsetToSpeakers);
        _bridgeB?.SetGain(micToHeadset);
        _settings.HeadsetToSpeakersGain = headsetToSpeakers;
        _settings.MicToHeadsetGain = micToHeadset;
    }

    // ------------------------------------------------------------------ ui

    public void ShowConfig()
    {
        if (_form == null || _form.IsDisposed)
        {
            _form = new DevicePickerForm(this);
            _form.Show();
        }
        else
        {
            _form.Show();
            _form.Activate();
        }
    }

    private void ShowError(string title, Exception ex)
    {
        MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ExitApp()
    {
        Stop();
        _form?.Close();
        _tray.Visible = false;
        _tray.Icon?.Dispose();
        Application.Exit();
    }
}
