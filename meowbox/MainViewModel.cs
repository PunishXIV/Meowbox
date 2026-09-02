using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using meowbox.Native;

namespace meowbox;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<Account> Accounts { get; }
    public ObservableCollection<Env> Envs { get; }
    public ObservableCollection<Instance> Instances { get; }
    public Settings Settings { get; }

    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private Account? _selectedAccount;
    [ObservableProperty] private Env? _selectedEnv;
    [ObservableProperty] private Instance? _selectedInstance;

    private bool _sweeping;
    private string? _lastSweepError;

    public bool? AllInstancesChecked
    {
        get => Instances.Count > 0 && Instances.All(i => i.Selected) ? true
             : Instances.Any(i => i.Selected) ? null : false;
        set { if(value is bool b) foreach(var i in Instances) i.Selected = b; }
    }

    public bool? AllAccountsChecked
    {
        get => Accounts.Count > 0 && Accounts.All(a => a.Selected) ? true
             : Accounts.Any(a => a.Selected) ? null : false;
        set { if(value is bool b) foreach(var a in Accounts) a.Selected = b; }
    }

    public bool? AllEnvsChecked
    {
        get => Envs.Count > 0 && Envs.All(e => e.Selected) ? true
             : Envs.Any(e => e.Selected) ? null : false;
        set { if(value is bool b) foreach(var e in Envs) e.Selected = b; }
    }

    public int CheckedInstanceCount => Instances.Count(i => i.Selected);
    public int CheckedAccountCount => Accounts.Count(a => a.Selected);
    public int CheckedEnvCount => Envs.Count(e => e.Selected);

    public bool AnyInstanceChecked => CheckedInstanceCount > 0;
    public bool AnyAccountChecked => CheckedAccountCount > 0;
    public bool AnyEnvChecked => CheckedEnvCount > 0;

    public MainViewModel()
    {
        var d = Store.Load();
        Accounts = new(d.Accounts);
        Envs = new(d.Envs);
        Instances = new(d.Instances);
        Settings = d.Settings ?? new();
        Track(Accounts); Track(Envs); Track(Instances);
        Settings.PropertyChanged += OnDirty;

        if(Store.LoadFailed) Status = "data.json could not be read - saving is off so it is not overwritten; a copy is at data.json.bak";

        new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, Sweep).Start();
    }

    [RelayCommand] private void AddAccount() => Accounts.Add(new());
    [RelayCommand] private void AddEnv() => Envs.Add(new());
    [RelayCommand] private void AddInstance() => Instances.Add(new());
    
    public void RemoveCheckedAccounts() => RemoveChecked(Accounts, a => a.Selected);
    public void RemoveCheckedEnvs() => RemoveChecked(Envs, e => e.Selected);
    public void RemoveCheckedInstances() => RemoveChecked(Instances, i => i.Selected);

    private static void RemoveChecked<T>(ObservableCollection<T> c, Func<T, bool> isChecked)
    {
        foreach(var x in c.Where(isChecked).ToList()) c.Remove(x);
    }

    public string Merge(List<Account> incoming)
    {
        var added = incoming.Where(a => Accounts.All(x => x.Id != a.Id)).ToList();
        foreach(var a in added) Accounts.Add(a);
        return $"imported {added.Count}, skipped {incoming.Count - added.Count} already present";
    }

    [RelayCommand]
    private async Task Launch(Instance? inst)
    {
        if(inst is null) return;
        if(Envs.FirstOrDefault(e => e.Id == inst.EnvId) is not { } env)
        {
            Status = $"{inst.Name}: pick an environment first";
            return;
        }

        var account = Accounts.FirstOrDefault(a => a.Id == inst.AccountId);
        try
        {
            var pid = await Task.Run(() => Launcher.Launch(env));
            Status = $"Launched {inst.Name} (pid {pid})";

            if(account is null || string.IsNullOrWhiteSpace(account.Username)) return;

            Status = $"{inst.Name}: signing in as {account.Username}";
            var timeout = TimeSpan.FromSeconds(Math.Max(1, Settings.LoginTimeoutSeconds));
            var failure = await Task.Run(() => Launcher.SignIn(pid, account.Username, account.Password, account.TotpSecret, timeout));
            Status = failure is null ? $"{inst.Name}: signed in" : $"{inst.Name}: {failure}";
        }
        catch(Exception ex)
        {
            Status = $"{inst.Name}: {ex.Message}";
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanLaunchSelected))]
    private async Task LaunchSelected()
    {
        var selected = Instances.Where(x => x.Selected).ToList();

        for(var i = 0; i < selected.Count; i++)
        {
            await Launch(selected[i]);
            if(i < selected.Count - 1) await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, Settings.LaunchDelaySeconds)));
        }
    }

    private bool CanLaunchSelected => CheckedInstanceCount > 0;
    
    private async void Sweep(object? sender, EventArgs e)
    {
        if(_sweeping) return;
        _sweeping = true;
        try
        {
            await Task.Run(MutexCloser.CloseFfxivMutants);

            var error = MutexCloser.LastErrors.FirstOrDefault();
            if(error != null && error != _lastSweepError) Status = $"Mutex sweep: {error}";
            _lastSweepError = error;
        }
        catch(Exception ex) { Status = $"Mutex sweep failed: {ex.Message}"; }
        finally { _sweeping = false; }
    }

    private void Track<T>(ObservableCollection<T> c) where T : ObservableObject
    {
        foreach(var i in c) i.PropertyChanged += OnDirty;
        c.CollectionChanged += (_, e) =>
        {
            foreach(ObservableObject i in e.OldItems ?? Array.Empty<object>()) i.PropertyChanged -= OnDirty;
            foreach(ObservableObject i in e.NewItems ?? Array.Empty<object>()) i.PropertyChanged += OnDirty;
            InstancesChecked();
            AccountsChecked();
            EnvsChecked();
            Save();
        };
    }
    
    private void OnDirty(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName != nameof(Instance.Selected)) { Save(); return; }
        if(sender is Account) AccountsChecked();
        else if(sender is Env) EnvsChecked();
        else InstancesChecked();
    }

    private void InstancesChecked()
    {
        OnPropertyChanged(nameof(AllInstancesChecked));
        OnPropertyChanged(nameof(CheckedInstanceCount));
        OnPropertyChanged(nameof(AnyInstanceChecked));
        LaunchSelectedCommand.NotifyCanExecuteChanged();
    }

    private void AccountsChecked()
    {
        OnPropertyChanged(nameof(AllAccountsChecked));
        OnPropertyChanged(nameof(CheckedAccountCount));
        OnPropertyChanged(nameof(AnyAccountChecked));
    }

    private void EnvsChecked()
    {
        OnPropertyChanged(nameof(AllEnvsChecked));
        OnPropertyChanged(nameof(CheckedEnvCount));
        OnPropertyChanged(nameof(AnyEnvChecked));
    }

    private void Save() => Store.Save(new([.. Accounts], [.. Envs], [.. Instances], Settings));
}
