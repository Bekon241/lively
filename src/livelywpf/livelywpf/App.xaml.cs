﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
using System.Xml.Schema;

namespace livelywpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static MainWindow AppWindow { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            //wpf app startup
            NLogger.SetupNLog();
            SetupUnhandledExceptionLogging();
            NLogger.LogHardwareInfo();

            try
            {
                //create directories if not exist, eg: C:\Users\<User>\AppData\Local
                Directory.CreateDirectory(Program.AppDataDir);
                Directory.CreateDirectory(Path.Combine(Program.AppDataDir, "logs"));
                Directory.CreateDirectory(Path.Combine(Program.AppDataDir, "temp"));
                //clear temp files if any.
                FileOperations.EmptyDirectory(Path.Combine(Program.AppDataDir, "temp"));
            }
            catch (Exception ex)
            {
                Logger.Error("Temp Directory creation fail:" + ex.ToString());
                MessageBox.Show(ex.Message, "Error: Failed to create data folder", MessageBoxButton.OK, MessageBoxImage.Error);
                Program.ExitApplication();
            }

            #region vm init

            Program.SettingsVM = new SettingsViewModel();
            Program.WallpaperDir = Program.SettingsVM.Settings.WallpaperDir;
            try
            {
                Directory.CreateDirectory(Path.Combine(Program.WallpaperDir, "wallpapers"));
                Directory.CreateDirectory(Path.Combine(Program.WallpaperDir, "SaveData", "wptmp"));
                Directory.CreateDirectory(Path.Combine(Program.WallpaperDir, "SaveData", "wpdata"));
            }
            catch (Exception ex)
            {
                Logger.Error("Wallpaper Directory creation fail:" + ex.ToString());
                MessageBox.Show(ex.Message, "Error: Failed to create wallpaper folder", MessageBoxButton.OK, MessageBoxImage.Error);
                Program.ExitApplication();
            }
            //previous installed appversion is different from current instance.
            if (!Program.SettingsVM.Settings.AppVersion.Equals(Assembly.GetExecutingAssembly().GetName().Version.ToString(), StringComparison.OrdinalIgnoreCase)
                || Program.SettingsVM.Settings.IsFirstRun)
            {
                Program.SettingsVM.Settings.WallpaperBundleVersion = ExtractWallpaperBundle();
                Program.SettingsVM.Settings.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Program.SettingsVM.UpdateConfigFile();
            }

            try
            {
                //"Wallpaper Type" string of libraryitems are localized, so set locale before library vm init.
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(Program.SettingsVM.Settings.Language);
            }
            catch (CultureNotFoundException)
            {
                Logger.Error("Localisation:Culture not found:" + Program.SettingsVM.Settings.Language);
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            }
            Program.AppRulesVM = new ApplicationRulesViewModel();
            Program.LibraryVM = new LibraryViewModel();

            #endregion //vm init

            AppWindow = new MainWindow();
            //uwp root app needs window to show.. is it possible to skip?
            AppWindow.Show();
            if (Program.SettingsVM.Settings.IsRestart)
            {
                Program.SettingsVM.Settings.IsRestart = false;
                Program.SettingsVM.UpdateConfigFile();
            }
            else
            {
                AppWindow.Hide();
            }
            base.OnStartup(e);
        }

        /// <summary>
        /// Extract default wallpapers.
        /// </summary>
        private int ExtractWallpaperBundle()
        {
            //todo: Add a "please wait" page in SetupWizard to indicate extraction in progress.
            int maxExtracted = Program.SettingsVM.Settings.WallpaperBundleVersion;
            try
            {
                //wallpaper bundles filenames are 0.zip, 1.zip ...
                var sortedBundles = Directory.GetFiles(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bundle"))
                    .OrderBy(x => x);

                foreach (var item in sortedBundles)
                {
                    if(int.TryParse(Path.GetFileNameWithoutExtension(item), out int val))
                    {
                        if (val > maxExtracted)
                        {
                            //Sharpzip library will overwrite files if exists during extraction.
                            ZipExtract.ZipExtractFile(item, Path.Combine(Program.WallpaperDir, "wallpapers"), false);
                            maxExtracted = val;
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Logger.Error("Base Wallpaper Extract Fail:" + e.ToString());
            }
            return maxExtracted;
        }

        private void SetupUnhandledExceptionLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            Dispatcher.UnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                message = string.Format("Unhandled exception in {0} v{1}", assemblyName.Name, assemblyName.Version);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception in LogUnhandledException");
            }
            finally
            {
                Logger.Error(message + "\n" + exception.ToString());
            }
        }
    }
}
