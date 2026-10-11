namespace WriterApp.Shared.Brainstorming;

public enum BrainstormingPlaybackKind { Answer, Draft }
public enum BrainstormingPlaybackState { Preparing, Playing, Completed, Stopped, Failed }
public sealed record BrainstormingPlayback(int Version,Guid Id,BrainstormingScope Scope,Guid SourceId,string Fingerprint,string Language,string Text,BrainstormingPlaybackKind Kind) {
    public void Validate(){BrainstormingValidation.Validate(Scope);BrainstormingSpeech.RequireLanguage(Language);if(Version!=1 || Id==Guid.Empty || SourceId==Guid.Empty || Fingerprint is null || Fingerprint.Length!=64 || !Fingerprint.All(Uri.IsHexDigit) || Text is null || Text.Length is <1 or >4000 || Text.Contains('\0') || !Enum.IsDefined(Kind))throw new InvalidDataException("Invalid bounded playback source.");}
}
public sealed record BrainstormingPlaybackProgress(Guid Id,BrainstormingPlaybackState State,string Message);
public interface IBrainstormingSpeechOutput {
    event Action<BrainstormingPlaybackProgress>? Progress;
    Task<BrainstormingCapability> AvailabilityAsync(string language,CancellationToken ct=default);
    Task PlayAsync(BrainstormingPlayback playback,CancellationToken ct=default);
    Task StopAsync();
}
public sealed class UnsupportedBrainstormingSpeechOutput:IBrainstormingSpeechOutput {
    public event Action<BrainstormingPlaybackProgress>? Progress {add{} remove{}}
    public Task<BrainstormingCapability> AvailabilityAsync(string language,CancellationToken ct=default)=>Task.FromResult(new BrainstormingCapability(BrainstormingCapabilityState.UnsupportedPlatform,"Install a supported English/Swedish local output voice or continue typing."));
    public Task PlayAsync(BrainstormingPlayback playback,CancellationToken ct=default)=>throw new InvalidOperationException("Speech output unavailable. Continue with text.");
    public Task StopAsync()=>Task.CompletedTask;
}
public enum BrainstormingVoiceCommand { Conversation, Keep, Planning, ConfirmationRequired }
public static class BrainstormingVoiceCommands {
    public static BrainstormingVoiceCommand Classify(string text) {
        var normalized=text.Trim().TrimEnd('.','!').ToLowerInvariant();
        if(normalized is "keep this version" or "keep this draft" or "behåll den här versionen" or "behåll denna version")return BrainstormingVoiceCommand.Keep;
        if(normalized is "approve these planning fields" or "save these planning changes" or "godkänn dessa planeringsfält" or "spara dessa planeringsändringar")return BrainstormingVoiceCommand.Planning;
        // Application-like speech is never sent through a generative interpretation as authority.
        if(System.Text.RegularExpressions.Regex.IsMatch(normalized,@"\b(keep|behåll)\b.*\b(version|draft|this|that|it|versionen|utkast|denna|detta|den|det)\b|\b(save|spara)\b.*\b(idea|note|changes|fields|planning|this|that|it|decision|scene|synopsis|character|idé|anteckning|ändringar|fält|planering|scen|det|den)\b|\b(approve|apply|delete|undo|redo|godkänn|tillämpa|radera|ångra)\b",System.Text.RegularExpressions.RegexOptions.CultureInvariant))return BrainstormingVoiceCommand.ConfirmationRequired;
        return BrainstormingVoiceCommand.Conversation;
    }
    public static string Bounded(string text,int limit){if(text.Length<=limit)return text;var length=limit;if(char.IsHighSurrogate(text[length-1]))length--;var boundary=text.LastIndexOf(' ',length-1);if(boundary>length/2)length=boundary;return text[..length];}
}

