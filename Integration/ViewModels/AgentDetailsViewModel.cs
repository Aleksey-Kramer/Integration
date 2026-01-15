using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Integration.Core;
using Integration.Services;

namespace Integration.ViewModels;

public sealed class AgentDetailsViewModel : INotifyPropertyChanged
{
    private readonly IEventBus _bus;
    private readonly AgentManager _manager;
    private readonly ParametersStore _parameters;
    private readonly RuntimeStateStore _runtimeState;
    private readonly string _agentId;
    private readonly Action _backToMenu;
    private readonly Action<Action> _ui;

    public string AgentId => _agentId;

    // --- UI поля (под твой DataTemplate) ---
    private string _title = "";
    public string Title { get => _title; private set { if (_title == value) return; _title = value; OnPropertyChanged(); } }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); } }

    // DB (пока заглушки)
    private string _dbConnectionName = "—";
    public string DbConnectionName { get => _dbConnectionName; private set { if (_dbConnectionName == value) return; _dbConnectionName = value; OnPropertyChanged(); } }

    private string _dbStatusText = "—";
    public string DbStatusText { get => _dbStatusText; private set { if (_dbStatusText == value) return; _dbStatusText = value; OnPropertyChanged(); } }

    // API (из parameters.json)
    private string _apiBaseUrl = "—";
    public string ApiBaseUrl { get => _apiBaseUrl; private set { if (_apiBaseUrl == value) return; _apiBaseUrl = value; OnPropertyChanged(); } }

    // API статус/индикатор (желтый/зеленый/красный)
    private string _apiStatusText = "Состояние: неизвестно";
    public string ApiStatusText { get => _apiStatusText; private set { if (_apiStatusText == value) return; _apiStatusText = value; OnPropertyChanged(); } }

    private Brush _apiStatusBrush = Brushes.Goldenrod;
    public Brush ApiStatusBrush { get => _apiStatusBrush; private set { if (Equals(_apiStatusBrush, value)) return; _apiStatusBrush = value; OnPropertyChanged(); } }

    public string NextRunText { get; private set; } = "—";
    public string ScheduleText { get; private set; } = "—";

    private string _lastErrorText = "—";
    public string LastErrorText { get => _lastErrorText; private set { if (_lastErrorText == value) return; _lastErrorText = value; OnPropertyChanged(); } }

    private string _innForRun = "";
    public string InnForRun
    {
        get => _innForRun;
        set { if (_innForRun == value) return; _innForRun = value; OnPropertyChanged(); }
    }

    private string _lastMessageId = "—";
    public string LastMessageId { get => _lastMessageId; private set { if (_lastMessageId == value) return; _lastMessageId = value; OnPropertyChanged(); } }

    public string FunctionDescription { get; private set; } = "Получение сертификатов UzStandart (пагинация)";

    public ObservableCollection<string> Logs { get; } = new();

    // --- Команды ---
    public ICommand BackToMenuCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StartNowCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand RunByInnCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentDetailsViewModel(
        IEventBus bus,
        AgentManager manager,
        ParametersStore parameters,
        RuntimeStateStore runtimeState,
        string agentId,
        Action backToMenu)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
        _agentId = string.IsNullOrWhiteSpace(agentId) ? throw new ArgumentException("agentId is required", nameof(agentId)) : agentId;
        _backToMenu = backToMenu ?? throw new ArgumentNullException(nameof(backToMenu));

        var dispatcher = Application.Current?.Dispatcher;
        _ui = dispatcher is null
            ? (Action<Action>)(a => a())
            : (a =>
            {
                if (dispatcher.CheckAccess()) a();
                else dispatcher.Invoke(a);
            });

        BackToMenuCommand = new RelayCommand<object>(_ => true, _ => _backToMenu());

        StopCommand = new RelayCommand<object>(_ => true, _ => _manager.Stop(_agentId));
        PauseCommand = new RelayCommand<object>(_ => true, _ => _manager.Pause(_agentId));

        StartNowCommand = new RelayCommand<object>(_ => true, _ => _ = StartNowAsync());
        RestartCommand = new RelayCommand<object>(_ => true, _ => _ = RestartAsync());

        RunByInnCommand = new RelayCommand<object>(_ => true, _ =>
        {
            // это лог ДЕТАЛЕЙ агента (не глобальный)
            _bus.PublishAgent(new AgentLogEntry(
                _agentId,
                DateTimeOffset.Now,
                LogLevel.info,
                $"RunByInn requested: inn='{InnForRun}'"));
        });

        // первичная инициализация (параметры + runtime_state)
        RefreshFromAgent();

        // подписки
        _bus.AgentLogAdded += OnAgentLogAdded;
        _bus.AgentStatusChanged += OnAgentStatusChanged;
    }

    private void RefreshFromAgent()
    {
        if (_manager.TryGetAgent(_agentId, out var agent) && agent is not null)
        {
            Title = agent.DisplayName;
            StatusText = agent.Status.ToString();
        }
        else
        {
            Title = _agentId;
            StatusText = "unknown";
        }

        // --- base_url: services[agents[agentId].service].base_url ---
        try
        {
            var snap = _parameters.GetSnapshot();

            if (!snap.Agents.TryGetValue(_agentId, out var agentCfg) || string.IsNullOrWhiteSpace(agentCfg.Service))
            {
                ApiBaseUrl = "—";
            }
            else if (!snap.Services.TryGetValue(agentCfg.Service, out var svc) || string.IsNullOrWhiteSpace(svc.Base_Url))
            {
                ApiBaseUrl = "—";
            }
            else
            {
                ApiBaseUrl = svc.Base_Url!.TrimEnd('/');
            }
        }
        catch
        {
            ApiBaseUrl = "—";
        }

        // --- API индикатор: из runtime_state (в памяти) ---
        ApplyApiIndicatorFromRuntime();
    }

    private void ApplyApiIndicatorFromRuntime()
    {
        var st = _runtimeState.GetOrCreate(_agentId);

        switch (st.ApiState)
        {
            case AgentApiState.Connected:
                ApiStatusBrush = Brushes.Green;
                ApiStatusText = "Состояние: подключен";
                break;

            case AgentApiState.Error:
                ApiStatusBrush = Brushes.Red;
                ApiStatusText = string.IsNullOrWhiteSpace(st.ApiError)
                    ? "Ошибка"
                    : $"Ошибка: {st.ApiError}";
                break;

            default:
                ApiStatusBrush = Brushes.Goldenrod;
                ApiStatusText = "Состояние: неизвестно";
                break;
        }

        // время последнего сбоя (из runtime, чтобы не затиралось при выходе/входе в детали)
        if (st.LastApiErrorAtUtc is not null)
            LastErrorText = st.LastApiErrorAtUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
    }

    private void OnAgentLogAdded(AgentLogEntry entry)
    {
        if (!entry.AgentId.Equals(_agentId, StringComparison.OrdinalIgnoreCase))
            return;

        _ui(() =>
        {
            var line = $"{entry.At:HH:mm:ss} [{entry.Level}] {entry.Message}";
            Logs.Add(line);

            if (Logs.Count > 2000)
                Logs.RemoveAt(0);

            // --- MVP-логика обновления API индикатора по факту лога ---
            // 1) любой error => считаем, что это ошибка API (позже заменим на точный тип ошибки/код)
            if (entry.Level == LogLevel.error)
            {
                var st = _runtimeState.GetOrCreate(_agentId);
                st.ApiState = AgentApiState.Error;
                st.ApiError = entry.Message; // позже сделаем нормальный код/тип
                st.LastApiErrorAtUtc = entry.At.UtcDateTime;

                ApiStatusBrush = Brushes.Red;
                ApiStatusText = $"Ошибка: {entry.Message}";
                LastErrorText = $"{entry.At:dd.MM.yyyy HH:mm:ss}";
            }
            else
            {
                // 2) если пришло что-то "успешное" по тику — считаем API подключенным
                // (да, это эвристика; потом заменим на явное событие AgentApiHealthChanged)
                if (entry.Message.Contains("page", StringComparison.OrdinalIgnoreCase) ||
                    entry.Message.Contains("items", StringComparison.OrdinalIgnoreCase) ||
                    entry.Message.Contains("tick finished", StringComparison.OrdinalIgnoreCase))
                {
                    var st = _runtimeState.GetOrCreate(_agentId);
                    st.ApiState = AgentApiState.Connected;
                    st.ApiError = null;

                    ApiStatusBrush = Brushes.Green;
                    ApiStatusText = "Состояние: подключен";
                }
            }
        });
    }

    private void OnAgentStatusChanged(string agentId, AgentStatus status)
    {
        if (!agentId.Equals(_agentId, StringComparison.OrdinalIgnoreCase))
            return;

        _ui(() => StatusText = status.ToString());
    }

    private Task StartNowAsync()
        => _manager.StartNowAsync(_agentId);

    private async Task RestartAsync()
    {
        _manager.Stop(_agentId);
        _manager.Resume(_agentId);
        await _manager.StartNowAsync(_agentId);
    }

    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    // summary: ViewModel экрана деталей агента. Отвечает за отображение статуса агента, логов,
    //          и индикаторов (API/DB), подписывается на события EventBus и читает/обновляет RuntimeStateStore
    //          для сохранения состояния (например, API ok/error) между открытиями экрана.
}

