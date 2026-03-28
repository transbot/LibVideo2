using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using LibVideo.Helpers;

namespace LibVideo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            if (File.Exists(AppPaths.LanguageFile))
            {
                string lang = File.ReadAllText(AppPaths.LanguageFile).Trim();
                ChangeLanguage(lang);
            }
            else
            {
                // First-time launch: check system locale
                if (!System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    ChangeLanguage("en");
                    File.WriteAllText(AppPaths.LanguageFile, "en");
                }
                else
                {
                    File.WriteAllText(AppPaths.LanguageFile, "zh");
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

        protected override void OnExit(ExitEventArgs e)
        {
            if (MainWindow?.DataContext is IDisposable disposableVm)
            {
                disposableVm.Dispose();
            }
            base.OnExit(e);
        }

        private void LogException(Exception ex)
        {
            Logger.Error(ex, "Unhandled Exception");
        }

        public static string CurrentLanguageCode { get; private set; } = "zh";

        public static void ChangeLanguage(string langCode)
        {
            CurrentLanguageCode = langCode;
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
