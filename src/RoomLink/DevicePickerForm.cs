using System;
using System.Drawing;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace RoomLink;

/// <summary>
/// Device picker / control window.
/// Top group: the remote person's Bluetooth headset (mic first, then speaker).
/// Bottom group: this PC (mic first, then speaker).
/// Closing the window hides it to the tray; the app lives in the tray until "Exit".
/// </summary>
public sealed class DevicePickerForm : Form
{
    private readonly TrayContext _app;

    private readonly ComboBox _headsetMic = new();
    private readonly ComboBox _headsetSpk = new();
    private readonly ComboBox _localMic = new();
    private readonly ComboBox _localSpk = new();
    private readonly TrackBar _gainRemote = new();
    private readonly TrackBar _gainLocal = new();
    private readonly Button _toggle = new();
    private readonly Label _status = new();
    private readonly System.Windows.Forms.Timer _meterTimer = new();

    public DevicePickerForm(TrayContext app)
    {
        _app = app;

        Text = "RoomLink";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(524, 476);

        // ------------------------------------------------ REMOTE group ----
        var grpRemote = new GroupBox
        {
            Text = "Remote person — Bluetooth headset",
            Location = new Point(12, 10),
            Size = new Size(500, 134),
        };
        grpRemote.Controls.Add(MakeLabel("Microphone  (their voice comes in)", 14, 22));
        _headsetMic = MakeCombo(DataFlow.Capture, new Point(14, 42));
        grpRemote.Controls.Add(MakeLabel("Speaker  (their earpiece)", 14, 74));
        _headsetSpk = MakeCombo(DataFlow.Render, new Point(14, 94));
        grpRemote.Controls.AddRange(new Control[] { _headsetMic, _headsetSpk });

        // ------------------------------------------------- LOCAL group ----
        var grpLocal = new GroupBox
        {
            Text = "This PC — you",
            Location = new Point(12, 148),
            Size = new Size(500, 134),
        };
        grpLocal.Controls.Add(MakeLabel("Microphone  (what they hear)", 14, 22));
        _localMic = MakeCombo(DataFlow.Capture, new Point(14, 42));
        grpLocal.Controls.Add(MakeLabel("Speakers  (where you hear them)", 14, 74));
        _localSpk = MakeCombo(DataFlow.Render, new Point(14, 94));
        grpLocal.Controls.AddRange(new Control[] { _localMic, _localSpk });

        // ------------------------------------------------------ gains ----
        var lblGainRemote = MakeLabel("Remote → You volume", 16, 288);
        _gainRemote.Minimum = 0;
        _gainRemote.Maximum = 100;
        _gainRemote.TickFrequency = 10;
        _gainRemote.Width = 320;
        _gainRemote.Location = new Point(16, 308);
        _gainRemote.Value = (int)Math.Round(_app.Settings.HeadsetToSpeakersGain * 100);

        var lblGainLocal = MakeLabel("You → Remote volume", 16, 336);
        _gainLocal.Minimum = 0;
        _gainLocal.Maximum = 100;
        _gainLocal.TickFrequency = 10;
        _gainLocal.Width = 320;
        _gainLocal.Location = new Point(16, 356);
        _gainLocal.Value = (int)Math.Round(_app.Settings.MicToHeadsetGain * 100);

        // ---------------------------------------------------- buttons ----
        _toggle.Size = new Size(150, 34);
        _toggle.Location = new Point(16, 392);
        _toggle.Click += (_, _) => Toggle();

        var btnRefresh = new Button
        {
            Text = "Refresh devices",
            Size = new Size(150, 34),
            Location = new Point(180, 392),
        };
        btnRefresh.Click += (_, _) => RefreshDeviceLists();

        // ----------------------------------------------------- status ----
        _status.AutoSize = true;
        _status.Location = new Point(16, 434);
        _status.ForeColor = Color.DimGray;

        Controls.AddRange(new Control[]
        {
            grpRemote, grpLocal,
            lblGainRemote, lblGainLocal, _gainRemote, _gainLocal,
            _toggle, btnRefresh, _status,
        });

        RefreshDeviceLists();
        UpdateControls();

        // Save device selection as soon as the user changes a dropdown.
        foreach (var box in new[] { _headsetMic, _headsetSpk, _localMic, _localSpk })
            box.SelectedIndexChanged += (_, _) => SaveSelection();

        // Live status meter while running
        _meterTimer.Interval = 1000;
        _meterTimer.Tick += (_, _) => UpdateStatus();
        _meterTimer.Start();

        _app.StateChanged += (_, _) => UpdateControls();

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing && !_app.IsExiting)
            {
                // Closing the window stops the bridge, then hides to tray.
                e.Cancel = true;
                _app.Stop();
                Hide();
            }
        };
    }

    // -------------------------------------------------------------- helpers

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text = text,
        AutoSize = true,
        Location = new Point(x, y),
    };

    private static ComboBox MakeCombo(DataFlow flow, Point location) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 466,
        Location = location,
        Tag = flow,
    };

    private void RefreshDeviceLists()
    {
        var s = _app.Settings;

        // Fresh list per combo — sharing one List instance makes WinForms
        // sync the selected item across all combos bound to it.
        Fill(_headsetMic, DeviceService.GetCaptureDevices(), s.HeadsetMicId);
        Fill(_localMic, DeviceService.GetCaptureDevices(), s.LocalMicId);
        Fill(_headsetSpk, DeviceService.GetRenderDevices(), s.HeadsetSpeakerId);
        Fill(_localSpk, DeviceService.GetRenderDevices(), s.LocalSpeakerId);
    }

    private static void Fill(ComboBox box, System.Collections.Generic.List<AudioDeviceInfo> devices, string? selectedId)
    {
        box.DataSource = devices;
        box.DisplayMember = nameof(AudioDeviceInfo.DisplayName);
        var idx = devices.FindIndex(d => d.Id == selectedId);
        box.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void SaveSelection()
    {
        var s = _app.Settings;
        if (_headsetMic.SelectedItem is AudioDeviceInfo d1) s.HeadsetMicId = d1.Id;
        if (_headsetSpk.SelectedItem is AudioDeviceInfo d2) s.HeadsetSpeakerId = d2.Id;
        if (_localMic.SelectedItem is AudioDeviceInfo d3) s.LocalMicId = d3.Id;
        if (_localSpk.SelectedItem is AudioDeviceInfo d4) s.LocalSpeakerId = d4.Id;
        s.Save();
    }

    // ------------------------------------------------------------- actions

    private void Toggle()
    {
        try
        {
            if (!_app.IsRunning) StartBridge();
            else _app.Stop();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "RoomLink", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        UpdateControls();
    }

    private void StartBridge()
    {
        // Guard: virtual loopback devices are silent destinations/sources.
        var picks = new (string role, AudioDeviceInfo? dev)[]
        {
            ("Remote microphone", _headsetMic.SelectedItem as AudioDeviceInfo),
            ("Remote speaker", _headsetSpk.SelectedItem as AudioDeviceInfo),
            ("Local microphone", _localMic.SelectedItem as AudioDeviceInfo),
            ("Local speaker", _localSpk.SelectedItem as AudioDeviceInfo),
        };
        var virtuals = picks.Where(p => p.dev?.IsVirtual == true).ToList();
        if (virtuals.Count > 0)
        {
            var names = string.Join("\n", virtuals.Select(v => $"• {v.role}: {v.dev!.Name}"));
            var go = MessageBox.Show(this,
                "These are virtual loopback devices — audio sent to/coming from them is NOT audible:\n\n" +
                names + "\n\nStart anyway?",
                "Virtual device selected", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (go != DialogResult.OK) return;
        }

        SaveSelection();
        _app.SetGains(_gainRemote.Value / 100f, _gainLocal.Value / 100f);
        _app.Start();
    }

    private void UpdateControls()
    {
        _toggle.Text = _app.IsRunning ? "Stop bridge" : "Start bridge";
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_app.IsRunning)
        {
            var a = _app.BridgeRemoteToLocal;   // headset mic → speakers
            var b = _app.BridgeLocalToRemote;   // your mic → headset

            _status.ForeColor = Color.ForestGreen;
            _status.Text =
                $"● Bridging   |   in-buffer {a?.BufferedMilliseconds ?? 0} ms, out-buffer {b?.BufferedMilliseconds ?? 0} ms" +
                $"   |   levels: headset-mic {a?.LevelPercent ?? 0}%, your-mic {b?.LevelPercent ?? 0}%";
        }
        else
        {
            _status.ForeColor = Color.DimGray;
            _status.Text = "○ Stopped";
        }
    }
}
