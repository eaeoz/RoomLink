using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace RoomLink;

/// <summary>
/// Device picker / control window. Closing the window hides it to the tray;
/// the app lives in the tray until "Exit".
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
        ClientSize = new Size(520, 470);

        var label1 = AddLabel("Remote headset — MICROPHONE (their voice comes in)", 16, 16);
        BindCapture(_headsetMic, 16, 40);
        var label2 = AddLabel("Local speakers — where you hear them", 16, 76);
        BindRender(_localSpk, 16, 100);
        var label3 = AddLabel("Local microphone — what they hear from you", 16, 136);
        BindCapture(_localMic, 16, 160);
        var label4 = AddLabel("Remote headset — SPEAKER (their earpiece)", 16, 196);
        BindRender(_headsetSpk, 16, 220);

        // Gains
        var label5 = AddLabel("Remote → You volume", 16, 258);
        _gainRemote.Minimum = 0; _gainRemote.Maximum = 100;
        _gainRemote.TickFrequency = 10; _gainRemote.Width = 320;
        _gainRemote.Left = 16; _gainRemote.Top = 282;
        _gainRemote.Value = (int)Math.Round(_app.Settings.HeadsetToSpeakersGain * 100);

        var label6 = AddLabel("You → Remote volume", 16, 318);
        _gainLocal.Minimum = 0; _gainLocal.Maximum = 100;
        _gainLocal.TickFrequency = 10; _gainLocal.Width = 320;
        _gainLocal.Left = 16; _gainLocal.Top = 342;
        _gainLocal.Value = (int)Math.Round(_app.Settings.MicToHeadsetGain * 100);

        // Buttons
        _toggle.Size = new Size(150, 34);
        _toggle.Location = new Point(16, 384);
        _toggle.Click += (_, _) => Toggle();

        var btnRefresh = new Button
        {
            Text = "Refresh devices",
            Size = new Size(150, 34),
            Location = new Point(180, 384),
        };
        btnRefresh.Click += (_, _) => RefreshDeviceLists();

        // Status
        _status.AutoSize = true;
        _status.Location = new Point(16, 426);
        _status.ForeColor = Color.DimGray;

        Controls.AddRange(new Control[]
        {
            label1, label2, label3, label4, label5, label6,
            _headsetMic, _localSpk, _localMic, _headsetSpk,
            _gainRemote, _gainLocal,
            _toggle, btnRefresh, _status,
        });

        RefreshDeviceLists();
        UpdateControls();

        // Live status meter while running
        _meterTimer.Interval = 1000;
        _meterTimer.Tick += (_, _) => UpdateStatus();
        _meterTimer.Start();

        _app.StateChanged += (_, _) => UpdateControls();

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Hide to tray instead of exiting; app keeps bridging.
                e.Cancel = true;
                Hide();
            }
        };
    }

    // -------------------------------------------------------------- helpers

    private Label AddLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
        };
    }

    private void BindCapture(ComboBox box, int x, int y)
    {
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.Width = 488;
        box.Location = new Point(x, y);
        box.Tag = DataFlow.Capture;
    }

    private void BindRender(ComboBox box, int x, int y)
    {
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        box.Width = 488;
        box.Location = new Point(x, y);
        box.Tag = DataFlow.Render;
    }

    private void RefreshDeviceLists()
    {
        var capture = DeviceService.GetCaptureDevices();
        var render = DeviceService.GetRenderDevices();
        var s = _app.Settings;

        Fill(_headsetMic, capture, s.HeadsetMicId);
        Fill(_headsetSpk, render, s.HeadsetSpeakerId);
        Fill(_localMic, capture, s.LocalMicId);
        Fill(_localSpk, render, s.LocalSpeakerId);
    }

    private static void Fill(ComboBox box, System.Collections.Generic.List<AudioDeviceInfo> devices, string? selectedId)
    {
        box.DataSource = devices;
        box.DisplayMember = nameof(AudioDeviceInfo.Name);
        var idx = devices.FindIndex(d => d.Id == selectedId);
        box.SelectedIndex = idx >= 0 ? idx : 0;
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
        // Read selections into settings, then start.
        var s = _app.Settings;
        s.HeadsetMicId = ((AudioDeviceInfo)_headsetMic.SelectedItem!).Id;
        s.LocalSpeakerId = ((AudioDeviceInfo)_localSpk.SelectedItem!).Id;
        s.LocalMicId = ((AudioDeviceInfo)_localMic.SelectedItem!).Id;
        s.HeadsetSpeakerId = ((AudioDeviceInfo)_headsetSpk.SelectedItem!).Id;
        s.Save();

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
            _status.ForeColor = Color.ForestGreen;
            _status.Text = "● Bridging — talk now. (buffer latency updates every second)";
        }
        else
        {
            _status.ForeColor = Color.DimGray;
            _status.Text = "○ Stopped";
        }
    }
}
