using System.Reflection;
using System.Resources;

namespace ScaleSwitcher.Properties
{
    internal class Resources
    {
        private static ResourceManager? _resourceManager;

        public static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("ScaleSwitcher.Properties.Resources", Assembly.GetExecutingAssembly());
                }
                return _resourceManager;
            }
        }

        public static string Menu_Scale => ResourceManager.GetString("Menu_Scale") ?? "Scale";
        public static string Menu_Resolution => ResourceManager.GetString("Menu_Resolution") ?? "Resolution";
        public static string Menu_RunAtStartup => ResourceManager.GetString("Menu_RunAtStartup") ?? "Run at Startup";
        public static string Menu_Settings => ResourceManager.GetString("Menu_Settings") ?? "Settings";
        public static string Menu_Exit => ResourceManager.GetString("Menu_Exit") ?? "Exit";
        public static string Settings_Title => ResourceManager.GetString("Settings_Title") ?? "Settings";
        public static string Settings_TargetDisplay => ResourceManager.GetString("Settings_TargetDisplay") ?? "Target Display:";
        public static string Settings_Scales => ResourceManager.GetString("Settings_Scales") ?? "Available Scales:";
        public static string Settings_Save => ResourceManager.GetString("Settings_Save") ?? "Save";
        public static string DisplayPrefix => ResourceManager.GetString("DisplayPrefix") ?? "Display";
    }
}
