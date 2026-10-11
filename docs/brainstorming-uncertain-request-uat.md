# Brainstorm uncertain request recovery — 2026-10-10

## Observed request

A read-only lookup of the retained desktop cache and its mapped server operation
confirmed that the screenshot was showing stale local state:

- Server accepted the request at 09:18:20 UTC and committed Cancelled at
  09:18:24 UTC. There was no provider text and no new draft.
- The server session contained the user turn and a readable cancellation turn.
- The desktop cache contained only the Accepted event, zero turns, and the
  original pending send/composer. The “uncertain” label did not reflect the
  durable server outcome.

The lookup inspected only the exact mapped operation/session. It did not edit the
author cache/database, replay generation, or apply manuscript changes.

## Root cause and correction

Two defects combine to produce this symptom:

1. `voiceLifetime` registers a visibility/page-hide callback even during typed
   conversations. `BackgroundVoice` used to call `InterruptVoice`, which stopped
   any busy generation. The handler could therefore cancel a typed request when
   the surface went into the background. It now stops only the cancellation
   source owned by the active spoken send. Typing remains independent of voice
   interruption, including when spoken mode was previously active.
2. Stream exceptions/cancellation skipped the workflow's final inspection, and
   opening a conversation read only the cache. Both paths now perform bounded,
   authenticated inspection of the saved immutable operation. They pull saved
   turns/drafts and clear pending state only after a known terminal result or
   authoritative operation absence. A short bounded recheck accommodates the
   server committing cancellation after its stream closes.

The database establishes the actual cancellation and stale cache. No timestamped
client visibility trace was available to establish which interruption triggered
this particular request. The background-handler defect is reproduced in the
component regression tests.

Completed responses restore the saved conversation and clear the matching sent
composer. Cancelled/failed responses retain the author message for an explicit
new request. Offline/failed inspection and genuinely Unknown outcomes retain
the pending identity. Inspection never dispatches a provider or applies writing.
Scope/epoch guards prevent old recovery results from replacing a newly selected
conversation. The shared component covers desktop and browser hosts.

## Validation and runtime handoff

Evidence is in `artifacts/brainstorming-uncertain/`:

- Eight new cases cover typed/spoken background interruption, missed terminal
  delivery, reopening Completed/Cancelled/Unknown, offline/inspection failure,
  and delayed server settlement.
- The real authenticated HTTP Stop/retry regression now requires automatic
  cancellation recovery, retained message/turns, and exactly one new provider
  call only after an explicit retry.
- All 433 relevant Brainstorm/provider/context regressions passed. Isolated
  server/web and desktop builds passed. Test/build logs and TRX retain the evidence.

The running author desktop was left open. Its normal executable must be rebuilt
and restarted to load this shared UI change. After saving and closing Prosa:

```powershell
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --no-restore
```

Then launch the usual desktop output and reopen the retained conversation. It
will inspect the existing request and show the saved cancellation. The author
can then explicitly resend the retained message. Do not delete its cache or
automatically replay it. The already-running backend needs no server change for
this correction. Installed/native visibility and live-provider acceptance remain
separate from the deterministic component/authenticated-transport evidence.
