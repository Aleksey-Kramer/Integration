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
using Integration.Models;
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
        _bus.AgentApiStateChanged += OnAgentApiStateChanged;
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
        var st = _runtimeState.GetAgent(_agentId);

        switch (st.Api.Status)
        {
            case ApiConnectionStatus.ok:
                ApiStatusBrush = Brushes.Green;
                ApiStatusText = "Состояние: подключен";
                break;

            case ApiConnectionStatus.error:
                ApiStatusBrush = Brushes.Red;

                ApiStatusText = st.Api.ErrorCode == AgentStatusErrors.none
                    ? "Ошибка"
                    : $"Ошибка: {st.Api.ErrorCode}";

                break;

            default:
                ApiStatusBrush = Brushes.Goldenrod;
                ApiStatusText = "Состояние: неизвестно";
                break;
        }

        if (st.LastErrorAt is not null)
            LastErrorText = st.LastErrorAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
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

            if (entry.Level == LogLevel.error)
            {
                _runtimeState.UpdateAgent(_agentId, st =>
                {
                    st.LastErrorAt = entry.At;
                    st.LastErrorMessage = entry.Message;
                });

                LastErrorText = $"{entry.At:dd.MM.yyyy HH:mm:ss}";
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
    
    // NEW: явное обновление статуса API (без эвристик по логам)
    private void OnAgentApiStateChanged(string agentId, ApiConnectionStatus status, string? errorCode, string? errorMessage)
    {
        if (!agentId.Equals(_agentId, StringComparison.OrdinalIgnoreCase))
            return;

        _ui(() =>
        {
            _runtimeState.UpdateAgent(_agentId, st =>
            {
                st.Api.Status = status;
                st.Api.LastCheckedAt = DateTimeOffset.Now;

                if (status == ApiConnectionStatus.ok)
                {
                    st.Api.ErrorCode = AgentStatusErrors.none;
                    st.Api.ErrorMessage = null;
                }
                else if (status == ApiConnectionStatus.error)
                {
                    // Пытаемся распарсить строковый код в enum, иначе unknown
                    st.Api.ErrorCode = Enum.TryParse<AgentStatusErrors>(errorCode, ignoreCase: true, out var parsed)
                        ? parsed
                        : AgentStatusErrors.unknown;

                    st.Api.ErrorMessage = errorMessage;

                    st.LastErrorAt = DateTimeOffset.Now;
                    st.LastErrorMessage = errorMessage ?? errorCode;
                }
                else
                {
                    st.Api.ErrorCode = AgentStatusErrors.none;
                    st.Api.ErrorMessage = null;
                }
            });

            ApplyApiIndicatorFromRuntime();
        });
    }


    // summary: ViewModel экрана деталей агента. Отвечает за отображение статуса агента, логов,
    //          и индикаторов (API/DB), подписывается на события EventBus и читает/обновляет RuntimeStateStore
    //          для сохранения состояния (например, API ok/error) между открытиями экрана.
}

