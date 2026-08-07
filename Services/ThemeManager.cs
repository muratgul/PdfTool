using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace PdfTool.Services
{
    public static class ThemeManager
    {
        private static string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.txt");

        public static bool IsDarkTheme { get; private set; }

        public static void InitializeTheme()
        {
            if (File.Exists(SettingsFilePath))
            {
                var val = File.ReadAllText(SettingsFilePath).Trim().ToLower();
                IsDarkTheme = val == "dark";
            }
            else
            {
                IsDarkTheme = false;
            }
            ApplyTheme();
        }

        public static void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            File.WriteAllText(SettingsFilePath, IsDarkTheme ? "dark" : "light");
            ApplyTheme();
        }

        private static void ApplyTheme()
        {
            var app = System.Windows.Application.Current;
            var uri = new Uri(IsDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);
            
            var dict = new ResourceDictionary() { Source = uri };
            
            var existingDict = app.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.StartsWith("Themes/"));
            if (existingDict != null)
            {
                app.Resources.MergedDictionaries.Remove(existingDict);
            }
            
            app.Resources.MergedDictionaries.Add(dict);
        }
    }
}
