using System;
using System.Collections.Generic;

namespace ScaleSwitcher.Models
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
        public int SettingsDisplayNumber { get; set; }
        public IntPtr MonitorHandle { get; set; }
        public string DeviceName { get; set; } = "";
        public bool IsPrimary { get; set; }
        
        public List<ResolutionInfo> AvailableResolutions { get; set; } = new();
        public ResolutionInfo? CurrentResolution { get; set; }
        
        public List<DpiInfo> AvailableDpis { get; set; } = new();
        public DpiInfo? CurrentDpi { get; set; }
    }
}
