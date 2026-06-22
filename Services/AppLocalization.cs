using System;
using System.Globalization;

namespace ScaleSwitcher.Services
{
    public enum UiLanguage
    {
        Japanese,
        English
    }

    public sealed class AppLocalization
    {
        private readonly UiLanguage _language;

        public static AppLocalization Instance { get; } = new AppLocalization();

        private AppLocalization()
        {
            _language = ResolveFromSystemCulture(CultureInfo.CurrentUICulture);
        }

        public string DisplayPrefix => _language switch
        {
            UiLanguage.English => "Display",
            _ => "ディスプレイ"
        };

        public string Menu_Scale => _language switch
        {
            UiLanguage.English => "Scale",
            _ => "スケーリング"
        };

        public string Menu_Resolution => _language switch
        {
            UiLanguage.English => "Resolution",
            _ => "解像度"
        };

        public string Menu_RunAtStartup => _language switch
        {
            UiLanguage.English => "Run at Windows startup",
            _ => "Windows起動時に実行"
        };

        public string Menu_Settings => _language switch
        {
            UiLanguage.English => "Settings...",
            _ => "設定..."
        };

        public string Menu_Exit => _language switch
        {
            UiLanguage.English => "Exit",
            _ => "終了"
        };

        public string Settings_Title => _language switch
        {
            UiLanguage.English => "ScaleSwitcher Settings",
            _ => "ScaleSwitcher 設定"
        };

        public string Settings_TargetDisplay => _language switch
        {
            UiLanguage.English => "Target Display (Left Click):",
            _ => "対象のディスプレイ (左クリック時):"
        };

        public string Settings_Scales => _language switch
        {
            UiLanguage.English => "Available Scales for Rotation:",
            _ => "ローテーションに含めるスケーリング:"
        };

        public string Settings_DisplayNumberSource => _language switch
        {
            UiLanguage.English => "Display Number Source:",
            _ => "ディスプレイ番号の取得元:"
        };

        public string DisplayNumberSource_PathOrder => _language switch
        {
            UiLanguage.English => "pathOrderDisplayNumber",
            _ => "pathOrderDisplayNumber"
        };

        public string DisplayNumberSource_SourceId => _language switch
        {
            UiLanguage.English => "sourceIdDisplayNumber",
            _ => "sourceIdDisplayNumber"
        };

        public string DisplayNumberSource_TargetId => _language switch
        {
            UiLanguage.English => "targetIdDisplayNumber",
            _ => "targetIdDisplayNumber"
        };

        public string DisplayNumberSource_GdiDeviceName => _language switch
        {
            UiLanguage.English => "gdiDeviceDisplayNumber",
            _ => "gdiDeviceDisplayNumber"
        };

        public string Settings_Save => _language switch
        {
            UiLanguage.English => "Save",
            _ => "保存"
        };

        private static UiLanguage ResolveFromSystemCulture(CultureInfo culture)
        {
            for (var current = culture; current != CultureInfo.InvariantCulture; current = current.Parent)
            {
                if (string.Equals(current.TwoLetterISOLanguageName, "ja", StringComparison.OrdinalIgnoreCase))
                {
                    return UiLanguage.Japanese;
                }
                if (string.Equals(current.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
                {
                    return UiLanguage.English;
                }
            }
            return UiLanguage.English;
        }
    }
}
