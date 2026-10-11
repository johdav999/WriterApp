# Brainstorming prompt 10 dictation evidence

Date: 2026-10-07. Original WriterApp checkout; P1–9 and unrelated work retained.

## Product profile and scope

An author dictates English or Swedish into the existing Brainstorm message composer
on web or Windows, corrects the final transcript, and explicitly sends it in the
same text conversation. An alternative explicit action places the corrected message
in the proposal panel using the reviewed context/destination. It creates author
prose, without manufacturing an AI request or granting Keep/planning approval.
Existing exact-version Keep, checked rich-content application and guarded Undo apply.
Standalone and project manuscript ownership/mapping remain the P9 paths.

Real production capture code exists. Automated speech, authentication and device
interop responses are synthetic; browser capture uses a labeled synthetic WebAudio
oscillator with production AudioWorklet/WAV code. Native executable startup is
separate from microphone acceptance. No user microphone recording, live provider
recognition or installed-package acceptance has been performed.

## Flow packets

| Flow | Actions | Outcome and evidence |
|---|---|---|
| P10-01 Permission and capture | Explicit Start, select English/Svenska, Stop; deny permission, missing device, short recording, late permission after Cancel. | Production JS requests permission only on Start; tracks/context release before final callback. Edge exercises actual worklet with synthetic audio, late permission cancellation and error codes. Windows uses MediaCapture on the UI thread with audio-only consent, in-memory PCM and microphone manifest capability; compiled/startup evidence only. |
| P10-02 Correct and Send (B18/B20) | Retain typed introduction, capture final English/Swedish, correct punctuation, explicitly Send. | Shared production panel through both actual web/device session and dictation adapters, authenticated TestServer and migrated SQLite. One audio dispatch/charge, no turn before Send, combined editable composer, then one normal conversational request. Final result cannot auto-send. |
| P10-03 Dictated proposal (B01–07) | Explicitly review current context/destination and place corrected prose, then separate Keep approval and Undo. | Both adapters persist AuthorDictated proposal, validated against owned completed transcription ledger. Repeated identical placement does not duplicate it. Separate migrated server test proves Prepare leaves manuscript unchanged, exact Commit applies and guarded Undo restores. Prior rich editor/application suites cover formatting/annotations and refusal paths. |
| P10-04 Interruption and recovery (B13–17) | Cancel, timeout, provider disconnect/poor recognition, lose response, reopen cache, fill composer, change authorization. | No partial/applicable recognition; composer retained. Operation inspected by original identity without audio reupload, extra charge or duplicate append. Cancelled late result cannot adopt; overfull final transcript is retained until explicit inspection after shortening. Authenticated foreign reads/writes and late account lease fail. Empty recognition has actionable language/noise/device guidance. |
| P10-05 Bounded and accessible (B23) | 60 seconds, invalid WAV/language, malformed/oversized provider response or accounting, pagehide, responsive keyboard review. | PCM duration derived from bytes; max 1,921,024-byte WAV and 8,000-character final text. Browser verifies hard frame bound, disposal on pagehide, keyboard controls and unclipped 390px production Razor fixtures. No page errors. |

Evidence: ignored `artifacts/brainstorming-p10`; final counts and executable details
are in [implementation status](brainstorming-implementation-status.md). Tests are
`BrainstormingSpeechTests.cs` plus prior regressions; production modules are exercised
by `Tests/brainstorming-dictation.mjs`. Actual editor modules are unchanged by P10.

## Transport, accounting and privacy

`POST api/brainstorming/dictation` accepts a version-1 scoped operation, explicit
`en`/`sv` and bounded PCM16 mono 16 kHz WAV. ASP.NET body limit is 2,700,000 bytes
including base64/JSON. The authenticated server verifies owned active document/session,
backend/account namespace, entitlement/rate policy and quota before dispatch. Dedicated
Audio routing requires the configured real billable provider; no text/mock fallback.
The server forwards multipart audio over HTTPS using its API key. Neither browser
nor Windows receives the provider credential. GET inspection rechecks ownership.

`BrainstormingTranscriptions` stores only SessionId, OperationId, payload hash and
typed result JSON. SQLite `20261007164538_BrainstormingTranscriptions` and SQL Server
`20261007164539_BrainstormingTranscriptionsSqlServer` add only that table and a
restricted session FK; no audio column. Both snapshots match. Real SQLite migration
and restart are tested; SQL Server script/model verification is not live execution.
Maximum 100 records per session. Unresolved server speech operations block another
operation in that session; retain uncertainty and deliberately create a new session
if inspection cannot establish a terminal outcome. Requests are never auto-replayed.

Version-1 cache gains optional Dictations. Drafts gain optional AuthorDictated origin
and transcription identity; default AI drafts omit these JSON fields. Matching
shared/UI/client/device/server builds and database migration are required. Old speech
servers yield unavailable/upgrade states; ordinary text remains usable. iOS keeps
the portable Unsupported microphone adapter and has no Windows API reference.

Normal completion charges reported input/output tokens and records one usage event.
Acceptance is persisted before provider dispatch; uncertain settlement is persisted
before billing side effects. Provider interruption without confirmed usage uses the
existing conservative token quota mechanism, clearly labeled Unknown, without
claiming measured actual usage. Inspection cannot replay/recharge. Silence detected
in PCM does not dispatch or charge; empty recognition after dispatch settles its
reported usage and adds no composer text. Nonempty recognition always requires author
review; no confidence/quality guarantee is fabricated from transcript text.

