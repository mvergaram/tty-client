using System.Windows;

namespace TtyClient;

public partial class MainWindow : Window
{
    private bool _readyToClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_readyToClose) return;

        e.Cancel = true;
        await CallView.ShutdownAsync();
        _readyToClose = true;
        Close();
    }
}
