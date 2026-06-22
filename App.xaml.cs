using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScaleSwitcher.Models;
using ScaleSwitcher.Services;
using ScaleSwitcher.Views;
using Forms = System.Windows.Forms;

namespace ScaleSwitcher
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon _notifyIcon = null!;
        private AppSettings _settings = null!;
        private int _currentScaleCycleIndex = 0;
        private Icon? _lightTrayIcon;
        private Icon? _darkTrayIcon;
        private static System.Threading.Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "ScaleSwitcher_Unique_Mutex_Name_2026";
            _mutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
                Shutdown();
                return;
            }

            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _settings = SettingsManager.Load();
            _lightTrayIcon = LoadIcon("pack://application:,,,/Assets/app.light.ico");
            _darkTrayIcon = LoadIcon("pack://application:,,,/Assets/app.dark.ico");

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Opening += (s, ev) => UpdateContextMenu();

            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = GetTrayIconForCurrentTheme() ?? SystemIcons.Application,
                Visible = true,
                Text = "ScaleSwitcher",
                ContextMenuStrip = contextMenu
            };

            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            UpdateContextMenu();
        }

        private static Icon? LoadIcon(string iconUri)
        {
            try
            {
                var sri = System.Windows.Application.GetResourceStream(new Uri(iconUri, UriKind.Absolute));
                if (sri == null) return null;

                using var stream = sri.Stream;
                return new Icon(stream);
            }
            catch
            {
                return null;
            }
        }

        private Icon? GetTrayIconForCurrentTheme()
        {
            return IsSystemLightTheme() ? _lightTrayIcon ?? _darkTrayIcon : _darkTrayIcon ?? _lightTrayIcon;
        }

        private static bool IsSystemLightTheme()
        {
            const string personalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string valueName = "SystemUsesLightTheme";

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(personalizeKeyPath);
                if (key?.GetValue(valueName) is int value)
                {
                    return value != 0;
                }
            }
            catch
            {
                // Fall through to the default.
            }

            return true;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
            {
                var icon = GetTrayIconForCurrentTheme();
                if (icon != null)
                {
                    _notifyIcon.Icon = icon;
                }
            }
        }

        private void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                CycleDpi();
            }
        }

        private void CycleDpi()
        {
            _settings = SettingsManager.Load(); // reload in case it changed
            if (_settings.ActiveDpiPercentages.Count == 0) return;

            var displays = DisplayManager.GetDisplays();
            if (_settings.TargetMonitorIndex >= displays.Count) return;

            var targetDisplay = displays[_settings.TargetMonitorIndex];

            // Initialize cycle index based on current DPI if possible
            if (targetDisplay.CurrentDpi != null && _settings.ActiveDpiPercentages.Contains(targetDisplay.CurrentDpi.Percentage))
            {
                _currentScaleCycleIndex = _settings.ActiveDpiPercentages.IndexOf(targetDisplay.CurrentDpi.Percentage);
            }

            // Next index
            _currentScaleCycleIndex = (_currentScaleCycleIndex + 1) % _settings.ActiveDpiPercentages.Count;
            int nextPercentage = _settings.ActiveDpiPercentages[_currentScaleCycleIndex];

            // Find DpiInfo
            var nextDpi = targetDisplay.AvailableDpis.FirstOrDefault(d => d.Percentage == nextPercentage);
            if (nextDpi != null)
            {
                DisplayManager.SetDpi(targetDisplay, nextDpi);
            }
        }

        private void UpdateContextMenu()
        {
            var menu = _notifyIcon.ContextMenuStrip;
            if (menu == null) return;

            menu.Items.Clear();

            var displays = DisplayManager.GetDisplays();
            
            if (displays.Count == 1)
            {
                var display = displays[0];

                // Scale SubMenu (Top Level)
                var scaleSubMenu = new Forms.ToolStripMenuItem(AppLocalization.Instance.Menu_Scale);
                foreach (var dpi in display.AvailableDpis.OrderBy(d => d.Percentage))
                {
                    var dpiItem = new Forms.ToolStripMenuItem($"{dpi.Percentage}%");
                    dpiItem.Checked = (display.CurrentDpi?.Percentage == dpi.Percentage);
                    dpiItem.Click += (s, ev) => DisplayManager.SetDpi(display, dpi);
                    scaleSubMenu.DropDownItems.Add(dpiItem);
                }
                menu.Items.Add(scaleSubMenu);

                // Resolution SubMenu (Top Level)
                var resSubMenu = new Forms.ToolStripMenuItem(AppLocalization.Instance.Menu_Resolution);
                foreach (var res in display.AvailableResolutions)
                {
                    var resItem = new Forms.ToolStripMenuItem($"{res.Width} x {res.Height}");
                    resItem.Checked = (display.CurrentResolution != null && display.CurrentResolution.Equals(res));
                    resItem.Click += (s, ev) => DisplayManager.SetResolution(display, res);
                    resSubMenu.DropDownItems.Add(resItem);
                }
                menu.Items.Add(resSubMenu);
            }
            else
            {
                for (int i = 0; i < displays.Count; i++)
                {
                    var display = displays[i];
                    string displayName = $"{AppLocalization.Instance.DisplayPrefix} {display.SettingsDisplayNumber}";
                    if (display.IsPrimary) displayName += " (Primary)";

                    var displayMenu = new Forms.ToolStripMenuItem(displayName);

                    // Scale SubMenu
                    var scaleSubMenu = new Forms.ToolStripMenuItem(AppLocalization.Instance.Menu_Scale);
                    foreach (var dpi in display.AvailableDpis.OrderBy(d => d.Percentage))
                    {
                        var dpiItem = new Forms.ToolStripMenuItem($"{dpi.Percentage}%");
                        dpiItem.Checked = (display.CurrentDpi?.Percentage == dpi.Percentage);
                        dpiItem.Click += (s, ev) => DisplayManager.SetDpi(display, dpi);
                        scaleSubMenu.DropDownItems.Add(dpiItem);
                    }
                    displayMenu.DropDownItems.Add(scaleSubMenu);

                    // Resolution SubMenu
                    var resSubMenu = new Forms.ToolStripMenuItem(AppLocalization.Instance.Menu_Resolution);
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
            }

            if (displays.Count > 0)
            {
                menu.Items.Add(new Forms.ToolStripSeparator());
            }

            var runAtStartupItem = new Forms.ToolStripMenuItem(AppLocalization.Instance.Menu_RunAtStartup)
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

            var settingsItem = new Forms.ToolStripMenuItem(AppLocalization.Instance.Menu_Settings);
            settingsItem.Click += (s, e) => OpenSettings();
            menu.Items.Add(settingsItem);

            var exitItem = new Forms.ToolStripMenuItem(AppLocalization.Instance.Menu_Exit);
            exitItem.Click += (s, e) => ExitApp();
            menu.Items.Add(exitItem);
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
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            _lightTrayIcon?.Dispose();
            _darkTrayIcon?.Dispose();

            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore
                }
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
