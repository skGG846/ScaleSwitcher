using System;
using Microsoft.Win32;

namespace ScaleSwitcher
{
    public static class StartupManager
    {
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "ScaleSwitcher";

        public static bool IsRegistered()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                if (key != null)
                {
                    var value = key.GetValue(AppName) as string;
                    // Check if it matches current exe
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch
            {
                // Ignore
            }
            return false;
        }

        public static void Register()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (key != null)
                {
                    string exePath = Environment.ProcessPath ?? "";
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
            }
            catch
            {
                // Ignore or log
            }
        }

        public static void Unregister()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                key?.DeleteValue(AppName, false);
            }
            catch
            {
                // Ignore
            }
        }
    }
}
