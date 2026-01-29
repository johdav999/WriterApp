using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Exporting;
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
        private bool _isExportDialogOpen;
        private bool _isTemplateManagerOpen;
        private bool _isTemplateEditorOpen;
        private bool _isTemplatesLoading;
        private bool _isTemplateSaving;
        private bool _isTemplateDeleting;
        private string? _templateLoadError;
        private string? _templateActionError;
        private readonly List<ExportTemplateDto> _exportTemplates = new();
        private Guid? _selectedTemplateId;
        private string _exportFormatSelection = "html";
        private string _createPresetKey = "manuscript";
        private Guid? _editingTemplateId;
        private ExportTemplateEditorModel? _templateEditor;
        private string _templateEditorPagePreset = "custom";
        private bool _isExportPreviewOpen;
        private bool _isPreviewLoading;
        private string? _previewError;
        private string _previewHtml = string.Empty;
        private double _previewZoom = 1.0;
        private string _previewScope = "document";
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
                }),
            new AiActionOption(
                "propose.next-paragraph",
                "Propose next paragraph",
                "Propose next paragraph",
                false,
                new Dictionary<string, object?>(),
                "Generate a 10-12 sentence continuation based on current section + scene beats.")
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
        private string _sceneNarrativePurpose = string.Empty;
        private string _sceneEmotionalBeat = string.Empty;
        private string _sceneKeyEvents = string.Empty;
        private string _sceneOpenQuestions = string.Empty;
        private string? _sceneStatus;
        private bool _sceneSaveInFlight;
        private CancellationTokenSource? _sceneAutosaveCts;
        private Guid? _sceneCardSectionId;
        private SectionSceneCardProposalDto? _sceneAiProposal;
        private string? _sceneAiExplanation;
        private Guid? _sceneAiProposalId;
        private string? _sceneAiError;
        private bool _sceneAiInFlight;
        private string? _outlineStatus;
        /*
         * Feature flag: outline node <-> section linking
         * - Disables link badges, connect picker, unlink action
         * - Disables navigation from outline node -> section
         * - Disables UI-driven updates to LinkedSectionId
         * Re-enable: set to true and rebuild.
         */
        private static readonly bool EnableOutlineSectionLinking = false;
        private readonly List<DocumentOutlineNodeDto> _outlineNodes = new();
        private readonly HashSet<Guid> _outlineCollapsed = new();
        private Guid? _outlineRenameId;
        private string _outlineRenameDraft = string.Empty;
        private string? _outlineRenameError;
        private Guid? _outlineLinkPickerOpenId;
        private string? _outlineLinkPickerError;
        private Guid? _outlineDragId;
        private Guid? _activeOutlineNodeId;
        private IReadOnlyList<DocumentOutlineNodeDto>? _outlineProposalNodes;
        private string? _outlineProposalPreview;
        private bool _outlineProposalTruncated;
        private bool _outlineGenerateInFlight;
        private string? _outlineGenerateError;
        private bool _outlineApplyInFlight;
        private string? _outlineApplyError;
        private bool _outlineApplyCreateMissing = true;
        private bool _outlineApplyReorder = true;
        private bool _outlineApplyRename;
        private bool _outlineApplyLinkNodes = true;
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
        private static readonly TimeSpan SceneCardAutosaveDebounce = TimeSpan.FromSeconds(2.5);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private PageEditor.PageBreakOptions PageBreaks =>
            new(PageBreakHeightPx, true, PageBreakGutterOffsetPx);
        private IEnumerable<AiActionOption> SelectionAiActions =>
            _aiActions.Where(action => action.RequiresSelection);
        private IEnumerable<AiActionOption> SectionAiActions =>
            _aiActions.Where(action => !action.RequiresSelection);

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
                _notesStatus = null;
                await LoadSceneCardAsync(_activeSection.Id);
                _outlineStatus = null;
                await LoadOutlineNodesAsync();
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

        private async Task DuplicateSectionAsync(Guid sectionId)
        {
            _sectionMenuOpenId = null;
            _sectionError = null;
            try
            {
                using HttpResponseMessage response =
                    await Http.PostAsync($"api/documents/{DocumentId}/sections/{sectionId}/duplicate", null);
                if (!response.IsSuccessStatusCode)
                {
                    _sectionError = "Duplicate failed.";
                    return;
                }

                SectionDto? created = await response.Content.ReadFromJsonAsync<SectionDto>();
                if (created is null)
                {
                    _sectionError = "Duplicate failed.";
                    return;
                }

                _sections.Add(created);
                _sections.Sort((left, right) => left.OrderIndex.CompareTo(right.OrderIndex));
                _pagesBySection[created.Id] = new List<PageDto>();
                await LastOpenedDocumentStateService.SaveAsync(DocumentId, created.Id);
                Navigation.NavigateTo($"documents/{DocumentId}/sections/{created.Id}", replace: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Duplicate section failed.");
                _sectionError = "Duplicate failed.";
            }
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

        private string? GetTooltip(string text)
        {
            return LayoutStateService.State.FocusMode ? null : text;
        }

        private async Task ToggleContextPanel()
        {
            LayoutState current = LayoutStateService.State;
            await LayoutStateService.SetStateAsync(current with { ContextCollapsed = !current.ContextCollapsed });
        }

        public void Dispose()
        {
            LayoutStateService.Changed -= OnLayoutStateChanged;
            _sceneAutosaveCts?.Cancel();
            _sceneAutosaveCts?.Dispose();
            _sceneAutosaveCts = null;

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

        private void OnSceneNarrativePurposeInput(ChangeEventArgs args)
        {
            _sceneNarrativePurpose = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneEmotionalBeatInput(ChangeEventArgs args)
        {
            _sceneEmotionalBeat = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneKeyEventsInput(ChangeEventArgs args)
        {
            _sceneKeyEvents = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneOpenQuestionsInput(ChangeEventArgs args)
        {
            _sceneOpenQuestions = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneCardInputChanged()
        {
            _sceneStatus = null;
            QueueSceneCardAutosave();
        }

        private async Task OnSceneCardSave()
        {
            if (_activeSection is null)
            {
                return;
            }

            await SaveSceneCardAsync(_activeSection.Id, isAutosave: false);
        }

        private async Task LoadSceneCardAsync(Guid sectionId)
        {
            _sceneAutosaveCts?.Cancel();
            _sceneAutosaveCts = null;
            _sceneStatus = null;
            _sceneAiProposal = null;
            _sceneAiExplanation = null;
            _sceneAiProposalId = null;
            _sceneAiError = null;
            _sceneCardSectionId = sectionId;

            try
            {
                SectionSceneCardDto? card =
                    await Http.GetFromJsonAsync<SectionSceneCardDto>($"api/sections/{sectionId}/scene-card");

                _sceneNarrativePurpose = card?.NarrativePurpose ?? string.Empty;
                _sceneEmotionalBeat = card?.EmotionalBeat ?? string.Empty;
                _sceneKeyEvents = card?.KeyEvents ?? string.Empty;
                _sceneOpenQuestions = card?.OpenQuestions ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Scene card load failed.");
                _sceneStatus = "Failed to load scene card.";
            }
        }

        private void QueueSceneCardAutosave()
        {
            if (_activeSection is null)
            {
                return;
            }

            _sceneAutosaveCts?.Cancel();
            _sceneAutosaveCts = new CancellationTokenSource();
            _ = DebouncedSceneCardSaveAsync(_sceneAutosaveCts, _activeSection.Id);
        }

        private async Task DebouncedSceneCardSaveAsync(CancellationTokenSource cts, Guid sectionId)
        {
            try
            {
                await Task.Delay(SceneCardAutosaveDebounce, cts.Token);
                if (cts.IsCancellationRequested || _sceneCardSectionId != sectionId)
                {
                    return;
                }

                await SaveSceneCardAsync(sectionId, isAutosave: true);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private async Task SaveSceneCardAsync(Guid sectionId, bool isAutosave)
        {
            if (_sceneSaveInFlight || _sceneCardSectionId != sectionId)
            {
                return;
            }

            _sceneSaveInFlight = true;
            try
            {
                SectionSceneCardUpdateRequest payload = new(
                    _sceneNarrativePurpose,
                    _sceneEmotionalBeat,
                    _sceneKeyEvents,
                    _sceneOpenQuestions);

                using HttpResponseMessage response =
                    await Http.PutAsJsonAsync($"api/sections/{sectionId}/scene-card", payload);

                if (!response.IsSuccessStatusCode)
                {
                    _sceneStatus = "Failed to save scene card.";
                    return;
                }

                SectionSceneCardDto? updated = await response.Content.ReadFromJsonAsync<SectionSceneCardDto>();
                if (updated is not null)
                {
                    _sceneNarrativePurpose = updated.NarrativePurpose ?? string.Empty;
                    _sceneEmotionalBeat = updated.EmotionalBeat ?? string.Empty;
                    _sceneKeyEvents = updated.KeyEvents ?? string.Empty;
                    _sceneOpenQuestions = updated.OpenQuestions ?? string.Empty;
                }

                _sceneStatus = isAutosave ? "Scene card saved." : "Scene card saved.";
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Scene card save failed.");
                _sceneStatus = "Failed to save scene card.";
            }
            finally
            {
                _sceneSaveInFlight = false;
            }
        }

        private async Task RunSceneAiAsync(string actionKey, string instruction)
        {
            if (_sceneAiInFlight || _activeSection is null)
            {
                return;
            }

            _sceneAiInFlight = true;
            _sceneAiError = null;
            try
            {
                string originalSnapshot = BuildSceneCardSnapshotJson();
                AiActionExecuteRequestDto payload = new(
                    DocumentId,
                    _activeSection.Id,
                    _activePage?.Id,
                    null,
                    null,
                    originalSnapshot,
                    null,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["instruction"] = instruction
                    });

                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/ai/actions/{actionKey}/execute", payload);
                if (!response.IsSuccessStatusCode)
                {
                    _sceneAiError = "AI action failed.";
                    return;
                }

                AiActionExecuteResponseDto? result =
                    await response.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                if (result?.ProposedSceneCard is null)
                {
                    _sceneAiError = "AI action returned no scene card.";
                    return;
                }

                _sceneAiProposal = result.ProposedSceneCard;
                _sceneAiExplanation = result.ProposalExplanation ?? result.ChangesSummary;
                _sceneAiProposalId = result.ProposalId;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Scene card AI failed.");
                _sceneAiError = "AI action failed.";
            }
            finally
            {
                _sceneAiInFlight = false;
            }
        }

        private async Task ApplySceneAiProposalAsync()
        {
            if (_sceneAiProposal is null || _activeSection is null || !_sceneAiProposalId.HasValue)
            {
                return;
            }

            string beforeSnapshot = BuildSceneCardSnapshotJson();
            _sceneNarrativePurpose = _sceneAiProposal.NarrativePurpose ?? string.Empty;
            _sceneEmotionalBeat = _sceneAiProposal.EmotionalBeat ?? string.Empty;
            _sceneKeyEvents = _sceneAiProposal.KeyEvents ?? string.Empty;
            _sceneOpenQuestions = _sceneAiProposal.OpenQuestions ?? string.Empty;

            await SaveSceneCardAsync(_activeSection.Id, isAutosave: false);

            string afterSnapshot = BuildSceneCardSnapshotJson();
            await RecordAiSceneCardAppliedAsync(_sceneAiProposalId.Value, beforeSnapshot, afterSnapshot);
            _sceneAiProposal = null;
            _sceneAiExplanation = null;
            _sceneAiProposalId = null;
            await LoadAiHistoryAsync();
        }

        private void DiscardSceneAiProposal()
        {
            _sceneAiProposal = null;
            _sceneAiExplanation = null;
            _sceneAiProposalId = null;
            _sceneAiError = null;
        }

        private string BuildSceneCardSnapshotJson()
        {
            SectionSceneCardProposalDto snapshot = new(
                _sceneNarrativePurpose,
                _sceneEmotionalBeat,
                _sceneKeyEvents,
                _sceneOpenQuestions);
            return JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        private async Task RecordAiSceneCardAppliedAsync(Guid proposalId, string before, string after)
        {
            var payload = new
            {
                DocumentId,
                SectionId = _activeSection?.Id,
                PageId = _activePage?.Id,
                BeforeContent = before,
                AfterContent = after
            };

            try
            {
                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/ai/actions/history/{proposalId}/applied", payload);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("AI history apply failed: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "AI history apply failed.");
            }
        }

        private async Task LoadOutlineNodesAsync()
        {
            _outlineNodes.Clear();
            _outlineCollapsed.Clear();
            _outlineStatus = null;
            _outlineProposalNodes = null;
            _outlineProposalPreview = null;
            _outlineProposalTruncated = false;
            _outlineGenerateError = null;
            try
            {
                List<DocumentOutlineNodeDto>? nodes =
                    await Http.GetFromJsonAsync<List<DocumentOutlineNodeDto>>(
                        $"api/documents/{DocumentId}/outline/nodes");
                if (nodes is not null)
                {
                    _outlineNodes.AddRange(nodes.OrderBy(node => node.ParentId).ThenBy(node => node.Order));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Outline load failed.");
                _outlineStatus = "Failed to load outline.";
            }
        }

        private IEnumerable<DocumentOutlineNodeDto> GetOutlineChildren(Guid? parentId)
        {
            return _outlineNodes
                .Where(node => node.ParentId == parentId)
                .OrderBy(node => node.Order);
        }

        private RenderFragment RenderOutlineNode(DocumentOutlineNodeDto node, int depth) => builder =>
        {
            int seq = 0;
            bool hasChildren = _outlineNodes.Any(child => child.ParentId == node.Id);
            bool isCollapsed = _outlineCollapsed.Contains(node.Id);
            bool isActive = _activeOutlineNodeId == node.Id;
            bool isLinkEditorOpen = _outlineLinkPickerOpenId == node.Id;

            builder.OpenElement(seq++, "li");
            builder.AddAttribute(seq++, "class", isActive ? "outline-tree-node outline-node outline-node--active" : "outline-tree-node outline-node");
            builder.AddAttribute(seq++, "ondragover", EventCallback.Factory.Create<DragEventArgs>(this, OnOutlineDragOver));
            builder.AddAttribute(seq++, "ondragover:preventDefault", true);
            builder.AddAttribute(seq++, "ondrop", EventCallback.Factory.Create<DragEventArgs>(this, () => OnOutlineDrop(node.Id)));

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "outline-node-row outline-node__row");
            builder.AddAttribute(seq++, "style", $"--outline-depth: {depth};");
            builder.AddAttribute(seq++, "tabindex", "0");
            builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => SetActiveOutlineNode(node.Id)));
            builder.AddAttribute(seq++, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, args => OnOutlineRowKeyDown(args, node.Id)));

            builder.OpenElement(seq++, "span");
            builder.AddAttribute(seq++, "class", "outline-node-indent");
            builder.CloseElement();

            if (hasChildren)
            {
            builder.OpenElement(seq++, "button");
            builder.AddAttribute(seq++, "type", "button");
            builder.AddAttribute(seq++, "class", "outline-node-toggle");
            builder.AddAttribute(seq++, "title", isCollapsed ? "Expand" : "Collapse");
            builder.AddAttribute(seq++, "aria-label", isCollapsed ? "Expand node" : "Collapse node");
            builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => ToggleOutlineNode(node.Id)));
            builder.AddAttribute(seq++, "onclick:stopPropagation", true);
            builder.AddContent(seq++, isCollapsed ? "+" : "-");
            builder.CloseElement();
            }
            else
            {
                builder.OpenElement(seq++, "span");
                builder.AddAttribute(seq++, "class", "outline-node-spacer");
                builder.AddContent(seq++, " ");
                builder.CloseElement();
            }

            builder.OpenElement(seq++, "button");
            builder.AddAttribute(seq++, "type", "button");
            builder.AddAttribute(seq++, "class", "outline-drag-handle outline-node__handle btn-icon");
            builder.AddAttribute(seq++, "draggable", "true");
            builder.AddAttribute(seq++, "ondragstart", EventCallback.Factory.Create<DragEventArgs>(this, () => OnOutlineDragStart(node.Id)));
            builder.AddAttribute(seq++, "title", "Drag to reorder");
            builder.AddAttribute(seq++, "aria-label", "Drag to reorder");
            builder.AddAttribute(seq++, "onclick:stopPropagation", true);
            builder.AddMarkupContent(seq++, "<svg viewBox=\"0 0 24 24\" width=\"14\" height=\"14\" aria-hidden=\"true\" fill=\"currentColor\"><circle cx=\"9\" cy=\"6\" r=\"1.5\"/><circle cx=\"15\" cy=\"6\" r=\"1.5\"/><circle cx=\"9\" cy=\"12\" r=\"1.5\"/><circle cx=\"15\" cy=\"12\" r=\"1.5\"/><circle cx=\"9\" cy=\"18\" r=\"1.5\"/><circle cx=\"15\" cy=\"18\" r=\"1.5\"/></svg>");
            builder.CloseElement();

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "outline-node-main outline-node__content");

            if (_outlineRenameId == node.Id)
            {
                builder.OpenElement(seq++, "input");
                builder.AddAttribute(seq++, "class", "outline-rename-input");
                builder.AddAttribute(seq++, "value", _outlineRenameDraft);
                builder.AddAttribute(seq++, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, OnOutlineRenameInput));
                builder.AddAttribute(seq++, "onblur", EventCallback.Factory.Create(this, () => CommitRenameNodeAsync(node.Id)));
                builder.AddAttribute(seq++, "autofocus", "autofocus");
                builder.CloseElement();

                if (!string.IsNullOrWhiteSpace(_outlineRenameError))
                {
                    builder.OpenElement(seq++, "span");
                    builder.AddAttribute(seq++, "class", "outline-rename-error");
                    builder.AddContent(seq++, _outlineRenameError);
                    builder.CloseElement();
                }
            }
            else
            {
                builder.OpenElement(seq++, "button");
                builder.AddAttribute(seq++, "type", "button");
                builder.AddAttribute(seq++, "class", node.LinkedSectionId.HasValue ? "outline-node-title outline-node__title is-link" : "outline-node-title outline-node__title");
                builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => OnOutlineNodeClicked(node)));
                builder.AddContent(seq++, node.Title);
                builder.CloseElement();
            }

            if (EnableOutlineSectionLinking)
            {
                builder.OpenElement(seq++, "span");
                builder.AddAttribute(seq++, "class", "outline-node__badge");
                builder.OpenElement(seq++, "button");
                builder.AddAttribute(seq++, "type", "button");
                builder.AddAttribute(seq++, "class", node.LinkedSectionId.HasValue ? "outline-badge outline-badge--linked outline-badge-button" : "outline-badge outline-badge--outlineonly outline-badge-button");
                if (node.LinkedSectionId.HasValue)
                {
                    string? linkedTitle = GetLinkedSectionTitle(node.LinkedSectionId.Value);
                    builder.AddAttribute(seq++, "title", string.IsNullOrWhiteSpace(linkedTitle)
                        ? "Connected to a section. Click title to navigate."
                        : $"Connected to section: {linkedTitle}. Click title to navigate.");
                }
                else
                {
                    builder.AddAttribute(seq++, "title", "This outline item isn't connected to a manuscript section yet.");
                }
                builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => ToggleLinkPicker(node.Id)));
                builder.AddAttribute(seq++, "onclick:stopPropagation", true);
                builder.AddContent(seq++, node.LinkedSectionId.HasValue ? "Linked" : "Outline-only");
                builder.CloseElement();
                if (node.LinkedSectionId.HasValue)
                {
                    string? linkedTitle = GetLinkedSectionTitle(node.LinkedSectionId.Value);
                    if (!string.IsNullOrWhiteSpace(linkedTitle))
                    {
                        builder.OpenElement(seq++, "span");
                        builder.AddAttribute(seq++, "class", "outline-badge-detail");
                        builder.AddContent(seq++, linkedTitle);
                        builder.CloseElement();
                    }
                }
                builder.CloseElement();
            }

            builder.CloseElement();

            builder.CloseElement();

            if (isLinkEditorOpen)
            {
                string linkSelectId = $"outline-link-{node.Id}";
                builder.OpenElement(seq++, "div");
                builder.AddAttribute(seq++, "class", "outline-link-popover outline-node__linkpanel");

                builder.OpenElement(seq++, "label");
                builder.AddAttribute(seq++, "class", "visually-hidden");
                builder.AddAttribute(seq++, "for", linkSelectId);
                builder.AddContent(seq++, "Connect to section");
                builder.CloseElement();

                builder.OpenElement(seq++, "select");
                builder.AddAttribute(seq++, "id", linkSelectId);
                builder.AddAttribute(seq++, "class", "outline-link-select outline-node__linkselect");
                builder.AddAttribute(seq++, "title", "Connect to a section");
                builder.AddAttribute(seq++, "value", GetLinkedSectionValue(node));
                builder.AddAttribute(seq++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, args => OnOutlineLinkSelected(node.Id, args)));
                builder.OpenElement(seq++, "option");
                builder.AddAttribute(seq++, "value", string.Empty);
                builder.AddContent(seq++, "Outline-only");
                builder.CloseElement();
                foreach (SectionDto section in _sections)
                {
                    builder.OpenElement(seq++, "option");
                    builder.AddAttribute(seq++, "value", section.Id.ToString());
                    builder.AddContent(seq++, section.Title);
                    builder.CloseElement();
                }
                builder.CloseElement();

                if (node.LinkedSectionId.HasValue)
                {
                    builder.OpenElement(seq++, "button");
                    builder.AddAttribute(seq++, "type", "button");
                    builder.AddAttribute(seq++, "class", "outline-link-unlink outline-btn outline-btn--ghost");
                    builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => UnlinkOutlineNodeAsync(node.Id)));
                    builder.AddContent(seq++, "Unlink");
                    builder.CloseElement();
                }

                if (!string.IsNullOrWhiteSpace(_outlineLinkPickerError))
                {
                    builder.OpenElement(seq++, "div");
                    builder.AddAttribute(seq++, "class", "outline-link-error");
                    builder.AddContent(seq++, _outlineLinkPickerError);
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            if (hasChildren && !isCollapsed)
            {
                builder.OpenElement(seq++, "ul");
                builder.AddAttribute(seq++, "class", "outline-tree-children");
                foreach (DocumentOutlineNodeDto child in GetOutlineChildren(node.Id))
                {
                    builder.AddContent(seq++, RenderOutlineNode(child, depth + 1));
                }
                builder.CloseElement();
            }

            builder.CloseElement();
        };

        private void SetActiveOutlineNode(Guid nodeId)
        {
            _activeOutlineNodeId = nodeId;
        }

        private void ToggleLinkPicker(Guid nodeId)
        {
            _outlineLinkPickerOpenId = _outlineLinkPickerOpenId == nodeId ? null : nodeId;
            _outlineLinkPickerError = null;
        }

        private void CloseLinkPicker(Guid nodeId)
        {
            if (_outlineLinkPickerOpenId == nodeId)
            {
                _outlineLinkPickerOpenId = null;
                _outlineLinkPickerError = null;
            }
        }

        private void OnOutlineRowKeyDown(KeyboardEventArgs args, Guid nodeId)
        {
            if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            {
                CloseLinkPicker(nodeId);
            }
        }

        private string? GetLinkedSectionTitle(Guid sectionId)
        {
            SectionDto? section = _sections.FirstOrDefault(item => item.Id == sectionId);
            return section?.Title;
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

        private string GetLinkedSectionValue(DocumentOutlineNodeDto node)
        {
            return node.LinkedSectionId?.ToString() ?? string.Empty;
        }

        private bool IsSectionLinkedElsewhere(Guid sectionId, Guid currentNodeId)
        {
            return _outlineNodes.Any(node => node.Id != currentNodeId && node.LinkedSectionId == sectionId);
        }

        private async Task OnOutlineLinkSelected(Guid nodeId, ChangeEventArgs args)
        {
            _outlineLinkPickerError = null;
            string raw = args.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out Guid selected))
            {
                if (IsSectionLinkedElsewhere(selected, nodeId))
                {
                    _outlineLinkPickerError = "That section is already linked to another outline item.";
                    return;
                }
            }

            await OnOutlineLinkChanged(nodeId, args);
        }

        private async Task UnlinkOutlineNodeAsync(Guid nodeId)
        {
            ChangeEventArgs args = new() { Value = string.Empty };
            await OnOutlineLinkChanged(nodeId, args);
        }

        private void ToggleOutlineNode(Guid nodeId)
        {
            if (_outlineCollapsed.Contains(nodeId))
            {
                _outlineCollapsed.Remove(nodeId);
            }
            else
            {
                _outlineCollapsed.Add(nodeId);
            }
        }

        private Task OnOutlineDragOver(DragEventArgs args)
        {
            return Task.CompletedTask;
        }

        private void OnOutlineDragStart(Guid nodeId)
        {
            _outlineDragId = nodeId;
        }

        private async Task OnOutlineDrop(Guid targetNodeId)
        {
            if (_outlineDragId is null || _outlineDragId == targetNodeId)
            {
                return;
            }

            DocumentOutlineNodeDto? dragged = _outlineNodes.FirstOrDefault(node => node.Id == _outlineDragId.Value);
            DocumentOutlineNodeDto? target = _outlineNodes.FirstOrDefault(node => node.Id == targetNodeId);
            if (dragged is null || target is null || dragged.ParentId != target.ParentId)
            {
                _outlineDragId = null;
                return;
            }

            List<DocumentOutlineNodeDto> siblings = GetOutlineChildren(dragged.ParentId).ToList();
            int fromIndex = siblings.FindIndex(node => node.Id == dragged.Id);
            int toIndex = siblings.FindIndex(node => node.Id == target.Id);
            if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
            {
                _outlineDragId = null;
                return;
            }

            DocumentOutlineNodeDto moving = siblings[fromIndex];
            siblings.RemoveAt(fromIndex);
            siblings.Insert(toIndex, moving);
            for (int index = 0; index < siblings.Count; index++)
            {
                DocumentOutlineNodeDto current = siblings[index];
                UpdateNode(current with { Order = index });
            }

            _outlineDragId = null;
            await SaveOutlineNodesAsync("Outline order saved.");
        }

        private async Task AddRootNodeAsync()
        {
            await InsertNodeAsync(null, GetOutlineChildren(null).Count());
        }

        private async Task AddSiblingNodeAsync(Guid nodeId)
        {
            DocumentOutlineNodeDto? node = _outlineNodes.FirstOrDefault(item => item.Id == nodeId);
            if (node is null)
            {
                return;
            }

            List<DocumentOutlineNodeDto> siblings = GetOutlineChildren(node.ParentId).ToList();
            int index = siblings.FindIndex(entry => entry.Id == nodeId);
            int insertIndex = Math.Max(0, index + 1);
            await InsertNodeAsync(node.ParentId, insertIndex);
        }

        private async Task AddChildNodeAsync(Guid nodeId)
        {
            await InsertNodeAsync(nodeId, GetOutlineChildren(nodeId).Count());
            _outlineCollapsed.Remove(nodeId);
        }

        private async Task InsertNodeAsync(Guid? parentId, int insertIndex)
        {
            List<DocumentOutlineNodeDto> siblings = GetOutlineChildren(parentId).ToList();
            for (int index = 0; index < siblings.Count; index++)
            {
                DocumentOutlineNodeDto sibling = siblings[index];
                int nextOrder = index >= insertIndex ? index + 1 : index;
                if (sibling.Order != nextOrder)
                {
                    UpdateNode(sibling with { Order = nextOrder });
                }
            }

            DocumentOutlineNodeDto created = new(
                Guid.NewGuid(),
                DocumentId,
                parentId,
                insertIndex,
                "New node",
                null,
                null);
            _outlineNodes.Add(created);
            _activeOutlineNodeId = created.Id;
            _outlineRenameId = created.Id;
            _outlineRenameDraft = created.Title;
            _outlineRenameError = null;
            await SaveOutlineNodesAsync("Outline updated.");
        }

        private async Task DeleteNodeAsync(Guid nodeId)
        {
            HashSet<Guid> toRemove = CollectDescendants(nodeId);
            if (toRemove.Count == 0)
            {
                return;
            }

            DocumentOutlineNodeDto? removedNode = _outlineNodes.FirstOrDefault(node => node.Id == nodeId);
            Guid? parentId = removedNode?.ParentId;
            List<DocumentOutlineNodeDto> siblingsBefore = GetOutlineChildren(parentId).ToList();
            int removedIndex = siblingsBefore.FindIndex(node => node.Id == nodeId);
            bool selectionRemoved = _activeOutlineNodeId.HasValue && toRemove.Contains(_activeOutlineNodeId.Value);
            _outlineNodes.RemoveAll(node => toRemove.Contains(node.Id));
            ReorderSiblings(parentId);
            if (selectionRemoved)
            {
                if (parentId.HasValue)
                {
                    _activeOutlineNodeId = parentId.Value;
                }
                else
                {
                    List<DocumentOutlineNodeDto> siblingsAfter = GetOutlineChildren(parentId).ToList();
                    if (removedIndex >= 0 && removedIndex < siblingsAfter.Count)
                    {
                        _activeOutlineNodeId = siblingsAfter[removedIndex].Id;
                    }
                    else if (removedIndex > 0 && siblingsAfter.Count > 0)
                    {
                        int previousIndex = Math.Min(removedIndex - 1, siblingsAfter.Count - 1);
                        _activeOutlineNodeId = siblingsAfter[previousIndex].Id;
                    }
                    else
                    {
                        _activeOutlineNodeId = siblingsAfter.FirstOrDefault()?.Id;
                    }
                }
            }
            await SaveOutlineNodesAsync("Outline updated.");
        }

        private HashSet<Guid> CollectDescendants(Guid nodeId)
        {
            HashSet<Guid> ids = new() { nodeId };
            Queue<Guid> queue = new();
            queue.Enqueue(nodeId);
            while (queue.Count > 0)
            {
                Guid current = queue.Dequeue();
                foreach (DocumentOutlineNodeDto child in _outlineNodes.Where(node => node.ParentId == current))
                {
                    if (ids.Add(child.Id))
                    {
                        queue.Enqueue(child.Id);
                    }
                }
            }

            return ids;
        }

        private void ReorderSiblings(Guid? parentId)
        {
            List<DocumentOutlineNodeDto> siblings = GetOutlineChildren(parentId).ToList();
            for (int index = 0; index < siblings.Count; index++)
            {
                UpdateNode(siblings[index] with { Order = index });
            }
        }

        private void StartRenameNode(Guid nodeId)
        {
            DocumentOutlineNodeDto? node = _outlineNodes.FirstOrDefault(item => item.Id == nodeId);
            if (node is null)
            {
                return;
            }

            _activeOutlineNodeId = nodeId;
            _outlineRenameId = nodeId;
            _outlineRenameDraft = node.Title;
            _outlineRenameError = null;
        }

        private void OnOutlineRenameInput(ChangeEventArgs args)
        {
            _outlineRenameDraft = args.Value?.ToString() ?? string.Empty;
        }

        private async Task CommitRenameNodeAsync(Guid nodeId)
        {
            if (_outlineRenameId != nodeId)
            {
                return;
            }

            string trimmed = _outlineRenameDraft.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _outlineRenameError = "Title is required.";
                return;
            }

            DocumentOutlineNodeDto? node = _outlineNodes.FirstOrDefault(item => item.Id == nodeId);
            if (node is null)
            {
                return;
            }

            UpdateNode(node with { Title = trimmed });
            _outlineRenameId = null;
            _outlineRenameDraft = string.Empty;
            _outlineRenameError = null;
            await SaveOutlineNodesAsync("Outline updated.");
        }

        private async Task OnOutlineLinkChanged(Guid nodeId, ChangeEventArgs args)
        {
            Guid? sectionId = null;
            string raw = args.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out Guid parsed))
            {
                sectionId = parsed;
            }

            try
            {
                DocumentOutlineLinkRequest payload = new(sectionId);
                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync(
                        $"api/documents/{DocumentId}/outline/nodes/{nodeId}/link-section",
                        payload);
                if (!response.IsSuccessStatusCode)
                {
                    _outlineStatus = "Failed to link section.";
                    return;
                }

                DocumentOutlineNodeDto? updated =
                    await response.Content.ReadFromJsonAsync<DocumentOutlineNodeDto>();
                if (updated is null)
                {
                    return;
                }

                UpdateNode(updated);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Outline link failed.");
                _outlineStatus = "Failed to link section.";
            }
        }

        private async Task OnOutlineNodeClicked(DocumentOutlineNodeDto node)
        {
            _activeOutlineNodeId = node.Id;
            if (node.LinkedSectionId.HasValue)
            {
                await OnSectionSelected(node.LinkedSectionId.Value);
            }
        }

        private void UpdateNode(DocumentOutlineNodeDto updated)
        {
            int index = _outlineNodes.FindIndex(node => node.Id == updated.Id);
            if (index >= 0)
            {
                _outlineNodes[index] = updated;
            }
            else
            {
                _outlineNodes.Add(updated);
            }
        }

        private async Task SaveOutlineNodesAsync(string statusMessage)
        {
            try
            {
                using HttpResponseMessage response =
                    await Http.PutAsJsonAsync(
                        $"api/documents/{DocumentId}/outline/nodes",
                        _outlineNodes);
                if (!response.IsSuccessStatusCode)
                {
                    _outlineStatus = "Outline save failed.";
                    return;
                }

                List<DocumentOutlineNodeDto>? updated =
                    await response.Content.ReadFromJsonAsync<List<DocumentOutlineNodeDto>>();
                if (updated is not null)
                {
                    _outlineNodes.Clear();
                    _outlineNodes.AddRange(updated.OrderBy(node => node.ParentId).ThenBy(node => node.Order));
                }

                _outlineStatus = statusMessage;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Outline save failed.");
                _outlineStatus = "Outline save failed.";
            }
        }

        private async Task GenerateOutlineAsync()
        {
            if (_outlineGenerateInFlight)
            {
                return;
            }

            _outlineGenerateInFlight = true;
            _outlineGenerateError = null;
            try
            {
                AiActionExecuteRequestDto payload = new(
                    DocumentId,
                    _activeSection?.Id ?? SectionId,
                    _activePage?.Id,
                    null,
                    null,
                    null,
                    null,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["instruction"] = "Generate a hierarchical outline as JSON."
                    });

                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync("api/ai/actions/generate.outline/execute", payload);
                if (!response.IsSuccessStatusCode)
                {
                    _outlineGenerateError = "Outline generation failed.";
                    return;
                }

                AiActionExecuteResponseDto? result =
                    await response.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                if (result?.OutlineNodes is null || result.OutlineNodes.Count == 0)
                {
                    _outlineGenerateError = "Outline generation returned no nodes.";
                    return;
                }

                _outlineProposalNodes = result.OutlineNodes;
                _outlineProposalPreview = result.PreviewText ?? string.Empty;
                _outlineProposalTruncated = result.WasTruncated == true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Outline generation failed.");
                _outlineGenerateError = "Outline generation failed.";
            }
            finally
            {
                _outlineGenerateInFlight = false;
            }
        }

        private async Task ApplyOutlineProposalAsync()
        {
            if (_outlineProposalNodes is null || _outlineProposalNodes.Count == 0)
            {
                return;
            }

            _outlineNodes.Clear();
            _outlineNodes.AddRange(_outlineProposalNodes);
            _outlineCollapsed.Clear();
            await SaveOutlineNodesAsync("Outline applied.");
            DiscardOutlineProposal();
        }

        private void DiscardOutlineProposal()
        {
            _outlineProposalNodes = null;
            _outlineProposalPreview = null;
            _outlineProposalTruncated = false;
            _outlineGenerateError = null;
        }

        private async Task ApplyOutlineToSectionsAsync()
        {
            if (_outlineApplyInFlight)
            {
                return;
            }

            _outlineApplyInFlight = true;
            _outlineApplyError = null;
            try
            {
                OutlineApplyOptionsDto payload = new(
                    _outlineApplyCreateMissing,
                    _outlineApplyReorder,
                    _outlineApplyRename,
                    _outlineApplyLinkNodes,
                    MatchByTitle: true,
                    MaxDepth: 1);

                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/documents/{DocumentId}/outline/apply-to-sections", payload);
                if (!response.IsSuccessStatusCode)
                {
                    _outlineApplyError = "Failed to apply outline.";
                    return;
                }

                OutlineApplyResultDto? result =
                    await response.Content.ReadFromJsonAsync<OutlineApplyResultDto>();
                if (result is null)
                {
                    _outlineApplyError = "Failed to apply outline.";
                    return;
                }

                _sections.Clear();
                _sections.AddRange(result.Sections.OrderBy(section => section.OrderIndex));
                _outlineNodes.Clear();
                _outlineNodes.AddRange(result.Nodes.OrderBy(node => node.ParentId).ThenBy(node => node.Order));

                if (_activeSection is not null)
                {
                    SectionDto? match = _sections.FirstOrDefault(section => section.Id == _activeSection.Id);
                    if (match is not null)
                    {
                        _activeSection = match;
                    }
                }

                _outlineStatus = "Outline applied to sections.";
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Apply outline to sections failed.");
                _outlineApplyError = "Failed to apply outline.";
            }
            finally
            {
                _outlineApplyInFlight = false;
            }
        }

        private string GetOutlineTextForAi()
        {
            if (_outlineNodes.Count == 0)
            {
                return string.Empty;
            }

            Dictionary<Guid, List<DocumentOutlineNodeDto>> byParent = _outlineNodes
                .GroupBy(node => node.ParentId ?? Guid.Empty)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(node => node.Order).ToList());

            List<string> lines = new();
            void Walk(Guid parentId, int depth)
            {
                if (!byParent.TryGetValue(parentId, out List<DocumentOutlineNodeDto>? children))
                {
                    return;
                }

                foreach (DocumentOutlineNodeDto child in children)
                {
                    string indent = new string(' ', depth * 2);
                    lines.Add($"{indent}- {child.Title}");
                    Walk(child.Id, depth + 1);
                }
            }

            Walk(Guid.Empty, 0);
            return string.Join(Environment.NewLine, lines);
        }
        private async Task OnExportRequested(string kind, string format)
        {
            _isDocumentMenuOpen = false;
            try
            {
                string templateQuery = string.Empty;
                if (string.Equals(kind, "document", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(format, "html", StringComparison.OrdinalIgnoreCase)
                    && _selectedTemplateId.HasValue)
                {
                    templateQuery = $"&templateId={_selectedTemplateId.Value}";
                }

                using HttpResponseMessage response = await Http.GetAsync(
                    $"api/documents/{DocumentId}/export?kind={kind}&format={format}{templateQuery}");

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
                string templateQuery = _selectedTemplateId.HasValue
                    ? $"&templateId={_selectedTemplateId.Value}"
                    : string.Empty;
                ExportPrintPayload? payload = await Http.GetFromJsonAsync<ExportPrintPayload>(
                    $"api/documents/{DocumentId}/export/print?kind=document{templateQuery}");
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

        private async Task OpenPreviewAsync()
        {
            _previewError = null;
            _isPreviewLoading = true;
            _isExportPreviewOpen = true;
            try
            {
                ExportTemplateDto? template = GetSelectedTemplate();
                ExportPreviewRequest request = new(
                    DocumentId,
                    _selectedTemplateId,
                    template?.TocEnabled ?? true,
                    _previewScope,
                    _previewScope == "section" ? _activeSection?.Id : null);

                using HttpResponseMessage response = await Http.PostAsJsonAsync("api/export/preview", request);
                if (!response.IsSuccessStatusCode)
                {
                    _previewError = "Preview failed.";
                    return;
                }

                ExportPreviewResponse? payload = await response.Content.ReadFromJsonAsync<ExportPreviewResponse>();
                if (payload is null || string.IsNullOrWhiteSpace(payload.Html))
                {
                    _previewError = "Preview failed.";
                    return;
                }

                _previewHtml = payload.Html;
                _previewZoom = 1.0;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Export preview failed.");
                _previewError = "Preview failed.";
            }
            finally
            {
                _isPreviewLoading = false;
            }
        }

        private void ClosePreview()
        {
            _isExportPreviewOpen = false;
            _previewHtml = string.Empty;
            _previewError = null;
        }

        private ExportTemplateDto? GetSelectedTemplate()
        {
            return _exportTemplates.FirstOrDefault(template => template.Id == _selectedTemplateId);
        }

        private string GetPreviewStyle()
        {
            ExportTemplateDto? template = GetSelectedTemplate();
            int width = template?.PageWidthMm ?? 210;
            return $"--preview-page-width:{width}mm; --preview-zoom:{_previewZoom.ToString(CultureInfo.InvariantCulture)};";
        }

        private async Task PrintPreviewAsync()
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            await _exportModule.InvokeVoidAsync("printIframe", "export-preview-frame");
        }

        private void SetPreviewZoom(double zoom)
        {
            _previewZoom = zoom;
        }

        private async Task OnExportDialogOpenAsync()
        {
            _isDocumentMenuOpen = false;
            _isExportDialogOpen = true;
            _templateActionError = null;
            await EnsureTemplatesLoadedAsync();
        }

        private void CloseExportDialog()
        {
            _isExportDialogOpen = false;
            _templateActionError = null;
        }

        private async Task OpenTemplateManagerAsync()
        {
            _isExportDialogOpen = false;
            _isTemplateManagerOpen = true;
            _templateActionError = null;
            await EnsureTemplatesLoadedAsync();
        }

        private void CloseTemplateManager()
        {
            _isTemplateManagerOpen = false;
            _isTemplateEditorOpen = false;
            _editingTemplateId = null;
            _templateEditor = null;
            _templateActionError = null;
        }

        private async Task ExecuteExportSelectionAsync()
        {
            if (string.Equals(_exportFormatSelection, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                await OnExportPdfRequested();
                _isExportDialogOpen = false;
                return;
            }

            string format = string.Equals(_exportFormatSelection, "markdown", StringComparison.OrdinalIgnoreCase)
                ? "markdown"
                : "html";

            await OnExportRequested("document", format);
            _isExportDialogOpen = false;
        }

        private async Task EnsureTemplatesLoadedAsync()
        {
            if (_isTemplatesLoading)
            {
                return;
            }

            if (_exportTemplates.Count > 0)
            {
                return;
            }

            await LoadExportTemplatesAsync();
        }

        private async Task LoadExportTemplatesAsync()
        {
            _isTemplatesLoading = true;
            _templateLoadError = null;
            try
            {
                List<ExportTemplateDto>? templates = await Http.GetFromJsonAsync<List<ExportTemplateDto>>(
                    "api/export/templates");
                _exportTemplates.Clear();
                if (templates is not null)
                {
                    _exportTemplates.AddRange(templates.OrderBy(template => template.Name));
                }

                if (_selectedTemplateId is null && _exportTemplates.Count > 0)
                {
                    ExportTemplateDto? manuscript = _exportTemplates
                        .FirstOrDefault(template => string.Equals(template.PresetKey, "manuscript", StringComparison.OrdinalIgnoreCase));
                    _selectedTemplateId = manuscript?.Id ?? _exportTemplates[0].Id;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load export templates.");
                _templateLoadError = "Unable to load templates.";
            }
            finally
            {
                _isTemplatesLoading = false;
            }
        }

        private string SelectedTemplateIdValue
        {
            get => _selectedTemplateId?.ToString() ?? string.Empty;
            set => _selectedTemplateId = Guid.TryParse(value, out Guid parsed) ? parsed : null;
        }

        private async Task CreateTemplateFromPresetAsync()
        {
            _templateActionError = null;
            ExportTemplateCreateRequest request = BuildCreateRequestFromPreset(_createPresetKey);
            try
            {
                using HttpResponseMessage response = await Http.PostAsJsonAsync("api/export/templates", request);
                if (!response.IsSuccessStatusCode)
                {
                    _templateActionError = "Failed to create template.";
                    return;
                }

                ExportTemplateDto? created = await response.Content.ReadFromJsonAsync<ExportTemplateDto>();
                if (created is not null)
                {
                    _exportTemplates.Add(created);
                    _selectedTemplateId = created.Id;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to create export template.");
                _templateActionError = "Failed to create template.";
            }
        }

        private void StartEditTemplate(ExportTemplateDto template)
        {
            _templateActionError = null;
            _editingTemplateId = template.Id;
            _templateEditor = ExportTemplateEditorModel.FromDto(template);
            _templateEditorPagePreset = GuessPagePreset(_templateEditor.PageWidthMm, _templateEditor.PageHeightMm);
            _isTemplateEditorOpen = true;
        }

        private void CancelTemplateEdit()
        {
            _isTemplateEditorOpen = false;
            _editingTemplateId = null;
            _templateEditor = null;
        }

        private async Task SaveTemplateAsync()
        {
            if (_templateEditor is null || _editingTemplateId is null)
            {
                return;
            }

            _isTemplateSaving = true;
            _templateActionError = null;
            try
            {
                ExportTemplateUpdateRequest request = _templateEditor.ToUpdateRequest();
                using HttpResponseMessage response = await Http.PutAsJsonAsync(
                    $"api/export/templates/{_editingTemplateId}", request);
                if (!response.IsSuccessStatusCode)
                {
                    _templateActionError = "Failed to save template.";
                    return;
                }

                ExportTemplateDto? updated = await response.Content.ReadFromJsonAsync<ExportTemplateDto>();
                if (updated is not null)
                {
                    int index = _exportTemplates.FindIndex(item => item.Id == updated.Id);
                    if (index >= 0)
                    {
                        _exportTemplates[index] = updated;
                    }
                }

                _isTemplateEditorOpen = false;
                _editingTemplateId = null;
                _templateEditor = null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to save export template.");
                _templateActionError = "Failed to save template.";
            }
            finally
            {
                _isTemplateSaving = false;
            }
        }

        private async Task DuplicateTemplateAsync(ExportTemplateDto template)
        {
            _templateActionError = null;
            string copyName = BuildCopyName(template.Name, _exportTemplates.Select(item => item.Name));
            ExportTemplateCreateRequest request = new(
                copyName,
                null,
                template.PageWidthMm,
                template.PageHeightMm,
                template.MarginTopMm,
                template.MarginRightMm,
                template.MarginBottomMm,
                template.MarginLeftMm,
                template.FontFamily,
                template.BodyFontSizePt,
                template.LineHeight,
                template.ParagraphSpacingPt,
                template.HeaderEnabled,
                template.HeaderLeft,
                template.HeaderCenter,
                template.HeaderRight,
                template.FooterEnabled,
                template.FooterLeft,
                template.FooterCenter,
                template.FooterRight,
                template.PageNumbersEnabled,
                template.PageNumberStart,
                template.TocEnabled,
                template.TocDepth);

            try
            {
                using HttpResponseMessage response = await Http.PostAsJsonAsync("api/export/templates", request);
                if (!response.IsSuccessStatusCode)
                {
                    _templateActionError = "Failed to duplicate template.";
                    return;
                }

                ExportTemplateDto? created = await response.Content.ReadFromJsonAsync<ExportTemplateDto>();
                if (created is not null)
                {
                    _exportTemplates.Add(created);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to duplicate export template.");
                _templateActionError = "Failed to duplicate template.";
            }
        }

        private async Task DeleteTemplateAsync(ExportTemplateDto template)
        {
            _isTemplateDeleting = true;
            _templateActionError = null;
            try
            {
                using HttpResponseMessage response = await Http.DeleteAsync($"api/export/templates/{template.Id}");
                if (!response.IsSuccessStatusCode)
                {
                    _templateActionError = "Failed to delete template.";
                    return;
                }

                _exportTemplates.RemoveAll(item => item.Id == template.Id);
                if (_selectedTemplateId == template.Id)
                {
                    _selectedTemplateId = _exportTemplates.FirstOrDefault()?.Id;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to delete export template.");
                _templateActionError = "Failed to delete template.";
            }
            finally
            {
                _isTemplateDeleting = false;
            }
        }

        private void ApplyPagePreset(string preset)
        {
            if (_templateEditor is null)
            {
                return;
            }

            switch (preset)
            {
                case "paperback_6x9":
                    _templateEditor.PageWidthMm = 152;
                    _templateEditor.PageHeightMm = 229;
                    break;
                case "a4":
                    _templateEditor.PageWidthMm = 210;
                    _templateEditor.PageHeightMm = 297;
                    break;
                case "manuscript":
                    _templateEditor.PageWidthMm = 216;
                    _templateEditor.PageHeightMm = 279;
                    break;
            }
        }

        private void OnTemplatePresetChanged(ChangeEventArgs args)
        {
            _templateEditorPagePreset = args.Value?.ToString() ?? "custom";
            ApplyPagePreset(_templateEditorPagePreset);
        }

        private static string GuessPagePreset(int width, int height)
        {
            if (width == 152 && height == 229)
            {
                return "paperback_6x9";
            }

            if (width == 210 && height == 297)
            {
                return "a4";
            }

            if (width == 216 && height == 279)
            {
                return "manuscript";
            }

            return "custom";
        }

        private static ExportTemplateCreateRequest BuildCreateRequestFromPreset(string presetKey)
        {
            ExportTemplatePresetDefinition? preset = ExportTemplatePresets.GetByKey(presetKey);
            preset ??= ExportTemplatePresets.GetByKey("manuscript");
            if (preset is null)
            {
                throw new InvalidOperationException("Export template presets are missing.");
            }

            return new ExportTemplateCreateRequest(
                preset.Name,
                preset.Key,
                preset.PageWidthMm,
                preset.PageHeightMm,
                preset.MarginTopMm,
                preset.MarginRightMm,
                preset.MarginBottomMm,
                preset.MarginLeftMm,
                preset.FontFamily,
                preset.BodyFontSizePt,
                preset.LineHeight,
                preset.ParagraphSpacingPt,
                preset.HeaderEnabled,
                preset.HeaderLeft,
                preset.HeaderCenter,
                preset.HeaderRight,
                preset.FooterEnabled,
                preset.FooterLeft,
                preset.FooterCenter,
                preset.FooterRight,
                preset.PageNumbersEnabled,
                preset.PageNumberStart,
                preset.TocEnabled,
                preset.TocDepth);
        }

        private static string BuildCopyName(string baseName, IEnumerable<string> existingNames)
        {
            string trimmed = string.IsNullOrWhiteSpace(baseName) ? "Template" : baseName.Trim();
            string candidate = $"{trimmed} (copy)";
            HashSet<string> names = new(existingNames.Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(candidate))
            {
                return candidate;
            }

            int counter = 2;
            while (names.Contains($"{trimmed} (copy {counter})"))
            {
                counter++;
            }

            return $"{trimmed} (copy {counter})";
        }

        private static string GetPresetTag(string? presetKey)
        {
            return presetKey switch
            {
                "manuscript" => "(Manuscript)",
                "paperback_6x9" => "(Paperback)",
                "a4" => "(A4)",
                _ => "(Custom)"
            };
        }

        private static IReadOnlyList<ExportTemplatePresetDefinition> PresetOptions => ExportTemplatePresets.All;

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
                    $"{Navigation.BaseUri}js/export.js");
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

            if (_activeSection is null)
            {
                return;
            }

            string? html = _pageEditor?.GetContent();
            string plain = PlainTextMapper.ToPlainText(html ?? string.Empty);
            TextRange selectionRange = new(0, 0);
            string selection = string.Empty;

            if (action.RequiresSelection)
            {
                if (_currentSelectionRange is null)
                {
                    return;
                }

                selectionRange = NormalizeRange(_currentSelectionRange, plain.Length);
                selection = ExtractRangeText(plain, selectionRange);
                if (string.IsNullOrWhiteSpace(selection))
                {
                    return;
                }
            }

            Dictionary<string, object?> parameters = new(action.Parameters)
            {
                ["instruction"] = action.Instruction
            };

            int? selectionStart = action.RequiresSelection ? selectionRange.Start : null;
            int? selectionEnd = action.RequiresSelection ? selectionRange.Start + selectionRange.Length : null;
            string? originalText = action.RequiresSelection ? selection : null;

            AiActionExecuteRequestDto request = new(
                DocumentId,
                _activeSection.Id,
                _activePage?.Id,
                selectionStart,
                selectionEnd,
                originalText,
                plain,
                GetOutlineTextForAi(),
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

            string? proposedText = response.ProposedText;
            if (string.Equals(action.ActionKey, "propose.next-paragraph", StringComparison.OrdinalIgnoreCase))
            {
                proposedText = NormalizeSingleParagraph(proposedText ?? string.Empty);
            }

            _pendingAiProposal = new PendingAiProposal(
                response.ProposalId,
                action.ActionKey,
                action.Instruction,
                response.OriginalText,
                proposedText,
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
                string.Empty,
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
            bool appendParagraph = string.Equals(
                pending.ActionKey,
                "propose.next-paragraph",
                StringComparison.OrdinalIgnoreCase);
            string proposedText = appendParagraph
                ? NormalizeSingleParagraph(pending.ProposedText)
                : pending.ProposedText;

            if (appendParagraph)
            {
                await InvokePageCommandAsync("appendParagraph", proposedText);
            }
            else
            {
                await InvokePageCommandAsync("replaceSelection", proposedText);
            }
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

            if (string.Equals(actionKey, "scene.suggest", StringComparison.OrdinalIgnoreCase))
            {
                return "Suggest scene card";
            }

            if (string.Equals(actionKey, "scene.refine", StringComparison.OrdinalIgnoreCase))
            {
                return "Refine scene card";
            }

            if (string.Equals(actionKey, "scene.find-open-questions", StringComparison.OrdinalIgnoreCase))
            {
                return "Find open questions";
            }

            if (string.Equals(actionKey, "propose.next-paragraph", StringComparison.OrdinalIgnoreCase))
            {
                return "Propose next paragraph";
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

        private static string NormalizeSingleParagraph(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = text
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\n', ' ')
                .Replace('\r', ' ');

            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized.Trim();
        }

        private sealed record AiActionOption(
            string ActionKey,
            string Label,
            string Instruction,
            bool RequiresSelection,
            Dictionary<string, object?> Parameters,
            string? Description = null);

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
            string ActionKey,
            string ActionLabel,
            string? OriginalText,
            string? ProposedText,
            string? ChangesSummary,
            string? ErrorMessage,
            DateTimeOffset CreatedUtc);

        private sealed record ExportPrintPayload(string Html);

        private sealed record TextRange(int Start, int Length);

        private sealed class ExportTemplateEditorModel
        {
            public string Name { get; set; } = string.Empty;
            public int PageWidthMm { get; set; }
            public int PageHeightMm { get; set; }
            public int MarginTopMm { get; set; }
            public int MarginRightMm { get; set; }
            public int MarginBottomMm { get; set; }
            public int MarginLeftMm { get; set; }
            public string FontFamily { get; set; } = "Georgia";
            public int BodyFontSizePt { get; set; }
            public decimal LineHeight { get; set; }
            public int ParagraphSpacingPt { get; set; }
            public bool HeaderEnabled { get; set; }
            public string? HeaderLeft { get; set; }
            public string? HeaderCenter { get; set; }
            public string? HeaderRight { get; set; }
            public bool FooterEnabled { get; set; }
            public string? FooterLeft { get; set; }
            public string? FooterCenter { get; set; }
            public string? FooterRight { get; set; }
            public bool PageNumbersEnabled { get; set; }
            public int PageNumberStart { get; set; }
            public bool TocEnabled { get; set; }
            public int TocDepth { get; set; }

            public static ExportTemplateEditorModel FromDto(ExportTemplateDto template)
            {
                return new ExportTemplateEditorModel
                {
                    Name = template.Name,
                    PageWidthMm = template.PageWidthMm,
                    PageHeightMm = template.PageHeightMm,
                    MarginTopMm = template.MarginTopMm,
                    MarginRightMm = template.MarginRightMm,
                    MarginBottomMm = template.MarginBottomMm,
                    MarginLeftMm = template.MarginLeftMm,
                    FontFamily = template.FontFamily,
                    BodyFontSizePt = template.BodyFontSizePt,
                    LineHeight = template.LineHeight,
                    ParagraphSpacingPt = template.ParagraphSpacingPt,
                    HeaderEnabled = template.HeaderEnabled,
                    HeaderLeft = template.HeaderLeft,
                    HeaderCenter = template.HeaderCenter,
                    HeaderRight = template.HeaderRight,
                    FooterEnabled = template.FooterEnabled,
                    FooterLeft = template.FooterLeft,
                    FooterCenter = template.FooterCenter,
                    FooterRight = template.FooterRight,
                    PageNumbersEnabled = template.PageNumbersEnabled,
                    PageNumberStart = template.PageNumberStart,
                    TocEnabled = template.TocEnabled,
                    TocDepth = template.TocDepth
                };
            }

            public ExportTemplateUpdateRequest ToUpdateRequest()
            {
                return new ExportTemplateUpdateRequest(
                    Name?.Trim(),
                    PageWidthMm,
                    PageHeightMm,
                    MarginTopMm,
                    MarginRightMm,
                    MarginBottomMm,
                    MarginLeftMm,
                    FontFamily,
                    BodyFontSizePt,
                    LineHeight,
                    ParagraphSpacingPt,
                    HeaderEnabled,
                    HeaderLeft,
                    HeaderCenter,
                    HeaderRight,
                    FooterEnabled,
                    FooterLeft,
                    FooterCenter,
                    FooterRight,
                    PageNumbersEnabled,
                    PageNumberStart,
                    TocEnabled,
                    TocDepth);
            }
        }

        private enum ContextTab
        {
            Notes,
            Scene,
            Outline,
            Ai
        }
    }
}
