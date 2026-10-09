using System.ComponentModel;
using System.Windows;

namespace HostComputer.IndustrialMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private bool _readyToClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    // WPF cannot await OnClosed. Cancel closing once, drain SQLite, then close.
    protected override async void OnClosing(CancelEventArgs e)
    {
        if (!_readyToClose)
        {
            e.Cancel = true;
            try { await _viewModel.StopAndWaitAsync(); }
            finally
            {
                _readyToClose = true;
                Dispatcher.BeginInvoke(new System.Action(Close));
            }
        }
        base.OnClosing(e);
    }
}
