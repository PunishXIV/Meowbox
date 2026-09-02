using System.Security.Cryptography;
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

    private void RemoveInstances(object? sender, RoutedEventArgs e) =>
        ConfirmRemove("instances", vm => vm.CheckedInstanceCount, vm => vm.RemoveCheckedInstances());

    private void RemoveAccounts(object? sender, RoutedEventArgs e) =>
        ConfirmRemove("accounts", vm => vm.CheckedAccountCount, vm => vm.RemoveCheckedAccounts());

    private void RemoveEnvs(object? sender, RoutedEventArgs e) =>
        ConfirmRemove("environments", vm => vm.CheckedEnvCount, vm => vm.RemoveCheckedEnvs());

    private async void ConfirmRemove(string plural, Func<MainViewModel, int> count, Action<MainViewModel> remove)
    {
        if(DataContext is not MainViewModel vm) return;

        try
        {
            var n = count(vm);
            if(n == 0) return;

            var noun = n == 1 ? plural[..^1] : plural;
            if(!await ConfirmWindow.Ask(this, $"Remove {plural}", $"Remove {n} {noun}?")) return;

            remove(vm);
            vm.Status = $"removed {n} {noun}";
        }
        catch(Exception ex) { vm.Status = $"remove failed: {ex.Message}"; }
    }

    private static readonly FilePickerFileType MeowboxFile =
        new("meowbox export") { Patterns = ["*.meowbox"] };

    private async void ExportAccounts(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainViewModel vm) return;

        try
        {
            var accounts = vm.Accounts.Where(a => a.Selected).ToList();
            if(accounts.Count == 0) return;

            if(await PassphraseWindow.Ask(this, $"Passphrase to encrypt {accounts.Count} account(s)", confirm: true) is not { } pass) return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export accounts", SuggestedFileName = "accounts.meowbox",
                DefaultExtension = "meowbox", FileTypeChoices = [MeowboxFile],
            });
            if(file is null) return;

            vm.Status = "exporting...";
            var blob = await Task.Run(() => Portable.Export(accounts, pass));
            await using(var stream = await file.OpenWriteAsync())
            await using(var writer = new StreamWriter(stream)) await writer.WriteAsync(blob);
            vm.Status = $"exported {accounts.Count} account(s) to {file.Name}";
        }
        catch(Exception ex) { vm.Status = $"export failed: {ex.Message}"; }
    }

    private async void ImportAccounts(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainViewModel vm) return;

        try
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import accounts", AllowMultiple = false, FileTypeFilter = [MeowboxFile],
            });
            if(picked.Count == 0) return;

            if(await PassphraseWindow.Ask(this, $"Passphrase for {picked[0].Name}", confirm: false) is not { } pass) return;

            vm.Status = "importing...";
            string blob;
            await using(var stream = await picked[0].OpenReadAsync())
            using(var reader = new StreamReader(stream)) blob = await reader.ReadToEndAsync();

            vm.Status = vm.Merge(await Task.Run(() => Portable.Import(blob, pass)));
        }
        catch(CryptographicException) { vm.Status = "import: wrong passphrase or corrupt file"; }
        catch(Exception ex) { vm.Status = $"import failed: {ex.Message}"; }
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
