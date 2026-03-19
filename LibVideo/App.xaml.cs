using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;

namespace LibVideo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            if (File.Exists("language.txt"))
            {
                string lang = File.ReadAllText("language.txt").Trim();
                ChangeLanguage(lang);
            }
            else
            {
                // First-time launch: check system locale
                if (!System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    ChangeLanguage("en");
                    File.WriteAllText("language.txt", "en");
                }
                else
                {
                    File.WriteAllText("language.txt", "zh");
                }
            }
            
            AppDomain.CurrentDomain.UnhandledException += (s, args) => 
                LogException(args.ExceptionObject as Exception);
            
            DispatcherUnhandledException += (s, args) => 
            {
                LogException(args.Exception);
                args.Handled = true;
                Environment.Exit(1);
            };
            
            TaskScheduler.UnobservedTaskException += (s, args) => 
                LogException(args.Exception);
        }

        private void LogException(Exception ex)
        {
            if (ex == null) return;
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] {ex.ToString()}\n\n");
            }
            catch { }
        }

        public static void ChangeLanguage(string langCode)
        {
            var dictionary = new ResourceDictionary();
            if (langCode == "en")
            {
                dictionary.Source = new System.Uri("Resources/Strings.en.xaml", System.UriKind.Relative);
            }
            else
            {
                dictionary.Source = new System.Uri("Resources/Strings.xaml", System.UriKind.Relative);
            }

            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary oldDict = null;
            foreach (var dict in mergedDicts)
            {
                if (dict.Source != null && dict.Source.OriginalString.Contains("Strings"))
                {
                    oldDict = dict;
                    break;
                }
            }

            if (oldDict != null)
            {
                mergedDicts.Remove(oldDict);
                mergedDicts.Add(dictionary);
            }
            else
            {
                mergedDicts.Add(dictionary); // If it wasn't pre-loaded in App.xaml during some race conditions
            }
        }
    }
}
