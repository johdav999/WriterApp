using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using BlazorApp.Components.Editor;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.State;
using WriterApp.Application.Usage;
using WriterApp.Application.Security;
using WriterApp.Application.Exporting;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Domain.Documents;

namespace BlazorApp.Components.Pages
{
    public partial class DocumentEditor : ComponentBase, IDisposable
    {
        [Parameter]
        public Guid DocumentId { get; set; }

        [Parameter]
        public Guid SectionId { get; set; }

        [Parameter]
        public Guid PageId { get; set; }

        [Inject]
        public HttpClient Http { get; set; } = default!;

        [Inject]
        public NavigationManager Navigation { get; set; } = default!;

        [Inject]
        public ILogger<DocumentEditor> Logger { get; set; } = default!;

        [Inject]
        public AppHeaderState HeaderState { get; set; } = default!;

        [Inject]
        public LayoutStateService LayoutStateService { get; set; } = default!;

        [Inject]
        public IAiOrchestrator AiOrchestrator { get; set; } = default!;

        [Inject]
        public IAiUsageStatusService AiUsageStatusService { get; set; } = default!;

        [Inject]
        public IOptions<WriterAiOptions> AiOptionsAccessor { get; set; } = default!;

        [Inject]
        public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

        [Inject]
        public IUserIdResolver UserIdResolver { get; set; } = default!;

        [Inject]
        public IJSRuntime JSRuntime { get; set; } = default!;

        [Inject]
        public ExportService ExportService { get; set; } = default!;

