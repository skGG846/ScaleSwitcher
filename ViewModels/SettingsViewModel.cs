using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ScaleSwitcher.Models;
using ScaleSwitcher.Services;

namespace ScaleSwitcher.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();
    }

    public class ScaleOptionViewModel : ViewModelBase
    {
        private bool _isSelected;

        public int Percentage { get; set; }
        public string DisplayText => $"{Percentage}%";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class DisplayItemViewModel
    {
        public DisplayInfo Display { get; }
        public string DisplayName { get; }
        public int Index { get; }

        public DisplayItemViewModel(DisplayInfo display, int index)
        {
            Display = display;
            Index = index;
            
            string name = $"{AppLocalization.Instance.DisplayPrefix} {display.SettingsDisplayNumber}";
            if (display.IsPrimary) name += " (Primary)";
            DisplayName = name;
        }
    }

    public class DisplayNumberSourceOptionViewModel
    {
        public string Value { get; }
        public string DisplayText { get; }

        public DisplayNumberSourceOptionViewModel(string value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }
    }

    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppSettings _settings;
        private readonly List<DisplayInfo> _rawDisplays;
        private DisplayItemViewModel? _selectedDisplay;
        private DisplayNumberSourceOptionViewModel? _selectedDisplayNumberSource;
        private ObservableCollection<ScaleOptionViewModel> _scaleOptions = new();

        public event Action<bool>? RequestClose;

        public string Title => AppLocalization.Instance.Settings_Title;
        public string TargetDisplayHeader => AppLocalization.Instance.Settings_TargetDisplay;
        public string ScalesHeader => AppLocalization.Instance.Settings_Scales;
        public string DisplayNumberSourceHeader => AppLocalization.Instance.Settings_DisplayNumberSource;
        public string SaveButtonText => AppLocalization.Instance.Settings_Save;

        public List<DisplayItemViewModel> Displays { get; }
        public List<DisplayNumberSourceOptionViewModel> DisplayNumberSources { get; }
        public ICommand SaveCommand { get; }

        public DisplayItemViewModel? SelectedDisplay
        {
            get => _selectedDisplay;
            set
            {
                if (SetProperty(ref _selectedDisplay, value))
                {
                    PopulateScales(value?.Display);
                }
            }
        }

        public ObservableCollection<ScaleOptionViewModel> ScaleOptions
        {
            get => _scaleOptions;
            set => SetProperty(ref _scaleOptions, value);
        }

        public DisplayNumberSourceOptionViewModel? SelectedDisplayNumberSource
        {
            get => _selectedDisplayNumberSource;
            set => SetProperty(ref _selectedDisplayNumberSource, value);
        }

        public SettingsViewModel()
        {
            _settings = SettingsManager.Load();
            _rawDisplays = DisplayManager.GetDisplays();

            Displays = _rawDisplays.Select((d, i) => new DisplayItemViewModel(d, i)).ToList();
            DisplayNumberSources = CreateDisplayNumberSourceOptions();
            SaveCommand = new RelayCommand(Save);

            // Select default target display
            int selectedIndex = _settings.TargetMonitorIndex;
            if (selectedIndex < 0 || selectedIndex >= Displays.Count)
            {
                selectedIndex = 0;
            }
            if (Displays.Count > 0)
            {
                SelectedDisplay = Displays[selectedIndex];
            }

            SelectedDisplayNumberSource = DisplayNumberSources.FirstOrDefault(o => o.Value == _settings.DisplayNumberSource)
                                          ?? DisplayNumberSources.First(o => o.Value == ScaleSwitcher.Models.DisplayNumberSources.SourceId);
        }

        private static List<DisplayNumberSourceOptionViewModel> CreateDisplayNumberSourceOptions()
        {
            var localization = AppLocalization.Instance;
            return new List<DisplayNumberSourceOptionViewModel>
            {
                new(ScaleSwitcher.Models.DisplayNumberSources.PathOrder, localization.DisplayNumberSource_PathOrder),
                new(ScaleSwitcher.Models.DisplayNumberSources.SourceId, localization.DisplayNumberSource_SourceId),
                new(ScaleSwitcher.Models.DisplayNumberSources.TargetId, localization.DisplayNumberSource_TargetId),
                new(ScaleSwitcher.Models.DisplayNumberSources.GdiDeviceName, localization.DisplayNumberSource_GdiDeviceName)
            };
        }

        private void PopulateScales(DisplayInfo? display)
        {
            ScaleOptions.Clear();
            if (display == null) return;

            var sortedDpis = display.AvailableDpis.OrderBy(d => d.Percentage).ToList();
            foreach (var dpi in sortedDpis)
            {
                bool isSelected = _settings.ActiveDpiPercentages.Contains(dpi.Percentage);
                if (_settings.ActiveDpiPercentages.Count == 0 && (dpi.Percentage == 100 || dpi.Percentage == 200))
                {
                    isSelected = true;
                }

                ScaleOptions.Add(new ScaleOptionViewModel
                {
                    Percentage = dpi.Percentage,
                    IsSelected = isSelected
                });
            }
        }

        private void Save()
        {
            if (SelectedDisplay != null)
            {
                _settings.TargetMonitorIndex = SelectedDisplay.Index;
            }
            _settings.ActiveDpiPercentages = ScaleOptions
                .Where(o => o.IsSelected)
                .Select(o => o.Percentage)
                .ToList();
            if (SelectedDisplayNumberSource != null)
            {
                _settings.DisplayNumberSource = SelectedDisplayNumberSource.Value;
            }

            SettingsManager.Save(_settings);
            RequestClose?.Invoke(true);
        }
    }
}