/// <summary>One audio floor per host scope. Playback cannot overlap capture; reviews are never inferred from speech.</summary>
public sealed class BrainstormingVoiceCoordinator(IBrainstormingSpeechOutput output):IDisposable,IAsyncDisposable {
    private readonly SemaphoreSlim gate=new(1,1);private readonly object reviewLock=new();private readonly Dictionary<BrainstormingVoiceCommand,Review> reviews=[];
    private CancellationTokenSource? playbackCancel;private Capture? capture;private bool disposed;
    public BrainstormingPlayback? Active {get;private set;}public BrainstormingPlaybackProgress? Progress {get;private set;}public bool IsCapturing=>capture is not null;
    public event Action? Changed;
    public event Action? Suspended;
    public async Task SuspendAsync(){Suspended?.Invoke();await StopAsync();}
    private sealed record Review(Guid Token,BrainstormingScope Scope,string Hash,Func<bool> Current,Func<Task> Confirm);
    public sealed record Ticket(Guid Token,BrainstormingVoiceCommand Command,BrainstormingScope Scope,string Hash);
    public Task<BrainstormingCapability> AvailabilityAsync(string language,CancellationToken ct=default)=>output.AvailabilityAsync(language,ct);
    public IDisposable Register(BrainstormingVoiceCommand command,BrainstormingScope scope,string hash,Func<bool> current,Func<Task> confirm){var value=new Review(Guid.NewGuid(),scope,hash,current,confirm);lock(reviewLock)reviews[command]=value;return new Registration(()=>{lock(reviewLock){if(reviews.GetValueOrDefault(command)?.Token==value.Token)reviews.Remove(command);}});}
    public Ticket[] CaptureReviews(BrainstormingScope scope){lock(reviewLock)return reviews.Where(p=>p.Value.Scope==scope && p.Value.Current()).Select(p=>new Ticket(p.Value.Token,p.Key,scope,p.Value.Hash)).ToArray();}
    public async Task<bool> ApproveAsync(BrainstormingScope scope,BrainstormingVoiceCommand command,BrainstormingRecognitionQuality quality,IReadOnlyList<Ticket> captured) {
        if(command is not (BrainstormingVoiceCommand.Keep or BrainstormingVoiceCommand.Planning) || quality!=BrainstormingRecognitionQuality.Clear)return false;
        Review? review;lock(reviewLock){review=reviews.GetValueOrDefault(command);if(review is null || review.Scope!=scope || !captured.Any(t=>t.Token==review.Token && t.Command==command && t.Scope==scope && t.Hash==review.Hash) || !review.Current())return false;reviews.Remove(command);}
        await review.Confirm();return true; // Entered the checked approval handler; inspect its actual receipt for success.
    }
    public async Task<Capture> AcquireMicrophoneAsync(BrainstormingScope scope,CancellationToken ct=default) {
        await gate.WaitAsync(ct);try{ObjectDisposedException.ThrowIf(disposed,this);if(capture is not null)throw new InvalidOperationException("Finish the current recording before opening another microphone.");await StopPlayback();var value=new Capture(this,scope,CancellationTokenSource.CreateLinkedTokenSource(ct));capture=value;Changed?.Invoke();return value;}finally{gate.Release();}
    }
    public sealed class Capture(BrainstormingVoiceCoordinator owner,BrainstormingScope scope,CancellationTokenSource cancellation):IAsyncDisposable {
        public BrainstormingScope Scope=>scope;public CancellationToken Token=>cancellation.Token;internal void Cancel()=>cancellation.Cancel();
        public async ValueTask DisposeAsync(){await owner.gate.WaitAsync();try{if(owner.capture!=this)return;owner.capture=null;cancellation.Cancel();cancellation.Dispose();owner.Changed?.Invoke();}finally{owner.gate.Release();}}
    }
    public async Task PlayAsync(BrainstormingPlayback value,CancellationToken ct=default) {
        value.Validate();CancellationTokenSource cancel;await gate.WaitAsync(ct);try{ObjectDisposedException.ThrowIf(disposed,this);if(capture is not null)throw new InvalidOperationException("Stop recording before playback; microphone and output never overlap.");await StopPlayback();cancel=CancellationTokenSource.CreateLinkedTokenSource(ct);cancel.CancelAfter(TimeSpan.FromSeconds(120));playbackCancel=cancel;Active=value;Progress=new(value.Id,BrainstormingPlaybackState.Preparing,"Preparing local synthetic voice…");output.Progress+=OnProgress;Changed?.Invoke();}finally{gate.Release();}
        try{await output.PlayAsync(value,cancel.Token);if(!cancel.IsCancellationRequested)OnProgress(new(value.Id,BrainstormingPlaybackState.Completed,"Playback completed; saved text is unchanged."));}
        catch(OperationCanceledException){OnProgress(new(value.Id,BrainstormingPlaybackState.Stopped,"Playback interrupted; saved text retained."));throw;}
        catch(Exception e) when(e is IOException or InvalidOperationException){OnProgress(new(value.Id,BrainstormingPlaybackState.Failed,e.Message));throw;}
        finally{await gate.WaitAsync();try{output.Progress-=OnProgress;if(ReferenceEquals(playbackCancel,cancel)){playbackCancel=null;Active=null;Changed?.Invoke();}cancel.Dispose();}finally{gate.Release();}}
    }
    private void OnProgress(BrainstormingPlaybackProgress progress){if(Active?.Id!=progress.Id)return;Progress=progress;Changed?.Invoke();}
    private async Task StopPlayback(){playbackCancel?.Cancel();await output.StopAsync();if(Active is { } active)Progress=new(active.Id,BrainstormingPlaybackState.Stopped,"Playback interrupted; saved text retained.");Active=null;}
    public async Task StopAsync(BrainstormingScope? scope=null){await gate.WaitAsync();try{if(scope is null || Active?.Scope==scope)await StopPlayback();if(capture is { } value && (scope is null || value.Scope==scope))value.Cancel();Changed?.Invoke();}finally{gate.Release();}}
    private Task? disposal;
    public void Dispose(){if(disposed)return;disposed=true;lock(reviewLock)reviews.Clear();disposal=StopAsync();}
    public async ValueTask DisposeAsync(){Dispose();if(disposal is not null)await disposal;}
    private sealed class Registration(Action unregister):IDisposable {public void Dispose()=>unregister();}
}
