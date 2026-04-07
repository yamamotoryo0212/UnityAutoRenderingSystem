using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoRenderingAgent;

public class AgentConfig
{
    public static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoRenderingAgent", "agent-config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string IpAddress { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 9000;
    public string UnityProjectPath { get; set; } = string.Empty;
    public int Take { get; set; } = 1;

    public static AgentConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var config = new AgentConfig();
            config.Save();
            return config;
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions) ?? new AgentConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
