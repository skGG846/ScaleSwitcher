using System;
using System.Drawing;
using System.Linq;
using System.Windows;
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

            Icon? appIcon = null;
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
                var sri = System.Windows.Application.GetResourceStream(iconUri);
                if (sri != null)
                {
                    using (var stream = sri.Stream)
                    {
                        appIcon = new Icon(stream);
                    }
                }
            }
            catch
            {
                // Fallback
            }

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Opening += (s, ev) => UpdateContextMenu();

            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = appIcon ?? SystemIcons.Application,
                Visible = true,
                Text = "ScaleSwitcher",
                ContextMenuStrip = contextMenu
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
                    string displayName = $"{AppLocalization.Instance.DisplayPrefix} {i + 1}";
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
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
