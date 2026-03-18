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
    }
}
