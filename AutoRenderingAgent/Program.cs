using Microsoft.Win32;
using CoreOSC;
using CoreOSC.IO;
using CoreOSC.Types;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace AutoRenderingAgent;

static class Program
{
    private const string AppName = "AutoRenderingAgent";
    private static AgentConfig _config = null!;
    private static Process? _currentUnityProcess;
    private static readonly object _processLock = new();
    private static UdpClient _udpClient = null!;

    static async Task Main(string[] args)
    {
        _config = AgentConfig.Load();
        RegisterStartup();

        Log($"Agent started. Listening on {_config.IpAddress}:{_config.Port}");
        Log($"Unity project: {(_config.UnityProjectPath is { Length: > 0 } p ? p : "(not set)")}");

        _udpClient = new UdpClient(_config.Port);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await _udpClient.ReceiveAsync(cts.Token);
                var senderEndPoint = result.RemoteEndPoint;

                try
                {
                    var message = ParseOscMessage(result.Buffer);
                    if (message is { } msg)
                    {
                        Log($"Received: {msg.Address.Value} from {senderEndPoint}");
                        ProcessMessage(msg, senderEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error parsing OSC: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log("Agent shutting down.");
        }
    }

    private static void ProcessMessage(OscMessage message, IPEndPoint senderEndPoint)
    {
        switch (message.Address.Value)
        {
            case "/render/start":
                Log("Render start requested.");
                var scenes = message.Arguments.Select(a => a?.ToString() ?? "").Where(s => s.Length > 0).ToList();
                foreach (var s in scenes)
                    Log($"  Scene: {s}");
                _ = Task.Run(() => RenderAllScenes(scenes, senderEndPoint));
                break;

            case "/render/stop":
                Log("Render stop requested.");
                StopCurrentRender();
                break;

            case "/scenes/list":
                Log("Scene list requested.");
                _ = Task.Run(() => SendSceneList(senderEndPoint));
                break;

            case "/ping":
                Log("Ping received.");
                break;

            default:
                Log($"Unknown address: {message.Address.Value}");
                break;
        }
    }

    private static void SendSceneList(IPEndPoint target)
    {
        if (string.IsNullOrEmpty(_config.UnityProjectPath))
        {
            SendCallback(target, "/scenes/result").Wait();
            return;
        }

        var assetsDir = Path.Combine(_config.UnityProjectPath, "Assets");
        if (!Directory.Exists(assetsDir))
        {
            SendCallback(target, "/scenes/result").Wait();
            return;
        }

        var sceneFiles = Directory.GetFiles(assetsDir, "*.unity", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<object>()
            .ToArray();

        Log($"Sending {sceneFiles.Length} scene(s) to {target}");
        SendCallback(target, "/scenes/result", sceneFiles).Wait();
    }

    private static void StopCurrentRender()
    {
        lock (_processLock)
        {
            if (_currentUnityProcess is { HasExited: false } process)
            {
                Log($"Killing Unity process (PID: {process.Id})...");
                process.Kill();
            }
        }
    }

    private static async Task SendCallback(IPEndPoint target, string address, params object[] args)
    {
        try
        {
            var message = new OscMessage(new Address(address), args);
            var converter = new OscMessageConverter();
            var dwords = converter.Serialize(message).ToArray();
            var bytes = dwords.SelectMany(d => d.Bytes).ToArray();
            await _udpClient.SendAsync(bytes, bytes.Length, target);
            Log($"Callback sent: {address} -> {target}");
        }
        catch (Exception ex)
        {
            Log($"Failed to send callback: {ex.Message}");
        }
    }

    private static void RenderAllScenes(List<string> scenes, IPEndPoint senderEndPoint)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.UnityProjectPath))
            {
                Log("Error: UnityProjectPath is not set in agent-config.json");
                return;
            }

            if (!Directory.Exists(_config.UnityProjectPath))
            {
                Log($"Error: Unity project not found: {_config.UnityProjectPath}");
                return;
            }

            if (scenes.Count == 0)
            {
                Log("Error: No scenes specified.");
                return;
            }

            var unityExe = FindUnityEditor(_config.UnityProjectPath);
            if (unityExe == null)
            {
                Log("Error: Could not find Unity Editor.");
                return;
            }

            Log($"Starting render queue: {scenes.Count} scene(s), Take: {_config.Take}");
            for (var i = 0; i < scenes.Count; i++)
            {
                var sceneName = scenes[i];
                var take = _config.Take;
                Log($"[{i + 1}/{scenes.Count}] Rendering: {sceneName} (Take {take})");

                // Notify Service that rendering has started
                SendCallback(senderEndPoint, "/render/started", sceneName).Wait();

                var exitCode = LaunchUnityAndWait(unityExe, sceneName, take);

                if (exitCode != 0)
                {
                    Log($"[{i + 1}/{scenes.Count}] Unity exited with code {exitCode}.");
                    SendCallback(senderEndPoint, "/render/stopped", sceneName).Wait();

                    if (exitCode == -1)
                    {
                        // Process kill or launch failure - abort queue
                        return;
                    }
                    // Otherwise continue to next scene
                    continue;
                }

                Log($"[{i + 1}/{scenes.Count}] Completed: {sceneName}");

                _config.Take++;
                _config.Save();
            }

            Log("All scenes rendered successfully.");
        }
        finally
        {
            SendCallback(senderEndPoint, "/render/finished").Wait();
        }
    }

    private static int LaunchUnityAndWait(string unityExe, string sceneName, int take)
    {
        var arguments = $"-projectPath \"{_config.UnityProjectPath}\" " +
                        $"-executeMethod UnityAutoRendering.RenderBootstrap.Run " +
                        $"-scene \"{sceneName}\" " +
                        $"-take {take}";

        Log($"Launching Unity: {unityExe}");
        Log($"  Scene: {sceneName}, Take: {take}");

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = unityExe,
                Arguments = arguments,
                UseShellExecute = false,
            });

            if (process == null)
            {
                Log("Error: Failed to start Unity process.");
                return -1;
            }

            lock (_processLock)
            {
                _currentUnityProcess = process;
            }

            Log($"Unity started (PID: {process.Id}). Waiting for exit...");
            process.WaitForExit();

            lock (_processLock)
            {
                _currentUnityProcess = null;
            }

            Log($"Unity exited (code: {process.ExitCode})");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Log($"Failed to launch Unity: {ex.Message}");
            return -1;
        }
    }

    private static string? FindUnityEditor(string projectPath)
    {
        var versionFile = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        if (File.Exists(versionFile))
        {
            var lines = File.ReadAllLines(versionFile);
            foreach (var line in lines)
            {
                if (!line.StartsWith("m_EditorVersion:")) continue;

                var version = line.Split(':')[1].Trim();
                var editorPath = Path.Combine(
                    @"C:\Program Files\Unity\Hub\Editor", version, "Editor", "Unity.exe");

                if (File.Exists(editorPath))
                {
                    Log($"Found Unity {version}");
                    return editorPath;
                }

                Log($"Unity {version} not found at: {editorPath}");
            }
        }

        var hubEditorRoot = @"C:\Program Files\Unity\Hub\Editor";
        if (Directory.Exists(hubEditorRoot))
        {
            var dirs = Directory.GetDirectories(hubEditorRoot)
                .OrderByDescending(d => d)
                .ToArray();

            foreach (var dir in dirs)
            {
                var exe = Path.Combine(dir, "Editor", "Unity.exe");
                if (File.Exists(exe))
                {
                    Log($"Fallback: using {Path.GetFileName(dir)}");
                    return exe;
                }
            }
        }

        return null;
    }

    private static void RegisterStartup()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key == null) return;

            var current = key.GetValue(AppName) as string;
            if (current == exePath) return;

            key.SetValue(AppName, exePath);
            Log("Registered to Windows startup.");
        }
        catch (Exception ex)
        {
            Log($"Failed to register startup: {ex.Message}");
        }
    }

    private static OscMessage? ParseOscMessage(byte[] data)
    {
        try
        {
            var padded = data;
            if (data.Length % 4 != 0)
            {
                padded = new byte[(data.Length + 3) / 4 * 4];
                Array.Copy(data, padded, data.Length);
            }

            var dwords = new List<DWord>();
            for (var i = 0; i < padded.Length; i += 4)
                dwords.Add(new DWord(padded[i], padded[i + 1], padded[i + 2], padded[i + 3]));

            var converter = new OscMessageConverter();
            converter.Deserialize(dwords, out var msg);
            return msg;
        }
        catch
        {
            return null;
        }
    }

    private static void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        Console.WriteLine(line);

        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoRenderingAgent", "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
        catch
        {
        }
    }
}
