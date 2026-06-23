using System.Collections.Generic;

namespace ScaleSwitcher.Models
{
    public static class DisplayNumberSources
    {
        public const string PathOrder = "PathOrder";
        public const string SourceId = "SourceId";
        public const string TargetId = "TargetId";
        public const string GdiDeviceName = "GdiDeviceName";

        public static readonly string[] All =
        {
            PathOrder,
            SourceId,
            TargetId,
            GdiDeviceName
        };
    }

    public class AppSettings
    {
        public int TargetMonitorIndex { get; set; } = 0;
        public List<int> ActiveDpiPercentages { get; set; } = new() { 100, 200 };
        public string DisplayNumberSource { get; set; } = DisplayNumberSources.TargetId;
    }
}