Raw audio is transient by default: browser samples/WAV are cleared after transfer,
tracks and AudioContext close on stop/cancel/pagehide; Windows disposes MediaCapture
and its in-memory stream; controller/service and shared panel clear WAV arrays in
finally paths. Managed/network serialization buffers are bounded and reclaimed by
the runtime, without promising secure erasure of every runtime copy. Audio is not
written to cache, DB, history, diagnostics or test evidence. Final transcripts are
private session data retained for correction/recovery, subject to owned reads and
the existing session storage lifecycle. Provider retention follows the configured
provider/account settings; this implementation makes no remote deletion guarantee.
There is no microphone monitoring, sound on load or playback in P10.

## Setup and official API decisions

Speech defaults to disabled with an empty model. Backend configuration uses
`WriterApp:AI:Enabled=true`, `WriterApp:AI:Speech:Enabled=true`, an explicitly accessible
model in `WriterApp:AI:Speech:Model`, and
`WriterApp:AI:Providers:DefaultAudioProviderId=openai-speech`. Supported token-accounted
models are `gpt-transcribe`, `gpt-4o-transcribe`, `gpt-4o-mini-transcribe`; obtain model
access rather than assuming availability. Server `OPENAI_API_KEY` supplies the key.
Use environment names such as `WriterApp__AI__Speech__Enabled`; do not store keys in
browser settings. Timeout defaults to 90 seconds and is clamped to 1–90; client
transport timeout is 100 seconds. Existing OpenAI HTTPS BaseUrl/Organization apply.

The current [OpenAI speech guide](https://developers.openai.com/api/docs/guides/speech-to-text)
and [transcription API](https://developers.openai.com/api/reference/resources/audio/subresources/transcriptions/methods/create)
were checked before implementation. This adapter uses original-language JSON
transcription, WAV multipart and explicit language: `languages[]` for gpt-transcribe,
`language` for the supported 4o models. It requires token usage in the response;
duration-billed models are not silently treated as token-accounted models.

Windows capture follows [MediaCapture](https://learn.microsoft.com/en-us/uwp/api/windows.media.capture.mediacapture?view=winrt-28000)
audio-only initialization/consent and disposal. Web uses secure-context
[getUserMedia](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia),
[AudioWorklet](https://developer.mozilla.org/en-US/docs/Web/API/AudioWorklet) and an
explicit supported [AudioContext sample rate](https://developer.mozilla.org/en-US/docs/Web/API/AudioContext/AudioContext).
Unsupported 16 kHz capture refuses with typing available.

## Issue ledger and open acceptance gates

| ID | Severity | Finding | Disposition |
|---|---|---|---|
| P10-I01 | P1 | Existing provider abstraction had no authenticated speech path. | Fixed: dedicated Audio provider, configured transport, ownership/policy/quota and typed durable operation ledger. |
| P10-I02 | P1 | Late permission/recognition could cross disposal or account/session boundaries. | Fixed: resource release, epoch/cancellation/lease guards, bounded permission timeout and late-track cleanup. Synthetic lifecycle tests pass. |
| P10-I03 | P1 | Lost response or repeated placement could append/rebill/duplicate a proposal. | Fixed: original-operation inspection and CAS adoption flag, unresolved-operation guard and identical-placement deduplication. |
| P10-I04 | P1 | Dictated author prose must not manufacture an AI turn or directly apply writing. | Fixed: explicit origin, owned transcription provenance and separate unchanged Keep boundary. |
| P10-G01 | P2 | Real English/Swedish user capture and provider recognition on browser and rebuilt Windows unavailable in this run. | Open. Backend files do not enable Speech or select a model; a server key exists but no live recognition call was attempted. No human microphone sample supplied/recorded. Follow reproduction below. |
| P10-G02 | P2 | Interactive signed-in journey, live SQL Server and installed microphone capability verification absent. | Open separate gates; retain P9 reproductions and add microphone permission tests after installing a rebuilt package. |
| P10-D01 | P2 | Prior targeted-quality valid/429-between diagnostic and P14 list-ending normalization gate. | Preserved; P10 does not change editor schema/assets or claim these workflows resolved. |

For P10-G01, use matching rebuilt hosts with the migrations applied to a disposable
development DB, configure an accessible speech model on that backend, sign in with
an entitled account, and open Brainstorm on a synthetic standalone manuscript and
a project manuscript. Type an introduction, select English, explicitly Start and
say: “She trusts the stranger too quickly. Make her more suspicious.” Stop, correct
a word, and confirm no turn or manuscript change occurred before Send. Send and
inspect the same session. Repeat with Svenska: “Åsa väntade vid dörren. Hon litade
inte på främlingen.” Correct punctuation, review a prose destination, place the
proposal, then separately approve Keep and Undo. Repeat in the rebuilt Windows
executable whose path is recorded in status, using its isolated data directory.
Verify pause/silence/noisy speech, cancel while listening/transcribing, deny permission,
unplug device, navigate/background/sign out during capture, and maximum duration.
Record platform/model/language, observed editable result, operation/usage outcome
and resource indicator release; retain no raw audio by default. This requires real
user speech, not a fixture clip. Do not label startup or feature detection as passing it.

Run automation with:

```powershell
./scripts/verify-brainstorming.ps1 -Phase p10 -Browser -PlaywrightModule 'C:/Users/Johan/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright' -NativeStartup
```

Prompt 11 must add explicit spoken orchestration/playback over this same session,
without changing dictation into automatic application authority.