        private readonly List<SectionDto> _sections = new();
        private readonly Dictionary<Guid, List<PageDto>> _pagesBySection = new();
        private SectionDto? _activeSection;
        private PageDto? _activePage;
        private bool _isLoading = true;
        private string? _loadError;
        private Guid _loadedDocumentId;
        private string _documentTitle = string.Empty;
        private string? _titleErrorMessage;
        private bool _layoutStateInitialized;
        private PageEditor? _pageEditor;
        private EditorFormattingState _formattingState = new()
        {
            CanBold = true,
            CanItalic = true
        };
        private bool _selectionBubbleVisible;
        private double _selectionBubbleX;
        private double _selectionBubbleY;
        private bool _isContextMenuOpen;
        private double _contextMenuX;
        private double _contextMenuY;
        private bool _shouldFocusContextMenu;
        private ElementReference _contextMenuRef;
        private bool _isToolbarOverflowOpen;
        private bool _isDocumentMenuOpen;
        private SectionEditor.EditorSelectionRange? _currentSelectionRange;
        private readonly List<AiActionOption> _aiActions = new()
        {
            new AiActionOption("Rewrite (Neutral)", "Rewrite (Neutral)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Neutral"
            }),
            new AiActionOption("Rewrite (Formal)", "Rewrite (Formal)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Formal"
            }),
            new AiActionOption("Rewrite (Casual)", "Rewrite (Casual)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Casual"
            }),
            new AiActionOption("Rewrite (Executive)", "Rewrite (Executive)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Executive"
            }),
            new AiActionOption("Shorten (Neutral)", "Shorten (Neutral)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Neutral"
            }),
            new AiActionOption("Fix grammar (Neutral)", "Fix grammar (Neutral)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Neutral"
            }),
            new AiActionOption("Change tone (Friendly)", "Change tone (Friendly)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Friendly"
            }),
            new AiActionOption("Change tone (Technical)", "Change tone (Technical)", AiActionScope.Selection, new Dictionary<string, object?>
            {
                ["tone"] = "Technical"
            })
        };
        private readonly List<AiHistoryEntry> _aiHistoryEntries = new();
        private Guid? _expandedAiHistoryId;
        private AiUsageStatusDto? _aiUsageStatus;
        private bool _aiUsageRefreshInProgress;
        private bool _canShowAiMenu;
        private bool? _lastAiMenuVisibility;
        private ContextTab _activeContextTab = ContextTab.Notes;
        private string _notesDraft = string.Empty;
        private string? _notesStatus;
        private PendingAiProposal? _pendingAiProposal;
        private bool _pendingDetailsExpanded;
        private IJSObjectReference? _exportModule;
        private const int PageBreakHeightPx = 980;
        private const int PageBreakGutterOffsetPx = 28;

        private PageEditor.PageBreakOptions PageBreaks =>
            new(PageBreakHeightPx, true, PageBreakGutterOffsetPx);

        protected override Task OnInitializedAsync()
        {
            HeaderState.DocumentTitleEdited += OnHeaderDocumentTitleEdited;
            _ = LoadAiUsageStatusAsync();
            return Task.CompletedTask;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && !_layoutStateInitialized)
            {
                _layoutStateInitialized = true;
                await LayoutStateService.InitializeAsync();
                LayoutStateService.Changed += OnLayoutStateChanged;
                await InvokeAsync(StateHasChanged);
            }

            if (_shouldFocusContextMenu)
            {
                _shouldFocusContextMenu = false;
                await _contextMenuRef.FocusAsync();
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            await LoadDocumentAsync();
        }

        private async Task LoadDocumentAsync()
        {
            _isLoading = true;
            _loadError = null;
            _titleErrorMessage = null;

            try
            {
                DocumentDetailDto? document = await Http.GetFromJsonAsync<DocumentDetailDto>($"api/documents/{DocumentId}");
                if (document is null)
                {
                    _loadError = "Document not found.";
                    return;
                }

                HeaderState.DocumentTitle = document.Title;
                HeaderState.DocumentId = document.Id.ToString();
                HeaderState.CanRenameDocumentTitle = true;
                _documentTitle = document.Title;

                if (_loadedDocumentId != DocumentId)
                {
                    _sections.Clear();
                    _pagesBySection.Clear();
                    _loadedDocumentId = DocumentId;
                }

                List<SectionDto>? sections = await Http.GetFromJsonAsync<List<SectionDto>>(
                    $"api/documents/{DocumentId}/sections");
                _sections.Clear();
                if (sections is not null)
                {
                    _sections.AddRange(sections.OrderBy(section => section.OrderIndex));
                }

                foreach (SectionDto section in _sections)
                {
                    List<PageDto>? pages = await Http.GetFromJsonAsync<List<PageDto>>(
                        $"api/sections/{section.Id}/pages");
                    List<PageDto> ordered = pages?.OrderBy(page => page.OrderIndex).ToList() ?? new List<PageDto>();
                    if (ordered.Count > 1)
                    {
                        string merged = string.Join("\n\n", ordered.Select(page => page.Content ?? string.Empty));
                        ordered = new List<PageDto> { ordered[0] with { Content = merged } };
                    }
                    _pagesBySection[section.Id] = ordered;
                }

                _activeSection = _sections.FirstOrDefault(section => section.Id == SectionId);
                if (_activeSection is null)
                {
                    Navigation.NavigateTo($"/documents/{DocumentId}", replace: true);
                    return;
                }

                _activePage = GetPrimaryPage(_activeSection.Id);
                if (_activePage is null)
                {
                    List<PageDto> pages = GetPages(_activeSection.Id);
                    if (pages.Count > 0)
                    {
                        Navigation.NavigateTo($"/documents/{DocumentId}/sections/{_activeSection.Id}/pages/{pages[0].Id}", replace: true);
                        return;
                    }

                    Navigation.NavigateTo($"/documents/{DocumentId}", replace: true);
                    return;
                }

                _notesDraft = await LoadPageNotesAsync(_activePage.Id);
                _notesStatus = null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Document editor load failed.");
                _loadError = "Failed to load the document.";
            }
            finally
            {
                _isLoading = false;
            }
        }

        private List<PageDto> GetPages(Guid sectionId)
        {
            return _pagesBySection.TryGetValue(sectionId, out List<PageDto>? pages)
                ? pages
                : new List<PageDto>();
        }

        private PageDto? GetPrimaryPage(Guid sectionId)
        {
            List<PageDto> pages = GetPages(sectionId);
            if (pages.Count == 0)
            {
                return null;
            }

            PageDto primary = pages[0];
            string combined = string.Join("\n\n", pages.Select(page => page.Content ?? string.Empty));
            return primary with { Content = combined };
        }

        private void OnSectionSelected(Guid sectionId)
        {
            List<PageDto> pages = GetPages(sectionId);
            if (pages.Count == 0)
            {
                Navigation.NavigateTo($"/documents/{DocumentId}");
                return;
            }

            Navigation.NavigateTo($"/documents/{DocumentId}/sections/{sectionId}/pages/{pages[0].Id}");
        }




        private async Task OnPageSaved(PageDto page)
        {
            if (_pagesBySection.TryGetValue(page.SectionId, out List<PageDto>? pages))
            {
                int index = pages.FindIndex(item => item.Id == page.Id);
                if (index >= 0)
                {
                    pages[index] = page;
                }
            }

            if (_activePage?.Id == page.Id)
            {
                _activePage = page;
            }

            await InvokeAsync(StateHasChanged);
        }


        private bool IsContextPanelCollapsed()
        {
            return LayoutStateService.State.FocusMode || LayoutStateService.State.ContextCollapsed;
        }

        private string GetContextPanelClass()
        {
            return IsContextPanelCollapsed() ? "is-collapsed" : string.Empty;
        }

        private string GetSectionsPanelClass()
        {
            return LayoutStateService.State.FocusMode || LayoutStateService.State.SectionsCollapsed
                ? "is-collapsed"
                : string.Empty;
        }

        private string GetLayoutStyle()
        {
            LayoutState state = LayoutStateService.State;
            string maxWidth = state.ManuscriptWidthMode == ManuscriptWidthMode.Manuscript ? "760px" : "none";
            double scale = state.EditorZoomPercent / 100.0;
            string scaleText = scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return "--editor-max-width: " + maxWidth + "; --editor-font-scale: " + scaleText + ";";
        }

        private string GetWorkspaceClass()
        {
            LayoutState state = LayoutStateService.State;
            bool contextCollapsed = state.FocusMode || state.ContextCollapsed;
            bool sectionsCollapsed = state.FocusMode || state.SectionsCollapsed;

            if (contextCollapsed && sectionsCollapsed)
            {
                return "is-panels-collapsed";
            }

            if (contextCollapsed)
            {
                return "is-context-collapsed";
            }

            if (sectionsCollapsed)
            {
                return "is-sections-collapsed";
            }

            return string.Empty;
        }

        private async Task ToggleContextPanel()
        {
            LayoutState current = LayoutStateService.State;
            await LayoutStateService.SetStateAsync(current with { ContextCollapsed = !current.ContextCollapsed });
        }

        private async void OnHeaderDocumentTitleEdited(string title)
        {
            if (DocumentId == Guid.Empty)
            {
                return;
            }

            try
            {
                DocumentUpdateRequest request = new(title);
                using HttpResponseMessage response = await Http.PutAsJsonAsync(
                    $"api/documents/{DocumentId}",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    HeaderState.DocumentTitle = _documentTitle;
                    _titleErrorMessage = "Could not rename the document. Please try again.";
                    return;
                }

                DocumentDetailDto? updated = await response.Content.ReadFromJsonAsync<DocumentDetailDto>();
                if (updated is not null)
                {
                    _documentTitle = updated.Title;
                    HeaderState.DocumentTitle = updated.Title;
                    _titleErrorMessage = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Document title update failed.");
                HeaderState.DocumentTitle = _documentTitle;
                _titleErrorMessage = "Could not rename the document. Please try again.";
            }

            await InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            HeaderState.DocumentTitleEdited -= OnHeaderDocumentTitleEdited;
            LayoutStateService.Changed -= OnLayoutStateChanged;
            HeaderState.DocumentTitle = null;
            HeaderState.DocumentId = null;
            HeaderState.CanRenameDocumentTitle = false;

            if (_exportModule is not null)
            {
                try
                {
                    _ = _exportModule.DisposeAsync();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (JSDisconnectedException)
                {
                }
                finally
                {
                    _exportModule = null;
                }
            }
        }

        private void OnLayoutStateChanged(LayoutState state)
        {
            if (_pageEditor is not null)
            {
                _ = _pageEditor.RefreshPageBreaksAsync();
            }

            InvokeAsync(StateHasChanged);
        }

        private string GetPageTitle()
        {
            string title = HeaderState.DocumentTitle ?? string.Empty;
            return string.IsNullOrWhiteSpace(title) ? "Writer" : $"{title} - Writer";
        }

        private string GetLayoutStateClass()
        {
            LayoutState state = LayoutStateService.State;
            List<string> classes = new();
            if (state.FocusMode)
            {
                classes.Add("is-focus-mode");
            }

            if (state.ManuscriptWidthMode == ManuscriptWidthMode.FullWidth)
            {
                classes.Add("is-full-width");
            }

            return string.Join(" ", classes);
        }

        private Task OnFormattingChanged(EditorFormattingState state)
        {
            _formattingState = state ?? new EditorFormattingState();
            return InvokeAsync(StateHasChanged);
        }

        private Task OnSelectionChanged(SectionEditor.EditorSelectionRange range)
        {
            if (range is null || range.End <= range.Start)
            {
                _currentSelectionRange = null;
            }
            else
            {
                _currentSelectionRange = range;
            }

            UpdateAiMenuVisibility();
            return InvokeAsync(StateHasChanged);
        }

        private Task OnEditorContextMenuRequested(SectionEditor.EditorContextMenuRequest request)
        {
            _contextMenuX = request.X;
            _contextMenuY = request.Y;
            _isContextMenuOpen = true;
            _shouldFocusContextMenu = true;
            return InvokeAsync(StateHasChanged);
        }

        private Task OnSelectionBubbleChanged(SectionEditor.EditorSelectionBubble bubble)
        {
            _selectionBubbleVisible = bubble.IsVisible;
            _selectionBubbleX = bubble.X;
            _selectionBubbleY = bubble.Y;
            return InvokeAsync(StateHasChanged);
        }

        private void CloseContextMenu()
        {
            _isContextMenuOpen = false;
        }

        private string GetContextMenuStyle()
        {
            string left = _contextMenuX.ToString(CultureInfo.InvariantCulture);
            string top = _contextMenuY.ToString(CultureInfo.InvariantCulture);
            return $"left: {left}px; top: {top}px;";
        }

        private string GetSelectionBubbleStyle()
        {
            string left = _selectionBubbleX.ToString(CultureInfo.InvariantCulture);
            string top = _selectionBubbleY.ToString(CultureInfo.InvariantCulture);
            return $"left: {left}px; top: {top}px;";
        }

        private string GetActiveClass(bool isActive)
        {
            return isActive ? "is-active" : string.Empty;
        }

        private async Task OnContextMenuCommand(Func<Task> command)
        {
            CloseContextMenu();
            await command();
        }

        private Task OnContextMenuKeyDown(KeyboardEventArgs args)
        {
            if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            {
                CloseContextMenu();
            }

            return Task.CompletedTask;
        }

        private Task OnBoldRequested()
        {
            return InvokePageCommandAsync("toggleBold");
        }

        private Task OnItalicRequested()
        {
            return InvokePageCommandAsync("toggleItalic");
        }

        private Task OnStrikeRequested()
        {
            return InvokePageCommandAsync("toggleStrike");
        }

        private Task OnCodeRequested()
        {
            return InvokePageCommandAsync("toggleCode");
        }

        private Task OnParagraphRequested()
        {
            return InvokePageCommandAsync("setParagraph");
        }

        private Task OnHeadingRequested(int level)
        {
            return InvokePageCommandAsync("toggleHeading", level);
        }

        private Task OnBlockquoteRequested()
        {
            return InvokePageCommandAsync("toggleBlockquote");
        }

        private Task OnHorizontalRuleRequested()
        {
            return InvokePageCommandAsync("insertHorizontalRule");
        }

        private Task OnBulletListRequested()
        {
            return InvokePageCommandAsync("toggleBulletList");
        }

        private Task OnOrderedListRequested()
        {
            return InvokePageCommandAsync("toggleOrderedList");
        }

        private Task OnAlignRequested(string alignment)
        {
            return InvokePageCommandAsync("setTextAlign", alignment);
        }

        private Task OnIndentIncreaseRequested()
        {
            return InvokePageCommandAsync("increaseIndent");
        }

        private Task OnIndentDecreaseRequested()
        {
            return InvokePageCommandAsync("decreaseIndent");
        }

        private async Task OnLinkRequested()
        {
            string? url = await JSRuntime.InvokeAsync<string?>("prompt", "Paste link URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                await InvokePageCommandAsync("unsetLink");
                return;
            }

            await InvokePageCommandAsync("setLink", url);
        }

        private Task OnUndoRequested()
        {
            return InvokePageCommandAsync("undo");
        }

        private Task OnRedoRequested()
        {
            return InvokePageCommandAsync("redo");
        }

        private async Task OnAiActionSelected(AiActionOption action)
        {
            if (!IsAiAvailable)
            {
                ShowAiMessage(GetAiBlockedMessage());
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (_activeSection is null || _currentSelectionRange is null || _pageEditor is null)
            {
                return;
            }

            if (!AiOrchestrator.CanRunAction(RewriteSelectionAction.ActionIdValue))
            {
                return;
            }

            string html = _pageEditor.GetContent();
            string plainText = PlainTextMapper.ToPlainText(html);
            if (plainText.Length == 0)
            {
                return;
            }

            TextRange selectionRange = NormalizeRange(_currentSelectionRange, plainText.Length);
            string selection = ExtractRangeText(plainText, selectionRange);
            Document aiDocument = BuildAiDocument(html);

            AiActionInput input = new(
                aiDocument,
                _activeSection.Id,
                selectionRange,
                selection,
                action.Instruction,
                action.Inputs);

            AiExecutionResult result;
            try
            {
                result = await AiOrchestrator.ExecuteActionAsync(
                    RewriteSelectionAction.ActionIdValue,
                    input,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                ShowAiMessage(ex.Message);
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (!result.Succeeded || result.Proposal is null)
            {
                ShowAiMessage(result.ErrorMessage ?? "AI is not available.");
                await InvokeAsync(StateHasChanged);
                return;
            }

            AiProposal proposal = result.Proposal;
            _pendingAiProposal = new PendingAiProposal(
                proposal,
                BuildActionLabel(action),
                selection,
                proposal.ProposedText ?? string.Empty,
                null,
                null,
                DateTime.UtcNow);
            _pendingDetailsExpanded = false;

            await InvokeAsync(StateHasChanged);
        }

        private void ToggleToolbarOverflow()
        {
            _isToolbarOverflowOpen = !_isToolbarOverflowOpen;
        }

        private void ToggleDocumentMenu()
        {
            _isDocumentMenuOpen = !_isDocumentMenuOpen;
        }

        private async Task OnSaveNow()
        {
            if (_pageEditor is null)
            {
                return;
            }

            _isDocumentMenuOpen = false;
            await _pageEditor.SaveNowAsync();
        }

        private async Task ToggleFocusMode()
        {
            LayoutState current = LayoutStateService.State;
            await LayoutStateService.SetStateAsync(current with { FocusMode = !current.FocusMode });
        }

        private async Task OnZoomOutRequested()
        {
            LayoutState current = LayoutStateService.State;
            int next = Math.Max(60, current.EditorZoomPercent - 10);
            await LayoutStateService.SetStateAsync(current with { EditorZoomPercent = next });
        }

        private async Task OnZoomInRequested()
        {
            LayoutState current = LayoutStateService.State;
            int next = Math.Min(200, current.EditorZoomPercent + 10);
            await LayoutStateService.SetStateAsync(current with { EditorZoomPercent = next });
        }

        private string GetZoomLabel()
        {
            return $"{LayoutStateService.State.EditorZoomPercent}%";
        }

        private string GetBlockTypeValue()
        {
            return string.IsNullOrWhiteSpace(_formattingState.BlockType)
                ? "paragraph"
                : _formattingState.BlockType;
        }

        private Task OnBlockTypeChanged(ChangeEventArgs args)
        {
            string? value = args.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return Task.CompletedTask;
            }

            if (string.Equals(value, "paragraph", StringComparison.Ordinal))
            {
                return InvokePageCommandAsync("setParagraph");
            }

            if (value.StartsWith("heading:", StringComparison.Ordinal)
                && int.TryParse(value.AsSpan("heading:".Length), out int level))
            {
                return InvokePageCommandAsync("setHeading", level);
            }

            return Task.CompletedTask;
        }

        private async Task OnToggleEditorWidthMode()
        {
            LayoutState current = LayoutStateService.State;
            ManuscriptWidthMode next = current.ManuscriptWidthMode == ManuscriptWidthMode.Manuscript
                ? ManuscriptWidthMode.FullWidth
                : ManuscriptWidthMode.Manuscript;
            await LayoutStateService.SetStateAsync(current with { ManuscriptWidthMode = next });
        }

        private string GetEditorWidthLabel()
        {
            LayoutState current = LayoutStateService.State;
            return current.ManuscriptWidthMode == ManuscriptWidthMode.Manuscript
                ? "Switch to full width"
                : "Switch to manuscript width";
        }

        private void SetContextTab(ContextTab tab)
        {
            _activeContextTab = tab;
        }

        private string GetContextTabClass(ContextTab tab)
        {
            return _activeContextTab == tab ? "is-active" : string.Empty;
        }

        private async Task OnNotesSave()
        {
            if (_activePage is null)
            {
                return;
            }

            try
            {
                await SavePageNotesAsync(_activePage.Id, _notesDraft);
                _notesStatus = "Notes saved.";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Notes save failed.");
                _notesStatus = "Failed to save notes.";
            }
            finally
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        private void OnNotesInput(ChangeEventArgs args)
        {
            _notesDraft = args.Value?.ToString() ?? string.Empty;
            _notesStatus = null;
        }

        private async Task<string> LoadPageNotesAsync(Guid pageId)
        {
            try
            {
                string? value = await JSRuntime.InvokeAsync<string?>(
                    "writerAppStorage.getPageNotes",
                    pageId.ToString());
                return value ?? string.Empty;
            }
            catch (JSException)
            {
                return string.Empty;
            }
        }

        private Task SavePageNotesAsync(Guid pageId, string value)
        {
            return JSRuntime.InvokeVoidAsync(
                "writerAppStorage.setPageNotes",
                pageId.ToString(),
                value ?? string.Empty).AsTask();
        }

        private async Task OnExportRequested(ExportKind kind, ExportFormat format)
        {
            _isDocumentMenuOpen = false;
            Document? document = await BuildExportDocumentAsync();
            if (document is null)
            {
                return;
            }

            ExportResult result = await ExportService.ExportAsync(document, kind, format, new ExportOptions());
            await DownloadExportAsync(result);
        }

        private async Task OnExportPdfRequested()
        {
            _isDocumentMenuOpen = false;
            Document? document = await BuildExportDocumentAsync();
            if (document is null)
            {
                return;
            }

            string bodyHtml = await ExportService.ExportHtmlBodyAsync(document, ExportKind.Document, new ExportOptions());
            string printShell = $"<html><body>{bodyHtml}</body></html>";
            await PrintExportAsync(printShell);
        }

        private async Task DownloadExportAsync(ExportResult result)
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            byte[] payload = result.Content;
            await _exportModule.InvokeVoidAsync("downloadFile", payload, result.MimeType, result.FileName);
        }

        private async Task PrintExportAsync(string html)
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            await _exportModule.InvokeVoidAsync("printHtmlAsPdf", html);
        }

        private async Task EnsureExportModuleAsync()
        {
            if (_exportModule is null)
            {
                _exportModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    "/js/export.js");
            }
        }

        private Document BuildAiDocument(string activeSectionHtml)
        {
            Chapter chapter = new()
            {
                Order = 0,
                Title = string.IsNullOrWhiteSpace(_documentTitle) ? "Draft" : _documentTitle,
                Sections = _sections
                    .OrderBy(section => section.OrderIndex)
                    .Select(section => new Section
                    {
                        SectionId = section.Id,
                        Order = section.OrderIndex,
                        Title = section.Title,
                        Content = new SectionContent
                        {
                            Format = "html",
                            Value = section.Id == _activeSection?.Id ? activeSectionHtml : string.Empty
                        },
                        Notes = section.NarrativePurpose ?? string.Empty,
                        AI = new SectionAIInfo()
                    })
                    .ToList()
            };

            return new Document
            {
                DocumentId = DocumentId,
                Metadata = new DocumentMetadata
                {
                    Title = _documentTitle,
                    Language = "en",
                    CreatedUtc = DateTime.UtcNow,
                    ModifiedUtc = DateTime.UtcNow
                },
                Chapters = new List<Chapter> { chapter }
            };
        }

        private Task<Document?> BuildExportDocumentAsync()
        {
            if (_sections.Count == 0)
            {
                return Task.FromResult<Document?>(null);
            }

            Chapter chapter = new()
            {
                Order = 0,
                Title = string.IsNullOrWhiteSpace(_documentTitle) ? "Draft" : _documentTitle,
                Sections = _sections
                    .OrderBy(section => section.OrderIndex)
                    .Select(section =>
                    {
                        string content = string.Join("\n", GetPages(section.Id).Select(page => page.Content ?? string.Empty));
                        return new Section
                        {
                            SectionId = section.Id,
                            Order = section.OrderIndex,
                            Title = section.Title,
                            Content = new SectionContent
                            {
                                Format = "html",
                                Value = content
                            },
                            Notes = section.NarrativePurpose ?? string.Empty,
                            AI = new SectionAIInfo()
                        };
                    })
                    .ToList()
            };

            return Task.FromResult<Document?>(new Document
            {
                DocumentId = DocumentId,
                Metadata = new DocumentMetadata
                {
                    Title = _documentTitle,
                    Language = "en",
                    CreatedUtc = DateTime.UtcNow,
                    ModifiedUtc = DateTime.UtcNow
                },
                Chapters = new List<Chapter> { chapter }
            });
        }

        private TextRange NormalizeRange(SectionEditor.EditorSelectionRange selection, int maxLength)
        {
            int start = Math.Clamp(selection.Start, 0, maxLength);
            int end = Math.Clamp(selection.End, 0, maxLength);
            if (end < start)
            {
                (start, end) = (end, start);
            }

            return new TextRange(start, Math.Max(0, end - start));
        }

        private static string ExtractRangeText(string plainText, TextRange range)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            int start = Math.Clamp(range.Start, 0, plainText.Length);
            int end = Math.Clamp(range.Start + range.Length, 0, plainText.Length);
            return plainText.Substring(start, Math.Max(0, end - start));
        }

        private static string BuildActionLabel(AiActionOption action)
        {
            return action.Label;
        }

        private void ShowAiMessage(string message)
        {
            _pendingAiProposal = new PendingAiProposal(
                null,
                "AI",
                null,
                null,
                null,
                message,
                DateTime.UtcNow);
            _pendingDetailsExpanded = false;
        }

        private async Task OnApplyPendingAiProposal()
        {
            if (_pendingAiProposal is null)
            {
                return;
            }

            PendingAiProposal pending = _pendingAiProposal;
            if (pending.ProposedText is null)
            {
                _pendingAiProposal = null;
                await InvokeAsync(StateHasChanged);
                return;
            }

            await InvokePageCommandAsync("replaceSelection", pending.ProposedText);
            Guid historyId = pending.Proposal?.ProposalId ?? Guid.NewGuid();
            _aiHistoryEntries.Add(new AiHistoryEntry(
                historyId,
                pending.Instruction,
                pending.Proposal?.SummaryLabel ?? pending.Instruction,
                pending.Proposal?.ProviderId,
                pending.Proposal?.TargetScope,
                pending.Proposal?.Reason,
                pending.Proposal?.Instruction ?? pending.Instruction,
                pending.OriginalText,
                pending.ProposedText,
                DateTime.UtcNow));
            _expandedAiHistoryId = historyId;
            _pendingDetailsExpanded = false;
            _pendingAiProposal = null;
            await InvokeAsync(StateHasChanged);
        }

        private async Task OnDiscardPendingAiProposal()
        {
            _pendingAiProposal = null;
            _pendingDetailsExpanded = false;
            await InvokeAsync(StateHasChanged);
        }

        private Task OnGenerateCoverImage()
        {
            ShowAiMessage("Cover image generation is not available for page-based documents yet.");
            return InvokeAsync(StateHasChanged);
        }

        private bool CanShowAiMenu => _canShowAiMenu;

        private bool IsAiAvailable => IsAiUiEnabled && IsAiEntitled && !IsAiQuotaExceeded;

        private bool IsAiUiEnabled => _aiUsageStatus?.UiEnabled == true;

        private bool IsAiEntitled => _aiUsageStatus?.AiEnabled == true;

        private bool IsAiQuotaExceeded => _aiUsageStatus is not null && _aiUsageStatus.QuotaRemaining <= 0;

        private void TogglePendingDetails()
        {
            _pendingDetailsExpanded = !_pendingDetailsExpanded;
        }

        private void OnToggleAiHistoryDetails(AiHistoryEntry entry)
        {
            if (_expandedAiHistoryId == entry.Id)
            {
                _expandedAiHistoryId = null;
                return;
            }

            _expandedAiHistoryId = entry.Id;
        }

        private static string FormatHistoryText(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? "No content captured." : text;
        }

        private string GetPendingSummary()
        {
            if (_pendingAiProposal?.Proposal is not null
                && !string.IsNullOrWhiteSpace(_pendingAiProposal.Proposal.SummaryLabel))
            {
                return _pendingAiProposal.Proposal.SummaryLabel;
            }

            return _pendingAiProposal?.Instruction ?? "AI change";
        }

        private static string GetAiHistoryDetailsId(AiHistoryEntry entry)
        {
            return $"ai-history-details-{entry.Id}";
        }

        private string GetAiBlockedMessage()
        {
            if (_aiUsageStatus is null)
            {
                return "AI is not available right now.";
            }

            if (!IsAiEntitled)
            {
                return "AI is not enabled for your plan.";
            }

            if (IsAiQuotaExceeded)
            {
                return "You've reached your monthly AI limit.";
            }

            return "AI usage is not available.";
        }

        private async Task LoadAiUsageStatusAsync()
        {
            if (_aiUsageRefreshInProgress)
            {
                return;
            }

            _aiUsageRefreshInProgress = true;
            try
            {
                AuthenticationState authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                ClaimsPrincipal user = authState.User;
                string userId = UserIdResolver.ResolveUserId(user);

                AiUsageStatus status = await AiUsageStatusService.GetStatusAsync(userId);
                WriterAiOptions aiOptions = AiOptionsAccessor.Value;

                _aiUsageStatus = new AiUsageStatusDto
                {
                    Plan = status.PlanName,
                    AiEnabled = aiOptions.Enabled && status.AiEnabled,
                    UiEnabled = aiOptions.Enabled && aiOptions.UI.ShowAiMenu,
                    QuotaTotal = status.QuotaTotal,
                    QuotaRemaining = status.QuotaRemaining
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "AI status resolution failed.");
                _aiUsageStatus = null;
            }
            finally
            {
                _aiUsageRefreshInProgress = false;
                UpdateAiMenuVisibility();
            }
        }

        private void UpdateAiMenuVisibility()
        {
            bool aiEnabled = _aiUsageStatus?.AiEnabled == true;
            bool uiEnabled = _aiUsageStatus?.UiEnabled == true;
            bool visible = uiEnabled && aiEnabled;

            if (_lastAiMenuVisibility is null || _lastAiMenuVisibility.Value != visible)
            {
                _lastAiMenuVisibility = visible;
            }

            _canShowAiMenu = visible;
        }

        private Task InvokePageCommandAsync(string command, params object?[] extraArgs)
        {
            if (_pageEditor is null)
            {
                return Task.CompletedTask;
            }

            return _pageEditor.InvokeCommandAsync(command, extraArgs);
        }

        private sealed record AiActionOption(
            string Label,
            string Instruction,
            AiActionScope Scope,
            Dictionary<string, object?> Inputs);

        private sealed record AiHistoryEntry(
            Guid Id,
            string Label,
            string? Summary,
            string? ProviderId,
            string? TargetScope,
            string? Reason,
            string? Instruction,
            string? BeforeText,
            string? AfterText,
            DateTime Timestamp);

        private sealed record PendingAiProposal(
            AiProposal? Proposal,
            string Instruction,
            string? OriginalText,
            string? ProposedText,
            string? ImageDataUrl,
            string? ErrorMessage,
            DateTime Timestamp);

        private enum ContextTab
        {
            Notes,
            Outline,
            Ai
        }
    }
}
