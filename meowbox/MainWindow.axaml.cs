using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using meowbox.Native;

namespace meowbox;

public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);

    private async void Copy(object? sender, RoutedEventArgs e)
    {
        if(sender is Button { Tag: string text } && Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }

    private async void CopyOtp(object? sender, RoutedEventArgs e)
    {
        if(sender is not Button { DataContext: Account account } || Clipboard is not { } clipboard) return;
        
        try
        {
            await clipboard.SetTextAsync(TotpGenerator.GenerateCode(account.TotpSecret));
        }
        catch(Exception ex)
        {
            if(DataContext is MainViewModel vm) vm.Status = $"{account.Label}: bad TOTP secret ({ex.Message})";
        }
    }

    private async void Browse(object? sender, RoutedEventArgs e)
    {
        if(sender is not Button { DataContext: Env env }) return;

        var options = new FolderPickerOpenOptions { Title = "Roaming path" };
        if(Directory.Exists(env.RoamingPath)) options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(env.RoamingPath);

        var picked = await StorageProvider.OpenFolderPickerAsync(options);
        if(picked.Count > 0 && picked[0].TryGetLocalPath() is { } path) env.RoamingPath = path;
    }
}
