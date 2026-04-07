using System.Net.Sockets;
using CoreOSC;
using CoreOSC.IO;

namespace AutoRenderingAgentConfig;

public class MainForm : Form
{
    private readonly TextBox _ipBox;
    private readonly NumericUpDown _portBox;
    private readonly TextBox _projectPathBox;
    private readonly Button _browseButton;
    private readonly NumericUpDown _takeBox;
    private readonly Button _saveButton;
    private readonly Button _stopRenderButton;
    private readonly Label _configPathLabel;
    private AgentConfig _config;

    public MainForm()
    {
        Text = "AutoRendering Agent Config";
        ClientSize = new Size(500, 310);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 7,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        // Row 0: IP Address
        layout.Controls.Add(CreateLabel("IP Address"), 0, 0);
        _ipBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_ipBox, 1, 0);
        layout.SetColumnSpan(_ipBox, 2);

        // Row 1: Port
        layout.Controls.Add(CreateLabel("Port"), 0, 1);
        _portBox = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 65535,
        };
        layout.Controls.Add(_portBox, 1, 1);
        layout.SetColumnSpan(_portBox, 2);

        // Row 2: Unity Project Path
        layout.Controls.Add(CreateLabel("Unity Project Path"), 0, 2);
        _projectPathBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_projectPathBox, 1, 2);
        _browseButton = new Button
        {
            Text = "...",
            Dock = DockStyle.Fill,
        };
        _browseButton.Click += OnBrowseClicked;
        layout.Controls.Add(_browseButton, 2, 2);

        // Row 3: Take
        layout.Controls.Add(CreateLabel("Take"), 0, 3);
        _takeBox = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 99999,
        };
        layout.Controls.Add(_takeBox, 1, 3);
        layout.SetColumnSpan(_takeBox, 2);

        // Row 4: Stop Rendering button
        _stopRenderButton = new Button
        {
            Text = "Stop Rendering",
            Dock = DockStyle.Fill,
            Height = 36,
            BackColor = Color.FromArgb(180, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        _stopRenderButton.Click += OnStopRenderClicked;
        layout.Controls.Add(_stopRenderButton, 0, 4);
        layout.SetColumnSpan(_stopRenderButton, 3);

        // Row 5: Save button
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        _saveButton = new Button
        {
            Text = "Save",
            Size = new Size(100, 34),
            BackColor = Color.FromArgb(40, 120, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        _saveButton.Click += OnSaveClicked;
        buttonPanel.Controls.Add(_saveButton);
        layout.Controls.Add(buttonPanel, 0, 5);
        layout.SetColumnSpan(buttonPanel, 3);

        // Row 6: Config path display
        _configPathLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(_configPathLabel, 0, 6);
        layout.SetColumnSpan(_configPathLabel, 3);

        Controls.Add(layout);

        _config = AgentConfig.Load();
        LoadFromConfig();
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
    }

    private void LoadFromConfig()
    {
        _ipBox.Text = _config.IpAddress;
        _portBox.Value = _config.Port;
        _projectPathBox.Text = _config.UnityProjectPath;
        _takeBox.Value = _config.Take;
        _configPathLabel.Text = AgentConfig.ConfigPath;
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Unity project folder (parent of Assets)",
            UseDescriptionForTitle = true,
        };

        if (!string.IsNullOrEmpty(_projectPathBox.Text))
            dialog.SelectedPath = _projectPathBox.Text;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _projectPathBox.Text = dialog.SelectedPath;
    }

    private async void OnStopRenderClicked(object? sender, EventArgs e)
    {
        try
        {
            using var client = new UdpClient();
            client.Connect("127.0.0.1", (int)_portBox.Value);
            var message = new OscMessage(new Address("/render/stop"));
            await client.SendMessageAsync(message);
            MessageBox.Show("Stop command sent.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        _config.IpAddress = _ipBox.Text.Trim();
        _config.Port = (int)_portBox.Value;
        _config.UnityProjectPath = _projectPathBox.Text.Trim();
        _config.Take = (int)_takeBox.Value;

        _config.Save();
        MessageBox.Show("Saved.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
