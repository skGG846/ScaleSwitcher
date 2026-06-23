using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace ScaleSwitcher.Models
{
    public static class DisplayManager
    {
        private static readonly int[] DpiArray = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };
        private static readonly List<ScaleSwitcher.Views.OsdWindow> DisplayInfoOsds = new();

        public static bool DisplayInfoOsdsVisible => DisplayInfoOsds.Count > 0;

        public static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();
            var settings = SettingsManager.Load();
            const string effectiveDisplayNumberSource = DisplayNumberSources.TargetId;
            var diagnostics = new StringBuilder();
            AppendDiagnosticsHeader(diagnostics, settings.DisplayNumberSource, effectiveDisplayNumberSource);
            var settingsDisplayNumbers = GetWindowsDisplayNumbers(diagnostics, effectiveDisplayNumberSource);
            int index = 0;

            diagnostics.AppendLine();
            diagnostics.AppendLine("[EnumDisplayMonitors]");
            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.Rect lprcMonitor, IntPtr dwData)
            {
                var mi = new NativeMethods.MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    var info = new DisplayInfo
                    {
                        MonitorIndex = index,
                        SettingsDisplayNumber = settingsDisplayNumbers.TryGetValue(mi.szDevice, out int settingsDisplayNumber)
                            ? settingsDisplayNumber
                            : index + 1,
                        MonitorHandle = hMonitor,
                        DeviceName = mi.szDevice,
                        IsPrimary = (mi.dwFlags & 1) != 0 // MONITORINFOF_PRIMARY
                    };

                    PopulateResolutions(info);
                    PopulateDpis(info);
                    
                    displays.Add(info);
                    diagnostics.AppendLine(
                        $"index={index}, device={mi.szDevice}, assignedDisplayNumber={info.SettingsDisplayNumber}, isPrimary={info.IsPrimary}, " +
                        $"monitorRect=({mi.rcMonitor.left},{mi.rcMonitor.top})-({mi.rcMonitor.right},{mi.rcMonitor.bottom}), " +
                        $"workRect=({mi.rcWork.left},{mi.rcWork.top})-({mi.rcWork.right},{mi.rcWork.bottom})");
                    index++;
                }
                return true;
            }, IntPtr.Zero);

            AppendWindowsFormsScreenDiagnostics(diagnostics);
            WriteDisplayDiagnostics(diagnostics);

            return displays;
        }

        private static Dictionary<string, int> GetWindowsDisplayNumbers(StringBuilder diagnostics, string displayNumberSource)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
            {
                diagnostics.AppendLine("GetDisplayConfigBufferSizes failed.");
                return result;
            }

            var paths = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];
            if (NativeMethods.QueryDisplayConfig(NativeMethods.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                diagnostics.AppendLine("QueryDisplayConfig failed.");
                return result;
            }

            diagnostics.AppendLine("[QueryDisplayConfig]");
            diagnostics.AppendLine($"pathCount={pathCount}, modeCount={modeCount}");
            diagnostics.AppendLine("[DISPLAYCONFIG_MODE_INFO]");
            for (int i = 0; i < modeCount; i++)
            {
                diagnostics.AppendLine(
                    $"modeIndex={i}, infoType={modes[i].infoType}, id={modes[i].id}, " +
                    $"adapter=({modes[i].adapterId.HighPart},{modes[i].adapterId.LowPart}), " +
                    $"sourceWidth={modes[i].modeInfo.sourceMode.width}, sourceHeight={modes[i].modeInfo.sourceMode.height}, " +
                    $"sourcePixelFormat={modes[i].modeInfo.sourceMode.pixelFormat}, " +
                    $"sourcePosition=({modes[i].modeInfo.sourceMode.position.x},{modes[i].modeInfo.sourceMode.position.y}), " +
                    $"targetActiveSize={modes[i].modeInfo.targetMode.targetVideoSignalInfo.activeSize.cx}x{modes[i].modeInfo.targetMode.targetVideoSignalInfo.activeSize.cy}, " +
                    $"targetTotalSize={modes[i].modeInfo.targetMode.targetVideoSignalInfo.totalSize.cx}x{modes[i].modeInfo.targetMode.targetVideoSignalInfo.totalSize.cy}");
            }

            diagnostics.AppendLine("[DISPLAYCONFIG_PATH_INFO]");
            for (int i = 0; i < pathCount; i++)
            {
                var sourceName = new NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = new NativeMethods.DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                        size = (uint)Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                        adapterId = paths[i].sourceInfo.adapterId,
                        id = paths[i].sourceInfo.id
                    },
                    viewGdiDeviceName = new string('\0', 32)
                };

                string sourceDeviceName = "";
                int? gdiDeviceDisplayNumber = null;
                int selectedDisplayNumber = ResolveDisplayNumber(displayNumberSource, i, paths[i], sourceDeviceName);
                if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceName) == 0)
                {
                    sourceDeviceName = sourceName.viewGdiDeviceName.TrimEnd('\0');
                    gdiDeviceDisplayNumber = TryGetGdiDeviceNumber(sourceDeviceName);
                    selectedDisplayNumber = ResolveDisplayNumber(displayNumberSource, i, paths[i], sourceDeviceName);
                    if (!string.IsNullOrWhiteSpace(sourceDeviceName))
                    {
                        result.TryAdd(sourceDeviceName, selectedDisplayNumber);
                    }
                }

                var targetName = new NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = new NativeMethods.DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                        size = (uint)Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        adapterId = paths[i].targetInfo.adapterId,
                        id = paths[i].targetInfo.id
                    },
                    monitorFriendlyDeviceName = new string('\0', 64),
                    monitorDevicePath = new string('\0', 128)
                };

                string targetFriendlyName = "";
                string targetDevicePath = "";
                uint targetNameFlags = 0;
                uint connectorInstance = 0;
                ushort edidManufactureId = 0;
                ushort edidProductCodeId = 0;
                int targetNameResult = NativeMethods.DisplayConfigGetDeviceInfo(ref targetName);
                if (targetNameResult == 0)
                {
                    targetFriendlyName = targetName.monitorFriendlyDeviceName.TrimEnd('\0');
                    targetDevicePath = targetName.monitorDevicePath.TrimEnd('\0');
                    targetNameFlags = targetName.flags;
                    connectorInstance = targetName.connectorInstance;
                    edidManufactureId = targetName.edidManufactureId;
                    edidProductCodeId = targetName.edidProductCodeId;
                }

                diagnostics.AppendLine(
                    $"pathIndex={i}, sourceAdapter=({paths[i].sourceInfo.adapterId.HighPart},{paths[i].sourceInfo.adapterId.LowPart}), " +
                    $"sourceId={paths[i].sourceInfo.id}, sourceModeInfoIdx={paths[i].sourceInfo.modeInfoIdx}, sourceStatusFlags=0x{paths[i].sourceInfo.statusFlags:X8}, " +
                    $"gdiDevice={sourceDeviceName}, pathOrderDisplayNumber={i + 1}, sourceIdDisplayNumber={(int)paths[i].sourceInfo.id + 1}, " +
                    $"targetIdDisplayNumber={(int)paths[i].targetInfo.id + 1}, gdiDeviceDisplayNumber={gdiDeviceDisplayNumber?.ToString() ?? ""}, " +
                    $"selectedDisplayNumber={selectedDisplayNumber}, " +
                    $"targetAdapter=({paths[i].targetInfo.adapterId.HighPart},{paths[i].targetInfo.adapterId.LowPart}), targetId={paths[i].targetInfo.id}, " +
                    $"targetModeInfoIdx={paths[i].targetInfo.modeInfoIdx}, outputTechnology={paths[i].targetInfo.outputTechnology}, rotation={paths[i].targetInfo.rotation}, " +
                    $"scaling={paths[i].targetInfo.scaling}, refresh={paths[i].targetInfo.refreshRate.Numerator}/{paths[i].targetInfo.refreshRate.Denominator}, " +
                    $"scanLineOrdering={paths[i].targetInfo.scanLineOrdering}, targetAvailable={paths[i].targetInfo.targetAvailable}, " +
                    $"targetStatusFlags=0x{paths[i].targetInfo.statusFlags:X8}, pathFlags=0x{paths[i].flags:X8}, " +
                    $"targetNameResult={targetNameResult}, targetNameFlags=0x{targetNameFlags:X8}, connectorInstance={connectorInstance}, " +
                    $"edidManufactureId=0x{edidManufactureId:X4}, edidProductCodeId=0x{edidProductCodeId:X4}, " +
                    $"monitorFriendlyName={targetFriendlyName}, monitorDevicePath={targetDevicePath}");
            }

            return result;
        }

        private static int ResolveDisplayNumber(string displayNumberSource, int pathIndex, NativeMethods.DISPLAYCONFIG_PATH_INFO path, string sourceDeviceName)
        {
            return displayNumberSource switch
            {
                DisplayNumberSources.PathOrder => pathIndex + 1,
                DisplayNumberSources.TargetId => (int)path.targetInfo.id + 1,
                DisplayNumberSources.GdiDeviceName => TryGetGdiDeviceNumber(sourceDeviceName) ?? pathIndex + 1,
                _ => (int)path.sourceInfo.id + 1
            };
        }

        private static int? TryGetGdiDeviceNumber(string sourceDeviceName)
        {
            const string prefix = @"\\.\DISPLAY";
            if (!sourceDeviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return int.TryParse(sourceDeviceName[prefix.Length..], out int displayNumber)
                ? displayNumber
                : null;
        }

        private static void AppendDiagnosticsHeader(StringBuilder diagnostics, string configuredDisplayNumberSource, string effectiveDisplayNumberSource)
        {
            diagnostics.AppendLine("ScaleSwitcher display diagnostics");
            diagnostics.AppendLine($"timestamp={DateTimeOffset.Now:O}");
            diagnostics.AppendLine($"baseDirectory={AppContext.BaseDirectory}");
            diagnostics.AppendLine($"machineName={Environment.MachineName}");
            diagnostics.AppendLine($"osVersion={Environment.OSVersion}");
            diagnostics.AppendLine($"configuredDisplayNumberSource={configuredDisplayNumberSource}");
            diagnostics.AppendLine($"effectiveDisplayNumberSource={effectiveDisplayNumberSource}");
            diagnostics.AppendLine("effectiveDisplayNumberFormula=DISPLAYCONFIG_PATH_INFO.targetInfo.id + 1");
        }

        private static void AppendWindowsFormsScreenDiagnostics(StringBuilder diagnostics)
        {
            diagnostics.AppendLine();
            diagnostics.AppendLine("[System.Windows.Forms.Screen]");
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                diagnostics.AppendLine(
                    $"device={screen.DeviceName}, primary={screen.Primary}, " +
                    $"bounds=({screen.Bounds.Left},{screen.Bounds.Top})-({screen.Bounds.Right},{screen.Bounds.Bottom}), " +
                    $"workingArea=({screen.WorkingArea.Left},{screen.WorkingArea.Top})-({screen.WorkingArea.Right},{screen.WorkingArea.Bottom}), " +
                    $"bitsPerPixel={screen.BitsPerPixel}");
            }
        }

        private static void WriteDisplayDiagnostics(StringBuilder diagnostics)
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "ScaleSwitcher.DisplayDiagnostics.log");
                File.WriteAllText(path, diagnostics.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Diagnostics must not prevent display enumeration.
            }
        }

        private static void PopulateResolutions(DisplayInfo info)
        {
            var resolutions = new HashSet<ResolutionInfo>();
            var devMode = new NativeMethods.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));

            // Get available
            int modeNum = 0;
            while (NativeMethods.EnumDisplaySettings(info.DeviceName, modeNum, ref devMode))
            {
                resolutions.Add(new ResolutionInfo { Width = devMode.dmPelsWidth, Height = devMode.dmPelsHeight });
                modeNum++;
            }
            
            info.AvailableResolutions = resolutions.OrderByDescending(r => r.Width).ThenByDescending(r => r.Height).ToList();

            // Get current
            if (NativeMethods.EnumDisplaySettings(info.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                info.CurrentResolution = new ResolutionInfo { Width = devMode.dmPelsWidth, Height = devMode.dmPelsHeight };
            }
        }

        private static readonly Dictionary<string, int> RecommendedDpiCache = new();

        private static void PopulateDpis(DisplayInfo info)
        {
            // Get current DPI percentage
            NativeMethods.GetDpiForMonitor(info.MonitorHandle, 0, out uint dpiX, out _);
            int currentPercentage = (int)(dpiX * 100 / 96);

            int recommendedPercentage;
            if (RecommendedDpiCache.TryGetValue(info.DeviceName, out int cachedRecommended))
            {
                recommendedPercentage = cachedRecommended;
            }
            else
            {
                // Get relative index using SPI_GETLOGICALDPIOVERRIDE
                int currentRelativeIndex = 0;
                IntPtr ptr = Marshal.AllocHGlobal(4);
                try
                {
                    if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETLOGICALDPIOVERRIDE, info.MonitorIndex, ptr, 0))
                    {
                        currentRelativeIndex = Marshal.ReadInt32(ptr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                int arrayIndex = Array.IndexOf(DpiArray, currentPercentage);
                if (arrayIndex != -1)
                {
                    int recommendedArrayIndex = arrayIndex - currentRelativeIndex;
                    recommendedArrayIndex = Math.Clamp(recommendedArrayIndex, 0, DpiArray.Length - 1);
                    recommendedPercentage = DpiArray[recommendedArrayIndex];
                    RecommendedDpiCache[info.DeviceName] = recommendedPercentage;
                }
                else
                {
                    recommendedPercentage = currentPercentage; // Fallback
                }
            }

            int recommendedIdx = Array.IndexOf(DpiArray, recommendedPercentage);
            if (recommendedIdx == -1) recommendedIdx = 2; // Fallback to 150%

            int currentIdx = Array.IndexOf(DpiArray, currentPercentage);
            info.CurrentDpi = new DpiInfo
            {
                Percentage = currentPercentage,
                RelativeIndex = currentIdx != -1 ? currentIdx - recommendedIdx : 0
            };

            if (currentIdx == -1)
            {
                info.AvailableDpis.Add(info.CurrentDpi);
                return;
            }

            // Populate available DPIs based on the static recommended index
            for (int i = 0; i < DpiArray.Length; i++)
            {
                int relIndex = i - recommendedIdx;
                // Avoid too extreme negative relatives. Min relative is usually -3. Max is usually 4.
                if (relIndex >= -3 && relIndex <= 4)
                {
                    info.AvailableDpis.Add(new DpiInfo { Percentage = DpiArray[i], RelativeIndex = relIndex });
                }
            }
        }

        public static bool SetResolution(DisplayInfo info, ResolutionInfo res)
        {
            var oldPos = System.Windows.Forms.Cursor.Position;
            var oldScreenObj = System.Windows.Forms.Screen.FromPoint(oldPos);
            var oldScreen = oldScreenObj.Bounds;
            int offsetX = oldScreen.Right - oldPos.X;
            int offsetY = oldScreen.Bottom - oldPos.Y;
            string deviceName = oldScreenObj.DeviceName;

            string oldResStr = info.CurrentResolution != null ? $"{info.CurrentResolution.Width}x{info.CurrentResolution.Height}" : "";
            string newResStr = $"{res.Width}x{res.Height}";
            var osd = ShowOsd($"{oldResStr} → {newResStr}", info);

            var devMode = new NativeMethods.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            if (NativeMethods.EnumDisplaySettings(info.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                devMode.dmPelsWidth = res.Width;
                devMode.dmPelsHeight = res.Height;
                devMode.dmFields = 0x00080000 | 0x00100000; // DM_PELSWIDTH | DM_PELSHEIGHT

                int result = NativeMethods.ChangeDisplaySettingsEx(info.DeviceName, ref devMode, IntPtr.Zero, 0, IntPtr.Zero);
                if (result == NativeMethods.DISP_CHANGE_SUCCESSFUL)
                {
                    // 解像度変更ではDPIによるアイコンのピクセルサイズは変わらないため、そのままのオフセットを渡す
                    RestoreCursorPosition(offsetX, offsetY, deviceName, osd);
                    return true;
                }
            }

            if (osd != null && System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => osd.CloseWithFade());
            }
            return false;
        }

        public static bool SetDpi(DisplayInfo info, DpiInfo dpi)
        {
            var oldPos = System.Windows.Forms.Cursor.Position;
            var oldScreenObj = System.Windows.Forms.Screen.FromPoint(oldPos);
            var oldScreen = oldScreenObj.Bounds;
            int offsetX = oldScreen.Right - oldPos.X;
            int offsetY = oldScreen.Bottom - oldPos.Y;
            string deviceName = oldScreenObj.DeviceName;

            string oldDpiStr = info.CurrentDpi != null ? $"{info.CurrentDpi.Percentage}%" : "";
            string newDpiStr = $"{dpi.Percentage}%";
            var osd = ShowOsd($"{oldDpiStr} → {newDpiStr}", info);

            bool success = NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETLOGICALDPIOVERRIDE, dpi.RelativeIndex, (IntPtr)info.MonitorIndex, 3);
            if (success)
            {
                // スケーリング変更時は、タスクバー等のサイズが変わるためDPIの比率をオフセットにかける
                double ratio = info.CurrentDpi != null && info.CurrentDpi.Percentage > 0 
                                ? (double)dpi.Percentage / info.CurrentDpi.Percentage 
                                : 1.0;
                int newOffsetX = (int)Math.Round(offsetX * ratio);
                int newOffsetY = (int)Math.Round(offsetY * ratio);
                RestoreCursorPosition(newOffsetX, newOffsetY, deviceName, osd);
            }
            else
            {
                if (osd != null && System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => osd.CloseWithFade());
                }
            }
            return success;
        }

        public static void ShowDisplayInfoOsds()
        {
            HideDisplayInfoOsds();

            var displays = GetDisplays();
            foreach (var display in displays)
            {
                var osd = ShowOsd(BuildDisplayInfoMessage(display), display, 30, captureMouse: false, hideCursor: false);
                if (osd != null)
                {
                    DisplayInfoOsds.Add(osd);
                }
            }
        }

        public static void HideDisplayInfoOsds()
        {
            if (DisplayInfoOsds.Count == 0) return;

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var osd in DisplayInfoOsds)
                    {
                        osd.CloseWithFade();
                    }
                    DisplayInfoOsds.Clear();
                });
            }
            else
            {
                DisplayInfoOsds.Clear();
            }
        }

        private static string BuildDisplayInfoMessage(DisplayInfo display)
        {
            string resolution = display.CurrentResolution != null
                ? $"{display.CurrentResolution.Width}x{display.CurrentResolution.Height}"
                : "unknown";
            string dpi = display.CurrentDpi != null ? $"{display.CurrentDpi.Percentage}%" : "unknown";

            return string.Join(Environment.NewLine,
                $"Windows settings display: {display.SettingsDisplayNumber}",
                $"Monitor index: {display.MonitorIndex}",
                $"Device: {display.DeviceName}",
                $"Primary: {display.IsPrimary}",
                $"Resolution: {resolution}",
                $"Scale: {dpi}");
        }

        private static ScaleSwitcher.Views.OsdWindow? ShowOsd(string message, DisplayInfo display)
        {
            return ShowOsd(message, display, 48, captureMouse: true, hideCursor: true);
        }

        private static ScaleSwitcher.Views.OsdWindow? ShowOsd(string message, DisplayInfo display, double fontSize, bool captureMouse, bool hideCursor)
        {
            ScaleSwitcher.Views.OsdWindow? osd = null;
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    osd = new ScaleSwitcher.Views.OsdWindow(message, fontSize, hideCursor);

                    osd.Show();
                    PositionOsdOnDisplay(osd, display);

                    if (captureMouse)
                    {
                        // Cursor="None" を確実に効かせるため、マウスをキャプチャする
                        osd.CaptureMouse();
                    }
                });
            }
            return osd;
        }

        private static void PositionOsdOnDisplay(ScaleSwitcher.Views.OsdWindow osd, DisplayInfo display)
        {
            var mi = new NativeMethods.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));

            NativeMethods.Rect rect;
            if (NativeMethods.GetMonitorInfo(display.MonitorHandle, ref mi))
            {
                rect = mi.rcMonitor;
            }
            else
            {
                var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == display.DeviceName)
                             ?? System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return;

                rect = new NativeMethods.Rect
                {
                    left = screen.Bounds.Left,
                    top = screen.Bounds.Top,
                    right = screen.Bounds.Right,
                    bottom = screen.Bounds.Bottom
                };
            }

            var hwnd = new WindowInteropHelper(osd).Handle;
            if (hwnd == IntPtr.Zero) return;

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                rect.left,
                rect.top,
                rect.right - rect.left,
                rect.bottom - rect.top,
                NativeMethods.SWP_NOACTIVATE);
        }

        private static async void RestoreCursorPosition(int offsetX, int offsetY, string deviceName, ScaleSwitcher.Views.OsdWindow? osd)
        {
            // OSによる画面レイアウトの再構成とマウスの中央リセット処理が完了するのを少し待つ
            await System.Threading.Tasks.Task.Delay(1500);

            var screenObj = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName) 
                         ?? System.Windows.Forms.Screen.PrimaryScreen;

            if (screenObj != null)
            {
                var newScreen = screenObj.Bounds;

                int newX = newScreen.Right - offsetX;
                int newY = newScreen.Bottom - offsetY;

                newX = Math.Max(newScreen.Left, Math.Min(newX, newScreen.Right - 1));
                newY = Math.Max(newScreen.Top, Math.Min(newY, newScreen.Bottom - 1));

                System.Windows.Forms.Cursor.Position = new System.Drawing.Point(newX, newY);
            }

            if (osd != null && System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    osd.CloseWithFade();
                });
            }
        }
    }
}
