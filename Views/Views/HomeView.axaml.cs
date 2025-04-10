using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace snakeql.Views.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        Console.WriteLine("HomeView ctr");
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
