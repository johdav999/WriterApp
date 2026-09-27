# Desktop AI writing actions (Release 1, Prompt 9)

The desktop editor offers Rewrite, Expand, Shorten, Summarize, and Custom instruction through the existing authenticated `/api/ai/actions/{actionKey}/execute` endpoint. It also reads `/api/ai/status` for current availability and quota. The device never contacts an AI provider or stores provider keys.

## Using the actions

Sign in, enable cloud sync for the document, and let its current local edit finish syncing. Select text for Rewrite, Expand, or Shorten. Custom instruction replaces the selected text when there is a selection; without one, it reads the section and adds its result after the current page. Summarize reads the current section and likewise adds its result after the current page, preserving the original prose.

Each action shows the source and proposed text in a separate preview. **Dismiss** leaves the document alone. **Apply** inserts plain text through TipTap's document schema, creates a normal local change, and saves it through the existing autosave/recovery flow. HTML-looking AI output remains text. The editor's Undo command can reverse the insertion during that editing session.

Before Apply, the device saves the original page HTML to `documents/ai-undo`. After the local save, it records the applied HTML. **Restore writing before last AI apply** only works while the current page still matches that applied version; it refuses to overwrite later edits. If Apply was interrupted, the earlier source remains in a pending backup, and the editor offers to save it as a separate local copy. Back up this folder with the documents and sync journal: it contains user writing.

The request captures TipTap's selection positions and exact plain text. Selection actions send the current page text as backend context. Section actions include all locally readable pages in section order. A legacy JSON page outside the active editor blocks a whole-section request until converted, so it cannot be silently omitted. The server owns the final plan and action checks; the client also checks the usage status first and reports sign-in, plan upgrade, quota, safety/content block, timeout, offline, and service failures separately. A completed AI call may count against quota even if its proposal is dismissed or a late cancellation arrives; it never edits the document without Apply.

Local-only documents, unsynced changes, and conflicts cannot call the existing backend action endpoint because that endpoint requires an owned server document, section, and page. The action first saves and runs sync, then checks that the page still matches the captured editor content. If it changed, the user must review it and request a new proposal. Network and provider failures leave local editing and saving available.

## Verification and rollout

The .NET tests cover all five request mappings, selection and section context, feature/quota/auth failures, cancellation, transport DTOs, and the durable pre-Apply backup. The browser editor harness covers actual TipTap selection mapping, explicit Apply, Undo/Redo, escaped provider text, stale proposal rejection, and guarded restore. The Release solution and both device hosts build without warnings.

Before a first release, verify against a staging backend with a configured native identity and AI provider: plan access for each action, quota exhaustion, cancel/timeout, selected prose with formatting, multi-page summaries, Apply/Undo/restart recovery, and later synchronization. No live provider call or Azure deployment was performed by this prompt. The next desktop step is [Prompt 10, import and export](release-1-desktop-prompts.md).
