using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ScaleSwitcher.Models
{
    public static class DisplayManager
    {
        private static readonly int[] DpiArray = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

        public static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();
            var settingsDisplayNumbers = GetSettingsDisplayNumbers();
            int index = 0;

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
                    index++;
                }
                return true;
            }, IntPtr.Zero);

            return displays;
        }

        private static Dictionary<string, int> GetSettingsDisplayNumbers()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
            {
                return result;
            }

            var paths = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];
            if (NativeMethods.QueryDisplayConfig(NativeMethods.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
            {
                return result;
            }

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

                if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceName) == 0)
                {
                    string sourceDeviceName = sourceName.viewGdiDeviceName.TrimEnd('\0');
                    if (!string.IsNullOrWhiteSpace(sourceDeviceName))
                    {
                        result.TryAdd(sourceDeviceName, i + 1);
                    }
                }
            }

            return result;
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
            var osd = ShowOsd($"{oldResStr} → {newResStr}");

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
            var osd = ShowOsd($"{oldDpiStr} → {newDpiStr}");

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

        private static ScaleSwitcher.Views.OsdWindow? ShowOsd(string message)
        {
            ScaleSwitcher.Views.OsdWindow? osd = null;
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    osd = new ScaleSwitcher.Views.OsdWindow(message);
                    
                    // 仮想スクリーン全体を覆うようにする
                    var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
                    
                    osd.Left = virtualScreen.Left;
                    osd.Top = virtualScreen.Top;
                    osd.Width = virtualScreen.Width;
                    osd.Height = virtualScreen.Height;

                    osd.Show();
                    
                    // Cursor="None" を確実に効かせるため、マウスをキャプチャする
                    osd.CaptureMouse();
                });
            }
            return osd;
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
