using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ScaleSwitcher.Models
{
    public static class SettingsManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScaleSwitcher",
            "config.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Ignore and return default
            }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Ignore
            }
        }
    }
}
