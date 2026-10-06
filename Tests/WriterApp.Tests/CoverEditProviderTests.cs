using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Covers;
using WriterApp.Application.Usage;
using WriterApp.Data.Usage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class CoverEditProviderTests
{
    private sealed class Provider:IAiProvider,IAiBillingProvider {
        public string ProviderId=>"openai";public AiProviderCapabilities Capabilities=>new(true,true);public bool RequiresEntitlement=>true;public bool IsBillable=>true;
        public Task<AiResult> ExecuteAsync(AiRequest request,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class Policy:IAiUsagePolicy {
        public int Calls;public bool Denied;public Task<AiUsageDecision> EvaluateAsync(IAiProvider provider,string action) {Calls++;Assert.Equal("generate.image.cover",action);return Task.FromResult(new AiUsageDecision(!Denied,"test-owner",Denied?"ai.quota_exceeded":null,Denied?"Quota denied":null));}
    }
    private sealed class Meter:IUsageMeter {
        public List<UsageEvent> Events=[];public Task RecordAsync(UsageEvent e){Events.Add(e);return Task.CompletedTask;}
        public Task<UsageSnapshot> GetCurrentPeriodAsync(string user,string kind)=>throw new NotSupportedException();
        public Task<UsageSnapshot> GetRangeAsync(string user,string kind,DateTime a,DateTime b)=>throw new NotSupportedException();
    }
    // Every provider HTTP request is intercepted. No real credential, file or external connection is used.
    private sealed class Wire:HttpMessageHandler {
        public int Calls;public string Mode="valid";public string? Prompt;public byte[]? Source;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct) {
            Calls++;Assert.Equal("https://api.openai.com/v1/images/edits",r.RequestUri!.AbsoluteUri);Assert.Equal("synthetic-test-key",r.Headers.Authorization!.Parameter);
            var body=Assert.IsType<MultipartFormDataContent>(r.Content);var fields=body.ToDictionary(x=>x.Headers.ContentDisposition!.Name!.Trim('"'));
            Assert.Equal("1",await fields["n"].ReadAsStringAsync(ct));Assert.Equal("png",await fields["output_format"].ReadAsStringAsync(ct));
            Assert.Equal("gpt-image-1",await fields["model"].ReadAsStringAsync(ct));Assert.Equal("image/png",fields["image"].Headers.ContentType!.MediaType);
            Source=await fields["image"].ReadAsByteArrayAsync(ct);Prompt=await fields["prompt"].ReadAsStringAsync(ct);
            if(Mode=="oversize") {var content=new StringContent("{}");content.Headers.ContentLength=4*1024*1024+1;return new(HttpStatusCode.OK){Content=content};}
            object data=Mode switch {"empty"=>Array.Empty<object>(),"double"=>new[]{new{b64_json=CoverTestFixture.SecondPng},new{b64_json=CoverTestFixture.SecondPng}},
                "unchanged"=>new[]{new{b64_json=CoverTestFixture.Png}},"invalid"=>new[]{new{b64_json="YWJj"}},_=>new[]{new{b64_json=CoverTestFixture.SecondPng}}};
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new{data,usage=new{input_tokens=100,output_tokens=200}})};
        }
    }
    private static CoverImageService Service(Wire wire,Policy policy,Meter meter,string model="gpt-image-1") {
        var key=(OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider),BindingFlags.NonPublic|BindingFlags.Instance,null,["synthetic-test-key"],null)!;
        var options=new WriterAiOptions();options.Providers.OpenAI.ImageModel=model;
        return new(new HttpClient(wire),key,new DefaultAiProviderRegistry([new Provider()]),policy,Options.Create(options),NullLogger<CoverImageService>.Instance,meter);
    }
    [Theory][InlineData("variation")][InlineData("darker")][InlineData("brighter")][InlineData("cinematic")][InlineData("minimal")]
    public async Task RealAdapterRoutesExactSourceMultipartAndDistinctInstructionsWithUsage(string operation) {
        using var wire=new Wire();var policy=new Policy();var meter=new Meter();var service=Service(wire,policy,meter);
        string image=await service.EditAsync(Convert.FromBase64String(CoverTestFixture.Png),operation,new(){Description="Åsa 日本語"});
        Assert.Equal(CoverTestFixture.SecondImage,image);Assert.Equal(Convert.FromBase64String(CoverTestFixture.Png),wire.Source);
        Assert.Contains(CoverEdits.Instruction(operation),wire.Prompt);Assert.Contains("Åsa 日本語",wire.Prompt);Assert.Equal(1,policy.Calls);
        var usage=Assert.Single(meter.Events);Assert.Equal(100,usage.InputTokens);Assert.Equal(200,usage.OutputTokens);Assert.Equal("test-owner",usage.UserId);
    }
    [Theory][InlineData("dall-e-3")][InlineData("unknown-model")]
    public async Task UnsupportedConfigurationRefusesBeforeQuotaOrTransport(string model) {
        using var wire=new Wire();var policy=new Policy();var service=Service(wire,policy,new(),model);Assert.Empty(service.EditCapabilities.Operations);
        await Assert.ThrowsAsync<CoverImageGenerationException>(()=>service.EditAsync(Convert.FromBase64String(CoverTestFixture.Png),"variation",new()));Assert.Equal(0,wire.Calls);Assert.Equal(0,policy.Calls);
    }
    [Fact] public async Task QuotaDenialNeverSendsImageOrRecordsUsage() {
        using var wire=new Wire();var policy=new Policy{Denied=true};var meter=new Meter();await Assert.ThrowsAsync<CoverImageGenerationException>(()=>Service(wire,policy,meter).EditAsync(Convert.FromBase64String(CoverTestFixture.Png),"darker",new()));Assert.Equal(0,wire.Calls);Assert.Empty(meter.Events);
    }
    [Theory][InlineData("empty")][InlineData("double")][InlineData("unchanged")][InlineData("invalid")][InlineData("oversize")]
    public async Task InvalidProviderMediaCannotBecomeAConcept(string mode) {
        using var wire=new Wire{Mode=mode};var meter=new Meter();await Assert.ThrowsAnyAsync<Exception>(()=>Service(wire,new(),meter).EditAsync(Convert.FromBase64String(CoverTestFixture.Png),"minimal",new()));
        Assert.Equal(1,wire.Calls);if(mode!="oversize")Assert.Single(meter.Events);
    }
}
