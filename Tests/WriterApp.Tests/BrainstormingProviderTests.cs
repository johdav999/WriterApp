using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.AI;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingProviderTests
{
    private sealed class Wire(string events):HttpMessageHandler,IHttpClientFactory {
        public int Calls;public JsonElement Payload;
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;Payload=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);return new(HttpStatusCode.OK){Content=new StringContent(events,Encoding.UTF8,"text/event-stream")};}
    }
    private static OpenAiProvider Provider(Wire wire) {
        var key=(OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider),BindingFlags.NonPublic|BindingFlags.Instance,null,["synthetic-test-key"],null)!;
        return new(wire,Options.Create(new WriterAiOptions{Providers=new(){OpenAI=new(){TextModel="configured-test-model"}}}),key,NullLogger<OpenAiProvider>.Instance);
    }
    private static string Event(object value,JsonSerializerOptions? options=null)=>"data: "+JsonSerializer.Serialize(value,options)+"\n\n";
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Utf8TransportPreservesUnicodeAndQuoteRepairOccursOnlyAfterJsonDecoding(bool escaped) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();
        const string prose="\u201cÅsa — 日本語 🧭,\u201d Anna said.\n\n\u001cWhat's going on?\u001d Luis asked.";
        var options=new JsonSerializerOptions(BrainstormingConversationContract.ResponseJson);
        if(!escaped)options.Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        var text=JsonSerializer.Serialize(new BrainstormingAnswer("Ready.",1,BrainstormingIntent.Draft,new(prose,"",[]),[],[]),options);
        // Small deltas also split JSON Unicode escapes across provider events.
        var body=string.Concat(text.Chunk(7).Select((part,index)=>Event(new{type="response.output_text.delta",delta=new string(part),sequence_number=index+1},options)))
            +Event(new{type="response.completed",sequence_number=text.Chunk(7).Count()+1,response=new{status="completed",output=new[]{new{type="message"}}}});
        if(!escaped)Assert.Contains("日本語",body);
        using var wire=new Wire(body);
        var request=BrainstormingConversationAction.Build(f.Send,(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!,f.Context,f.Target);
        var streamed=new StringBuilder();await foreach(var item in Provider(wire).StreamAsync(request,default))if(item is AiStreamEvent.TextDelta delta)streamed.Append(delta.Delta);
        Assert.Equal(text,streamed.ToString());
        Assert.Equal("\u201cÅsa — 日本語 🧭,\u201d Anna said.\n\n\u201cWhat's going on?\u201d Luis asked.",BrainstormingConversationContract.ReadAnswer(streamed.ToString(),f.Send,f.Context).Draft!.Text);
        Assert.Equal(1,wire.Calls);
    }
    [Fact]
    public async Task SceneCreationCarriesSemanticInstructionsAndAnUnchangedAuthorRequestThroughTheRealAdapter() {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();
        var send=f.Send with {Instruction="Add a scene where scientist anna is with her team in the arctics and drilling ice cores for environmental research"};
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,send.Scope.SessionId,default))!;
        var request=BrainstormingConversationAction.Build(send,session,f.Context,f.Target);
        var answer=new BrainstormingAnswer("An editable scene draft is ready for review. Keep commits it to the manuscript.",1,BrainstormingIntent.Draft,new("Anna braced the drill as the Arctic wind swept across the team's instruments.\n\nThe ice core rose into the pale light, its layers preserving the history they had come to study.","Environmental field research; manuscript prose",["Anna leads an Arctic ice-coring team"]),[],[]);
        var text=JsonSerializer.Serialize(answer,BrainstormingConversationContract.ResponseJson);
        using var wire=new Wire(Event(new{type="response.output_text.delta",delta=text,sequence_number=1})+Event(new{type="response.completed",sequence_number=2,response=new{status="completed",model="configured-test-model",output=new[]{new{type="message"}},usage=new{input_tokens=123,output_tokens=45}}}));
        var streamed=new StringBuilder();await foreach(var item in Provider(wire).StreamAsync(request,default))if(item is AiStreamEvent.TextDelta delta)streamed.Append(delta.Delta);
        Assert.Equal(answer.Draft!.Text,BrainstormingConversationContract.ReadAnswer(streamed.ToString(),send,f.Context).Draft!.Text);
        using var data=JsonDocument.Parse(wire.Payload.GetProperty("input").GetString()!);
        Assert.Equal(send.Instruction,data.RootElement.GetProperty("instruction").GetString());
        Assert.Single(request.Inputs);Assert.False(data.RootElement.TryGetProperty("forcedIntent",out _));
        Assert.Equal(BrainstormingConversationAction.SystemInstruction,wire.Payload.GetProperty("instructions").GetString());
        Assert.Contains("meaning of the whole current instruction",wire.Payload.GetProperty("instructions").GetString());
        Assert.Equal(1,wire.Calls);Assert.False(wire.Payload.GetProperty("store").GetBoolean());
        var format=wire.Payload.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema",format.GetProperty("type").GetString());Assert.True(format.GetProperty("strict").GetBoolean());
        var schema=format.GetProperty("schema");
        Assert.Contains("contradictions",schema.GetProperty("required").EnumerateArray().Select(x=>x.GetString()));
        Assert.Contains("revisionInstruction",schema.GetProperty("properties").GetProperty("draft").GetProperty("required").EnumerateArray().Select(x=>x.GetString()));
        void StrictObjects(JsonElement node) {
            if(node.TryGetProperty("properties",out var properties)) {
                Assert.False(node.GetProperty("additionalProperties").GetBoolean());
                Assert.Equal(properties.EnumerateObject().Select(p=>p.Name).Order(),node.GetProperty("required").EnumerateArray().Select(p=>p.GetString()).Order());
                foreach(var property in properties.EnumerateObject())StrictObjects(property.Value);
            }
            if(node.TryGetProperty("items",out var items))StrictObjects(items);
        }
        StrictObjects(schema);
        // Optional review packet for one separately authorized live-model check.
        if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_INTENT_PAYLOAD") is {Length:>0} path) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{request,send,context=f.Context,fictionalContext=true},BrainstormingValidation.Json));
        }
    }
    [Theory][InlineData("completed")][InlineData("eof")][InlineData("failed")][InlineData("incomplete")][InlineData("tool")][InlineData("reordered")]
    public async Task ActualOpenAiAdapterRequiresTerminalEventAndTransportsActualUsageWithoutRetry(string mode) {
        var text="{\"answer\":\"Three ideas.\",\"version\":1,\"intent\":\"Explore\",\"draft\":null,\"planning\":[],\"contradictions\":[]}";
        var body=Event(new{type="response.output_text.delta",delta=text,sequence_number=1});
        if(mode=="completed"||mode=="tool"||mode=="reordered")body+=Event(new{type="response.completed",sequence_number=mode=="reordered"?1:2,response=new{status="completed",model="configured-test-model",output=new[]{new{type=mode=="tool"?"function_call":"message"}},usage=new{input_tokens=123,output_tokens=45}}});
        if(mode is "failed" or "incomplete")body+=Event(new{type="response."+mode,sequence_number=2,response=new{status=mode}});
        using var wire=new Wire(body);var request=new AiRequest(Guid.NewGuid(),BrainstormingConversationAction.Id,[AiModality.Text],new(Guid.NewGuid(),Guid.Empty,new(0,0),"",null,null,null,null,null,null,null,null,null,null),new(){{"conversation_data","quoted story data"}},new(),new());
        var events=new List<AiStreamEvent>();async Task Run(){await foreach(var e in Provider(wire).StreamAsync(request,default))events.Add(e);}
        if(mode is "eof" or "reordered")await Assert.ThrowsAnyAsync<Exception>(Run);else await Run();
        if(mode=="completed") {var complete=Assert.IsType<AiStreamEvent.Completed>(events[^1]);Assert.Equal(123,complete.Usage!.InputTokens);Assert.Equal(45,complete.Usage.OutputTokens);Assert.Equal("configured-test-model",complete.Model);}
        else Assert.DoesNotContain(events,e=>e is AiStreamEvent.Completed);
        Assert.Equal(1,wire.Calls);Assert.True(wire.Payload.GetProperty("stream").GetBoolean());Assert.False(wire.Payload.GetProperty("store").GetBoolean());Assert.False(wire.Payload.TryGetProperty("tools",out _));Assert.Contains("Infer intent",wire.Payload.GetProperty("instructions").GetString());
    }
    [Theory][InlineData("Draft",true)][InlineData("Explore",false)][InlineData("ProposePlanning",false)]
    public async Task ProviderStringEnumsAreValidatedAndUnknownOrDuplicateFieldsRejected(string intent,bool prose) {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start();
        var wire=JsonSerializer.Serialize(new BrainstormingAnswer("Explanation",1,Enum.Parse<BrainstormingIntent>(intent),prose?new("Proposed prose","",[]):null,
            intent=="ProposePlanning"?[new(f.Context.Sources[0].Id,BrainstormingPlanningKind.SceneIdea,null,"idea","","Station")]:[],[]),BrainstormingConversationContract.ResponseJson);
        if(intent=="ProposePlanning")Assert.Throws<InvalidDataException>(()=>BrainstormingConversationContract.ReadAnswer(wire,f.Send,f.Context));
        else Assert.Equal(Enum.Parse<BrainstormingIntent>(intent),BrainstormingConversationContract.ReadAnswer(wire,f.Send,f.Context).Intent);
        Assert.ThrowsAny<Exception>(()=>BrainstormingConversationContract.ReadAnswer(wire[..^1]+",\"toolCall\":\"apply\"}",f.Send,f.Context));
        Assert.ThrowsAny<Exception>(()=>BrainstormingConversationContract.ReadAnswer(wire[..^1]+",\"answer\":\"Changed\"}",f.Send,f.Context));
    }
    [Fact]
    public async Task ExportServerProjectionFixturesForActualWebAndDeviceBundleComparison() {
        string[] html=["<h2>Åsa 日本語 🧭</h2><p>Hon <strong>tvivlar</strong>.<br>Ännu.</p>","<ul><li><p>First</p></li><li><p>Second</p></li></ul><table><tbody><tr><td><p>Third</p></td><td><p>Fourth</p></td></tr></tbody></table>","<p></p>","<p>A  B\nC</p><p></p><p>After</p>","<p>Before</p><hr><p>After</p>","<p>Before<img src=\"https://test.invalid/image.png\">After</p>","<pre><code> A\n B </code></pre><blockquote><p>Quote</p></blockquote>","<p>A</p>\n  <p>B</p>"];
        var fixtures=new List<object>();foreach(var content in html){var projected=await new BrainstormingServerProjector().ProjectAsync(new(Guid.NewGuid(),Guid.NewGuid(),0,"",content,"Html","v1",BrainstormingFreshness.Saved),default);fixtures.Add(new{content,expected=projected.PlainText});}
        if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"server-projection.json"),JsonSerializer.Serialize(fixtures));
    }
}
