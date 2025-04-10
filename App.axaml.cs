using System;
using System.Diagnostics;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Live.Avalonia;
using ReactiveUI;
using snakeql.Themes;
using snakeql.ViewModels;
using snakeql.Views;

namespace snakeql
{
    public class App : Application, ILiveView
    {
        public static IThemeManager? ThemeManager;

        public override void Initialize()
        {
            ThemeManager = new FluentThemeManager();
            ThemeManager.Initialize(this);

            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // DockManager.s_enableSplitToWindow = true;

            var mainWindowViewModel = new MainWindowViewModel();

            //if (Debugger.IsAttached || isProduction())
            //{
                // Debugging requires pdb loading etc, so we disable live reloading
                // during a test run with an attached debugger.
                // Disable live reload in production

                switch (ApplicationLifetime)
                {
                    case IClassicDesktopStyleApplicationLifetime desktopLifetime:
                    {
                        var mainWindow = new MainWindow { DataContext = mainWindowViewModel };

                        mainWindow.Closing += (_, _) =>
                        {
                            mainWindowViewModel.CloseLayout();
                        };

                        desktopLifetime.MainWindow = mainWindow;

                        desktopLifetime.Exit += (_, _) =>
                        {
                            mainWindowViewModel.CloseLayout();
                        };

                        break;
                    }
                    case ISingleViewApplicationLifetime singleViewLifetime:
                    {
                        var mainView = new MainView() { DataContext = mainWindowViewModel };

                        singleViewLifetime.MainView = mainView;

                        break;
                    }
                }
            //}
            //else
            //{
            //    Console.WriteLine("Modalità hot reload");
            //
            //    // Here, we create a new LiveViewHost, located in the 'Live.Avalonia'
            //    // namespace, and pass an ILiveView implementation to it. The ILiveView
            //    // implementation should have a parameterless constructor! Next, we
            //    // start listening for any changes in the source files. And then, we
            //    // show the LiveViewHost window. Simple enough, huh?
            //    var window = new LiveViewHost(this, Console.WriteLine);
            //    window.StartWatchingSourceFilesForHotReloading();
            //    window.Show();
            //}

            // Here we subscribe to ReactiveUI default exception handler to avoid app
            // termination in case if we do something wrong in our view models. See:
            // https://www.reactiveui.net/docs/handbook/default-exception-handler/
            //
            // In case if you are using another MV* framework, please refer to its
            // documentation explaining global exception handling.
            RxApp.DefaultExceptionHandler = Observer.Create<Exception>(Console.WriteLine);

            base.OnFrameworkInitializationCompleted();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        // When any of the source files change, a new version of
        // the assembly is built, and this method gets called.
        // The returned content gets embedded into the window.
        public object CreateView(Window window)
        {
            if (window.DataContext == null)
                window.DataContext = new MainWindowViewModel();
            //            var mainWindowViewModel = new MainWindowViewModel;
            // The AppView class will inherit the DataContext
            // of the window. The AppView class can be a
            // UserControl, a Grid, a TextBlock, whatever.
            //          window.DataContext ??= mainWindowViewModel;
            return new MainView();
        }

        static bool isProduction()
        {
#if DEBUG
            return false;
#else
            return true;
#endif
        }
    }
}
