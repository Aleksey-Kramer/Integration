using System.Windows;

namespace Integration;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // summary: WPF-окно-хост. Инициализирует XAML и НЕ создаёт ViewModel.
    //          DataContext задаётся в точке композиции (App.OnStartup), чтобы не ломать DI.
}