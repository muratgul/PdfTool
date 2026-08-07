using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace PdfTool.Services
{
    public static class LanguageManager
    {
        private static string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language.txt");

        public static string CurrentLanguage { get; private set; } // "tr" or "en"

        public static void InitializeLanguage()
        {
            if (File.Exists(SettingsFilePath))
            {
                var val = File.ReadAllText(SettingsFilePath).Trim().ToLower();
                CurrentLanguage = (val == "en") ? "en" : "tr";
            }
            else
            {
                CurrentLanguage = "tr";
            }
            ApplyLanguage();
        }

        public static void ToggleLanguage()
        {
            CurrentLanguage = (CurrentLanguage == "tr") ? "en" : "tr";
            File.WriteAllText(SettingsFilePath, CurrentLanguage);
            ApplyLanguage();
        }

        private static void ApplyLanguage()
        {
            var app = System.Windows.Application.Current;
            var uri = new Uri(CurrentLanguage == "tr" ? "Languages/tr.xaml" : "Languages/en.xaml", UriKind.Relative);
            
            var dict = new ResourceDictionary() { Source = uri };
            
            // We shouldn't clear, we should find the existing language dictionary and replace it or just add the new one.
            // Since we know the language dictionaries are loaded from "Languages/...", we can find and replace.
            var existingDict = app.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.StartsWith("Languages/"));
            
            if (existingDict != null)
            {
                app.Resources.MergedDictionaries.Remove(existingDict);
            }
            
            app.Resources.MergedDictionaries.Add(dict);
        }
    }
}
