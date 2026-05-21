using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GHelperAutoProfileSwitcher
{
    public class AppConfig
    {
        public TargetMode DefaultMode { get; set; } = TargetMode.Balanced;
        public List<AppProfile> Profiles { get; set; } = new List<AppProfile>();
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppConfig LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
                
                try
                {
                    if (json.TrimStart().StartsWith("{"))
                    {
                        return JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
                    }
                    else
                    {
                        var profiles = JsonSerializer.Deserialize<List<AppProfile>>(json, options) ?? new List<AppProfile>();
                        return new AppConfig { Profiles = profiles };
                    }
                }
                catch
                {
                    return new AppConfig();
                }
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void SaveConfig(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save config: {ex.Message}");
            }
        }
    }
}
