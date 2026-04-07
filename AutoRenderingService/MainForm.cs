using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using CoreOSC;
using CoreOSC.IO;
using CoreOSC.Types;

namespace AutoRenderingService;

public class MainForm : Form
{
    private readonly TreeView _treeView;
    private readonly BindingList<EndPointEntry> _entries = new();
    private readonly Button _addButton;
    private readonly Button _removeButton;
    private readonly Button _refreshButton;
    private readonly Button _startRenderButton;
    private readonly TextBox _logBox;
    private AppConfig _config;
    private readonly List<UdpClient> _activeClients = new();
    private CancellationTokenSource? _renderCts;
    private bool _isRecording;
    private int _activeRenderCount;

    public MainForm()
    {
        Text = "Auto Rendering Service";
        Size = new Size(550, 500);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(450, 350);

        // --- TreeView: IP:Port nodes with .unity children ---
        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            CheckBoxes = true,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HideSelection = false,
            ItemHeight = 24,
            Font = new Font("Segoe UI", 9.5f),
        };
        _treeView.AfterCheck += OnTreeAfterCheck;
        _treeView.NodeMouseDoubleClick += OnNodeDoubleClick;

        // --- Bottom panel: Add / Remove / Refresh buttons ---
        _addButton = new Button { Text = "Add", Size = new Size(90, 32) };
        _removeButton = new Button { Text = "Remove", Size = new Size(90, 32) };
        _refreshButton = new Button { Text = "Refresh", Size = new Size(90, 32) };

        _addButton.Click += OnAddClicked;
        _removeButton.Click += OnRemoveClicked;
        _refreshButton.Click += OnRefreshClicked;

