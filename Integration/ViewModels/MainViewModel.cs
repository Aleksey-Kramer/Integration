using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Integration.Core;
using Integration.Services;

namespace Integration.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IEventBus _bus;
    private readonly AgentManager _manager;
    private readonly ParametersStore _parameters;
    private readonly RuntimeStateStore _runtimeState;
    private readonly Action<Action> _ui;

    public ObservableCollection<AgentVm> Agents { get; } = new();
    public ObservableCollection<string> GlobalLogs { get; } = new();

    private AgentVm? _selectedAgent;
    public AgentVm? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (ReferenceEquals(_selectedAgent, value)) return;
            _selectedAgent = value;
            OnPropertyChanged();
        }
    }

    private object? _currentView;
    public object? CurrentView
    {
        get => _currentView;
        private set
        {
            if (ReferenceEquals(_currentView, value)) return;
            _currentView = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenAgentDetailsCommand { get; }
    public ICommand BackToMenuCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    public MainViewModel(
        IEventBus bus,
        AgentManager manager,
        ParametersStore parameters,
        RuntimeStateStore runtimeState)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));

        var dispatcher = Application.Current?.Dispatcher;
        _ui = dispatcher is null
            ? (Action<Action>)(a => a())
            : (a =>
            {
                if (dispatcher.CheckAccess()) a();
                else dispatcher.Invoke(a);
            });

        CurrentView = this;

        OpenAgentDetailsCommand = new RelayCommand<AgentVm>(
            vm => vm is not null,
            vm => OpenAgentDetails(vm!.Id));

        BackToMenuCommand = new RelayCommand<object>(
            _ => true,
            _ => CurrentView = this);

        var agents = _manager.GetAgents();
        foreach (var a in agents)
            Agents.Add(new AgentVm(a, _manager, OpenAgentDetails));

        _bus.GlobalLogAdded += OnGlobalLogAdded;
        _bus.AgentStatusChanged += OnAgentStatusChanged;
    }

    private void OnGlobalLogAdded(LogEntry entry)
    {
        _ui(() =>
        {
            var line = $"{entry.At:HH:mm:ss} [{entry.Level}] {entry.Message}";
            GlobalLogs.Add(line);

            if (GlobalLogs.Count > 500)
                GlobalLogs.RemoveAt(0);
        });
    }

    private void OnAgentStatusChanged(string agentId, AgentStatus status)
    {
        _ui(() =>
        {
            var vm = Agents.FirstOrDefault(x => x.Id.Equals(agentId, StringComparison.OrdinalIgnoreCase));
            vm?.ApplyStatus(status);
        });
    }

    private void OpenAgentDetails(string agentId)
    {
        _bus.PublishGlobal(new LogEntry(DateTimeOffset.Now, LogLevel.info, $"Open details requested: {agentId}"));

        CurrentView = new AgentDetailsViewModel(
            _bus,
            _manager,
            _parameters,
            _runtimeState,
            agentId,
            () => _ui(() => CurrentView = this)
        );
    }
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Predicate<T?> _canExecute;
    private readonly Action<T?> _execute;

    public RelayCommand(Predicate<T?> canExecute, Action<T?> execute)
    {
        _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public bool CanExecute(object? parameter)
        => _canExecute((T?)parameter);

    public void Execute(object? parameter)
        => _execute((T?)parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
