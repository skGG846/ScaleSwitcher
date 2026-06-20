using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ScaleSwitcher
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon _notifyIcon;
        private AppSettings _settings;
        private int _currentScaleCycleIndex = 0;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _settings = SettingsManager.Load();

            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = SystemIcons.Application, // Fallback icon
                Visible = true,
                Text = "ScaleSwitcher"
            };

            _notifyIcon.MouseClick += NotifyIcon_MouseClick;

            UpdateContextMenu();
        }

        private void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                CycleDpi();
            }
        }

        private void LogDebug(string message)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ScaleSwitcher",
                    "debug.log");
                var dir = Path.GetDirectoryName(logPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore
            }
        }

        private void CycleDpi()
        {
            _settings = SettingsManager.Load(); // reload in case it changed
            LogDebug($"ActiveDpiPercentages: {string.Join(", ", _settings.ActiveDpiPercentages)}");
            if (_settings.ActiveDpiPercentages.Count == 0) return;

            var displays = DisplayManager.GetDisplays();
            if (_settings.TargetMonitorIndex >= displays.Count)
            {
                LogDebug($"TargetMonitorIndex {_settings.TargetMonitorIndex} out of range (Displays: {displays.Count})");
                return;
            }

            var targetDisplay = displays[_settings.TargetMonitorIndex];
            LogDebug($"Target Monitor: {targetDisplay.DeviceName}, Current DPI: {targetDisplay.CurrentDpi?.Percentage}%");

            // Initialize cycle index based on current DPI if possible
            if (targetDisplay.CurrentDpi != null && _settings.ActiveDpiPercentages.Contains(targetDisplay.CurrentDpi.Percentage))
            {
                _currentScaleCycleIndex = _settings.ActiveDpiPercentages.IndexOf(targetDisplay.CurrentDpi.Percentage);
                LogDebug($"Current DPI in active list. Index initialized to {_currentScaleCycleIndex}");
            }
            else
            {
                LogDebug($"Current DPI ({targetDisplay.CurrentDpi?.Percentage}%) NOT in active list!");
            }

            // Next index
            _currentScaleCycleIndex = (_currentScaleCycleIndex + 1) % _settings.ActiveDpiPercentages.Count;
            int nextPercentage = _settings.ActiveDpiPercentages[_currentScaleCycleIndex];
            LogDebug($"Next Percentage: {nextPercentage}% (index {_currentScaleCycleIndex})");

            // Find DpiInfo
            var nextDpi = targetDisplay.AvailableDpis.FirstOrDefault(d => d.Percentage == nextPercentage);
            if (nextDpi != null)
            {
                LogDebug($"Applying DPI: {nextDpi.Percentage}% (RelativeIndex: {nextDpi.RelativeIndex})");
                bool success = DisplayManager.SetDpi(targetDisplay, nextDpi);
                LogDebug($"SetDpi success: {success}");
            }
            else
            {
                LogDebug($"Target DPI {nextPercentage}% NOT found in AvailableDpis! Available: {string.Join(", ", targetDisplay.AvailableDpis.Select(d => d.Percentage))}");
            }
        }

        private void UpdateContextMenu()
        {
            var menu = new Forms.ContextMenuStrip();

            var displays = DisplayManager.GetDisplays();
            for (int i = 0; i < displays.Count; i++)
            {
                var display = displays[i];
                string displayName = $"{ScaleSwitcher.Properties.Resources.DisplayPrefix} {i + 1}";
                if (display.IsPrimary) displayName += " (Primary)";

                var displayMenu = new Forms.ToolStripMenuItem(displayName);

                // Scale SubMenu
                var scaleSubMenu = new Forms.ToolStripMenuItem(ScaleSwitcher.Properties.Resources.Menu_Scale);
                foreach (var dpi in display.AvailableDpis.OrderBy(d => d.Percentage))
                {
                    var dpiItem = new Forms.ToolStripMenuItem($"{dpi.Percentage}%");
                    dpiItem.Checked = (display.CurrentDpi?.Percentage == dpi.Percentage);
                    dpiItem.Click += (s, ev) => DisplayManager.SetDpi(display, dpi);
                    scaleSubMenu.DropDownItems.Add(dpiItem);
                }
                displayMenu.DropDownItems.Add(scaleSubMenu);

                // Resolution SubMenu
                var resSubMenu = new Forms.ToolStripMenuItem(ScaleSwitcher.Properties.Resources.Menu_Resolution);
                foreach (var res in display.AvailableResolutions)
                {
                    var resItem = new Forms.ToolStripMenuItem($"{res.Width} x {res.Height}");
                    resItem.Checked = (display.CurrentResolution != null && display.CurrentResolution.Equals(res));
                    resItem.Click += (s, ev) => DisplayManager.SetResolution(display, res);
                    resSubMenu.DropDownItems.Add(resItem);
                }
                displayMenu.DropDownItems.Add(resSubMenu);

                menu.Items.Add(displayMenu);
            }

            if (displays.Count > 0)
            {
                menu.Items.Add(new Forms.ToolStripSeparator());
            }

            var runAtStartupItem = new Forms.ToolStripMenuItem(ScaleSwitcher.Properties.Resources.Menu_RunAtStartup)
            {
                CheckOnClick = true,
                Checked = StartupManager.IsRegistered()
            };
            runAtStartupItem.CheckedChanged += (s, e) =>
            {
                if (runAtStartupItem.Checked)
                    StartupManager.Register();
                else
                    StartupManager.Unregister();
            };
            menu.Items.Add(runAtStartupItem);

            var settingsItem = new Forms.ToolStripMenuItem(ScaleSwitcher.Properties.Resources.Menu_Settings);
            settingsItem.Click += (s, e) => OpenSettings();
            menu.Items.Add(settingsItem);

            var exitItem = new Forms.ToolStripMenuItem(ScaleSwitcher.Properties.Resources.Menu_Exit);
            exitItem.Click += (s, e) => ExitApp();
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow();
            if (settingsWindow.ShowDialog() == true)
            {
                // Refresh context menu after settings changed
                UpdateContextMenu();
            }
        }

        private void ExitApp()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Shutdown();
        }
    }
}
