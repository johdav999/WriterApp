using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.AI.Providers.OpenAI;

public sealed partial class OpenAiProvider
{
    private static readonly JsonElement ConversationResponseFormat = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "json_schema", "name": "brainstorming_answer", "strict": true,
          "schema": {
            "type": "object", "additionalProperties": false,
            "required": ["answer", "version", "intent", "draft", "planning", "contradictions"],
            "properties": {
              "answer": {"type": "string"},
              "version": {"type": "integer", "enum": [1]},
              "intent": {"type": "string", "enum": ["Explore", "Clarify", "Draft", "Revise", "ProposePlanning"]},
              "draft": {
                "type": ["object", "null"], "additionalProperties": false,
                "required": ["text", "constraints", "introducedFacts", "continuation", "revisionInstruction"],
                "properties": {
                  "text": {"type": "string"},
                  "constraints": {"type": "string"},
                  "introducedFacts": {"type": "array", "items": {"type": "string"}},
                  "continuation": {"type": "boolean"},
                  "revisionInstruction": {"type": ["string", "null"]}
                }
              },
              "planning": {
                "type": "array", "items": {
                  "type": "object", "additionalProperties": false,
                  "required": ["sourceId", "kind", "entityKey", "field", "before", "proposed"],
                  "properties": {
                    "sourceId": {"type": "string"},
                    "kind": {"type": "string", "enum": ["SceneIdea", "StoryNote", "SynopsisField", "CharacterNote"]},
                    "entityKey": {"type": ["string", "null"]},
                    "field": {"type": "string"},
                    "before": {"type": "string"},
                    "proposed": {"type": "string"}
                  }
                }
              },
              "contradictions": {"type": "array", "items": {"type": "string"}}
            }
          }
        }
        """);

    private async IAsyncEnumerable<AiStreamEvent> StreamConversationAsync(AiRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) { yield return new AiStreamEvent.Failed("OpenAI is not configured."); yield break; }
        var payload = new { model = _options.TextModel, instructions = BrainstormingConversationAction.SystemInstruction,
            input = GetInputValue(request,"conversation_data",""), stream = true, store = false,
            text = new { format = ConversationResponseFormat },
            max_output_tokens = Math.Clamp(Math.Max(_options.MaxOutputTokens,8192),8192,32768) };
        using var message = new HttpRequestMessage(HttpMethod.Post,BuildUri(ResponsesEndpoint)) { Content = new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer",_apiKey);
        using var response = await _httpClientFactory.CreateClient(nameof(OpenAiProvider)).SendAsync(message,HttpCompletionOption.ResponseHeadersRead,ct);
        await EnsureSuccessAsync(response,ct);
        yield return new AiStreamEvent.Started();
        var clock=Stopwatch.StartNew(); await using var stream=await response.Content.ReadAsStreamAsync(ct); using var reader=new StreamReader(stream,Encoding.UTF8);
        int total=0; long last=-1;
        while(await ReadConversationLine(reader,ct) is { } line) {
            if(!line.StartsWith("data:",StringComparison.Ordinal))continue;
            var data=line[5..].Trim(); if(data=="[DONE]")break;
            using var json=JsonDocument.Parse(data); var root=json.RootElement;
            if(root.TryGetProperty("sequence_number",out var order)) { var number=order.GetInt64(); if(number<=last)throw new InvalidDataException("Reordered provider stream.");last=number; }
            string? type=root.GetProperty("type").GetString();
            if(type=="response.output_text.delta") { var text=root.GetProperty("delta").GetString()??"";total+=text.Length;
                if(total>BrainstormingConversationContract.MaxOutput)throw new InvalidDataException("Oversized provider response."); yield return new AiStreamEvent.TextDelta(text); }
            else if(type=="response.completed") {
                var result=root.GetProperty("response"); if(result.GetProperty("status").GetString()!="completed")break;
                // No model tool call is executed or admitted as a conversation result.
                if(result.TryGetProperty("output",out var output) && output.EnumerateArray().Any(i=>i.GetProperty("type").GetString() is not ("message" or "reasoning"))) {
                    yield return new AiStreamEvent.Failed("Unexpected provider tool output."); yield break; }
                var usage=result.TryGetProperty("usage",out var u) && u.ValueKind==JsonValueKind.Object
                    ? new AiUsage(u.GetProperty("input_tokens").GetInt32(),u.GetProperty("output_tokens").GetInt32(),clock.Elapsed) : new(0,0,clock.Elapsed);
                yield return new AiStreamEvent.Completed(usage,result.TryGetProperty("model",out var model)?model.GetString():_options.TextModel);yield break;
            }
            else if(type is "response.failed" or "response.incomplete" or "error") { yield return new AiStreamEvent.Failed("Provider returned a failed or incomplete conversation."); yield break; }
        }
        // EOF is not proof of completion; the service retains this operation as uncertain.
        throw new IOException("Provider stream ended without a completed response.");
    }
    private static async Task<string?> ReadConversationLine(StreamReader reader,CancellationToken ct) {
        var line=new StringBuilder(); var character=new char[1];
        while(await reader.ReadAsync(character.AsMemory(),ct)>0) {
            if(character[0]=='\n')return line.ToString().TrimEnd('\r');
            if(line.Length>=1_000_000)throw new InvalidDataException("Oversized provider event.");line.Append(character[0]);
        }
        return line.Length==0?null:line.ToString();
    }
}
