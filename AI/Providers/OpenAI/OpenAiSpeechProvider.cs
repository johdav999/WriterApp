using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using WriterApp.AI.Abstractions;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.AI.Providers.OpenAI;

public sealed class OpenAiSpeechProvider(HttpClient http,WriterAiOptions options,string key):IAiProvider,IAiBillingProvider {
    public const string Id="openai-speech",Action="brainstorming.dictation";
    public string ProviderId=>Id;
    public AiProviderCapabilities Capabilities=>new(false,false,true);
    public bool IsBillable=>true;
    public bool RequiresEntitlement=>true;
    public static bool Configured(WriterAiOptions o,bool hasKey)=>o.Enabled && o.Speech.Enabled && hasKey && o.Providers.DefaultAudioProviderId==Id && o.Speech.Model is "gpt-transcribe" or "gpt-4o-transcribe" or "gpt-4o-mini-transcribe";
    public async Task<AiResult> ExecuteAsync(AiRequest request,CancellationToken ct) {
        if(!Configured(options,!string.IsNullOrWhiteSpace(key)) || request.ActionId!=Action || !request.Modalities.SequenceEqual([AiModality.Audio])
            || !request.Inputs.TryGetValue("audio",out var audio) || audio is not byte[] wav || !request.Inputs.TryGetValue("language",out var language) || language is not string lang)throw new InvalidOperationException("Configured transcription is unavailable.");
        BrainstormingSpeech.ValidateWav(wav);BrainstormingSpeech.RequireLanguage(lang);
        var baseAddress=new Uri(string.IsNullOrWhiteSpace(options.Providers.OpenAI.BaseUrl)?"https://api.openai.com/v1/":options.Providers.OpenAI.BaseUrl.TrimEnd('/')+"/");
        if(baseAddress.Scheme!="https")throw new InvalidOperationException("Speech provider requires HTTPS.");
        using var message=new HttpRequestMessage(HttpMethod.Post,new Uri(baseAddress,"audio/transcriptions"));message.Headers.Authorization=new("Bearer",key);
        if(!string.IsNullOrWhiteSpace(options.Providers.OpenAI.Organization))message.Headers.TryAddWithoutValidation("OpenAI-Organization",options.Providers.OpenAI.Organization);
        using var form=new MultipartFormDataContent();using var file=new ByteArrayContent(wav);file.Headers.ContentType=new("audio/wav");form.Add(file,"file","dictation.wav");form.Add(new StringContent(options.Speech.Model),"model");form.Add(new StringContent("json"),"response_format");
        form.Add(new StringContent(lang),options.Speech.Model=="gpt-transcribe"?"languages[]":"language");message.Content=form;
        if(options.Speech.Model!="gpt-transcribe")form.Add(new StringContent("logprobs"),"include[]");
        var clock=Stopwatch.StartNew();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Speech.TimeoutSeconds,1,90)));
        using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(!response.IsSuccessStatusCode)throw new BrainstormingSpeechException(response.StatusCode==System.Net.HttpStatusCode.TooManyRequests?"quota":"provider","Transcription provider unavailable; composer retained. Inspect this operation before recording again.");
        await using var input=await response.Content.ReadAsStreamAsync(timeout.Token);using var bytes=new MemoryStream();var block=new byte[4096];int count;
        while((count=await input.ReadAsync(block,timeout.Token))>0){if(bytes.Length+count>100_000)throw new InvalidDataException("Transcription response exceeded its limit.");bytes.Write(block,0,count);}
        using var json=JsonDocument.Parse(bytes.ToArray());var root=json.RootElement;
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("text",out var transcript) || transcript.ValueKind!=JsonValueKind.String)throw new InvalidDataException("Invalid recognition response.");var text=transcript.GetString()??"";
        if(text.Length>8000 || text.Contains('\0'))throw new InvalidDataException("Recognition exceeded composer capacity. Record a shorter passage.");
        if(!root.TryGetProperty("usage",out var usage) || usage.ValueKind!=JsonValueKind.Object || !usage.TryGetProperty("type",out var type) || type.ValueKind!=JsonValueKind.String || type.GetString()!="tokens"
            || !usage.TryGetProperty("input_tokens",out var inputTokens) || !inputTokens.TryGetInt32(out var incoming) || !usage.TryGetProperty("output_tokens",out var outputTokens) || !outputTokens.TryGetInt32(out var outgoing)
            || incoming<0 || outgoing<0 || (long)incoming+outgoing>100_000)throw new InvalidDataException("Configured transcription requires valid token usage accounting.");
        var quality=BrainstormingRecognitionQuality.Unassessed;
        if(root.TryGetProperty("logprobs",out var probabilities) && probabilities.ValueKind==JsonValueKind.Array && probabilities.GetArrayLength() is >0 and <=16000) {
            var values=new List<double>();foreach(var token in probabilities.EnumerateArray()){if(token.ValueKind!=JsonValueKind.Object || !token.TryGetProperty("logprob",out var log) || log.ValueKind!=JsonValueKind.Number || !log.TryGetDouble(out var number) || !double.IsFinite(number) || number>0){values.Clear();break;}values.Add(number);}
            if(values.Count>0)quality=values.Min()>=-1 && values.Average()>=-.3?BrainstormingRecognitionQuality.Clear:BrainstormingRecognitionQuality.NeedsReview;
        }
        return new(request.RequestId,[],new(incoming,outgoing,clock.Elapsed),new(){{"text",text},{"model",options.Speech.Model},{"recognitionQuality",quality}});
    }
}
