using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfSandbox;

/// <summary>
/// Plain WPF view model. Nothing here knows about DrawnUI — the point of the sample is that drawn
/// controls bind to it exactly like native WPF controls do.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private int _pokes;

    /// <summary>Text shown at the top, bound into a drawn label.</summary>
    public string Greeting => _pokes == 0
        ? "Nobody has poked the canvas yet"
        : "Poked from a WPF ICommand";

    /// <summary>Counter bound into a drawn label; proves INotifyPropertyChanged reaches the canvas.</summary>
    public int Pokes
    {
        get => _pokes;
        private set
        {
            if (_pokes == value)
                return;

            _pokes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Greeting));
        }
    }

    /// <summary>Bound to a drawn button's CommandTapped.</summary>
    public ICommand PokeCommand { get; }

    /// <summary>Bound to a second drawn button, resets the counter.</summary>
    public ICommand ResetCommand { get; }

    /// <summary>Creates the view model.</summary>
    public MainViewModel()
    {
        PokeCommand = new RelayCommand(() => Pokes++);
        ResetCommand = new RelayCommand(() => Pokes = 0);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Minimal ICommand so the sample needs no MVVM package.</summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;

    /// <summary>Creates a command running the given action.</summary>
    public RelayCommand(Action execute) => _execute = execute;

    /// <inheritdoc/>
    public event EventHandler CanExecuteChanged { add { } remove { } }

    /// <inheritdoc/>
    public bool CanExecute(object parameter) => true;

    /// <inheritdoc/>
    public void Execute(object parameter) => _execute();
}
