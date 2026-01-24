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
using Integration.Scheduling;
using Integration.Services;

namespace Integration.ViewModels;

public sealed class AgentDetailsViewModel : INotifyPropertyChanged
{
    private readonly IEventBus _bus;
    private readonly AgentManager _manager;
    private readonly ParametersStore _parameters;
    private readonly RuntimeStateStore _runtimeState;
    private readonly QuartzSchedulerService _scheduler;

    private readonly string _agentId;
    private readonly Action _backToMenu;
    private readonly Action<Action> _ui;

    public string AgentId => _agentId;

    private string _title = "";
    public string Title { get => _title; private set { if (_title == value) return; _title = value; OnPropertyChanged(); } }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set { if (_statusText == value) return; _statusText = value; OnPropertyChanged(); } }

    // === Pause/Resume (для MainWindow.xaml) ===
    public string PauseResumeText { get; private set; } = "Пауза";
    public ICommand PauseResumeCommand { get; }

    // DB
    private string _dbConnectionName = "—";
    public string DbConnectionName { get => _dbConnectionName; private set { if (_dbConnectionName == value) return; _dbConnectionName = value; OnPropertyChanged(); } }

    private string _dbStatusText = "—";
    public string DbStatusText { get => _dbStatusText; private set { if (_dbStatusText == value) return; _dbStatusText = value; OnPropertyChanged(); } }

    private Brush _dbStatusBrush = Brushes.Goldenrod;
    public Brush DbStatusBrush { get => _dbStatusBrush; private set { if (Equals(_dbStatusBrush, value)) return; _dbStatusBrush = value; OnPropertyChanged(); } }

    private string _dbNameText = "—";
    public string DbNameText { get => _dbNameText; private set { if (_dbNameText == value) return; _dbNameText = value; OnPropertyChanged(); } }

    // API (из parameters.json)
    private string _apiBaseUrl = "—";
    public string ApiBaseUrl { get => _apiBaseUrl; private set { if (_apiBaseUrl == value) return; _apiBaseUrl = value; OnPropertyChanged(); } }

    private string _apiStatusText = "Состояние: неизвестно";
    public string ApiStatusText { get => _apiStatusText; private set { if (_apiStatusText == value) return; _apiStatusText = value; OnPropertyChanged(); } }

    private Brush _apiStatusBrush = Brushes.Goldenrod;
    public Brush ApiStatusBrush { get => _apiStatusBrush; private set { if (Equals(_apiStatusBrush, value)) return; _apiStatusBrush = value; OnPropertyChanged(); } }

    // Schedule
    public string NextRunText { get; private set; } = "—";
    public string ScheduleText { get; private set; } = "—";
    public string TimeToNextRunText { get; private set; } = "—";

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

    // Commands (остальные)
    public ICommand BackToMenuCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StartNowCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand RunByInnCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AgentDetailsViewModel(
        IEventBus bus,
        AgentManager manager,
        ParametersStore parameters,
        RuntimeStateStore runtimeState,
        QuartzSchedulerService scheduler,
        string agentId,
        Action backToMenu)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
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

        // Toggle pause/resume
        PauseResumeCommand = new RelayCommand<object>(_ => true, _ =>
        {
            if (_manager.TryGetAgent(_agentId, out var ag) && ag is not null)
            {
                if (ag.Status == AgentStatus.paused) _manager.Resume(_agentId);
                else if (ag.Status == AgentStatus.active) _manager.Pause(_agentId);
            }
        });

        StartNowCommand = new RelayCommand<object>(_ => true, _ => _ = StartNowAsync());
        RestartCommand = new RelayCommand<object>(_ => true, _ => _ = RestartAsync());

        RunByInnCommand = new RelayCommand<object>(_ => true, _ =>
        {
            _bus.PublishAgent(new AgentLogEntry(
                _agentId,
                DateTimeOffset.Now,
                LogLevel.info,
                $"RunByInn requested: inn='{InnForRun}'"));
        });

        RefreshFromAgent();
        _ = RefreshScheduleAsync();

        _bus.AgentLogAdded += OnAgentLogAdded;
        _bus.AgentStatusChanged += OnAgentStatusChanged;
        _bus.AgentApiStateChanged += OnAgentApiStateChanged;
        _bus.AgentScheduleChanged += OnAgentScheduleChanged;
    }

    private void RefreshFromAgent()
    {
        if (_manager.TryGetAgent(_agentId, out var agent) && agent is not null)
        {
            Title = agent.DisplayName;
            StatusText = agent.Status.ToString();
            UpdatePauseButtonText(agent.Status);
        }
        else
        {
            Title = _agentId;
            StatusText = "unknown";
            UpdatePauseButtonText(AgentStatus.stopped);
        }

        try
        {
            var snap = _parameters.GetSnapshot();

            if (!snap.Agents.TryGetValue(_agentId, out var agentCfg) || string.IsNullOrWhiteSpace(agentCfg.Service))
                ApiBaseUrl = "—";
            else if (!snap.Services.TryGetValue(agentCfg.Service, out var svc) || string.IsNullOrWhiteSpace(svc.Base_Url))
                ApiBaseUrl = "—";
            else
                ApiBaseUrl = svc.Base_Url!.TrimEnd('/');
        }
        catch
        {
            ApiBaseUrl = "—";
        }

        ApplyApiIndicatorFromRuntime();
        ApplyDbIndicatorFromRuntime();
    }

    private void UpdatePauseButtonText(AgentStatus status)
    {
        PauseResumeText = status switch
        {
            AgentStatus.active => "Пауза",
            AgentStatus.paused => "Продолжить",
            _ => "—"
        };
        OnPropertyChanged(nameof(PauseResumeText));
    }

    private void ApplyApiIndicatorFromRuntime()
    {
        var st = _runtimeState.GetAgent(_agentId);

        switch (st.Api.Status)
        {
            case ApiConnectionStatus.ok:
                ApiStatusBrush = Brushes.Green;
                ApiStatusText = string.IsNullOrWhiteSpace(st.Api.Text) ? "Состояние: подключен" : st.Api.Text;
                break;

            case ApiConnectionStatus.error:
                ApiStatusBrush = Brushes.Red;

                var code = st.Api.Last_Error?.Code;
                ApiStatusText = code is null || code == AgentStatusErrors.none
                    ? (string.IsNullOrWhiteSpace(st.Api.Text) ? "Ошибка" : st.Api.Text)
                    : $"Ошибка: {code}";

                if (st.Api.Last_Error_At_Utc is not null)
                    LastErrorText = st.Api.Last_Error_At_Utc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
                break;

            default:
                ApiStatusBrush = Brushes.Goldenrod;
                ApiStatusText = string.IsNullOrWhiteSpace(st.Api.Text) ? "Состояние: неизвестно" : st.Api.Text;
                break;
        }
    }

    private void ApplyDbIndicatorFromRuntime()
    {
        var st = _runtimeState.GetAgent(_agentId);

        DbConnectionName = string.IsNullOrWhiteSpace(st.Db.Connection_Name) ? "—" : st.Db.Connection_Name!;
        DbNameText = string.IsNullOrWhiteSpace(st.Db.Db_Name) ? "—" : st.Db.Db_Name!;

        switch (st.Db.State)
        {
            case ConnectionStateKind.ok:
                DbStatusBrush = Brushes.Green;
                DbStatusText = string.IsNullOrWhiteSpace(st.Db.Text) ? "Состояние: подключен" : st.Db.Text;
                break;

            case ConnectionStateKind.error:
                DbStatusBrush = Brushes.Red;

                var code = st.Db.Last_Error?.Code;
                DbStatusText = code is null || code == AgentStatusErrors.none
                    ? (string.IsNullOrWhiteSpace(st.Db.Text) ? "Ошибка" : st.Db.Text)
                    : $"Ошибка: {code}";

                if (st.Db.Last_Error_At_Utc is not null)
                    LastErrorText = st.Db.Last_Error_At_Utc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
                break;

            default:
                DbStatusBrush = Brushes.Goldenrod;
                DbStatusText = string.IsNullOrWhiteSpace(st.Db.Text) ? "Состояние: неизвестно" : st.Db.Text;
                break;
        }
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

        _ui(() =>
        {
            StatusText = status.ToString();
            UpdatePauseButtonText(status);
            _ = RefreshScheduleAsync();
        });
    }

    private Task StartNowAsync()
        => _manager.StartNowAsync(_agentId);

    private async Task RestartAsync()
    {
        _manager.Stop(_agentId);
        _manager.Resume(_agentId);
        await _manager.StartNowAsync(_agentId);
    }

    private void OnAgentApiStateChanged(string agentId, ApiConnectionStatus status, string? errorCode, string? errorMessage)
    {
        if (!agentId.Equals(_agentId, StringComparison.OrdinalIgnoreCase))
            return;

        _ui(() =>
        {
            _runtimeState.UpdateAgent(_agentId, st =>
            {
                st.Api.Status = status;

                var nowUtc = DateTimeOffset.UtcNow;

                if (status == ApiConnectionStatus.ok)
                {
                    st.Api.Text = "Состояние: подключен";
                    st.Api.Last_Success_At_Utc = nowUtc;

                    st.Api.Last_Error_At_Utc = null;
                    if (st.Api.Last_Error is not null)
                    {
                        st.Api.Last_Error.Code = AgentStatusErrors.none;
                        st.Api.Last_Error.Kind = null;
                        st.Api.Last_Error.Message = null;
                    }
                }
                else if (status == ApiConnectionStatus.error)
                {
                    var parsed = Enum.TryParse<AgentStatusErrors>(errorCode, ignoreCase: true, out var code)
                        ? code
                        : AgentStatusErrors.unknown;

                    st.Api.Text = parsed == AgentStatusErrors.none ? "Ошибка" : $"Ошибка: {parsed}";
                    st.Api.Last_Error_At_Utc = nowUtc;

                    if (st.Api.Last_Error is not null)
                    {
                        st.Api.Last_Error.Code = parsed;
                        st.Api.Last_Error.Kind = errorCode;
                        st.Api.Last_Error.Message = errorMessage;
                    }

                    st.LastErrorAt = DateTimeOffset.Now;
                    st.LastErrorMessage = errorMessage ?? errorCode;
                }
                else
                {
                    st.Api.Text = "Состояние: неизвестно";

                    st.Api.Last_Error_At_Utc = null;
                    if (st.Api.Last_Error is not null)
                    {
                        st.Api.Last_Error.Code = AgentStatusErrors.none;
                        st.Api.Last_Error.Kind = null;
                        st.Api.Last_Error.Message = null;
                    }
                }
            });

            _runtimeState.Save();
            ApplyApiIndicatorFromRuntime();
        });
    }

    private void OnAgentScheduleChanged(string agentId)
    {
        if (!agentId.Equals(_agentId, StringComparison.OrdinalIgnoreCase))
            return;

        _ = RefreshScheduleAsync();
    }

    private async Task RefreshScheduleAsync()
    {
        try
        {
            var next = await _scheduler.GetNextRunAsync(_agentId).ConfigureAwait(false);
            var desc = await _scheduler.GetScheduleDescriptionAsync(_agentId).ConfigureAwait(false);

            var nextText = next.HasValue ? next.Value.ToString("dd.MM.yyyy HH:mm:ss") : "—";
            var scheduleText = string.IsNullOrWhiteSpace(desc) ? "—" : desc;

            var deltaText = "—";
            if (next.HasValue)
            {
                var delta = next.Value - DateTimeOffset.Now;
                if (delta < TimeSpan.Zero) delta = TimeSpan.Zero;
                deltaText = $"{(int)delta.TotalHours:00}:{delta.Minutes:00}:{delta.Seconds:00}";
            }

            _ui(() =>
            {
                NextRunText = nextText;
                ScheduleText = scheduleText;
                TimeToNextRunText = deltaText;

                OnPropertyChanged(nameof(NextRunText));
                OnPropertyChanged(nameof(ScheduleText));
                OnPropertyChanged(nameof(TimeToNextRunText));
            });
        }
        catch
        {
            _ui(() =>
            {
                NextRunText = "—";
                ScheduleText = "—";
                TimeToNextRunText = "—";

                OnPropertyChanged(nameof(NextRunText));
                OnPropertyChanged(nameof(ScheduleText));
                OnPropertyChanged(nameof(TimeToNextRunText));
            });
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
