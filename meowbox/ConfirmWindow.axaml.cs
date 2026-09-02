using Avalonia.Controls;
using Avalonia.Interactivity;

namespace meowbox;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow() => InitializeComponent();

    public static Task<bool> Ask(Window owner, string title, string message)
    {
        var w = new ConfirmWindow { Title = title };
        w.Message.Text = message;
        return w.ShowDialog<bool>(owner);
    }

    private void Yes(object? sender, RoutedEventArgs e) => Close(true);
    private void No(object? sender, RoutedEventArgs e) => Close(false);
}
