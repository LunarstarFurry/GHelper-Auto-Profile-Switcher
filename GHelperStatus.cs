using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GHelperAutoProfileSwitcher
{
    public static class GHelperStatus
    {
        private static string? FindConfigPath()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GHelper",
                "config.json");

            if (File.Exists(appDataPath))
            {
                return appDataPath;
            }

            // Fallback for portable G-Helper installations
            Process[] processes = Process.GetProcessesByName("GHelper");
            try
            {
                foreach (var proc in processes)
                {
                    try
                    {
                        string? exePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            string? dir = Path.GetDirectoryName(exePath);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                string portablePath = Path.Combine(dir, "config.json");
                                if (File.Exists(portablePath))
                                {
                                    return portablePath;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore access denied or exited process errors
                    }
                }
            }
            finally
            {
                foreach (var proc in processes)
                {
                    proc.Dispose();
                }
            }

            return null;
        }

        public static TargetMode? GetCurrentMode()
        {
            try
            {
                string? configPath = FindConfigPath();
                if (string.IsNullOrEmpty(configPath))
                {
                    return null;
                }

                using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var doc = JsonDocument.Parse(stream);

                if (doc.RootElement.TryGetProperty("performance_mode", out JsonElement modeElement) &&
                    modeElement.TryGetInt32(out int modeValue))
                {
                    return modeValue switch
                    {
                        0 => TargetMode.Balanced,
                        1 => TargetMode.Turbo,
                        2 => TargetMode.Silent,
                        _ => null
                    };
                }
            }
            catch
            {
                // Config may be mid-write or inaccessible; caller treats null as unknown
            }

            return null;
        }
    }
}