        _startRenderButton = new Button
        {
            Text = "▶ Start Rendering",
            Size = new Size(140, 36),
            BackColor = Color.FromArgb(40, 120, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Right,
        };
        _startRenderButton.Click += OnStartRenderClicked;

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            ColumnCount = 2,
            Padding = new Padding(6),
        };
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
        };
        leftButtons.Controls.Add(_addButton);
        leftButtons.Controls.Add(_removeButton);
        leftButtons.Controls.Add(_refreshButton);

        buttonPanel.Controls.Add(leftButtons, 0, 0);
        buttonPanel.Controls.Add(_startRenderButton, 1, 0);

        // --- Log area ---
        _logBox = new TextBox
        {
            Dock = DockStyle.Bottom,
            Height = 100,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Consolas", 9f),
        };

        // --- Layout ---
        Controls.Add(_treeView);
        Controls.Add(_logBox);
        Controls.Add(buttonPanel);

        // --- Load config ---
        _config = AppConfig.Load();
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        if (_config.EndPoints.Count == 0)
        {
            AddEntry(new EndPointEntry("127.0.0.1", 9000));
            SaveConfig();
            return;
        }

        foreach (var ep in _config.EndPoints)
        {
            var entry = new EndPointEntry(ep.IpAddress, ep.Port);
            AddEntry(entry, ep.CheckedScenes);
        }
    }

    private void SaveConfig()
    {
        _config.EndPoints.Clear();

        foreach (TreeNode rootNode in _treeView.Nodes)
        {
            if (rootNode.Tag is not EndPointEntry entry) continue;

            var epConfig = new EndPointConfig
            {
                IpAddress = entry.IpAddress,
                Port = entry.Port,
            };

            foreach (TreeNode child in rootNode.Nodes)
            {
                if (child.Checked)
                    epConfig.CheckedScenes.Add(child.Text);
            }

            _config.EndPoints.Add(epConfig);
        }

        _config.Save();
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        using var dialog = new EndPointDialog("127.0.0.1", 9000);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var entry = new EndPointEntry(dialog.IpAddress, dialog.Port);
            AddEntry(entry);
            SaveConfig();
            _ = FetchScenes(entry, _treeView.Nodes[^1]);
        }
    }

    private void OnNodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node is not { Level: 0, Tag: EndPointEntry entry }) return;

        using var dialog = new EndPointDialog(entry.IpAddress, entry.Port);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        entry.IpAddress = dialog.IpAddress;
        entry.Port = dialog.Port;
        e.Node.Text = $"{entry.IpAddress} : {entry.Port}";
        SaveConfig();
        _ = FetchScenes(entry, e.Node);
    }

    private void OnRemoveClicked(object? sender, EventArgs e)
    {
        TreeNode? target = _treeView.SelectedNode switch
        {
            { Level: 0 } node => node,
            { Level: 1, Parent: { } parent } => parent,
            _ => null,
        };

        if (target == null) return;

        var index = _treeView.Nodes.IndexOf(target);
        _entries.RemoveAt(index);
        _treeView.Nodes.Remove(target);
        SaveConfig();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        foreach (TreeNode rootNode in _treeView.Nodes)
        {
            if (rootNode.Tag is EndPointEntry entry)
                await FetchScenes(entry, rootNode);
        }
    }

    private void AddEntry(EndPointEntry entry, List<string>? checkedScenes = null)
    {
        _entries.Add(entry);

        var rootNode = new TreeNode($"{entry.IpAddress} : {entry.Port}")
        {
            Tag = entry,
        };

        if (checkedScenes is { Count: > 0 })
        {
            foreach (var scene in checkedScenes)
            {
                rootNode.Nodes.Add(new TreeNode(scene) { Checked = true });
            }
        }

        _treeView.Nodes.Add(rootNode);
        rootNode.Expand();
    }

    private async Task FetchScenes(EndPointEntry entry, TreeNode node)
    {
        AppendLog($"Fetching scenes from {entry.IpAddress}:{entry.Port}...");

        try
        {
            using var client = new UdpClient();
            client.Connect(entry.IpAddress, entry.Port);

            var request = new OscMessage(new Address("/scenes/list"));
            await client.SendMessageAsync(request);

            using var cts = new CancellationTokenSource(5000);
            var response = await client.ReceiveMessageAsync().WaitAsync(cts.Token);

            if (response.Address.Value != "/scenes/result")
            {
                AppendLog("Unexpected response.");
                return;
            }

            var checkedScenes = new List<string>();
            foreach (TreeNode child in node.Nodes)
            {
                if (child.Checked)
                    checkedScenes.Add(child.Text);
            }

            node.Nodes.Clear();
            var scenes = response.Arguments.Select(a => a?.ToString() ?? "").Where(s => s.Length > 0).ToList();

            foreach (var scene in scenes)
            {
                node.Nodes.Add(new TreeNode(scene)
                {
                    Checked = checkedScenes.Contains(scene),
                });
            }

            node.Expand();
            AppendLog($"Got {scenes.Count} scene(s) from {entry.IpAddress}:{entry.Port}");
            SaveConfig();
        }
        catch (OperationCanceledException)
        {
            AppendLog($"Timeout: {entry.IpAddress}:{entry.Port} did not respond.");
        }
        catch (Exception ex)
        {
            AppendLog($"Failed: {ex.Message}");
        }
    }

    private void OnTreeAfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (e.Action == TreeViewAction.Unknown) return;

        _treeView.AfterCheck -= OnTreeAfterCheck;
        try
        {
            if (e.Node is { Level: 0 } parent)
            {
                foreach (TreeNode child in parent.Nodes)
                    child.Checked = parent.Checked;
            }
        }
        finally
        {
            _treeView.AfterCheck += OnTreeAfterCheck;
        }

        SaveConfig();
    }

    private async void OnStartRenderClicked(object? sender, EventArgs e)
    {
        if (_isRecording)
        {
            foreach (var client in _activeClients)
            {
                try
                {
                    var stopMsg = new OscMessage(new Address("/render/stop"));
                    await client.SendMessageAsync(stopMsg);
                    AppendLog("Stop requested.");
                }
                catch (Exception ex)
                {
                    AppendLog($"Failed to send stop: {ex.Message}");
                }
            }
            return;
        }

        _renderCts = new CancellationTokenSource();
        _activeRenderCount = 0;
        var sent = 0;

        foreach (TreeNode rootNode in _treeView.Nodes)
        {
            if (rootNode.Tag is not EndPointEntry entry) continue;

            var checkedScenes = new List<string>();
            foreach (TreeNode child in rootNode.Nodes)
            {
                if (child.Checked)
                    checkedScenes.Add(child.Text);
            }

            if (checkedScenes.Count == 0) continue;

            try
            {
                var client = new UdpClient();
                client.Connect(entry.IpAddress, entry.Port);

                var message = new OscMessage(
                    new Address("/render/start"),
                    checkedScenes.Cast<object>());

                await client.SendMessageAsync(message);
                _activeClients.Add(client);
                _activeRenderCount++;
                sent++;
                AppendLog($"Sent to {entry.IpAddress}:{entry.Port} -> {string.Join(", ", checkedScenes)}");

                _ = ListenForCallbacks(client, _renderCts.Token);
            }
            catch (Exception ex)
            {
                AppendLog($"Failed: {entry.IpAddress}:{entry.Port} -> {ex.Message}");
            }
        }

        if (sent == 0)
        {
            AppendLog("No endpoints with checked scenes.");
        }
        else
        {
            SetRecordingState(true);
        }
    }

    private async Task ListenForCallbacks(UdpClient client, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await client.ReceiveMessageAsync();
                if (ct.IsCancellationRequested) break;
                Invoke(() => HandleCallback(msg));
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private void HandleCallback(OscMessage msg)
    {
        switch (msg.Address.Value)
        {
            case "/render/started":
                var scene = msg.Arguments.FirstOrDefault()?.ToString() ?? "";
                AppendLog($"Rendering: {scene}");
                break;

            case "/render/stopped":
                var stoppedScene = msg.Arguments.FirstOrDefault()?.ToString() ?? "";
                AppendLog($"Stopped: {stoppedScene}");
                break;

            case "/render/finished":
                _activeRenderCount--;
                AppendLog("Render queue finished.");
                if (_activeRenderCount <= 0)
                    ResetRenderState();
                break;
        }
    }

    private void SetRecordingState(bool recording)
    {
        _isRecording = recording;
        if (recording)
        {
            _startRenderButton.Text = "⏹ Recording";
            _startRenderButton.BackColor = Color.FromArgb(180, 40, 40);
        }
        else
        {
            _startRenderButton.Text = "▶ Start Rendering";
            _startRenderButton.BackColor = Color.FromArgb(40, 120, 40);
        }
    }

    private void ResetRenderState()
    {
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _renderCts = null;

        foreach (var c in _activeClients)
            c.Dispose();
        _activeClients.Clear();
        _activeRenderCount = 0;

        SetRecordingState(false);
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        _logBox.AppendText(line);
    }
}

public class EndPointEntry
{
    public string IpAddress { get; set; }
    public int Port { get; set; }

    public EndPointEntry(string ipAddress, int port)
    {
        IpAddress = ipAddress;
        Port = port;
    }
}
