using System;
using System.Windows;
using Integration.Agents.UzStandart;
using Integration.Core;
using Integration.Services;
using Integration.ViewModels;

namespace Integration;

public partial class App : Application
{
    public static IEventBus EventBus { get; private set; } = null!;
    public static AgentManager AgentManager { get; private set; } = null!;
    public static ParametersStore Parameters { get; private set; } = null!;
    public static RuntimeStateStore RuntimeState { get; private set; } = null!;
    public static HttpClientProvider Http { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // 1) core services
            EventBus = new EventBus();
            Parameters = new ParametersStore("parameters.json");
            RuntimeState = new RuntimeStateStore("runtime_state.json");
            RuntimeState.Reload(); // читаем файл -> в память

            Http = new HttpClientProvider(Parameters);
            AgentManager = new AgentManager(EventBus);

            // 2) agents (register only, do not start)
            var uzClient = new UzStandartClient(Http);
            var uzAgent = new UzStandartAgent(Parameters, uzClient);

            // Если у агента по умолчанию Stopped — оставляем Activate().
            uzAgent.Activate();

            AgentManager.Register(uzAgent);

            base.OnStartup(e);

            // 3) UI
            var window = new MainWindow
            {
                DataContext = new MainViewModel(EventBus, AgentManager, Parameters, RuntimeState)
            };

            MainWindow = window;
            window.Show();

            // 4) logs
            EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, "App started."));
            EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Parameters loaded: {Parameters.FilePath}"));
            EventBus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Runtime state loaded: {RuntimeState.FilePath}"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Startup error:\n{ex.GetType().Name}: {ex.Message}",
                "Integration",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }
}
