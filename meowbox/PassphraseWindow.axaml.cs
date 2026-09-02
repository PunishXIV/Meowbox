using Avalonia.Controls;
using Avalonia.Interactivity;

namespace meowbox;

public partial class PassphraseWindow : Window
{
    public PassphraseWindow()
    {
        InitializeComponent();
        First.TextChanged += Validate;
        Second.TextChanged += Validate;
    }
    
    public static Task<string?> Ask(Window owner, string prompt, bool confirm)
    {
        var w = new PassphraseWindow { Title = prompt };
        w.Prompt.Text = prompt;
        w.Second.IsVisible = confirm;
        return w.ShowDialog<string?>(owner);
    }

    private void Validate(object? sender, TextChangedEventArgs e)
    {
        var mismatch = Second.IsVisible && First.Text != Second.Text;
        Hint.IsVisible = mismatch && !string.IsNullOrEmpty(Second.Text);
        Ok.IsEnabled = !string.IsNullOrEmpty(First.Text) && !mismatch;
    }

    private void Accept(object? sender, RoutedEventArgs e) => Close(First.Text);
    private void Cancel(object? sender, RoutedEventArgs e) => Close(null);
}
