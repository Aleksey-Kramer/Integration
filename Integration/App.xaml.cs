using System;
using System.Windows;
using Integration.Agents.Template; // ДОБАВИЛ
using Integration.Agents.UzStandart;
using Integration.Core;
using Integration.Repositories;
using Integration.Services;
using Integration.Scheduling;
using Integration.ViewModels;

namespace Integration;

public partial class App : Application
{
    public static IEventBus EventBus { get; private set; } = null!;
    public static AgentManager AgentManager { get; private set; } = null!;
    public static ParametersStore Parameters { get; private set; } = null!;
    public static RuntimeStateStore RuntimeState { get; private set; } = null!;

    // Quartz scheduler (in-memory)
    public static QuartzSchedulerService Scheduler { get; private set; } = null!;

    public static HttpClientProvider Http { get; private set; } = null!;

    public static DbConnectionFactory DbConnections { get; private set; } = null!;
    public static DbHealthcheckRepository DbHealthcheck { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // 1) core services
            EventBus = new EventBus();
            Parameters = new ParametersStore("parameters.json");
            RuntimeState = new RuntimeStateStore("runtime_state.json");
            RuntimeState.Reload();

            Http = new HttpClientProvider(Parameters);
            AgentManager = new AgentManager(EventBus);

            // DB services
            DbConnections = new DbConnectionFactory(Parameters);
            DbHealthcheck = new DbHealthcheckRepository(DbConnections);

            // 2) agents (register first)
            var uzClient = new UzStandartClient(Http);

            var uzAgent = new UzStandartAgent(
                Parameters,
                uzClient,
                RuntimeState,
                DbHealthcheck
            );

            // если агент по умолчанию stopped — активируем
            uzAgent.Activate();
            AgentManager.Register(uzAgent);

            // =========================
            // TEMPLATE AGENT (DISABLED)
            // =========================
            // ДОБАВИЛ: заготовку регистрации TemplateAgent, НО ОСТАВИЛ ЗАКОММЕНТИРОВАННОЙ,
            // чтобы TemplateAgent не был виден в UI и не запускался.
            /*
            // var snap = Parameters.GetSnapshot();
            // if (snap.Agents.TryGetValue("template", out var cfg) && cfg.Enabled)
            // {
            //     var templateHttp = Http.GetClient("template");
            //     var templateClient = new TemplateClient(templateHttp);
            //     var templateRepository = new TemplateRepository();
            //     var templateService = new TemplateService(templateClient, templateRepository);
            //     var templateAgent = new TemplateAgent(Parameters, templateService, RuntimeState);
            //
            //     templateAgent.Activate();
            //     AgentManager.Register(templateAgent);
            // }
            */

            // 3) Quartz: создаём сервис, регистрируем job'ы и стартуем scheduler
            // ВАЖНО: QuartzAgentJob читает AgentManager и EventBus из SchedulerContext
            Scheduler = new QuartzSchedulerService(Parameters, AgentManager, EventBus);
            Scheduler.RegisterAgentsAsync().GetAwaiter().GetResult();
            Scheduler.StartAsync().GetAwaiter().GetResult();

            // первичное уведомление UI о расписании
            foreach (var a in AgentManager.GetAgents())
                EventBus.PublishAgentScheduleChanged(a.Id);

            base.OnStartup(e);

            // 4) UI
            var window = new MainWindow
            {
                DataContext = new MainViewModel(
                    EventBus,
                    AgentManager,
                    Parameters,
                    RuntimeState,
                    Scheduler)
            };

            MainWindow = window;
            window.Show();

            // 5) logs
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

    // graceful Quartz shutdown
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Scheduler?.ShutdownAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // ignore shutdown errors
        }

        base.OnExit(e);
    }
}
