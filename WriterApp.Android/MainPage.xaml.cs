using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WriterApp.Android;

public partial class MainPage : ContentPage
{
    public MainPage() { InitializeComponent(); }
    public async Task<bool> BackAsync()
    {
        bool handled = false;
        bool dispatched = await Browser.TryDispatchAsync(services =>
        {
            var navigation = services.GetRequiredService<NavigationManager>();
            if (navigation.ToBaseRelativePath(navigation.Uri).Trim('/').Length == 0) return;
            navigation.NavigateTo("/");
            handled = true;
        });
        return dispatched && handled;
    }

    public async Task<bool> ClosePanelAsync()
    {
        Task<bool>? closing = null;
        bool dispatched = await Browser.TryDispatchAsync(services =>
        {
            closing = CloseAsync(services.GetRequiredService<IJSRuntime>());
        });
        return dispatched && closing is not null && await closing;
    }

    private static async Task<bool> CloseAsync(IJSRuntime js)
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", "/_content/WriterApp.UI.Shared/editor-panels.js");
        await using (module)
            return await module.InvokeAsync<bool>("closeActive");
    }
}
