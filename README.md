# ScaleSwitcher

[日本語はこちら](./README.ja.md)

ScaleSwitcher is a lightweight Windows system tray utility built with WPF and .NET 10. It allows you to quickly change display scaling (DPI) and screen resolutions for multiple monitors.

![ScaleSwitcher demo](./ScaleSwitcher.png)

## Features

- **Left-Click to Cycle Scaling**: Instantly cycle through predefined scaling factors (e.g., 100% -> 150% -> 100%) on a selected display with a single click.
- **Dynamic Context Menu (Right-Click)**:
  - Supports multiple monitors dynamically.
  - Submenus for changing scaling factor (DPI) per monitor.
  - Submenus for changing screen resolution per monitor.
  - Toggle "Run at Startup" to launch the app automatically on Windows login.
- **Settings Window**:
  - Select the target display for left-click rotation.
  - Configure which scaling percentages are included in the left-click rotation cycle.
- **Localization**: Displays in Japanese on Japanese OS environments, and defaults to English on others.
- **DPI Aware**: Native DPI Awareness (`PerMonitorV2`) ensures accurate display information detection.

## System Requirements

- Windows 11
- .NET 10.0 Runtime (WPF enabled)

## Installation & Running

Clone or copy the project files to your local drive and build/run using the dotnet CLI.

### Run the Application
```bash
dotnet run
```

### Build the Project
```bash
dotnet build
```

### Release Build
```bash
dotnet build -c Release
```

The compiled binary will be located in: `bin/Release/net10.0-windows/ScaleSwitcher.exe`.

## Configuration Path

User settings are saved in JSON format at:
```
%LOCALAPPDATA%\ScaleSwitcher\settings.json
```

## Technologies Used

- C# / WPF (.NET 10)
- Win32 APIs via P/Invoke (`user32.dll`, `shcore.dll`)
- Native Windows DPI Awareness configurations
- Windows Forms `NotifyIcon` (no third-party dependencies)
