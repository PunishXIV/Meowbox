using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace meowbox;

public partial class Account : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] private string _label = "New account";
    [ObservableProperty] private string _username = "";

    // plaintext in memory, DPAPI blob on disk
    [ObservableProperty][property: JsonIgnore] private string _password = "";
    [ObservableProperty][property: JsonIgnore] private string _totpSecret = "";

    [JsonPropertyName("password")]
    public string PasswordEnc { get => Dpapi.Protect(Password); set => Password = Dpapi.Unprotect(value); }

    [JsonPropertyName("totp")]
    public string TotpEnc { get => Dpapi.Protect(TotpSecret); set => TotpSecret = Dpapi.Unprotect(value); }
}

public partial class Env : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] private string _name = "New environment";
    [ObservableProperty] private string _roamingPath = "";
}

public partial class Instance : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [ObservableProperty] private string _name = "New instance";
    [ObservableProperty] private Guid _accountId;
    [ObservableProperty] private Guid _envId;
    [ObservableProperty][property: JsonIgnore] private bool _selected;
}

public partial class Settings : ObservableObject
{
    [ObservableProperty] private int _loginTimeoutSeconds = 30;
    [ObservableProperty] private int _launchDelaySeconds = 3;
}
