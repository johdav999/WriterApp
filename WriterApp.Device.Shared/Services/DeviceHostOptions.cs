namespace WriterApp.Device.Shared.Services;

public sealed record DeviceHostOptions(string HostName, Uri ApiBaseAddress)
{
    public static readonly Uri ProductionApiBaseAddress = new("https://app.prosa-app.com/", UriKind.Absolute);

    public static Uri ResolveApiBaseAddress(string? configuredAddress) =>
        Uri.TryCreate(configuredAddress, UriKind.Absolute, out Uri? configured)
            ? configured
            : ProductionApiBaseAddress;
}
