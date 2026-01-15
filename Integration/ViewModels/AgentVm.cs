using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Integration.Core;
using Integration.Services;

namespace Integration.ViewModels;

public sealed class AgentVm : INotifyPropertyChanged
{
    private readonly AgentManager _manager;

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
        }
    }

    // Текст под статусом (Paused by user / Stopped manually / Stopped due to error)
    // Пока не используем — оставляем задел.
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

    // Пока Quartz не подключен — просто заглушки, чтобы UI уже был готов.
    public string NextRunText { get; private set; } = "—";
    public string IterationsText { get; private set; } = "—";

    // Команды строки
    public ICommand StartNowCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand OpenDetailsCommand { get; }

    public AgentVm(IAgent agent, AgentManager manager, Action<string>? openDetails)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));

        Id = agent?.Id ?? throw new ArgumentNullException(nameof(agent));
        DisplayName = agent.DisplayName;
        Status = agent.Status;

        StartNowCommand = new AsyncRelayCommand(async () => await _manager.StartNowAsync(Id));

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
