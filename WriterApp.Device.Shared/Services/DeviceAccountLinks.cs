namespace WriterApp.Device.Shared.Services;

public enum AccountDestination { Billing, UpgradeStandard, UpgradePro, Documentation }
public sealed class UnavailableExternalLauncher : IDeviceExternalLauncher
{ public Task OpenAsync(Uri address) => throw new InvalidOperationException("System-browser handoff is unavailable in this host."); }
public sealed class DeviceAccountLinks(DeviceHostOptions host, IDeviceExternalLauncher launcher)
{
    public bool IsAvailable => launcher is not UnavailableExternalLauncher;
    public Uri Address(AccountDestination destination)
    {
        if (destination == AccountDestination.Documentation) return new("https://docs.prosa-app.com/");
        var root = host.ApiBaseAddress;
        if (root.Scheme != "https" || !string.IsNullOrEmpty(root.UserInfo) || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment))
            throw new InvalidOperationException("Billing handoff requires a trusted HTTPS backend configuration.");
        string path = destination switch { AccountDestination.Billing => "/app/account/billing", AccountDestination.UpgradeStandard => "/start?plan=standard", AccountDestination.UpgradePro => "/start?plan=pro", _ => throw new ArgumentOutOfRangeException(nameof(destination)) };
        return new Uri(root, path);
    }
    public Task OpenAsync(AccountDestination destination) => launcher.OpenAsync(Address(destination));
}
