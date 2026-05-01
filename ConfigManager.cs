using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GHelperAutoProfileSwitcher
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static List<AppProfile> LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                return new List<AppProfile>();
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
                return JsonSerializer.Deserialize<List<AppProfile>>(json, options) ?? new List<AppProfile>();
            }
            catch
            {
                return new List<AppProfile>();
            }
        }

        public static void SaveConfig(List<AppProfile> profiles)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
                var json = JsonSerializer.Serialize(profiles, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save config: {ex.Message}");
            }
        }
    }
}