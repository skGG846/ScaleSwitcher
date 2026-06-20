using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ScaleSwitcher
{
    public class ResolutionInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        
        public override string ToString() => $"{Width} x {Height}";
        public override bool Equals(object? obj) => obj is ResolutionInfo r && r.Width == Width && r.Height == Height;
        public override int GetHashCode() => HashCode.Combine(Width, Height);
    }

    public class DpiInfo
    {
        public int Percentage { get; set; }
        public int RelativeIndex { get; set; }
        
        public override string ToString() => $"{Percentage}%";
    }

    public class DisplayInfo
    {
        public int MonitorIndex { get; set; }
        public IntPtr MonitorHandle { get; set; }
        public string DeviceName { get; set; } = "";
        public bool IsPrimary { get; set; }
        
        public List<ResolutionInfo> AvailableResolutions { get; set; } = new();
        public ResolutionInfo? CurrentResolution { get; set; }
        
        public List<DpiInfo> AvailableDpis { get; set; } = new();
        public DpiInfo? CurrentDpi { get; set; }
    }

    public static class DisplayManager
    {
        private static readonly int[] DpiArray = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

        public static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();
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
            var devMode = new NativeMethods.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(NativeMethods.DEVMODE));
            if (NativeMethods.EnumDisplaySettings(info.DeviceName, NativeMethods.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                devMode.dmPelsWidth = res.Width;
                devMode.dmPelsHeight = res.Height;
                devMode.dmFields = 0x00080000 | 0x00100000; // DM_PELSWIDTH | DM_PELSHEIGHT

                int result = NativeMethods.ChangeDisplaySettingsEx(info.DeviceName, ref devMode, IntPtr.Zero, 0, IntPtr.Zero);
                return result == NativeMethods.DISP_CHANGE_SUCCESSFUL;
            }
            return false;
        }

        public static bool SetDpi(DisplayInfo info, DpiInfo dpi)
        {
            return NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETLOGICALDPIOVERRIDE, dpi.RelativeIndex, (IntPtr)info.MonitorIndex, 1);
        }
    }
}
