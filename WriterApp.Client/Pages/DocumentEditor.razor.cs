using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Client.Diagnostics;
using WriterApp.Client.State;
using WriterApp.Application.Usage;
using WriterApp.Client.Components.Editor;

namespace WriterApp.Client.Pages
{
    public partial class DocumentEditor : ComponentBase, IDisposable
    {
        [Parameter]
        public Guid DocumentId { get; set; }

        [Parameter]
        public Guid SectionId { get; set; }

        [Inject]
        public HttpClient Http { get; set; } = default!;

        [Inject]
        public NavigationManager Navigation { get; set; } = default!;

        [Inject]
        public ILogger<DocumentEditor> Logger { get; set; } = default!;

        [Inject]
        public LayoutStateService LayoutStateService { get; set; } = default!;

        [Inject]
        public CurrentDocumentStateService CurrentDocumentStateService { get; set; } = default!;

        [Inject]
        public LastOpenedDocumentStateService LastOpenedDocumentStateService { get; set; } = default!;

        [Inject]
        public IJSRuntime JSRuntime { get; set; } = default!;

        [Inject]
        public IConfiguration Configuration { get; set; } = default!;

        private readonly List<SectionDto> _sections = new();
        private readonly Dictionary<Guid, List<PageDto>> _pagesBySection = new();
        private SectionDto? _activeSection;
        private PageDto? _activePage;
        private bool _isLoading = true;
        private string? _loadError;
        private string? _sectionError;
        private bool _isCreatingSection;
        private Guid? _renamingSectionId;
        private string _sectionRenameDraft = string.Empty;
        private string _sectionRenameOriginal = string.Empty;
        private string? _sectionRenameError;
        private bool _isRenamingSectionSaving;
        private Guid? _sectionMenuOpenId;
        private bool _isDeleteDialogOpen;
        private Guid? _pendingDeleteSectionId;
        private string _pendingDeleteSectionTitle = string.Empty;
        private string? _sectionDeleteError;
        private bool _isDeletingSection;
        private Guid _loadedDocumentId;
        private string _documentTitle = string.Empty;
        private bool _layoutStateInitialized;
        private PageEditor? _pageEditor;
        private Guid? _draggedSectionId;
        private bool _isReorderingSections;
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
        private readonly List<AiActionOption> _aiActions = new();
        private readonly List<AiActionOption> _aiActionPresets = new()
        {
            new AiActionOption(
                "rewrite.selection",
                "Rewrite (Neutral)",
                "Rewrite (Neutral)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Neutral",
                    ["length"] = "Same",
                    ["preserve_terms"] = true
                }),
            new AiActionOption(
                "rewrite.selection",
                "Rewrite (Formal)",
                "Rewrite (Formal)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Formal",
                    ["length"] = "Same",
                    ["preserve_terms"] = true
                }),
            new AiActionOption(
                "rewrite.selection",
                "Rewrite (Casual)",
                "Rewrite (Casual)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Casual",
                    ["length"] = "Same",
                    ["preserve_terms"] = true
                }),
            new AiActionOption(
                "rewrite.selection",
                "Rewrite (Executive)",
                "Rewrite (Executive)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Executive",
                    ["length"] = "Same",
                    ["preserve_terms"] = true
                }),
            new AiActionOption(
                "rewrite.selection",
                "Shorten (Neutral)",
                "Shorten (Neutral)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Neutral",
                    ["length"] = "Shorter",
                    ["preserve_terms"] = true
                }),
            new AiActionOption(
                "rewrite.selection",
                "Fix grammar (Neutral)",
                "Fix grammar (Neutral)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Neutral",
                    ["length"] = "Same",
                    ["preserve_terms"] = true
                }),
            new AiActionOption(
                "rewrite.selection",
                "Change tone (Friendly)",
                "Change tone (Friendly)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Friendly",
                    ["length"] = "Same",
                    ["preserve_terms"] = true
                }),
            new AiActionOption(
                "rewrite.selection",
                "Change tone (Technical)",
                "Change tone (Technical)",
                true,
                new Dictionary<string, object?>
                {
                    ["tone"] = "Technical",
                    ["length"] = "Same",
                    ["preserve_terms"] = true
                })
        };
        private readonly HashSet<string> _availableActionKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<AiHistoryEntry> _aiHistoryEntries = new();
        private Guid? _expandedAiHistoryId;
        private AiUsageStatusDto? _aiUsageStatus;
        private bool _aiUsageRefreshInProgress;
        private bool _canShowAiMenu;
        private bool? _lastAiMenuVisibility;
        private ContextTab _activeContextTab = ContextTab.Notes;
        private string _notesDraft = string.Empty;
        private string? _notesStatus;
        private string _outlineDraft = string.Empty;
        private string? _outlineStatus;
        private PendingAiProposal? _pendingAiProposal;
        private bool _pendingDetailsExpanded;
        private bool _aiUndoRedoInFlight;
        private bool _canAiUndo;
        private bool _canAiRedo;
        private string? _lastReorderStatus;
        private int _lastReorderCount;
        private string? _lastReorderCorrelationId;
        private bool _sectionReorderDiagnosticsEnabled;
        private IJSObjectReference? _exportModule;
        private const int SectionTitleMaxLength = 120;
        private const int PageBreakHeightPx = 980;
        private const int PageBreakGutterOffsetPx = 28;

        private PageEditor.PageBreakOptions PageBreaks =>
            new(PageBreakHeightPx, true, PageBreakGutterOffsetPx);

        protected override async Task OnInitializedAsync()
        {
            await LoadAiUsageStatusAsync();
            await LoadAiActionsAsync();
            _sectionReorderDiagnosticsEnabled = SectionReorderDiagnostics.IsEnabled(Configuration);
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
            CurrentDocumentStateService.SetCurrent(DocumentId, SectionId);
            await LoadDocumentAsync();
        }

        private async Task LoadDocumentAsync()
        {
            _isLoading = true;
            _loadError = null;
            ResetSectionRename();
            CancelDeleteSection();

            try
            {
                DocumentDetailDto? document = await Http.GetFromJsonAsync<DocumentDetailDto>($"api/documents/{DocumentId}");
                if (document is null)
                {
                    _loadError = "Document not found.";
                    return;
                }

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
                    SectionDto? first = _sections.FirstOrDefault();
                    if (first is null)
                    {
                        _loadError = "No sections available.";
                        return;
                    }

                    Navigation.NavigateTo($"documents/{DocumentId}/sections/{first.Id}", replace: true);
                    return;
                }

                await EnsureSectionHasPageAsync(_activeSection.Id);

                _activePage = GetPrimaryPage(_activeSection.Id);
                if (_activePage is null)
                {
                    _loadError = "No pages available.";
                    return;
                }

                await LastOpenedDocumentStateService.SaveAsync(DocumentId, _activeSection.Id);

                _notesDraft = await LoadPageNotesAsync(_activePage.Id);
                _outlineDraft = await LoadDocumentOutlineAsync(DocumentId);
                _notesStatus = null;
                _outlineStatus = null;
                await LoadAiHistoryAsync();
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

        private async Task EnsureSectionHasPageAsync(Guid sectionId)
        {
            List<PageDto> pages = GetPages(sectionId);
            if (pages.Count > 0)
            {
                return;
            }

            try
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                PageCreateRequest request = new(
                    Id: null,
                    Title: "Page 1",
                    Content: string.Empty,
                    OrderIndex: 0,
                    CreatedAt: now,
                    UpdatedAt: now);

                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/sections/{sectionId}/pages", request);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Create default page failed: {Status}", response.StatusCode);
                    return;
                }

                List<PageDto>? updated = await Http.GetFromJsonAsync<List<PageDto>>($"api/sections/{sectionId}/pages");
                _pagesBySection[sectionId] = updated?.OrderBy(page => page.OrderIndex).ToList()
                    ?? new List<PageDto>();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Create default page failed.");
            }
        }

        private async Task OnSectionSelected(Guid sectionId)
        {
            await LastOpenedDocumentStateService.SaveAsync(DocumentId, sectionId);
            Navigation.NavigateTo($"documents/{DocumentId}/sections/{sectionId}");
        }

        private void BeginSectionRename(Guid sectionId)
        {
            SectionDto? section = _sections.FirstOrDefault(item => item.Id == sectionId);
            if (section is null)
            {
                return;
            }

            _sectionMenuOpenId = null;
            _renamingSectionId = sectionId;
            _sectionRenameDraft = section.Title ?? string.Empty;
            _sectionRenameOriginal = section.Title?.Trim() ?? string.Empty;
            _sectionRenameError = null;
        }

        private void ResetSectionRename()
        {
            _renamingSectionId = null;
            _sectionRenameDraft = string.Empty;
            _sectionRenameOriginal = string.Empty;
            _sectionRenameError = null;
            _isRenamingSectionSaving = false;
        }

        private void CancelSectionRename()
        {
            ResetSectionRename();
        }

        private void OnSectionRenameInput(ChangeEventArgs args)
        {
            _sectionRenameDraft = args.Value?.ToString() ?? string.Empty;
            _ = TryGetTrimmedSectionTitle(out _);
        }

        private async Task OnSectionRenameBlurAsync(Guid sectionId)
        {
            await CommitSectionRenameAsync(sectionId);
        }

        private async Task OnSectionRenameKeyDown(KeyboardEventArgs args, Guid sectionId)
        {
            if (args.Key == "Escape")
            {
                CancelSectionRename();
                return;
            }

            if (args.Key == "Enter")
            {
                await CommitSectionRenameAsync(sectionId);
            }
        }

        private bool TryGetTrimmedSectionTitle(out string trimmed)
        {
            trimmed = _sectionRenameDraft.Trim();
            if (trimmed.Length == 0)
            {
                _sectionRenameError = "Title is required.";
                return false;
            }

            if (trimmed.Length > SectionTitleMaxLength)
            {
                _sectionRenameError = $"Keep the title under {SectionTitleMaxLength} characters.";
                return false;
            }

            _sectionRenameError = null;
            return true;
        }

        private async Task CommitSectionRenameAsync(Guid sectionId)
        {
            if (_isRenamingSectionSaving || _renamingSectionId != sectionId)
            {
                return;
            }

            if (!TryGetTrimmedSectionTitle(out string trimmed))
            {
                return;
            }

            if (string.Equals(trimmed, _sectionRenameOriginal, StringComparison.Ordinal))
            {
                CancelSectionRename();
                return;
            }

            _isRenamingSectionSaving = true;
            try
            {
                SectionDto? current = _sections.FirstOrDefault(item => item.Id == sectionId);
                SectionUpdateRequest request = new(trimmed, current?.NarrativePurpose);
                using HttpResponseMessage response =
                    await Http.PutAsJsonAsync($"api/documents/{DocumentId}/sections/{sectionId}", request);

                if (!response.IsSuccessStatusCode)
                {
                    _sectionRenameError = "Rename failed.";
                    return;
                }

                SectionDto? updated = await response.Content.ReadFromJsonAsync<SectionDto>();
                if (updated is null)
                {
                    _sectionRenameError = "Rename failed.";
                    return;
                }

                ApplySectionRename(updated);
                ResetSectionRename();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Rename section failed.");
                _sectionRenameError = "Rename failed.";
            }
            finally
            {
                _isRenamingSectionSaving = false;
            }
        }

        private void ApplySectionRename(SectionDto updated)
        {
            int index = _sections.FindIndex(section => section.Id == updated.Id);
            if (index >= 0)
            {
                _sections[index] = updated;
            }

            if (_activeSection?.Id == updated.Id)
            {
                _activeSection = updated;
            }
        }

        private void ToggleSectionMenu(Guid sectionId)
        {
            _sectionMenuOpenId = _sectionMenuOpenId == sectionId ? null : sectionId;
        }

        private void PromptDeleteSection(Guid sectionId)
        {
            SectionDto? section = _sections.FirstOrDefault(item => item.Id == sectionId);
            if (section is null)
            {
                return;
            }

            _sectionMenuOpenId = null;
            _pendingDeleteSectionId = sectionId;
            _pendingDeleteSectionTitle = section.Title;
            _sectionDeleteError = null;
            _isDeleteDialogOpen = true;
        }

        private void CancelDeleteSection()
        {
            _isDeleteDialogOpen = false;
            _pendingDeleteSectionId = null;
            _pendingDeleteSectionTitle = string.Empty;
            _sectionDeleteError = null;
        }

        private async Task ConfirmDeleteSectionAsync()
        {
            if (_pendingDeleteSectionId is null)
            {
                CancelDeleteSection();
                return;
            }

            Guid sectionId = _pendingDeleteSectionId.Value;
            Guid? nextSectionId = ResolveNextSectionId(sectionId);
            _isDeletingSection = true;
            try
            {
                using HttpResponseMessage response =
                    await Http.DeleteAsync($"api/documents/{DocumentId}/sections/{sectionId}");

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    string? message = await TryReadMessageAsync(response);
                    _sectionDeleteError = message ?? "Delete blocked.";
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _sectionDeleteError = "Delete failed.";
                    return;
                }

                _sections.RemoveAll(section => section.Id == sectionId);
                _pagesBySection.Remove(sectionId);
                _isDeleteDialogOpen = false;

                if (_activeSection?.Id == sectionId && nextSectionId is not null)
                {
                    await LastOpenedDocumentStateService.SaveAsync(DocumentId, nextSectionId.Value);
                    Navigation.NavigateTo($"documents/{DocumentId}/sections/{nextSectionId}", replace: true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Delete section failed.");
                _sectionDeleteError = "Delete failed.";
            }
            finally
            {
                _isDeletingSection = false;
            }
        }

        private Guid? ResolveNextSectionId(Guid sectionId)
        {
            int index = _sections.FindIndex(section => section.Id == sectionId);
            if (index < 0)
            {
                return _sections.FirstOrDefault()?.Id;
            }

            if (index + 1 < _sections.Count)
            {
                return _sections[index + 1].Id;
            }

            if (index - 1 >= 0)
            {
                return _sections[index - 1].Id;
            }

            return null;
        }

        private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response)
        {
            try
            {
                Dictionary<string, string>? payload =
                    await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                if (payload is not null && payload.TryGetValue("message", out string? message))
                {
                    return message;
                }
            }
            catch
            {
            }

            return null;
        }

        private void OnSectionDragStart(Guid sectionId)
        {
            if (_isReorderingSections)
            {
                return;
            }

            _draggedSectionId = sectionId;
            SectionReorderDiagnostics.LogDebug(
                Logger,
                Configuration,
                "UI drag start DocId={DocumentId} SectionId={SectionId}",
                DocumentId,
                sectionId);
        }

        private async Task OnSectionDrop(Guid targetSectionId)
        {
            if (_isReorderingSections || _draggedSectionId is null)
            {
                return;
            }

            Guid sourceSectionId = _draggedSectionId.Value;
            _draggedSectionId = null;
            if (sourceSectionId == targetSectionId)
            {
                return;
            }

            int sourceIndex = _sections.FindIndex(section => section.Id == sourceSectionId);
            int targetIndex = _sections.FindIndex(section => section.Id == targetSectionId);
            if (sourceIndex < 0 || targetIndex < 0)
            {
                return;
            }

            SectionDto moved = _sections[sourceIndex];
            _sections.RemoveAt(sourceIndex);
            if (targetIndex > sourceIndex)
            {
                targetIndex--;
            }

            _sections.Insert(targetIndex, moved);
            for (int index = 0; index < _sections.Count; index++)
            {
                _sections[index] = _sections[index] with { OrderIndex = index };
            }

            SectionReorderDiagnostics.LogDebug(
                Logger,
                Configuration,
                "UI drop DocId={DocumentId} Count={Count} FirstId={FirstId} LastId={LastId}",
                DocumentId,
                _sections.Count,
                _sections.FirstOrDefault()?.Id,
                _sections.LastOrDefault()?.Id);

            await SaveSectionOrderAsync();
        }

        private async Task SaveSectionOrderAsync()
        {
            if (_isReorderingSections)
            {
                return;
            }

            _isReorderingSections = true;
            try
            {
                string correlationId = Guid.NewGuid().ToString("N");
                SectionReorderRequest payload = new(_sections.Select(section => section.Id).ToList());
                using HttpRequestMessage request = new(
                    HttpMethod.Post,
                    $"api/documents/{DocumentId}/sections/reorder")
                {
                    Content = JsonContent.Create(payload)
                };
                request.Headers.Add("X-Reorder-Correlation", correlationId);

                SectionReorderDiagnostics.LogDebug(
                    Logger,
                    Configuration,
                    "HTTP send DocId={DocumentId} Count={Count} Corr={CorrelationId}",
                    DocumentId,
                    payload.OrderedSectionIds.Count,
                    correlationId);

                using HttpResponseMessage response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    string? body = null;
                    try
                    {
                        body = await response.Content.ReadAsStringAsync();
                    }
                    catch
                    {
                    }

                    _lastReorderStatus = response.StatusCode.ToString();
                    _lastReorderCount = payload.OrderedSectionIds.Count;
                    _lastReorderCorrelationId = response.Headers.TryGetValues("X-Reorder-Correlation", out var values)
                        ? values.FirstOrDefault()
                        : correlationId;

                    SectionReorderDiagnostics.LogWarning(
                        Logger,
                        Configuration,
                        "HTTP failed DocId={DocumentId} Status={Status} Body={Body} Corr={CorrelationId}",
                        DocumentId,
                        response.StatusCode,
                        body ?? string.Empty,
                        _lastReorderCorrelationId);

                    await ReloadSectionsAsync();
                    return;
                }

                List<SectionDto>? updated = await response.Content.ReadFromJsonAsync<List<SectionDto>>();
                if (updated is not null)
                {
                    _sections.Clear();
                    _sections.AddRange(updated.OrderBy(section => section.OrderIndex));
                    _lastReorderStatus = response.StatusCode.ToString();
                    _lastReorderCount = updated.Count;
                    _lastReorderCorrelationId = response.Headers.TryGetValues("X-Reorder-Correlation", out var values)
                        ? values.FirstOrDefault()
                        : correlationId;
                    SectionReorderDiagnostics.LogDebug(
                        Logger,
                        Configuration,
                        "HTTP success DocId={DocumentId} Count={Count} Corr={CorrelationId}",
                        DocumentId,
                        updated.Count,
                        _lastReorderCorrelationId);
                }
            }
            catch (Exception ex)
            {
                _lastReorderStatus = "Exception";
                _lastReorderCount = _sections.Count;
                SectionReorderDiagnostics.LogWarning(
                    Logger,
                    Configuration,
                    "HTTP exception DocId={DocumentId} Error={Error}",
                    DocumentId,
                    ex.Message);
                await ReloadSectionsAsync();
            }
            finally
            {
                _isReorderingSections = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ReloadSectionsAsync()
        {
            List<SectionDto>? sections = await Http.GetFromJsonAsync<List<SectionDto>>(
                $"api/documents/{DocumentId}/sections");
            if (sections is null)
            {
                return;
            }

            _sections.Clear();
            _sections.AddRange(sections.OrderBy(section => section.OrderIndex));
        }

        private async Task CreateSectionAsync()
        {
            if (_isCreatingSection)
            {
                return;
            }

            _isCreatingSection = true;
            _sectionError = null;

            try
            {
                SectionCreateRequest request = new(
                    Id: null,
                    Title: "New section",
                    NarrativePurpose: null,
                    OrderIndex: _sections.Count,
                    CreatedAt: null,
                    UpdatedAt: null);

                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/documents/{DocumentId}/sections", request);
                response.EnsureSuccessStatusCode();

                SectionDto? created = await response.Content.ReadFromJsonAsync<SectionDto>();
                if (created is null)
                {
                    _sectionError = "Failed to create section.";
                    return;
                }

                _sections.Add(created);
                _sections.Sort((left, right) => left.OrderIndex.CompareTo(right.OrderIndex));
                _pagesBySection[created.Id] = new List<PageDto>();
                Navigation.NavigateTo($"documents/{DocumentId}/sections/{created.Id}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Create section failed.");
                _sectionError = "Failed to create section.";
            }
            finally
            {
                _isCreatingSection = false;
                await InvokeAsync(StateHasChanged);
            }
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
            string scaleText = scale.ToString("0.###", CultureInfo.InvariantCulture);
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

        public void Dispose()
        {
            LayoutStateService.Changed -= OnLayoutStateChanged;

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
            string title = _documentTitle ?? string.Empty;
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
            return InvokePageCommandAsync("setHeading", level);
        }

        private Task OnBulletListRequested()
        {
            return InvokePageCommandAsync("toggleBulletList");
        }

        private Task OnOrderedListRequested()
        {
            return InvokePageCommandAsync("toggleOrderedList");
        }

        private Task OnBlockquoteRequested()
        {
            return InvokePageCommandAsync("toggleBlockquote");
        }

        private async Task OnLinkRequested()
        {
            if (_pageEditor is null)
            {
                return;
            }

            string? link = await JSRuntime.InvokeAsync<string?>("prompt", "Link URL", string.Empty);
            if (string.IsNullOrWhiteSpace(link))
            {
                await InvokePageCommandAsync("unsetLink");
                return;
            }

            await InvokePageCommandAsync("setLink", link);
        }

        private Task OnHorizontalRuleRequested()
        {
            return InvokePageCommandAsync("insertHorizontalRule");
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

        private Task OnUndoRequested()
        {
            return InvokePageCommandAsync("undo");
        }

        private Task OnRedoRequested()
        {
            return InvokePageCommandAsync("redo");
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

        private async Task OnOutlineSave()
        {
            try
            {
                await SaveDocumentOutlineAsync(DocumentId, _outlineDraft);
                _outlineStatus = "Outline saved.";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Outline save failed.");
                _outlineStatus = "Failed to save outline.";
            }
            finally
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        private void OnOutlineInput(ChangeEventArgs args)
        {
            _outlineDraft = args.Value?.ToString() ?? string.Empty;
            _outlineStatus = null;
        }

        private async Task<string> LoadPageNotesAsync(Guid pageId)
        {
            try
            {
                PageNotesDto? result = await Http.GetFromJsonAsync<PageNotesDto>($"api/pages/{pageId}/notes");
                return result?.Notes ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Notes load failed.");
                return string.Empty;
            }
        }

        private async Task SavePageNotesAsync(Guid pageId, string value)
        {
            PageNotesDto payload = new(pageId, value ?? string.Empty, DateTimeOffset.UtcNow);
            using HttpResponseMessage response = await Http.PutAsJsonAsync($"api/pages/{pageId}/notes", payload);
            response.EnsureSuccessStatusCode();
        }

        private async Task<string> LoadDocumentOutlineAsync(Guid documentId)
        {
            try
            {
                DocumentOutlineDto? result = await Http.GetFromJsonAsync<DocumentOutlineDto>($"api/documents/{documentId}/outline");
                return result?.Outline ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Outline load failed.");
                return string.Empty;
            }
        }

        private async Task SaveDocumentOutlineAsync(Guid documentId, string outline)
        {
            DocumentOutlineDto payload = new(documentId, outline ?? string.Empty, DateTimeOffset.UtcNow);
            using HttpResponseMessage response = await Http.PutAsJsonAsync($"api/documents/{documentId}/outline", payload);
            response.EnsureSuccessStatusCode();
        }
        private async Task OnExportRequested(string kind, string format)
        {
            _isDocumentMenuOpen = false;
            try
            {
                using HttpResponseMessage response = await Http.GetAsync(
                    $"api/documents/{DocumentId}/export?kind={kind}&format={format}");

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Export failed: {Status}", response.StatusCode);
                    return;
                }

                byte[] payload = await response.Content.ReadAsByteArrayAsync();
                string base64 = Convert.ToBase64String(payload);
                string fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName
                    ?? $"export.{format}";
                string mime = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                await DownloadExportAsync(base64, mime, fileName.Trim('"'));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Export failed.");
            }
        }

        private async Task OnExportPdfRequested()
        {
            _isDocumentMenuOpen = false;
            try
            {
                ExportPrintPayload? payload = await Http.GetFromJsonAsync<ExportPrintPayload>(
                    $"api/documents/{DocumentId}/export/print?kind=document");
                if (payload is null || string.IsNullOrWhiteSpace(payload.Html))
                {
                    return;
                }

                await PrintExportAsync(payload.Html);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "PDF export failed.");
            }
        }

        private async Task DownloadExportAsync(string base64, string mimeType, string fileName)
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            await _exportModule.InvokeVoidAsync("downloadFile", base64, mimeType, fileName);
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

        private async Task OnAiActionSelected(AiActionOption action)
        {
            if (!IsAiAvailable)
            {
                ShowAiMessage(GetAiBlockedMessage());
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (_currentSelectionRange is null || _activeSection is null)
            {
                return;
            }

            string? html = _pageEditor?.GetContent();
            string plain = PlainTextMapper.ToPlainText(html);
            TextRange selectionRange = NormalizeRange(_currentSelectionRange, plain.Length);
            string selection = ExtractRangeText(plain, selectionRange);

            if (action.RequiresSelection && string.IsNullOrWhiteSpace(selection))
            {
                return;
            }

            Dictionary<string, object?> parameters = new(action.Parameters)
            {
                ["instruction"] = action.Instruction
            };

            AiActionExecuteRequestDto request = new(
                DocumentId,
                _activeSection.Id,
                _activePage?.Id,
                selectionRange.Start,
                selectionRange.Start + selectionRange.Length,
                selection,
                plain,
                _outlineDraft,
                parameters);

            AiActionExecuteResponseDto? response;
            try
            {
                using HttpResponseMessage result = await Http.PostAsJsonAsync(
                    $"api/ai/actions/{action.ActionKey}/execute",
                    request);
                if (!result.IsSuccessStatusCode)
                {
                    ShowAiMessage("AI action failed.");
                    await InvokeAsync(StateHasChanged);
                    return;
                }

                response = await result.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                if (response is null)
                {
                    ShowAiMessage("AI action failed.");
                    await InvokeAsync(StateHasChanged);
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowAiMessage(ex.Message);
                await InvokeAsync(StateHasChanged);
                return;
            }

            _pendingAiProposal = new PendingAiProposal(
                response.ProposalId,
                action.Instruction,
                response.OriginalText,
                response.ProposedText,
                response.ChangesSummary,
                null,
                response.CreatedUtc);
            _pendingDetailsExpanded = false;
            await LoadAiHistoryAsync();
            await InvokeAsync(StateHasChanged);
        }

        private void ShowAiMessage(string message)
        {
            _pendingAiProposal = new PendingAiProposal(
                Guid.NewGuid(),
                "AI",
                null,
                null,
                null,
                message,
                DateTimeOffset.UtcNow);
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

            string? beforeContent = _pageEditor is null ? null : await _pageEditor.GetContentAsync();
            await InvokePageCommandAsync("replaceSelection", pending.ProposedText);
            string? afterContent = _pageEditor is null ? null : await _pageEditor.GetContentAsync();
            DateTimeOffset appliedAt = DateTimeOffset.UtcNow;
            UpdateAiHistoryAppliedState(pending.ProposalId, appliedAt);
            _expandedAiHistoryId = pending.ProposalId;
            _pendingDetailsExpanded = false;
            _pendingAiProposal = null;
            _ = RecordAppliedEventAsync(pending.ProposalId, appliedAt, beforeContent, afterContent);
            UpdateAiUndoRedoAvailability();
            await InvokeAsync(StateHasChanged);
        }

        private async Task OnDiscardPendingAiProposal()
        {
            _pendingAiProposal = null;
            _pendingDetailsExpanded = false;
            await InvokeAsync(StateHasChanged);
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

        private void UpdateAiUndoRedoAvailability()
        {
            _canAiUndo = _aiHistoryEntries.Any(entry => entry.IsApplied);
            _canAiRedo = _aiHistoryEntries.Any(entry => entry.AppliedCount > 0 && !entry.IsApplied);
        }

        private static string GetActionLabel(string actionKey)
        {
            if (string.Equals(actionKey, "rewrite.selection", StringComparison.OrdinalIgnoreCase))
            {
                return "Rewrite selection";
            }

            if (string.Equals(actionKey, "generate.image.cover", StringComparison.OrdinalIgnoreCase))
            {
                return "Generate cover image";
            }

            if (string.Equals(actionKey, "synopsis.story_coach", StringComparison.OrdinalIgnoreCase))
            {
                return "Story Coach";
            }

            return "AI";
        }

        private static string FormatHistoryText(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? "No content captured." : text;
        }

        private string GetPendingSummary()
        {
            if (!string.IsNullOrWhiteSpace(_pendingAiProposal?.ChangesSummary))
            {
                return _pendingAiProposal?.ChangesSummary ?? "AI change";
            }

            return _pendingAiProposal?.ActionLabel ?? "AI change";
        }

        private static string GetAiHistoryDetailsId(AiHistoryEntry entry)
        {
            return $"ai-history-details-{entry.Id}";
        }

        private void UpdateAiHistoryAppliedState(Guid historyEntryId, DateTimeOffset appliedAt)
        {
            if (historyEntryId == Guid.Empty)
            {
                return;
            }

            int index = _aiHistoryEntries.FindIndex(entry => entry.Id == historyEntryId);
            if (index >= 0)
            {
                AiHistoryEntry current = _aiHistoryEntries[index];
                int nextCount = Math.Max(1, current.AppliedCount + 1);
                DateTimeOffset nextAppliedAt = current.LastAppliedAt.HasValue && current.LastAppliedAt > appliedAt
                    ? current.LastAppliedAt.Value
                    : appliedAt;
                _aiHistoryEntries[index] = current with
                {
                    IsApplied = true,
                    AppliedCount = nextCount,
                    LastAppliedAt = nextAppliedAt
                };
                return;
            }

            _aiHistoryEntries.Add(new AiHistoryEntry(
                historyEntryId,
                "unknown",
                "AI",
                null,
                null,
                null,
                appliedAt,
                true,
                appliedAt,
                1));
        }

        private async Task RecordAppliedEventAsync(
            Guid historyEntryId,
            DateTimeOffset appliedAt,
            string? beforeContent,
            string? afterContent)
        {
            if (historyEntryId == Guid.Empty)
            {
                return;
            }

            var payload = new
            {
                DocumentId,
                SectionId,
                PageId = _activePage?.Id,
                BeforeContent = beforeContent,
                AfterContent = afterContent
            };

            try
            {
                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/ai/actions/history/{historyEntryId}/applied", payload);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Apply AI history event failed: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Apply AI history event failed.");
            }
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
                using HttpResponseMessage response = await Http.GetAsync("api/ai/status");
                if (!response.IsSuccessStatusCode)
                {
                    _aiUsageStatus = null;
                    return;
                }

                AiUsageStatusDto? status = await response.Content.ReadFromJsonAsync<AiUsageStatusDto>();
                _aiUsageStatus = status;
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

        private async Task LoadAiActionsAsync()
        {
            try
            {
                List<AiActionDescriptorDto>? actions = await Http.GetFromJsonAsync<List<AiActionDescriptorDto>>("api/ai/actions");
                _availableActionKeys.Clear();
                if (actions is not null)
                {
                    foreach (AiActionDescriptorDto action in actions)
                    {
                        _availableActionKeys.Add(action.ActionKey);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "AI actions load failed.");
            }

            _aiActions.Clear();
            if (_availableActionKeys.Count == 0)
            {
                return;
            }

            foreach (AiActionOption preset in _aiActionPresets)
            {
                if (_availableActionKeys.Contains(preset.ActionKey))
                {
                    _aiActions.Add(preset);
                }
            }
        }

        private async Task LoadAiHistoryAsync()
        {
            try
            {
                List<AiActionHistoryEntryDto>? entries =
                    await Http.GetFromJsonAsync<List<AiActionHistoryEntryDto>>($"api/ai/actions/history?documentId={DocumentId}");
                _aiHistoryEntries.Clear();
                if (entries is not null)
                {
                    foreach (AiActionHistoryEntryDto entry in entries.OrderByDescending(item => item.CreatedUtc))
                    {
                        string label = string.IsNullOrWhiteSpace(entry.Summary)
                            ? GetActionLabel(entry.ActionKey)
                            : entry.Summary;
                        _aiHistoryEntries.Add(new AiHistoryEntry(
                            entry.ProposalId,
                            entry.ActionKey,
                            label,
                            entry.Summary,
                            entry.OriginalText,
                            entry.ProposedText,
                            entry.CreatedUtc,
                            entry.IsApplied,
                            entry.LastAppliedAt,
                            entry.AppliedCount));
                    }
                }
            }
            catch
            {
                _aiHistoryEntries.Clear();
            }
            finally
            {
                UpdateAiUndoRedoAvailability();
            }
        }

        private async Task OnAiUndoRequested()
        {
            if (_aiUndoRedoInFlight || _pageEditor is null || _activeSection is null)
            {
                return;
            }

            _aiUndoRedoInFlight = true;
            try
            {
                AiActionUndoRedoRequestDto request = new(DocumentId, _activeSection.Id, _activePage?.Id);
                using HttpResponseMessage response = await Http.PostAsJsonAsync("api/ai/actions/history/undo", request);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("AI undo failed: {Status}", response.StatusCode);
                    return;
                }

                AiActionUndoRedoResponseDto? payload = await response.Content.ReadFromJsonAsync<AiActionUndoRedoResponseDto>();
                if (payload is null || string.IsNullOrWhiteSpace(payload.Content))
                {
                    return;
                }

                await _pageEditor.SetContentAsync(payload.Content, markDirty: true);
                await _pageEditor.SaveNowAsync();
                await LoadAiHistoryAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "AI undo failed.");
            }
            finally
            {
                _aiUndoRedoInFlight = false;
            }
        }

        private async Task OnAiRedoRequested()
        {
            if (_aiUndoRedoInFlight || _pageEditor is null || _activeSection is null)
            {
                return;
            }

            _aiUndoRedoInFlight = true;
            try
            {
                AiActionUndoRedoRequestDto request = new(DocumentId, _activeSection.Id, _activePage?.Id);
                using HttpResponseMessage response = await Http.PostAsJsonAsync("api/ai/actions/history/redo", request);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("AI redo failed: {Status}", response.StatusCode);
                    return;
                }

                AiActionUndoRedoResponseDto? payload = await response.Content.ReadFromJsonAsync<AiActionUndoRedoResponseDto>();
                if (payload is null || string.IsNullOrWhiteSpace(payload.Content))
                {
                    return;
                }

                await _pageEditor.SetContentAsync(payload.Content, markDirty: true);
                await _pageEditor.SaveNowAsync();
                await LoadAiHistoryAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "AI redo failed.");
            }
            finally
            {
                _aiUndoRedoInFlight = false;
            }
        }

        private void UpdateAiMenuVisibility()
        {
            bool visible = IsAiUiEnabled && IsAiEntitled;
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

        private static TextRange NormalizeRange(SectionEditor.EditorSelectionRange selection, int maxLength)
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

        private sealed record AiActionOption(
            string ActionKey,
            string Label,
            string Instruction,
            bool RequiresSelection,
            Dictionary<string, object?> Parameters);

        private sealed record AiHistoryEntry(
            Guid Id,
            string ActionKey,
            string Label,
            string? Summary,
            string? BeforeText,
            string? AfterText,
            DateTimeOffset Timestamp,
            bool IsApplied = false,
            DateTimeOffset? LastAppliedAt = null,
            int AppliedCount = 0);

        private sealed record PendingAiProposal(
            Guid ProposalId,
            string ActionLabel,
            string? OriginalText,
            string? ProposedText,
            string? ChangesSummary,
            string? ErrorMessage,
            DateTimeOffset CreatedUtc);

        private sealed record ExportPrintPayload(string Html);

        private sealed record TextRange(int Start, int Length);

        private enum ContextTab
        {
            Notes,
            Outline,
            Ai
        }
    }
}
