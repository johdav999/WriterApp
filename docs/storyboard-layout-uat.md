# Storyboard layout repair — 2026-10-04

The storyboard document selector, page heading/actions, error message, and board layout now use a column flow. The previous desktop grid allocated only two rows for three children after the selector was added, squeezing the heading and placing the panels over its text. The web grid also had no spare row when an error appeared.

Both hosts reserve the natural height of the controls above the board. The panel area fills the remaining height with a 320px minimum; short windows scroll through the shell instead of squeezing the panels. Only the board panel reserves a heading row. Insights and Scene Detail give their single child the full panel height. Narrow windows stack the three panels with bounded scroll regions, heading actions wrap, and the document selector stays within the available width.

| ID | Severity | Flow | Evidence | Acceptance | Result |
|---|---|---|---|---|---|
| SB-LAYOUT-01 | P2 | Open storyboard and select scenes | User screenshot; `artifacts/uat/storyboard-layout/` | Document selector, heading, actions, toolbar and panels do not overlap; cards and scene fields remain scrollable | Passed browser layout fixture; native acceptance pending |

Verification:

- `Tests/storyboard-layout.mjs` passed 42 cases in headless Edge: desktop/web at 1696×568, 1440×900, 1280×720, 1201×720, 1024×768, 729×900, and 390×844. Each size covers selection, page error plus bulk actions, and collapsed insights.
- Checks measure heading/selector/panel/toolbar bounds, panel separation, horizontal chapter scrolling, inspector scrolling and field containment. Screenshots at 1696×568 and 390×844 were captured; selected desktop screenshots were visually inspected.
- Running the same check with `--baseline` fails against the original CSS at 1696×568 because the desktop header is shorter than its text, reproducing the reported defect.
- Desktop build: `dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --no-restore -o WriterApp.Desktop/bin/storyboard-layout-validation -v minimal` — zero warnings/errors.
- Web build: `dotnet build WriterApp.Client/WriterApp.Client.csproj --no-restore -p:BaseOutputPath=bin/storyboard-layout-validation/ -v minimal` — zero warnings/errors.

Run the browser check from the repository root with `PLAYWRIGHT_MODULE` pointing to an installed Playwright package if it is not on Node's module path:

```powershell
node Tests/storyboard-layout.mjs
```

The fixture uses production CSS and the host component DOM structure with synthetic chapters and planning fields. It does not establish native WebView acceptance or authenticated web acceptance. The existing desktop process was left running, so it has not been confirmed to have loaded the rebuilt styles. Reopen the storyboard after restarting the rebuilt app to complete native acceptance.
