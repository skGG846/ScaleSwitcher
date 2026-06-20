using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ScaleSwitcher
{
    public class ScaleOption
    {
        public int Percentage { get; set; }
        public bool IsSelected { get; set; }
        public string DisplayText => $"{Percentage}%";
    }

    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;
        private List<DisplayInfo> _displays;
        private List<ScaleOption> _currentOptions = new();

        public SettingsWindow()
        {
            InitializeComponent();
            ApplyLocalization();
            
            _settings = SettingsManager.Load();
            _displays = DisplayManager.GetDisplays();

            PopulateDisplays();
        }

        private void ApplyLocalization()
        {
            Title = Properties.Resources.Settings_Title;
            TargetDisplayLabel.Text = Properties.Resources.Settings_TargetDisplay;
            ScalesLabel.Text = Properties.Resources.Settings_Scales;
            SaveButton.Content = Properties.Resources.Settings_Save;
        }

        private void PopulateDisplays()
        {
            DisplayComboBox.Items.Clear();
            int selectedIndex = 0;
            
            for (int i = 0; i < _displays.Count; i++)
            {
                var d = _displays[i];
                string name = $"{Properties.Resources.DisplayPrefix} {i + 1}";
                if (d.IsPrimary) name += " (Primary)";
                
                DisplayComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = d });
                
                if (i == _settings.TargetMonitorIndex)
                {
                    selectedIndex = i;
                }
            }

            if (DisplayComboBox.Items.Count > 0)
            {
                if (selectedIndex >= DisplayComboBox.Items.Count) selectedIndex = 0;
                DisplayComboBox.SelectedIndex = selectedIndex;
            }
        }

        private void DisplayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DisplayComboBox.SelectedItem is ComboBoxItem item && item.Tag is DisplayInfo display)
            {
                PopulateScales(display);
            }
        }

        private void PopulateScales(DisplayInfo display)
        {
            _currentOptions.Clear();
            
            // Sort dpis ascending
            var sortedDpis = display.AvailableDpis.OrderBy(d => d.Percentage).ToList();
            
            foreach (var dpi in sortedDpis)
            {
                bool isSelected = _settings.ActiveDpiPercentages.Contains(dpi.Percentage);
                
                // If settings are empty, default to 100% and 200% (or closest available if they were requested as default)
                if (_settings.ActiveDpiPercentages.Count == 0 && (dpi.Percentage == 100 || dpi.Percentage == 200))
                {
                    isSelected = true;
                }

                _currentOptions.Add(new ScaleOption
                {
                    Percentage = dpi.Percentage,
                    IsSelected = isSelected
                });
            }

            ScalesItemsControl.ItemsSource = null;
            ScalesItemsControl.ItemsSource = _currentOptions;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.TargetMonitorIndex = DisplayComboBox.SelectedIndex;
            _settings.ActiveDpiPercentages = _currentOptions.Where(o => o.IsSelected).Select(o => o.Percentage).ToList();
            
            SettingsManager.Save(_settings);
            
            DialogResult = true;
            Close();
        }
    }
}
