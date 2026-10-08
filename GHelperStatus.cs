using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GHelperAutoProfileSwitcher
{
    public static class GHelperStatus
    {
        private static readonly string AppDataConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GHelper",
            "config.json");

        public static bool IsGHelperRunning()
        {
            var procs = Process.GetProcessesByName("GHelper");
            try
            {
                return procs.Length > 0;
            }
            finally
            {
                foreach (var p in procs)
                {
                    p.Dispose();
                }
            }
        }

        public static TargetMode? GetActiveGHelperMode()
        {
            // 1. Try standard AppData path
            var mode = TryReadModeFromConfig(AppDataConfigPath);
            if (mode.HasValue) return mode;

            // 2. Try portable directory next to running GHelper executable
            var procs = Process.GetProcessesByName("GHelper");
            try
            {
                if (procs.Length > 0)
                {
                    try
                    {
                        var exePath = procs[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            var dir = Path.GetDirectoryName(exePath);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                var portableConfig = Path.Combine(dir, "config.json");
                                if (File.Exists(portableConfig))
                                {
                                    mode = TryReadModeFromConfig(portableConfig);
                                    if (mode.HasValue) return mode;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // MainModule may throw if elevated/access denied
                    }
                }
            }
            finally
            {
                foreach (var p in procs)
                {
                    p.Dispose();
                }
            }

            return null;
        }

        private static TargetMode? TryReadModeFromConfig(string configPath)
        {
            if (!File.Exists(configPath)) return null;

            try
            {
                using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("performance_mode", out var prop) && prop.TryGetInt32(out int modeVal))
                {
                    return modeVal switch
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
                // File might be briefly locked during G-Helper write operations
            }

            return null;
        }
    }
}
