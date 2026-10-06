using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Pages;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class RemoteCoverPublishingPageTests
{
    private sealed class Components:IComponentActivator {
        public Publishing Page=null!;
        public IComponent CreateInstance(Type type){var component=(IComponent)Activator.CreateInstance(type)!;if(component is Publishing page)Page=page;return component;}
    }
    private sealed class Files:IDeviceFileDialog {
        public bool IsAvailable=>true;public byte[]? Content;public int Saves;
        public Task<DeviceImportFile?> PickImportAsync(CancellationToken cancellationToken=default)=>Task.FromResult<DeviceImportFile?>(null);
        public Task<bool> SaveAsync(string name,string extension,byte[] content,CancellationToken ct=default){Saves++;Content=content;return Task.FromResult(true);}
    }
    [Fact]
    public async Task ActualPublishingPageUsesScopedOfflineAssetAndClearsPreviewAndRefusesExportAfterSignOut() {
        using var f=new CoverTestFixture();await f.Start();await RemoteCoverAssetTests.SetRemote(f);await f.Studio.MaterializeProjectCoverAsync(f.Source);f.Network.SetOnline(false);
        var settings=new LocalPublishingStore(Path.Combine(f.Root,"publishing"));var state=await settings.LoadAsync(f.Source);
        await settings.SaveAsync(f.Source,state with{Options=new(DeviceExportFormat.Html,IncludeCover:true)});
        var components=new Components();var files=new Files();using var services=new ServiceCollection().AddLogging()
            .AddSingleton(f.Documents).AddSingleton(f.Account).AddSingleton(f.Studio).AddSingleton(settings)
            .AddSingleton<IDeviceFileDialog>(files).AddSingleton<IDevicePdfExport>(new UnavailableDevicePdfExport()).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Publishing>(ParameterView.FromDictionary(new Dictionary<string,object?>{[nameof(Publishing.DocumentId)]=f.Source.DocumentId})));
        Task Event(string name)=>renderer.Dispatcher.InvokeAsync(()=>EventCallback.Factory.Create(components.Page,(Func<Task>)(()=>
            (Task)typeof(Publishing).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(components.Page,null)!)).InvokeAsync());
        Task<string> Html()=>renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Equal(CoverTestFixture.Image,new AngleSharp.Html.Parser.HtmlParser().ParseDocument(await Html()).QuerySelector("img.cover")!.GetAttribute("src"));
        await Event("Preview");Assert.Contains("Saved writing revision",await Html());
        if(Environment.GetEnvironmentVariable("WRITERAPP_P16_EVIDENCE") is { } evidence) {
            string folder=Path.Combine(evidence,"p16");Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"remote-publishing-offline.html"),await Html());
        }
        await Event("Export");Assert.Equal(1,files.Saves);Assert.Contains(CoverTestFixture.Png,System.Text.Encoding.UTF8.GetString(files.Content!));
        await renderer.Dispatcher.InvokeAsync(()=>f.Account.SignOutAsync());Assert.DoesNotContain(CoverTestFixture.Png,await Html());
        await Event("Export");Assert.Equal(1,files.Saves);Assert.Contains("Cache the exact owned project cover",await Html());Assert.Equal(1,f.Remote!.Calls);
    }
}
