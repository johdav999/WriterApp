# Brainstorming prompt 11 spoken dialogue evidence

Date: 2026-10-07. Profile: authenticated manuscript-aware brainstorming in the
web client/server and Windows desktop. Authors explicitly record each spoken turn
in their existing text session, inspect final transcripts, hear concise answers,
interrupt output, revise proposals and approve only an exact visible review.
Standalone documents and mapped project manuscripts keep their existing ownership,
source selection, target, draft ancestry, history and recovery rules.

Implementation is complete; actual human English/Swedish microphone, recognition
and audible playback acceptance remain open. Synthetic audio, provider responses
and output events below are deliberately labeled. Native startup is a separate check.

## Pipeline and limits

The pipeline uses P10's explicitly configured, authenticated server transcription,
the existing metered text conversation and platform local text-to-speech. There is
no second assistant memory or client provider credential. Default Speech remains
disabled with a blank model. Backend capability requires the configured real Audio
provider/model/key; the host additionally checks capture and local output support.
The selected language must have its own installed/local voice. Unsupported hosts
retain typing, dictation when available, and cached review.

Web uses SpeechSynthesis with only voices whose `localService` is true. Remote
browser voices are refused. Windows uses installed SpeechSynthesizer voices and
MediaPlayer, plain text rather than SSML. Local synthesis adds no backend AI usage
charge; transcription and generation retain their actual existing quota/usage.
English and Svenska are explicit choices. No sound/capture starts from rendering,
capability discovery, view changes or selecting a language.

Official API checks used for this implementation:

