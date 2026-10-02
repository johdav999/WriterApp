using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Desktop;

/// <summary>A dedicated WebView2 renders the immutable export HTML, never the live editor.</summary>
public sealed class WindowsPdfExport : IDevicePdfExport
{
    public bool IsAvailable => true;
    public Task<byte[]?> RenderAsync(string html, double widthMm, double heightMm, CancellationToken ct=default) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        ct.ThrowIfCancellationRequested();
        if (!double.IsFinite(widthMm) || !double.IsFinite(heightMm) || widthMm<=0 || heightMm<=0)
            throw new ArgumentException("PDF page dimensions must be positive.");
        var window=Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window
            ?? throw new InvalidOperationException("Open the desktop window before exporting PDF.");
        var view=new Microsoft.UI.Xaml.Controls.WebView2 { Height=520, HorizontalAlignment=Microsoft.UI.Xaml.HorizontalAlignment.Stretch };
        var dialog=new ContentDialog { Title="PDF preview", Content=view, PrimaryButtonText="Create PDF", CloseButtonText="Cancel",
            IsPrimaryButtonEnabled=false, XamlRoot=window.Content.XamlRoot };
        byte[]? result=null; Exception? failure=null;
        view.Loaded += async (_,_) => {
            try {
                await view.EnsureCoreWebView2Async();
                view.CoreWebView2.Settings.IsScriptEnabled=false;
                view.CoreWebView2.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
                view.CoreWebView2.WebResourceRequested += (_,e) => {
                    if (!e.Request.Uri.StartsWith("data:",StringComparison.OrdinalIgnoreCase) && !e.Request.Uri.StartsWith("about:",StringComparison.OrdinalIgnoreCase))
                        e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(null,403,"Offline export","");
                };
                view.CoreWebView2.NavigationCompleted += (_,e) => { if(e.IsSuccess) dialog.IsPrimaryButtonEnabled=true; else { failure=new IOException("PDF preview could not load."); dialog.Hide(); } };
                view.CoreWebView2.NavigateToString(html);
            } catch(Exception e) { failure=e; dialog.Hide(); }
        };
        dialog.PrimaryButtonClick += async (_,e) => {
            var deferral=e.GetDeferral();
            try {
                ct.ThrowIfCancellationRequested();
                var settings=view.CoreWebView2.Environment.CreatePrintSettings();
                settings.PageWidth=widthMm/25.4; settings.PageHeight=heightMm/25.4;
                settings.ShouldPrintBackgrounds=true;
                settings.ShouldPrintHeaderAndFooter=false;
                settings.MarginTop=0; settings.MarginBottom=0; settings.MarginLeft=0; settings.MarginRight=0;
                using var pdf=await view.CoreWebView2.PrintToPdfStreamAsync(settings);
                using var source=pdf.AsStreamForRead();
                using var buffer=new MemoryStream();
                await source.CopyToAsync(buffer,ct);
                result=buffer.ToArray();
                if(result.Length<5 || !result.AsSpan(0,5).SequenceEqual("%PDF-"u8)) throw new IOException("Windows did not produce a valid PDF.");
            } catch(Exception error) { failure=error; }
            finally { deferral.Complete(); }
        };
        try { await dialog.ShowAsync(); if(failure is not null) throw new IOException("Windows PDF export failed.",failure); return result; }
        finally { view.Close(); }
    });
}
