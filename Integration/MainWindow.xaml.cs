using System.Windows;
using Integration.ViewModels;

namespace Integration;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Главный ViewModel приложения
        DataContext = new MainViewModel(App.EventBus, App.AgentManager, App.Parameters);
    }
}