- [OpenAI speech-to-text guide](https://developers.openai.com/api/docs/guides/speech-to-text): configured model, JSON final transcript, explicit language and supported logprobs. The 4o transcription family requests `include[]=logprobs`; gpt-transcribe does not receive that unsupported parameter.
- [Browser SpeechSynthesis](https://developer.mozilla.org/en-US/docs/Web/API/SpeechSynthesis) and [localService](https://developer.mozilla.org/en-US/docs/Web/API/SpeechSynthesisVoice/localService): explicit speak/cancel, available voices and local output selection.
- [Windows SpeechSynthesizer](https://learn.microsoft.com/en-us/uwp/api/windows.media.speechsynthesis.speechsynthesizer?view=winrt-28000) and [MediaPlayer](https://learn.microsoft.com/en-us/uwp/api/windows.media.playback.mediaplayer?view=winrt-28000): installed voices, synthesis streams, playback events and stop/release.

Only explicit Finish submits a final spoken turn. STT remains bounded to P10's
60-second PCM16 WAV and durable operation inspection; no raw audio is persisted.
Output reads the first 600 characters of a saved answer or, on a separate explicit
action, the first 1,000 characters of the identified draft. Full answer and prose
remain visible. Portable playback is bounded to 4,000 characters, a source ID,
fingerprint, language and scope; Windows transient synthesized audio is capped at
4 MB. Playback stops at 120 seconds; web refuses a start that takes over 15 seconds.
Visible state distinguishes preparation, actual output start, completion, stop and
failure. Turn elapsed time is measured, without claiming a latency guarantee.

The shared audio floor stops output before opening capture, refuses output while
capture holds the floor, and rejects stale output IDs. A 350 ms drain precedes a
new explicitly requested spoken capture. There is no continuous microphone,
automatic next turn or playback-fed instruction stream. Physical speaker reverberation
and audible echo behavior still need live acceptance, including a headphones check.

Spoken approval requires an exact finalized command, no mixed typed prefix, Clear
recognition evidence and a review ticket captured before recording. The same exact
version/target or selected planning fields must still be visibly reviewed and current.
The ticket enters the existing checked Keep/Approve handler once; only its durable
receipt establishes a commit. Negation, questions, changed reviews and unknown/poor
recognition request focused visible confirmation. The provider quality heuristic
requires finite nonpositive token logprobs, minimum >= -1 and mean >= -0.3;
missing/invalid evidence is Unassessed. This is conservative evidence, not a guarantee
of semantic recognition accuracy. gpt-transcribe without such evidence requires
button confirmation for application. Low-confidence ordinary speech is retained for
correction and explicit Send. Narrative instructions such as “keep the dialogue
understated” remain conversation, not an application command.

No P11 migration or editor vocabulary/asset change. Optional typed recognition
Quality is added to existing P10 result/cache JSON, omitted when Unassessed; matching
server and host builds are required for strict decoders. P10 transcription migrations,
P9 RecoveryJson migrations and sync v5 remain required. The shared pull revision
clock now advances to at least the imported server clock so multi-turn accepted
requests retain their original revision provenance and remain valid locally.

## Flow packets and evidence

| Flow | Production behavior exercised | Evidence scope |
|---|---|---|
| P11-F01 Explore, type, draft and spoken revision | Shared Start/Finish uses real web and mapped device adapters, owned authenticated TestServer, migrated SQLite and the same context/target. Typed prefix and final transcript are ordered once; saved answer source is used for output. Typed drafting then spoken revision produces two linked unapplied versions and keeps manuscript intact; End returns to typing in the same session. Charges remain STT plus text generation only. | BrainstormingSpeechTests; microphone/provider/output are synthetic. Does not establish audible speech. |
| P11-F02 Interrupt and mute/end | Shared Interrupt cancels the actual pending generation and stops microphone/output. Incomplete output stays inspectable, with no draft/application or spoken answer. Coordinator proves exclusive capture/output, replacement, scope-specific stop and rejection of old events. Browser module proves silent initialization, local EN/SV discovery, cancellation during voice discovery, late callbacks, pagehide, output error and bounded timeout. | C# actual production workflows; actual browser JS with synthetic Web Speech engine/events. Physical echo/device acceptance open. |
| P11-F03 Reviewed spoken Keep/planning | Exact final commands enter the shared review boundary; actual web and device Keep/selected StoryNote approval handlers persist once and guarded Undo uses existing operations. Negation, question, typed prefix, uncertain quality, changed review/hash and foreign scope cannot invoke application. | BrainstormingKeepHostTests, BrainstormingPlanningHostTests, BrainstormingSpeechTests, BrainstormingVoiceTests; synthetic recognition. Actual host callback persistence is exercised, not signed-in UI navigation. |
| P11-F04 Version-bound read opening aloud | Rendering is silent. Explicit read uses candidate ID and fingerprint, with local selected-language output. Editing stops playback before saving; changing version, identity/scope and disposal invalidate output/review. Existing rich editing/Keep modules are rerun unchanged. | Shared proposal production read/edit callback and fake output; actual editor browser regressions. Audible/native read check open. |
| P11-F05 Lifecycle, failure and responsive controls | Unsupported/remote-only voice and output loss retain text without retry. Existing STT authorization/quota/connectivity/unknown/recovery tests stay included. Browser fieldset controls and final transcripts render at wide/390px with keyboard operation. Native deactivation/closing cancels audio and preserves existing save/flush. | Portable contract/coordinator tests, real Edge production-module/render fixtures, rebuilt Windows startup. Permission/unplug/sign-out while actually speaking needs live evidence. |

Run all checks using:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p11 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Evidence is under ignored `artifacts/brainstorming-p11`: combined TRX/log, sequential
server/web Release and Windows Debug builds, ten browser suite logs/JSON/screenshots,
and native-startup.json. Exact final counts and native PID/path are recorded in status.
Full iOS/package/live SQL execution is outside this run; portable Device.Shared builds
with no Windows speech API reference and its Unsupported output fallback remains.

## Issue ledger and live reproductions

| ID | Priority | Finding | Disposition |
|---|---|---|---|
| P11-D01 | P1 | Third conversational generation completed remotely but local pull could reject it: streaming advances server revision twice per turn while the local revision advanced once. | Fixed shared pull clock without rewriting captured request provenance. Web/device explore -> draft -> spoken revision and sync regressions pass. |
| P11-D02 | P2 | Broad command classification could treat narrative “keep the dialogue understated” as an apply-like instruction. | Fixed targeted phrase detection; exact application commands remain narrow and uncertain apply-like speech requires confirmation. Narrative EN/SV tests pass. |
| P11-D03 | P1 | Output-only background cancellation could allow an in-flight reply to finish and speak later. | Native singleton coordinator shared by App/WebView and browser visibility callback now end voice and stop generation; browser output also refuses hidden playback. Production generation/background test and JS lifetime/disposal checks pass; native build/startup repeated after registration correction. |
| P11-G01 | P1 | P10 real user microphone/provider recognition in EN/SV on browser and rebuilt Windows is unverified. | Open. No human recording or live provider recognition call was made. Speech defaults disabled; key presence is not model access/recognition proof. |
| P11-G02 | P1 | Installed/local audible EN/SV output, physical echo, interruption and spoken application in signed-in hosts unverified. | Open. JS engines/output callbacks are synthetic and native launch is startup only. Follow reproduction below. |
| P11-G03 | P2 | Live SQL, interactive signed-in journeys and installed/package microphone/output capability verification. | Open separate gates; retain P9/P10 reproductions. |
| P11-G04 | P2 | Unrelated targeted-quality valid/429-between diagnostic and P14 list-ending normalization gate. | Preserved. P11 does not change editor assets/schema or establish those workflows fixed. |

For live acceptance, configure an accessible supported STT model on an entitled
backend with matching migrations and rebuilt hosts. Install local English and
Swedish output voices; use disposable standalone and project manuscripts. Open
Brainstorm, prepare Chapter 2 context/target, type an introduction, explicitly Start
spoken dialogue and say “Explore three opening ideas.” Finish, inspect the final
transcript/ordered turns and hear the concise explanation. Draft an opening, then
Speak next turn: “She trusts the stranger too quickly. Make her more suspicious,
but keep the dialogue understated.” Inspect linked draft versions and unchanged
manuscript. Interrupt during output, then during generation; inspect saved/incomplete
states, no late resumed audio, and released capture. Mute, unmute, End and type a
follow-up in that same session. Repeat Svenska with “Gör henne mer misstänksam,
men behåll dialogen diskret.” Record observed language/model/output voice, latency,
operation state and actual settled usage, without retaining raw audio by default.

Open exact Keep review before starting the next microphone turn. Say “Keep this
version.” or “Behåll den här versionen.” Inspect the actual receipt and Undo. If
recognition quality is Unassessed/NeedsReview, use focused button confirmation and
confirm it did not apply through voice. Repeat with selected planning fields and
“Approve these planning fields.” / “Godkänn dessa planeringsfält.” Verify negation,
questions, noisy speech, mixed typed prefix and changed destination cannot apply.
Read the draft opening explicitly, edit it while speaking, switch versions and
confirm audio stops. Deny permission, unplug input/output, disconnect, background,
sign out/change account and reopen the original session. Test both headphones and
speaker playback before explicitly capturing again; no AI answer may become a new
author turn. Browser/native audible evidence and provider evidence must be recorded
separately. Preserve the user's existing editor; use the isolated executable/data
path in status and stop only the test process you launched.
