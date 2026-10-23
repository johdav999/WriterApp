using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class CoverBriefTests
{
    private sealed class Components:IComponentActivator {
        public CoverBrief Brief=null!;
        public IComponent CreateInstance(Type type){var result=(IComponent)Activator.CreateInstance(type)!;if(result is CoverBrief brief)Brief=brief;return result;}
    }
    [Fact]
    public async Task ConsecutiveFieldEventsRetainTypedDescriptionBeforeParentParameterRefresh()
    {
        var components=new Components();var updates=new List<CoverPrompt>();
        await using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<CoverBrief>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(CoverBrief.Value)]=new CoverPrompt{Genre="Fantasy",Mood="Dark",Style="Cinematic",ColorPalette="Warm"},
            [nameof(CoverBrief.ValueChanged)]=EventCallback.Factory.Create<CoverPrompt>(updates,(CoverPrompt value)=>updates.Add(value))
        })));
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var change=typeof(CoverBrief).GetMethod("Change",BindingFlags.Instance|BindingFlags.NonPublic)!;
            await (Task)change.Invoke(components.Brief,["A lighthouse over a quiet sea.",null,null,null,null])!;
            await (Task)change.Invoke(components.Brief,[null,null,null,"Painted",null])!;
            await (Task)change.Invoke(components.Brief,[null,"Literary","Hopeful",null,"Cold"])!;
        });
        var latest=updates[^1];Assert.Equal("A lighthouse over a quiet sea.",latest.Description);Assert.Equal("Painted",latest.Style);
        Assert.Equal("Literary",latest.Genre);Assert.Equal("Hopeful",latest.Mood);Assert.Equal("Cold",latest.ColorPalette);
        Assert.Equal("Cinematic",updates[0].Style);
    }
}
