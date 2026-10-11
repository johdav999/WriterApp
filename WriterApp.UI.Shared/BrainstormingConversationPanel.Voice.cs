using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.UI.Shared;

public partial class BrainstormingConversationPanel {
    [Parameter] public BrainstormingCapability? VoiceAvailability {get;set;}
    private BrainstormingVoiceCoordinator? _voice;
    private BrainstormingVoiceCoordinator Voice=>_voice??=Services.GetService(typeof(BrainstormingVoiceCoordinator)) as BrainstormingVoiceCoordinator??new(new UnsupportedBrainstormingSpeechOutput());
    private BrainstormingCapability? _outputAvailability;private bool _voiceMode,_voiceTurn,_sendSpoken,_muted,_voiceSubscribed;private long _voiceSequence;private string? _voiceMessage;
    private CancellationTokenSource? _spokenCancellation;
    private async Task CheckVoice(){if(!_voiceSubscribed){Voice.Changed+=VoiceChanged;Voice.Suspended+=VoiceSuspended;_voiceSubscribed=true;}try{_outputAvailability=await Voice.AvailabilityAsync(_speechLanguage);}catch(Exception e) when(e is IOException or InvalidOperationException or JSException){_outputAvailability=new(BrainstormingCapabilityState.Unavailable,"Speech output unavailable. Continue typing.");}if(!_disposed)StateHasChanged();}
    private void VoiceSuspended(){if(!_disposed)_=InvokeAsync(BackgroundVoice);}
    [JSInvokable] public async Task BackgroundVoice(){_voiceMode=false;await InterruptVoice();if(!_disposed){_voiceMessage="Voice stopped on background/navigation. Inspect pending operations; start again explicitly.";StateHasChanged();}}
    private void VoiceChanged(){if(!_disposed)_=InvokeAsync(StateHasChanged);}
    private async Task StartSpokenMode(){if(_disposed || _busy || _dictating || !Ready || VoiceAvailability?.IsAvailable!=true)return;await CheckVoice();if(_outputAvailability?.IsAvailable!=true){_voiceMessage=_outputAvailability?.Message;return;}_voiceMode=true;_voiceMessage="Spoken dialogue started. Each microphone turn is explicit; no listening during playback.";await RecordSpokenTurn();}
    private async Task RecordSpokenTurn(){if(!_voiceMode || _busy || _dictating || !Ready)return;var epoch=_epoch;var sequence=++_voiceSequence;var scope=Scope;var previous=_cache?.Dictations?.LastOrDefault()?.OperationId;var reviews=Voice.CaptureReviews(scope);_voiceTurn=true;_sendSpoken=false;var clock=System.Diagnostics.Stopwatch.StartNew();
        try {await Voice.StopAsync(scope);await Task.Delay(350);if(sequence!=_voiceSequence || epoch!=_epoch || !_voiceMode)return;await StartDictation();if(sequence!=_voiceSequence || epoch!=_epoch || _disposed || !_voiceMode || !_sendSpoken)return;
            var result=_cache?.Dictations?.LastOrDefault()?.Result;if(result?.State!=BrainstormingSpeechState.Completed || result.OperationId==previous){_voiceMessage="No final spoken turn was sent. Correct the retained composer or inspect dictation.";return;}
            _voiceTurn=false;var command=BrainstormingVoiceCommands.Classify(result.Text);if(command==BrainstormingVoiceCommand.Conversation && BrainstormingVoiceCommands.Classify(_composer)!=BrainstormingVoiceCommand.Conversation)command=BrainstormingVoiceCommand.ConfirmationRequired;
            if(command!=BrainstormingVoiceCommand.Conversation){var approved=_composer.Trim()==result.Text.Trim() && await Voice.ApproveAsync(scope,command,result.Quality,reviews);_voiceMessage=approved?"Spoken approval entered the exact reviewed action. Inspect its application receipt; recognition alone is not proof of a commit.":"Confirm the exact visible Keep version or selected planning fields. Negation, uncertainty, changed reviews and mixed typed text never apply automatically.";return;}
            if(result.Quality==BrainstormingRecognitionQuality.NeedsReview){_voiceMessage="Recognition needs review. Correct your message and choose Send; no spoken turn was submitted.";return;}
            var previousGeneration=_cache?.LastGeneration?.Send.OperationId;
            await SendConversation(spoken:true);
            if(sequence!=_voiceSequence || epoch!=_epoch || !_voiceMode || _muted || _cache?.LastGeneration?.State!=BrainstormingGenerationState.Completed || _cache.LastGeneration.Send.OperationId==previousGeneration)return;
            var answer=_cache.Session.Turns.LastOrDefault(t=>t.Role==BrainstormingRole.Assistant && t.State==BrainstormingTurnState.Complete);if(answer is null)return;var text=BrainstormingVoiceCommands.Bounded(answer.Text,600);_voiceMessage=$"Answer saved. First {text.Length} characters queued for local synthetic voice · turn elapsed {clock.Elapsed.TotalSeconds:F1}s. Full answer/prose remains visible.";
            await Voice.PlayAsync(new(1,Guid.NewGuid(),scope,answer.Id,BrainstormingValidation.Fingerprint(answer.Text),_speechLanguage,text,BrainstormingPlaybackKind.Answer));
            if(sequence==_voiceSequence && epoch==_epoch)_voiceMessage=$"Spoken turn finished · elapsed {clock.Elapsed.TotalSeconds:F1}s. Choose Speak next turn or continue typing.";
        }catch(Exception e) when(e is IOException or InvalidOperationException or OperationCanceledException or JSException){if(epoch==_epoch && !_disposed)_voiceMessage=e is OperationCanceledException?"Spoken turn interrupted; saved transcript and composer retained. Start the next turn explicitly.":e.Message;}
        finally{if(epoch==_epoch && sequence==_voiceSequence){_voiceTurn=false;_sendSpoken=false;}}
    }
    private async Task FinishSpokenTurn(){if(!_voiceTurn || _recording is null)return;_sendSpoken=true;await StopDictation();}
    private async Task InterruptVoice(){_voiceSequence++;_voiceTurn=false;_sendSpoken=false;await Voice.StopAsync(Scope);await CancelDictation();if(_spokenCancellation is not null && ReferenceEquals(_cancel,_spokenCancellation) && _busy)await Stop();_voiceMessage="Interrupted. Microphone/playback stopped; completed turns remain and incomplete generation must be inspected.";}
    private async Task EndSpokenMode(){_voiceMode=false;await InterruptVoice();_voiceMessage="Spoken dialogue ended. Continue in the same text conversation.";}
    private async Task ToggleVoiceMute(){_muted=!_muted;if(_muted)await Voice.StopAsync(Scope);_voiceMessage=_muted?"Spoken answers muted. Transcripts and text conversation remain available.":"Spoken answers enabled for the next explicitly submitted spoken turn.";}
    private void InvalidateVoice(){_voiceSequence++;_voiceMode=false;_sendSpoken=false;if(_voice is not null)_=Voice.StopAsync(_loaded);}
}
