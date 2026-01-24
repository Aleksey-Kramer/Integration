using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Integration.Core;
using Integration.Services;
using Integration.Scheduling;

namespace Integration.ViewModels;

public sealed class AgentVm : INotifyPropertyChanged
{
    private readonly AgentManager _manager;
    private readonly QuartzSchedulerService _scheduler;
    private readonly Action<Action> _ui;

    public string Id { get; }
    public string DisplayName { get; }

    private AgentStatus _status;
    public AgentStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(PauseResumeText));

            if (PauseResumeCommand is RelayCommand pr) pr.RaiseCanExecuteChanged();
            if (StopCommand is RelayCommand st) st.RaiseCanExecuteChanged();
        }
    }

    private string? _statusNote;
    public string? StatusNote
    {
        get => _statusNote;
        private set
        {
            if (_statusNote == value) return;
            _statusNote = value;
            OnPropertyChanged();
        }
    }

    public string NextRunText { get; private set; } = "—";
    public string IterationsText { get; private set; } = "—";

    public string DbStatusText { get; private set; } = "DB: —";
    public string DbNameText { get; private set; } = "—";

    public ICommand StartNowCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand OpenDetailsCommand { get; }

    public AgentVm(IAgent agent, AgentManager manager, QuartzSchedulerService scheduler, Action<string>? openDetails)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

        var dispatcher = Application.Current?.Dispatcher;
        _ui = dispatcher is null
            ? (Action<Action>)(a => a())
            : (a =>
            {
                if (dispatcher.CheckAccess()) a();
                else dispatcher.Invoke(a);
            });

        Id = agent?.Id ?? throw new ArgumentNullException(nameof(agent));
        DisplayName = agent.DisplayName;
        Status = agent.Status;

        // NEW: "Запуск сейчас" должен снимать с паузы
        StartNowCommand = new AsyncRelayCommand(async () =>
        {
            if (Status == AgentStatus.paused)
                _manager.Resume(Id);

            await _manager.StartNowAsync(Id).ConfigureAwait(false);
        });

        PauseResumeCommand = new RelayCommand(() =>
        {
            if (Status == AgentStatus.paused)
                _manager.Resume(Id);
            else if (Status == AgentStatus.active)
                _manager.Pause(Id);
        }, () => Status != AgentStatus.stopped);

        StopCommand = new RelayCommand(() => _manager.Stop(Id), () => Status != AgentStatus.stopped);

        OpenDetailsCommand = new RelayCommand(() => openDetails?.Invoke(Id));
    }

    public string StatusText => Status switch
    {
        AgentStatus.active => "Active",
        AgentStatus.paused => "Paused",
        AgentStatus.stopped => "Stopped",
        _ => Status.ToString()
    };

    // NEW: текст для кнопки Pause/Resume в списке
    public string PauseResumeText => Status == AgentStatus.paused ? "Продолжить" : "Пауза";

    public void ApplyStatus(AgentStatus status, string? note = null)
    {
        Status = status;
        if (note is not null)
            StatusNote = note;
    }

    public void UpdateScheduleInfo(string? nextRunText, string? iterationsText)
    {
        if (nextRunText is not null) NextRunText = nextRunText;
        if (iterationsText is not null) IterationsText = iterationsText;

        OnPropertyChanged(nameof(NextRunText));
        OnPropertyChanged(nameof(IterationsText));
    }

    public void ApplyDbState(DbConnectionState? db)
    {
        _ui(() =>
        {
            if (db is null)
            {
                DbStatusText = "DB: —";
                DbNameText = "—";
            }
            else
            {
                var code = db.Last_Error?.Code;

                DbStatusText = db.State switch
                {
                    ConnectionStateKind.ok => "DB: OK",
                    ConnectionStateKind.error => $"DB: ERROR ({code?.ToString() ?? "unknown"})",
                    _ => "DB: —"
                };

                DbNameText = string.IsNullOrWhiteSpace(db.Db_Name) ? "—" : db.Db_Name;
            }

            OnPropertyChanged(nameof(DbStatusText));
            OnPropertyChanged(nameof(DbNameText));
        });
    }

    public async Task RefreshScheduleAsync()
    {
        try
        {
            var next = await _scheduler.GetNextRunAsync(Id).ConfigureAwait(false);
            var desc = await _scheduler.GetScheduleDescriptionAsync(Id).ConfigureAwait(false);

            var nextText = next.HasValue
                ? next.Value.ToString("dd.MM.yyyy HH:mm:ss")
                : "—";

            var itText = string.IsNullOrWhiteSpace(desc) ? "—" : desc;

            _ui(() => UpdateScheduleInfo(nextText, itText));
        }
        catch
        {
            _ui(() => UpdateScheduleInfo("—", "—"));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Обычная синхронная команда.
/// </summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Асинхронная команда для кнопок (StartNow).
/// </summary>
internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public bool CanExecute(object? parameter) => !_isRunning;

    public async void Execute(object? parameter)
    {
        if (_isRunning) return;

        try
        {
            _isRunning = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            await _execute().ConfigureAwait(false);
        }
        finally
        {
            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CanExecuteChanged;
}
