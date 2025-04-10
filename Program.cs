using System;
using Avalonia;

namespace snakeql
{
    internal class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            //hello
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
}

