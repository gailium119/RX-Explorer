﻿using CommandLine;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json;
using RX_Explorer.Class;
using RX_Explorer.View;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.System.Power;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer
{
    sealed partial class App : Application
    {
        private bool IsInBackgroundMode;

        public App()
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Suspending += App_Suspending;
            UnhandledException += App_UnhandledException;
            EnteredBackground += App_EnteredBackground;
            LeavingBackground += App_LeavingBackground;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            MemoryManager.AppMemoryUsageIncreased += MemoryManager_AppMemoryUsageIncreased;
            MemoryManager.AppMemoryUsageLimitChanging += MemoryManager_AppMemoryUsageLimitChanging;
            PowerManager.EnergySaverStatusChanged += PowerManager_EnergySaverStatusChanged;
            PowerManager.PowerSupplyStatusChanged += PowerManager_PowerSupplyStatusChanged;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void PowerManager_PowerSupplyStatusChanged(object sender, object e)
        {
            SendActivateToast();
        }

        private void PowerManager_EnergySaverStatusChanged(object sender, object e)
        {
            SendActivateToast();
        }

        protected override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            TitleBar.ButtonBackgroundColor = Colors.Transparent;
            TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
        }

        private void SendActivateToast()
        {
            if (IsInBackgroundMode
                && (AuxiliaryTrustProcessController.IsAnyCommandExecutingInAllControllers
                    || GeneralTransformer.IsAnyTransformTaskRunning
                    || QueueTaskController.IsAnyTaskRunningInController))
            {
                try
                {
                    ToastNotificationManager.History.Remove("EnterBackgroundTips");

                    if (PowerManager.EnergySaverStatus == EnergySaverStatus.On)
                    {
                        ToastContentBuilder Builder = new ToastContentBuilder()
                                                      .SetToastScenario(ToastScenario.Reminder)
                                                      .AddToastActivationInfo("EnterBackgroundTips", ToastActivationType.Foreground)
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_1"))
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_2"))
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_4"))
                                                      .AddButton(new ToastButton(Globalization.GetString("Toast_EnterBackground_ActionButton"), "EnterBackgroundTips"))
                                                      .AddButton(new ToastButtonDismiss(Globalization.GetString("Toast_EnterBackground_Dismiss")));

                        ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml())
                        {
                            Tag = "EnterBackgroundTips",
                            Priority = ToastNotificationPriority.High
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Toast notification could not be sent");
                }
            }
        }

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            try
            {
                LogTracer.MakeSureLogIsFlushed(2000);
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }

                Debug.WriteLine($"A exception was threw when suspending, message: {ex.Message}");
#endif
            }
        }

        private void MemoryManager_AppMemoryUsageLimitChanging(object sender, AppMemoryUsageLimitChangingEventArgs e)
        {
            if (IsInBackgroundMode)
            {
                if (MemoryManager.AppMemoryUsage >= e.NewLimit)
                {
                    ReduceMemoryUsage();
                }
            }
        }

        private void MemoryManager_AppMemoryUsageIncreased(object sender, object e)
        {
            if (IsInBackgroundMode)
            {
                if (MemoryManager.AppMemoryUsageLevel is AppMemoryUsageLevel.OverLimit or AppMemoryUsageLevel.High)
                {
                    ReduceMemoryUsage();
                }
            }
        }

        private void ReduceMemoryUsage()
        {
            GC.Collect();
        }

        private void App_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            IsInBackgroundMode = false;
        }

        private void App_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            IsInBackgroundMode = true;
        }

        private async void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Exception UnhandledException = e.Exception;
            LogTracer.Log(UnhandledException, "UnhandledException");
            LogTracer.MakeSureLogIsFlushed(2000);

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Window.Current.Content is Frame RootFrame)
                {
                    RootFrame.Navigate(typeof(BlueScreen), UnhandledException);
                }
                else
                {
                    Frame Frame = new Frame();
                    Window.Current.Content = Frame;
                    Frame.Navigate(typeof(BlueScreen), UnhandledException);
                }
            });
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogTracer.Log(ex, "UnhandledException");
                LogTracer.MakeSureLogIsFlushed(2000);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();

            Exception UnhandledException = e.Exception;
            LogTracer.Log(UnhandledException, "UnobservedException");
            LogTracer.MakeSureLogIsFlushed(2000);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await OnLaunchOrOnActivate(e);
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await OnLaunchOrOnActivate(args);
        }

        private async Task OnLaunchOrOnActivate(IActivatedEventArgs args)
        {
            try
            {
                if (args is not ToastNotificationActivatedEventArgs)
                {
                    Globalization.Initialize();
                    FontFamilyController.Initialize();
                    SystemInformation.Instance.TrackAppUse(args);

                    if (AppInstance.GetInstances().Count == 1)
                    {
                        TaskBarController.SetBadge(0);
                    }

                    Parser ArguementParser = new Parser((With) =>
                    {
                        With.AutoHelp = true;
                        With.CaseInsensitiveEnumValues = true;
                        With.IgnoreUnknownArguments = true;
                        With.CaseSensitive = true;
                    });

                    switch (args)
                    {
                        case LaunchActivatedEventArgs:
                        case CommandLineActivatedEventArgs:
                            {
                                IEnumerable<string> Arguments = args switch
                                {
                                    LaunchActivatedEventArgs LaunchArgs => Regex.Matches(LaunchArgs.Arguments, @"[\""].+?[\""]|[^ ]+").Select((Match) => Match.Value.Trim('"').TrimEnd('\\')),
                                    CommandLineActivatedEventArgs CmdArgs => Regex.Matches(CmdArgs.Operation.Arguments, @"[\""].+?[\""]|[^ ]+").Skip(1).Select((Match) => Match.Value.Trim('"').TrimEnd('\\')).Select((Path) => Path == "." ? CmdArgs.Operation.CurrentDirectoryPath : Path),
                                    _ => throw new NotSupportedException()
                                };

                                await ArguementParser.ParseArguments<LaunchArguementOptions>(Arguments)
                                                     .WithNotParsed((ErrorList) =>
                                                     {
                                                         if (ErrorList.All(e => e.Tag is not ErrorType.HelpRequestedError and not ErrorType.VersionRequestedError))
                                                         {
                                                             LogTracer.Log($"Startup arguments parsing failed, reason: {string.Join('|', ErrorList.Select((Error) => Enum.GetName(typeof(ErrorType), Error.Tag)))}");
                                                         }

                                                         Window.Current.Content = new ExtendedSplash(args.SplashScreen);
                                                     })
                                                     .WithParsedAsync(async (Options) =>
                                                     {
                                                         IEnumerable<string[]> OpenPathListOnEachTab = Enumerable.Empty<string[]>();

                                                         switch (Options.RecoveryReason)
                                                         {
                                                             case RecoveryReason.Crash when !string.IsNullOrEmpty(Options.RecoveryData):
                                                                 {
                                                                     ToastContentBuilder Builder = new ToastContentBuilder()
                                                                                                       .SetToastScenario(ToastScenario.Reminder)
                                                                                                       .AddToastActivationInfo("RecoveryRestartTips", ToastActivationType.Foreground)
                                                                                                       .AddText(Globalization.GetString("Toast_Restart_Common_Text_1"))
                                                                                                       .AddText(Globalization.GetString("Toast_Restart_Crash_Text"))
                                                                                                       .AddText(Globalization.GetString("Toast_Restart_Common_Text_2"));

                                                                     ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml())
                                                                     {
                                                                         Tag = "RecoveryRestartTips",
                                                                         Priority = ToastNotificationPriority.Default
                                                                     });

                                                                     OpenPathListOnEachTab = JsonConvert.DeserializeObject<IEnumerable<string[]>>(Encoding.UTF8.GetString(Convert.FromBase64String(Options.RecoveryData)));

                                                                     break;
                                                                 }
                                                             case RecoveryReason.Freeze when !string.IsNullOrEmpty(Options.RecoveryData):
                                                                 {
                                                                     ToastContentBuilder Builder = new ToastContentBuilder()
                                                                                                       .SetToastScenario(ToastScenario.Reminder)
                                                                                                       .AddToastActivationInfo("RecoveryRestartTips", ToastActivationType.Foreground)
                                                                                                       .AddText(Globalization.GetString("Toast_Restart_Common_Text_1"))
                                                                                                       .AddText(Globalization.GetString("Toast_Restart_Hang_Text"))
                                                                                                       .AddText(Globalization.GetString("Toast_Restart_Common_Text_2"));

                                                                     ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml())
                                                                     {
                                                                         Tag = "RecoveryRestartTips",
                                                                         Priority = ToastNotificationPriority.Default
                                                                     });

                                                                     OpenPathListOnEachTab = JsonConvert.DeserializeObject<IEnumerable<string[]>>(Encoding.UTF8.GetString(Convert.FromBase64String(Options.RecoveryData)));

                                                                     break;
                                                                 }
                                                             case RecoveryReason.Restart when !string.IsNullOrEmpty(Options.RecoveryData):
                                                                 {
                                                                     OpenPathListOnEachTab = JsonConvert.DeserializeObject<IEnumerable<string[]>>(Encoding.UTF8.GetString(Convert.FromBase64String(Options.RecoveryData)));

                                                                     break;
                                                                 }
                                                             default:
                                                                 {
                                                                     OpenPathListOnEachTab = Options.PathList.Where((Value) => !string.IsNullOrWhiteSpace(Value)).SkipWhile((Value) => Regex.IsMatch(Value, @"^::\{[A-Za-z0-9-]{36}\}$")).Select((Path) => new string[] { Path });

                                                                     break;
                                                                 }
                                                         }

                                                         if (Window.Current.Content is Frame Frame)
                                                         {
                                                             if (Frame.Content is MainPage Main && Main.NavFrame.Content is TabViewContainer TabContainer)
                                                             {
                                                                 if (OpenPathListOnEachTab.Any())
                                                                 {
                                                                     await TabContainer.CreateNewTabAsync(OpenPathListOnEachTab);
                                                                 }
                                                                 else
                                                                 {
                                                                     await TabContainer.CreateNewTabAsync();
                                                                 }
                                                             }
                                                         }
                                                         else
                                                         {
                                                             if (OpenPathListOnEachTab.Any())
                                                             {
                                                                 Window.Current.Content = new ExtendedSplash(args.SplashScreen, OpenPathListOnEachTab.ToArray());
                                                             }
                                                             else
                                                             {
                                                                 await LaunchWithStartupMode(args);
                                                             }
                                                         }
                                                     });

                                break;
                            }
                        case ProtocolActivatedEventArgs ProtocalArgs:
                            {
                                if (string.IsNullOrWhiteSpace(ProtocalArgs.Uri.AbsolutePath))
                                {
                                    Window.Current.Content = new ExtendedSplash(ProtocalArgs.SplashScreen);
                                }
                                else
                                {
                                    Window.Current.Content = new ExtendedSplash(ProtocalArgs.SplashScreen, JsonConvert.DeserializeObject<IReadOnlyList<string[]>>(Uri.UnescapeDataString(ProtocalArgs.Uri.AbsolutePath)));
                                }

                                break;
                            }
                        default:
                            {
                                Window.Current.Content = new ExtendedSplash(args.SplashScreen);
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Switching to normal startup mode for unknown exception was threw");
                Window.Current.Content = new ExtendedSplash(args.SplashScreen);
            }
            finally
            {
                Window.Current.Activate();
            }
        }

        private async Task LaunchWithStartupMode(IActivatedEventArgs LaunchArgs)
        {
            switch (StartupModeController.Mode)
            {
                case StartupMode.CreateNewTab:
                    {
                        Window.Current.Content = new ExtendedSplash(LaunchArgs.SplashScreen);
                        break;
                    }
                case StartupMode.LastOpenedTab:
                    {
                        List<string[]> LastOpenedPathArray = await StartupModeController.GetAllPathAsync(StartupMode.LastOpenedTab).ToListAsync();

                        if (LastOpenedPathArray.Count > 0)
                        {
                            Window.Current.Content = new ExtendedSplash(LaunchArgs.SplashScreen, LastOpenedPathArray);
                        }
                        else
                        {
                            Window.Current.Content = new ExtendedSplash(LaunchArgs.SplashScreen);
                        }

                        break;
                    }
                case StartupMode.SpecificTab:
                    {
                        string[] SpecificPathArray = await StartupModeController.GetAllPathAsync(StartupMode.SpecificTab)
                                                                                .Select((Item) => Item.SingleOrDefault())
                                                                                .ToArrayAsync();

                        if (SpecificPathArray.Length > 0)
                        {
                            Window.Current.Content = new ExtendedSplash(LaunchArgs.SplashScreen, SpecificPathArray);
                        }
                        else
                        {
                            Window.Current.Content = new ExtendedSplash(LaunchArgs.SplashScreen);
                        }

                        break;
                    }
            }
        }
    }
}
