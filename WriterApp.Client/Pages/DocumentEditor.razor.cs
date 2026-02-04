using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
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
using SelectionDocRange = WriterApp.Client.Components.Editor.PageEditor.SelectionDocRange;

namespace WriterApp.Client.Pages
{
    public partial class DocumentEditor : ComponentBase, IDisposable
    {
        [Parameter]
        public Guid DocumentId { get; set; }

        [Parameter]
        public Guid SectionId { get; set; }

        [SupplyParameterFromQuery(Name = "search")]
        public string? SearchQuery { get; set; }

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
        private string? _documentLanguageCode;
        private Guid? _documentTranslationGroupId;
        private string? _sectionLanguageCode;
        private Guid? _sectionTranslationGroupId;
        private readonly List<DocumentTranslationLinkDto> _documentTranslationLinks = new();
        private readonly List<SectionTranslationLinkDto> _sectionTranslationLinks = new();
        private bool _layoutStateInitialized;
        private PageEditor? _pageEditor;
        private string _headingTraceId = string.Empty;
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
        private int[] _headingPrefixCounters = new int[7];
        private string? _templateLoadError;
        private string? _templateActionError;
        private readonly List<ExportTemplateDto> _exportTemplates = new();
        private readonly List<ExportPresetDto> _exportPresets = new();
        private Guid? _selectedTemplateId;
        private Guid? _selectedExportPresetId;
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
        private bool _previewShowPageBreaks = true;
        private bool _previewSidebarOpen = true;
        private bool _previewInitialized;
        private bool _previewFrameLoaded;
        private bool _previewHasFrontMatter;
        private int _previewPageCount = 1;
        private int _previewCurrentPage = 1;
        private string _previewSearchTerm = string.Empty;
        private DotNetObjectReference<DocumentEditor>? _previewScrollRef;
        private string _exportScopeType = "document";
        private bool _exportIncludeTitlePage = true;
        private bool _exportIncludeToc = true;
        private int _exportTocDepth = 2;
        private readonly HashSet<string> _exportChapterBreakRules = new(StringComparer.OrdinalIgnoreCase);
        private string _titlePageTitle = string.Empty;
        private string _titlePageSubtitle = string.Empty;
        private string _titlePageAuthor = string.Empty;
        private string _titlePageDraftLabel = string.Empty;
        private string _titlePageDate = string.Empty;
        private readonly HashSet<Guid> _exportScopeSectionIds = new();
        private string _exportScopeSearch = string.Empty;
        private string? _exportSelectionText;
        private SectionEditor.EditorSelectionRange? _exportSelectionRange;
        private bool _isPresetsLoading;
        private bool _isPresetSaveOpen;
        private bool _presetMakeGlobalDefault;
        private string _presetNameDraft = string.Empty;
        private string? _presetLoadError;
        private string? _presetActionError;
        private ProjectExportSettingsDto? _projectExportSettings;
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
                "translate.selection",
                "Translate selection...",
                "Translate selection",
                true,
                new Dictionary<string, object?>()),
            new AiActionOption(
                "translate.section",
                "Translate section...",
                "Translate section",
                false,
                new Dictionary<string, object?>()),
            new AiActionOption(
                "translate.document",
                "Translate document...",
                "Translate document",
                false,
                new Dictionary<string, object?>()),
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
        private bool _isTranslateModalOpen;
        private AiActionOption? _pendingTranslateAction;
        private string _translateSourceLanguage = "auto";
        private string _translateTargetLanguage = "en";
        private string _translateStyle = "natural";
        private string _translationAlignmentMode = "paragraph";
        private string _translationApplyMode = "replace";
        private TranslateContext? _pendingTranslateContext;
        private ContextTab _activeContextTab = ContextTab.Notes;
        private readonly List<PageVersionListItemDto> _pageVersions = new();
        private bool _versionsLoading;
        private string? _versionsError;
        private Guid? _diffBaseVersionId;
        private Guid? _diffCompareVersionId;
        private PageVersionDiffResultDto? _diffResult;
        private readonly List<DiffChangeBlock> _diffChangeBlocks = new();
        private DiffSummary _diffSummary = DiffSummary.Empty;
        private bool _diffLoading;
        private string? _diffError;
        private string _diffGranularity = "word";
        private string _diffViewMode = "inline";
        private bool _isDiffMode;
        private bool _diffShowDeletions;
        private bool _diffSyncScroll = true;
        private int _diffChangeIndex = -1;
        private bool _diffSyncInProgress;
        private ElementReference _diffHeaderRef;
        private ElementReference _diffCanvasRef;
        private bool _isRestoreDialogOpen;
        private PageVersionListItemDto? _pendingRestoreVersion;
        private bool _restoreInFlight;
        private string? _restoreError;
        private string? _versionStatusMessage;
        private DateTimeOffset? _lastVersionSeenAt;
        private CancellationTokenSource? _versionStatusCts;
        private readonly List<PageAnnotationDto> _annotations = new();
        private bool _annotationsLoading;
        private string? _annotationsError;
        private string _annotationFilterStatus = "open";
        private string _annotationFilterKind = "all";
        private string _annotationDraftContent = string.Empty;
        private bool _annotationSaving;
        private bool _annotationAnchorsUpdating;
        private string? _annotationActionError;
        private bool _canCreateAnnotation;
        private Guid? _annotationFocusedId;
        private readonly List<PageQualityIssueDto> _qualityIssues = new();
        private bool _qualityLoading;
        private string? _qualityError;
        private bool _qualityFromCache;
        private string _qualityScope = "page";
        private string _qualityFilterSeverity = "all";
        private string _qualityFilterKind = "all";
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
        private const int PageBreakGapPx = 32;
        private const int PagePaddingX = 20;
        private const int PagePaddingY = 24;
        private static readonly TimeSpan SceneCardAutosaveDebounce = TimeSpan.FromSeconds(2.5);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private PageEditor.PageBreakOptions PageBreaks
        {
            get
            {
                LayoutState state = LayoutStateService.State;
                string mode = state.PrintLayoutEnabled ? "print" : "simple";
                bool showRule = !state.PrintLayoutEnabled;
                bool debug = IsDevelopmentEnvironment();
                return new PageEditor.PageBreakOptions(
                    PageBreakHeightPx,
                    showRule,
                    PageBreakGutterOffsetPx,
                    PageBreakGapPx,
                    mode,
                    debug);
            }
        }
        private IEnumerable<AiActionOption> SelectionAiActions =>
            _aiActions.Where(action => action.RequiresSelection);
        private IEnumerable<AiActionOption> SectionAiActions =>
            _aiActions.Where(action => !action.RequiresSelection);
        private bool IsTranslationProposal => IsTranslationActionKey(_pendingAiProposal?.ActionKey);
        private bool ShowTranslationSwitcher => GetTranslationLinks().Any(item => !item.IsActive);

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

            if (_isExportPreviewOpen && !_isPreviewLoading && !_previewInitialized && _previewFrameLoaded && !string.IsNullOrWhiteSpace(_previewHtml))
            {
                _previewInitialized = true;
                await InitializePreviewFrameAsync();
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
                _headingTraceId = Guid.NewGuid().ToString("N");
                await DebugHeadingLogAsync("NAVIGATE_BEGIN", new
                {
                    traceId = _headingTraceId,
                    documentId = DocumentId,
                    sectionId = SectionId
                });

                DocumentDetailDto? document = await Http.GetFromJsonAsync<DocumentDetailDto>($"api/documents/{DocumentId}");
                if (document is null)
                {
                    _loadError = "Document not found.";
                    return;
                }

                if (document.DeletedAt is not null)
                {
                    _loadError = "Document is in Trash; restore to edit.";
                    return;
                }

                _documentTitle = document.Title;
                _documentLanguageCode = document.LanguageCode;
                _documentTranslationGroupId = document.TranslationGroupId;

                if (_loadedDocumentId != DocumentId)
                {
                    _sections.Clear();
                    _pagesBySection.Clear();
                    _loadedDocumentId = DocumentId;
                }

                List<SectionDto>? sections = await Http.GetFromJsonAsync<List<SectionDto>>(
                    $"api/documents/{DocumentId}/sections");
                List<SectionDto> orderedSections = sections?.OrderBy(section => section.OrderIndex).ToList()
                    ?? new List<SectionDto>();
                _sections.Clear();
                _sections.AddRange(orderedSections);

                foreach (SectionDto section in orderedSections)
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
                _sectionLanguageCode = _activeSection.LanguageCode;
                _sectionTranslationGroupId = _activeSection.TranslationGroupId;

                _activePage = GetPrimaryPage(_activeSection.Id);
                if (_activePage is null)
                {
                    _loadError = "No pages available.";
                    return;
                }
                ResetVersionStatusTracking();

                Logger.LogDebug(
                    "HeadingPrefix PageContentLoaded TraceId={TraceId} DocumentId={DocumentId} SectionId={SectionId} PageId={PageId} Length={Length} Hash={Hash} Source={Source}",
                    _headingTraceId,
                    DocumentId,
                    _activeSection.Id,
                    _activePage.Id,
                    _activePage.Content?.Length ?? 0,
                    ComputeShortHash(_activePage.Content),
                    "db");

                await DebugHeadingLogAsync("PAGE_OPEN", new
                {
                    traceId = _headingTraceId,
                    documentId = DocumentId,
                    sectionId = _activeSection.Id,
                    pageId = _activePage.Id
                });

                await LastOpenedDocumentStateService.SaveAsync(DocumentId, _activeSection.Id);

                await LoadHeadingPrefixCountersAsync();
                _notesDraft = await LoadPageNotesAsync(_activePage.Id);
                _notesStatus = null;
                await LoadSceneCardAsync(_activeSection.Id);
                _outlineStatus = null;
                await LoadOutlineNodesAsync();
                await LoadAiHistoryAsync();
                await LoadPageVersionsAsync();
                await LoadAnnotationsAsync();
                await LoadQualityIssuesAsync();
                await LoadTranslationLinksAsync();
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
            if (_pageEditor is not null)
            {
                await _pageEditor.ForceSaveIfDifferentAsync("navigate");
            }

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
                    await LoadHeadingPrefixCountersAsync();
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
                await LoadPageVersionsAsync();
                await UpdateAnnotationAnchorsAsync();
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
            string pageWidth = state.ManuscriptWidthMode == ManuscriptWidthMode.Manuscript ? "760px" : "900px";
            return "--editor-max-width: " + maxWidth
                   + "; --editor-font-scale: " + scaleText
                   + "; --page-width-px: " + pageWidth
                   + "; --page-height-px: " + PageBreakHeightPx + "px"
                   + "; --page-gap-px: " + PageBreakGapPx + "px"
                   + "; --page-padding-x: " + PagePaddingX + "px"
                   + "; --page-padding-y: " + PagePaddingY + "px"
                   + "; --canvas-bg: #e1e1e1;";
        }

        private bool IsDevelopmentEnvironment()
        {
            string? env = Configuration?["ASPNETCORE_ENVIRONMENT"];
            if (string.IsNullOrWhiteSpace(env))
            {
                env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            }

            return string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
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

        private string GetHeadingNumberingScope()
        {
            return LayoutStateService.State.HeadingNumberingScope == HeadingNumberingScope.Section
                ? "section"
                : "document";
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
            _versionStatusCts?.Cancel();
            _versionStatusCts?.Dispose();
            _versionStatusCts = null;

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
                _ = _pageEditor.SetHeadingNumberingEnabledAsync(state.HeadingNumberingEnabled);
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

            if (state.PrintLayoutEnabled)
            {
                classes.Add("is-print-layout");
            }
            else
            {
                classes.Add("is-simple-layout");
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
                _canCreateAnnotation = false;
            }
            else
            {
                _currentSelectionRange = range;
                _canCreateAnnotation = true;
                _annotationActionError = null;
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

        private async Task OnTogglePrintLayout()
        {
            LayoutState current = LayoutStateService.State;
            await LayoutStateService.SetStateAsync(current with { PrintLayoutEnabled = !current.PrintLayoutEnabled });
        }

        private string GetEditorWidthLabel()
        {
            LayoutState current = LayoutStateService.State;
            return current.ManuscriptWidthMode == ManuscriptWidthMode.Manuscript
                ? "Switch to full width"
                : "Switch to manuscript width";
        }

        private string GetPrintLayoutLabel()
        {
            return LayoutStateService.State.PrintLayoutEnabled
                ? "Switch to simple page breaks"
                : "Switch to print layout";
        }

        private async Task SetContextTabAsync(ContextTab tab)
        {
            _activeContextTab = tab;
            if (tab == ContextTab.Annotations)
            {
                await LoadAnnotationsAsync();
            }
            else if (tab == ContextTab.Quality)
            {
                await LoadQualityIssuesAsync();
            }
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
                if (!string.Equals(kind, "document", StringComparison.OrdinalIgnoreCase))
                {
                    string templateQuery = string.Empty;
                    if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase)
                        && _selectedTemplateId.HasValue)
                    {
                        templateQuery = $"&templateId={_selectedTemplateId.Value}";
                    }

                    using (HttpResponseMessage legacyResponse = await Http.GetAsync(
                               $"api/documents/{DocumentId}/export?kind={kind}&format={format}{templateQuery}"))
                    {
                        if (!legacyResponse.IsSuccessStatusCode)
                        {
                            Logger.LogWarning("Export failed: {Status}", legacyResponse.StatusCode);
                            return;
                        }

                        byte[] legacyPayload = await legacyResponse.Content.ReadAsByteArrayAsync();
                        string legacyBase64 = Convert.ToBase64String(legacyPayload);
                        string legacyFileName = legacyResponse.Content.Headers.ContentDisposition?.FileNameStar
                            ?? legacyResponse.Content.Headers.ContentDisposition?.FileName
                            ?? $"export.{format}";
                        string legacyMime = legacyResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                        await DownloadExportAsync(legacyBase64, legacyMime, legacyFileName.Trim('"'));
                    }
                    return;
                }

                if (!ValidateScope(out string? error))
                {
                    _templateActionError = error;
                    return;
                }

                ExportDocumentRequest request = new(
                    DocumentId,
                    format,
                    _selectedTemplateId,
                    _exportScopeType,
                    BuildScopeIdsForRequest(),
                    _exportSelectionRange is null ? null : new SelectionRangeDto(_exportSelectionRange.Start, _exportSelectionRange.End),
                    _exportSelectionText,
                    _exportIncludeTitlePage,
                    _exportIncludeToc,
                    _exportTocDepth,
                    _exportChapterBreakRules.Count == 0 ? null : _exportChapterBreakRules.ToList(),
                    string.IsNullOrWhiteSpace(_titlePageTitle) ? null : _titlePageTitle,
                    string.IsNullOrWhiteSpace(_titlePageSubtitle) ? null : _titlePageSubtitle,
                    string.IsNullOrWhiteSpace(_titlePageAuthor) ? null : _titlePageAuthor,
                    string.IsNullOrWhiteSpace(_titlePageDraftLabel) ? null : _titlePageDraftLabel,
                    string.IsNullOrWhiteSpace(_titlePageDate) ? null : _titlePageDate);

                using HttpResponseMessage exportResponse = await Http.PostAsJsonAsync(
                    $"api/documents/{DocumentId}/export",
                    request);

                if (!exportResponse.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Export failed: {Status}", exportResponse.StatusCode);
                    return;
                }

                byte[] exportPayload = await exportResponse.Content.ReadAsByteArrayAsync();
                string exportBase64 = Convert.ToBase64String(exportPayload);
                string exportFileName = exportResponse.Content.Headers.ContentDisposition?.FileNameStar
                    ?? exportResponse.Content.Headers.ContentDisposition?.FileName
                    ?? $"export.{format}";
                string exportMime = exportResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                await DownloadExportAsync(exportBase64, exportMime, exportFileName.Trim('"'));
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
                if (!ValidateScope(out string? error))
                {
                    _templateActionError = error;
                    return;
                }

                ExportDocumentRequest request = new(
                    DocumentId,
                    "pdf",
                    _selectedTemplateId,
                    _exportScopeType,
                    BuildScopeIdsForRequest(),
                    _exportSelectionRange is null ? null : new SelectionRangeDto(_exportSelectionRange.Start, _exportSelectionRange.End),
                    _exportSelectionText,
                    _exportIncludeTitlePage,
                    _exportIncludeToc,
                    _exportTocDepth,
                    _exportChapterBreakRules.Count == 0 ? null : _exportChapterBreakRules.ToList(),
                    string.IsNullOrWhiteSpace(_titlePageTitle) ? null : _titlePageTitle,
                    string.IsNullOrWhiteSpace(_titlePageSubtitle) ? null : _titlePageSubtitle,
                    string.IsNullOrWhiteSpace(_titlePageAuthor) ? null : _titlePageAuthor,
                    string.IsNullOrWhiteSpace(_titlePageDraftLabel) ? null : _titlePageDraftLabel,
                    string.IsNullOrWhiteSpace(_titlePageDate) ? null : _titlePageDate);

                using HttpResponseMessage response = await Http.PostAsJsonAsync(
                    $"api/documents/{DocumentId}/export/print",
                    request);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("PDF export failed: {Status}", response.StatusCode);
                    return;
                }

                ExportPrintPayload? payload = await response.Content.ReadFromJsonAsync<ExportPrintPayload>();
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
            _previewFrameLoaded = false;
            _previewHasFrontMatter = false;
            try
            {
                ExportTemplateDto? template = GetSelectedTemplate();
                if (!ValidateScope(out string? error))
                {
                    _previewError = error;
                    return;
                }

                ExportPreviewRequest request = new(
                    DocumentId,
                    _selectedTemplateId,
                    _exportIncludeToc,
                    _exportScopeType,
                    BuildScopeIdsForRequest(),
                    _exportSelectionRange is null ? null : new SelectionRangeDto(_exportSelectionRange.Start, _exportSelectionRange.End),
                    _exportSelectionText,
                    _exportIncludeTitlePage,
                    _exportTocDepth,
                    _exportChapterBreakRules.Count == 0 ? null : _exportChapterBreakRules.ToList(),
                    string.IsNullOrWhiteSpace(_titlePageTitle) ? null : _titlePageTitle,
                    string.IsNullOrWhiteSpace(_titlePageSubtitle) ? null : _titlePageSubtitle,
                    string.IsNullOrWhiteSpace(_titlePageAuthor) ? null : _titlePageAuthor,
                    string.IsNullOrWhiteSpace(_titlePageDraftLabel) ? null : _titlePageDraftLabel,
                    string.IsNullOrWhiteSpace(_titlePageDate) ? null : _titlePageDate);

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

                _previewHtml = BuildPreviewHtml(payload.Html);
                _previewZoom = 1.0;
                _previewInitialized = false;
                _previewFrameLoaded = false;
                _previewHasFrontMatter = false;
                _previewSearchTerm = string.Empty;
                _previewPageCount = 1;
                _previewCurrentPage = 1;
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
            _previewSearchTerm = string.Empty;
            _previewInitialized = false;
            _previewFrameLoaded = false;
            _previewHasFrontMatter = false;
            _previewScrollRef?.Dispose();
            _previewScrollRef = null;
        }

        private async Task OnPreviewFrameLoadedAsync()
        {
            if (!_isExportPreviewOpen)
            {
                return;
            }

            _previewFrameLoaded = true;
            _previewInitialized = true;
            await InitializePreviewFrameAsync();

            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            _previewScrollRef?.Dispose();
            _previewScrollRef = DotNetObjectReference.Create(this);
            await _exportModule.InvokeVoidAsync("registerPreviewScroll", "export-preview-frame", _previewScrollRef);
        }

        [JSInvokable]
        public Task OnPreviewScroll(int pageCount, int currentPage, bool hasFrontMatter)
        {
            int nextCount = Math.Max(1, pageCount);
            _previewHasFrontMatter = hasFrontMatter;
            _previewPageCount = nextCount;
            _previewCurrentPage = currentPage <= 0 ? 0 : Math.Clamp(currentPage, 1, nextCount);
            return InvokeAsync(StateHasChanged);
        }

        private ExportTemplateDto? GetSelectedTemplate()
        {
            return _exportTemplates.FirstOrDefault(template => template.Id == _selectedTemplateId);
        }

        private string GetPreviewStyle()
        {
            ExportTemplateDto? template = GetSelectedTemplate();
            int width = template?.PageWidthMm ?? 210;
            int height = template?.PageHeightMm ?? 297;
            return $"--preview-page-width:{width}mm; --preview-page-height:{height}mm; --preview-zoom:{_previewZoom.ToString(CultureInfo.InvariantCulture)};";
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

        private string PreviewZoomPercent => $"{Math.Round(_previewZoom * 100)}%";

        private void AdjustPreviewZoom(double delta)
        {
            double next = Math.Clamp(_previewZoom + delta, 0.5, 2.5);
            _previewZoom = next;
        }

        private void TogglePreviewSidebar()
        {
            _previewSidebarOpen = !_previewSidebarOpen;
        }

        private async Task InitializePreviewFrameAsync()
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            ExportTemplateDto? template = GetSelectedTemplate();
            int width = template?.PageWidthMm ?? 210;
            int height = template?.PageHeightMm ?? 297;

            PreviewMetrics? metrics = await _exportModule.InvokeAsync<PreviewMetrics?>(
                "initPreviewFrame",
                "export-preview-frame",
                width,
                height,
                _previewShowPageBreaks);

            if (metrics is null)
            {
                return;
            }

            _previewHasFrontMatter = metrics.HasFrontMatter;
            _previewPageCount = Math.Max(1, metrics.PageCount);
            _previewCurrentPage = metrics.CurrentPage <= 0 ? 0 : Math.Clamp(metrics.CurrentPage, 1, _previewPageCount);
            await RefreshPreviewFitAsync(width, height);
        }

        private async Task RefreshPreviewFitAsync(int pageWidthMm, int pageHeightMm)
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            PreviewFit? fit = await _exportModule.InvokeAsync<PreviewFit?>(
                "getPreviewFit",
                "export-preview-frame",
                pageWidthMm,
                pageHeightMm);

            if (fit is null)
            {
                return;
            }

            _previewFitWidthZoom = fit.FitWidth;
            _previewFitPageZoom = fit.FitPage;
        }

        private double _previewFitWidthZoom = 1.0;
        private double _previewFitPageZoom = 1.0;

        private async Task SetFitWidthAsync()
        {
            if (_previewFitWidthZoom <= 0)
            {
                return;
            }

            _previewZoom = _previewFitWidthZoom;
            await InvokeAsync(StateHasChanged);
        }

        private async Task SetFitPageAsync()
        {
            if (_previewFitPageZoom <= 0)
            {
                return;
            }

            _previewZoom = _previewFitPageZoom;
            await InvokeAsync(StateHasChanged);
        }

        private async Task TogglePreviewPageBreaksAsync()
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            await _exportModule.InvokeVoidAsync(
                "setPreviewPageBreaks",
                "export-preview-frame",
                _previewShowPageBreaks);
        }

        private async Task JumpToPreviewPageAsync(int page)
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            await _exportModule.InvokeVoidAsync("scrollPreviewToPage", "export-preview-frame", page);
            _previewCurrentPage = page;
        }

        private async Task JumpToFrontMatterAsync()
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            await _exportModule.InvokeVoidAsync("scrollPreviewToFrontMatter", "export-preview-frame");
            _previewCurrentPage = 0;
        }

        private async Task RunPreviewSearchAsync()
        {
            await EnsureExportModuleAsync();
            if (_exportModule is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_previewSearchTerm))
            {
                await _exportModule.InvokeVoidAsync("clearPreviewSearch", "export-preview-frame");
                return;
            }

            await _exportModule.InvokeVoidAsync("searchPreview", "export-preview-frame", _previewSearchTerm);
        }

        private async Task ClearPreviewSearch()
        {
            _previewSearchTerm = string.Empty;
            await RunPreviewSearchAsync();
        }

        private string BuildPreviewHtml(string rawHtml)
        {
            const string marker = "__WRITER_PREVIEW__";
            if (string.IsNullOrWhiteSpace(rawHtml))
            {
                return rawHtml;
            }

            string injected = PreviewBootstrapScript;
            if (rawHtml.Contains(marker, StringComparison.Ordinal))
            {
                return rawHtml;
            }

            if (rawHtml.Contains("</body>", StringComparison.OrdinalIgnoreCase))
            {
                return rawHtml.Replace("</body>", $"{injected}</body>", StringComparison.OrdinalIgnoreCase);
            }

            return $"<html><head><meta charset=\"utf-8\"></head><body>{rawHtml}{injected}</body></html>";
        }

        private const string PreviewBootstrapScript = @"
<style id=""__WRITER_PREVIEW__"">
    body { margin: 0; padding: 24px; box-sizing: border-box; position: relative; }
    .preview-pagebreak-overlay { position: absolute; left: 0; right: 0; top: 0; pointer-events: none; }
    .preview-pagebreak-line { position: absolute; left: 0; right: 0; border-top: 1px dashed rgba(148, 163, 184, 0.7); }
    mark.preview-search-hit { background: #fde68a; padding: 0 2px; border-radius: 3px; }
    html, body { scroll-behavior: smooth; }
</style>
<script id=""__WRITER_PREVIEW__"">window.__writerPreviewReady=true;</script>";

        private sealed record PreviewMetrics(int PageCount, int CurrentPage, bool HasFrontMatter);
        private sealed record PreviewFit(double FitWidth, double FitPage);

        private async Task OnExportDialogOpenAsync()
        {
            _isDocumentMenuOpen = false;
            _isExportDialogOpen = true;
            _templateActionError = null;
            _presetActionError = null;
            await EnsureTemplatesLoadedAsync();
            await LoadExportPresetsAsync();
            await LoadProjectExportSettingsAsync();
            InitializeExportDefaults();
            ApplyDefaultExportPreset();
        }

        private void CloseExportDialog()
        {
            _isExportDialogOpen = false;
            _templateActionError = null;
            _presetActionError = null;
            _isPresetSaveOpen = false;
        }

        private void InitializeExportDefaults()
        {
            ExportTemplateDto? template = GetSelectedTemplate();
            _exportIncludeTitlePage = true;
            _exportIncludeToc = template?.TocEnabled ?? true;
            _exportTocDepth = template?.TocDepth ?? 2;
            _exportChapterBreakRules.Clear();
            _titlePageTitle = _documentTitle ?? string.Empty;
            _titlePageSubtitle = string.Empty;
            _titlePageAuthor = string.Empty;
            _titlePageDraftLabel = string.Empty;
            _titlePageDate = string.Empty;
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

        private async Task LoadExportPresetsAsync()
        {
            _isPresetsLoading = true;
            _presetLoadError = null;
            try
            {
                List<ExportPresetDto>? presets = await Http.GetFromJsonAsync<List<ExportPresetDto>>(
                    "api/export/presets");
                _exportPresets.Clear();
                if (presets is not null)
                {
                    _exportPresets.AddRange(presets.OrderBy(preset => preset.Name));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load export presets.");
                _presetLoadError = "Unable to load export presets.";
            }
            finally
            {
                _isPresetsLoading = false;
            }
        }

        private async Task LoadProjectExportSettingsAsync()
        {
            try
            {
                _projectExportSettings = await Http.GetFromJsonAsync<ProjectExportSettingsDto>(
                    $"api/documents/{DocumentId}/export-settings");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load project export settings.");
                _projectExportSettings = null;
            }
        }

        private void ApplyDefaultExportPreset()
        {
            Guid? presetId = _projectExportSettings?.DefaultPresetId;
            if (presetId is null)
            {
                presetId = _exportPresets.FirstOrDefault(preset => preset.IsGlobalDefault)?.Id;
            }

            if (presetId.HasValue)
            {
                ExportPresetDto? preset = _exportPresets.FirstOrDefault(item => item.Id == presetId.Value);
                if (preset is not null)
                {
                    _selectedExportPresetId = preset.Id;
                    ApplyExportPreset(preset);
                }
            }
        }

        private void ApplyExportPreset(ExportPresetDto preset)
        {
            ExportPresetSettingsDto settings = preset.Settings;
            _exportFormatSelection = string.IsNullOrWhiteSpace(settings.Format) ? "html" : settings.Format;
            _exportScopeType = string.IsNullOrWhiteSpace(settings.Scope) ? "document" : settings.Scope;
            _exportIncludeTitlePage = settings.IncludeTitlePage;
            _exportIncludeToc = settings.IncludeToc;
            _exportTocDepth = settings.TocDepth > 0 ? settings.TocDepth : _exportTocDepth;
            _titlePageTitle = settings.TitlePageTitle ?? _documentTitle ?? string.Empty;
            _titlePageSubtitle = settings.TitlePageSubtitle ?? string.Empty;
            _titlePageAuthor = settings.TitlePageAuthor ?? string.Empty;
            _titlePageDraftLabel = settings.TitlePageDraftLabel ?? string.Empty;
            _titlePageDate = settings.TitlePageDate ?? string.Empty;
            _exportChapterBreakRules.Clear();
            if (settings.ChapterBreakRules is not null)
            {
                foreach (string rule in settings.ChapterBreakRules)
                {
                    _exportChapterBreakRules.Add(rule);
                }
            }
            _exportScopeSectionIds.Clear();
            if (settings.ScopeIds is not null)
            {
                foreach (Guid id in settings.ScopeIds)
                {
                    _exportScopeSectionIds.Add(id);
                }
            }

            Guid? templateId = settings.TemplateId;
            if (templateId.HasValue && _exportTemplates.All(template => template.Id != templateId.Value))
            {
                templateId = null;
            }

            if (templateId.HasValue)
            {
                _selectedTemplateId = templateId;
            }
            else if (_selectedTemplateId is null && _exportTemplates.Count > 0)
            {
                _selectedTemplateId = _exportTemplates[0].Id;
            }
        }

        private ExportPresetSettingsDto BuildPresetSettingsFromForm()
        {
            ExportTemplateDto? template = GetSelectedTemplate();
            return new ExportPresetSettingsDto(
                _exportFormatSelection,
                _selectedTemplateId,
                _exportScopeType,
                BuildScopeIdsForPreset(),
                _exportSelectionRange is null ? null : new SelectionRangeDto(_exportSelectionRange.Start, _exportSelectionRange.End),
                _exportIncludeToc,
                _exportTocDepth,
                _exportIncludeTitlePage,
                string.IsNullOrWhiteSpace(_titlePageTitle) ? null : _titlePageTitle,
                string.IsNullOrWhiteSpace(_titlePageSubtitle) ? null : _titlePageSubtitle,
                string.IsNullOrWhiteSpace(_titlePageAuthor) ? null : _titlePageAuthor,
                string.IsNullOrWhiteSpace(_titlePageDraftLabel) ? null : _titlePageDraftLabel,
                string.IsNullOrWhiteSpace(_titlePageDate) ? null : _titlePageDate,
                template?.HeaderEnabled ?? false,
                template?.HeaderLeft,
                template?.HeaderCenter,
                template?.HeaderRight,
                template?.FooterEnabled ?? false,
                template?.FooterLeft,
                template?.FooterCenter,
                template?.FooterRight,
                _exportChapterBreakRules.Count == 0 ? null : _exportChapterBreakRules.ToList(),
                null,
                null);
        }

        private IReadOnlyList<Guid>? BuildScopeIdsForPreset()
        {
            return _exportScopeType switch
            {
                "sections" => _exportScopeSectionIds.Count == 0 ? null : _exportScopeSectionIds.ToList(),
                "section" => _activeSection is null ? null : new List<Guid> { _activeSection.Id },
                "page" => _activePage is null ? null : new List<Guid> { _activePage.Id },
                _ => null
            };
        }

        private IEnumerable<SectionDto> FilteredScopeSections =>
            _sections.Where(section =>
                string.IsNullOrWhiteSpace(_exportScopeSearch)
                || section.Title.Contains(_exportScopeSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(section => section.OrderIndex);

        private bool IsSectionSelected(Guid sectionId) => _exportScopeSectionIds.Contains(sectionId);

        private void ToggleSectionSelection(Guid sectionId, ChangeEventArgs args)
        {
            bool isSelected = args.Value switch
            {
                bool value => value,
                string text when bool.TryParse(text, out bool parsed) => parsed,
                _ => false
            };
            if (isSelected)
            {
                _exportScopeSectionIds.Add(sectionId);
            }
            else
            {
                _exportScopeSectionIds.Remove(sectionId);
            }

            MarkPresetAsCustom();
        }

        private void ToggleSelectAllSections()
        {
            if (_sections.Count == 0)
            {
                return;
            }

            if (_exportScopeSectionIds.Count == _sections.Count)
            {
                _exportScopeSectionIds.Clear();
            }
            else
            {
                _exportScopeSectionIds.Clear();
                foreach (SectionDto section in _sections)
                {
                    _exportScopeSectionIds.Add(section.Id);
                }
            }

            MarkPresetAsCustom();
        }

        private bool IsChapterBreakRuleEnabled(string rule) => _exportChapterBreakRules.Contains(rule);

        private void ToggleChapterBreakRule(string rule, ChangeEventArgs args)
        {
            bool isEnabled = args.Value switch
            {
                bool value => value,
                string text when bool.TryParse(text, out bool parsed) => parsed,
                _ => false
            };

            if (isEnabled)
            {
                _exportChapterBreakRules.Add(rule);
            }
            else
            {
                _exportChapterBreakRules.Remove(rule);
            }

            MarkPresetAsCustom();
        }

        private string ExportSelectionSummary =>
            string.IsNullOrWhiteSpace(_exportSelectionText)
                ? "No selection captured."
                : $"{_exportSelectionText.Length} chars selected";

        private async Task CaptureSelectionAsync()
        {
            _presetActionError = null;
            if (_pageEditor is null)
            {
                _presetActionError = "Editor is not ready.";
                return;
            }

            string? selectionText = await _pageEditor.GetSelectionTextAsync();
            SectionEditor.EditorSelectionRange? range = _currentSelectionRange;
            if (string.IsNullOrWhiteSpace(selectionText))
            {
                _presetActionError = "Selection is empty.";
                return;
            }

            _exportSelectionText = selectionText;
            _exportSelectionRange = range;
        }

        private IReadOnlyList<Guid>? BuildScopeIdsForRequest()
        {
            return _exportScopeType switch
            {
                "sections" => _exportScopeSectionIds.Count == 0 ? null : _exportScopeSectionIds.ToList(),
                "section" => _activeSection is null ? null : new List<Guid> { _activeSection.Id },
                "page" => _activePage is null ? null : new List<Guid> { _activePage.Id },
                _ => null
            };
        }

        private bool ValidateScope(out string? error)
        {
            error = null;
            if (string.Equals(_exportScopeType, "sections", StringComparison.OrdinalIgnoreCase)
                && _exportScopeSectionIds.Count == 0)
            {
                error = "Select at least one section.";
                return false;
            }

            if (string.Equals(_exportScopeType, "section", StringComparison.OrdinalIgnoreCase)
                && _activeSection is null)
            {
                error = "No active section.";
                return false;
            }

            if (string.Equals(_exportScopeType, "page", StringComparison.OrdinalIgnoreCase)
                && _activePage is null)
            {
                error = "No active page.";
                return false;
            }

            if (string.Equals(_exportScopeType, "selection", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(_exportSelectionText))
            {
                error = "Capture a selection first.";
                return false;
            }

            return true;
        }

        private void MarkPresetAsCustom()
        {
            _selectedExportPresetId = null;
        }

        private void OpenPresetSave()
        {
            _presetActionError = null;
            _isPresetSaveOpen = true;
            _presetNameDraft = string.Empty;
            _presetMakeGlobalDefault = false;
        }

        private void ClosePresetSave()
        {
            _isPresetSaveOpen = false;
        }

        private async Task SavePresetAsync()
        {
            _presetActionError = null;
            string name = _presetNameDraft.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                _presetActionError = "Preset name is required.";
                return;
            }

            ExportPresetCreateRequest request = new(
                name,
                _presetMakeGlobalDefault,
                BuildPresetSettingsFromForm());

            try
            {
                using HttpResponseMessage response = await Http.PostAsJsonAsync("api/export/presets", request);
                if (!response.IsSuccessStatusCode)
                {
                    _presetActionError = "Failed to save preset.";
                    return;
                }

                ExportPresetDto? created = await response.Content.ReadFromJsonAsync<ExportPresetDto>();
                if (created is not null)
                {
                    _exportPresets.Add(created);
                    _selectedExportPresetId = created.Id;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to save export preset.");
                _presetActionError = "Failed to save preset.";
            }
            finally
            {
                _isPresetSaveOpen = false;
            }
        }

        private async Task UpdateSelectedPresetAsync()
        {
            _presetActionError = null;
            if (_selectedExportPresetId is null)
            {
                return;
            }

            ExportPresetDto? existing = _exportPresets.FirstOrDefault(item => item.Id == _selectedExportPresetId);
            if (existing is null)
            {
                _presetActionError = "Preset not found.";
                return;
            }

            ExportPresetUpdateRequest request = new(
                existing.Name,
                existing.IsGlobalDefault,
                BuildPresetSettingsFromForm());

            try
            {
                using HttpResponseMessage response = await Http.PutAsJsonAsync(
                    $"api/export/presets/{existing.Id}",
                    request);
                if (!response.IsSuccessStatusCode)
                {
                    _presetActionError = "Failed to update preset.";
                    return;
                }

                ExportPresetDto? updated = await response.Content.ReadFromJsonAsync<ExportPresetDto>();
                if (updated is not null)
                {
                    int index = _exportPresets.FindIndex(item => item.Id == updated.Id);
                    if (index >= 0)
                    {
                        _exportPresets[index] = updated;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to update export preset.");
                _presetActionError = "Failed to update preset.";
            }
        }

        private async Task SetProjectDefaultPresetAsync()
        {
            _presetActionError = null;
            if (_selectedExportPresetId is null)
            {
                return;
            }

            ProjectExportSettingsUpdateRequest request = new(
                _selectedExportPresetId,
                null);

            try
            {
                using HttpResponseMessage response = await Http.PutAsJsonAsync(
                    $"api/documents/{DocumentId}/export-settings",
                    request);
                if (!response.IsSuccessStatusCode)
                {
                    _presetActionError = "Failed to set project default.";
                    return;
                }

                ProjectExportSettingsDto? updated = await response.Content.ReadFromJsonAsync<ProjectExportSettingsDto>();
                if (updated is not null)
                {
                    _projectExportSettings = updated;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to set project export default.");
                _presetActionError = "Failed to set project default.";
            }
        }

        private string SelectedTemplateIdValue
        {
            get => _selectedTemplateId?.ToString() ?? string.Empty;
            set => _selectedTemplateId = Guid.TryParse(value, out Guid parsed) ? parsed : null;
        }

        private string SelectedPresetIdValue
        {
            get => _selectedExportPresetId?.ToString() ?? string.Empty;
            set
            {
                Guid? presetId = Guid.TryParse(value, out Guid parsed) ? parsed : null;
                _selectedExportPresetId = presetId;
                if (presetId.HasValue)
                {
                    ExportPresetDto? preset = _exportPresets.FirstOrDefault(item => item.Id == presetId.Value);
                    if (preset is not null)
                    {
                        ApplyExportPreset(preset);
                    }
                }
            }
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

            if (IsTranslationActionKey(action.ActionKey))
            {
                OpenTranslateModal(action, plain, selectionRange, selection);
                await InvokeAsync(StateHasChanged);
                return;
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

            _translationApplyMode = "replace";
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

        private void OpenTranslateModal(
            AiActionOption action,
            string plainText,
            TextRange selectionRange,
            string selectionText)
        {
            _pendingTranslateAction = action;
            _pendingTranslateContext = new TranslateContext(plainText, selectionRange, selectionText);
            _translationApplyMode = "replace";
            if (string.IsNullOrWhiteSpace(_translateSourceLanguage) || string.Equals(_translateSourceLanguage, "auto", StringComparison.OrdinalIgnoreCase))
            {
                _translateSourceLanguage = _documentLanguageCode ?? "auto";
            }
            _isTranslateModalOpen = true;
        }

        private void CloseTranslateModal()
        {
            _isTranslateModalOpen = false;
        }

        private async Task ConfirmTranslateAsync()
        {
            if (_pendingTranslateAction is null || _pendingTranslateContext is null || _activeSection is null)
            {
                CloseTranslateModal();
                return;
            }

            await ExecuteTranslateActionAsync(_pendingTranslateAction, _pendingTranslateContext);
            _isTranslateModalOpen = false;
            await InvokeAsync(StateHasChanged);
        }

        private async Task ExecuteTranslateActionAsync(AiActionOption action, TranslateContext context)
        {
            Dictionary<string, object?> parameters = new(action.Parameters)
            {
                ["instruction"] = action.Instruction,
                ["source_language"] = _translateSourceLanguage,
                ["target_language"] = _translateTargetLanguage,
                ["style"] = _translateStyle
            };

            int? selectionStart = action.RequiresSelection ? context.SelectionRange.Start : null;
            int? selectionEnd = action.RequiresSelection ? context.SelectionRange.Start + context.SelectionRange.Length : null;
            string? originalText = action.RequiresSelection ? context.SelectionText : null;

            AiActionExecuteRequestDto request = new(
                DocumentId,
                _activeSection!.Id,
                _activePage?.Id,
                selectionStart,
                selectionEnd,
                originalText,
                context.PlainText,
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
                    ShowAiMessage("AI translation failed.");
                    await InvokeAsync(StateHasChanged);
                    return;
                }

                response = await result.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                if (response is null)
                {
                    ShowAiMessage("AI translation failed.");
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
                action.ActionKey,
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

        private Task OnTranslationAlignmentChanged(string next)
        {
            _translationAlignmentMode = string.IsNullOrWhiteSpace(next) ? "paragraph" : next;
            return InvokeAsync(StateHasChanged);
        }

        private async Task CopyTranslatedTextAsync()
        {
            string text = _pendingAiProposal?.ProposedText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Copy translated text failed.");
            }
        }

        private void OpenTranslateModalFromProposal()
        {
            if (_pendingAiProposal is null)
            {
                return;
            }

            AiActionOption? action = _aiActions.FirstOrDefault(
                candidate => string.Equals(candidate.ActionKey, _pendingAiProposal.ActionKey, StringComparison.OrdinalIgnoreCase));
            if (action is null)
            {
                return;
            }

            if (action.RequiresSelection && _pendingTranslateContext is not null)
            {
                OpenTranslateModal(
                    action,
                    _pendingTranslateContext.PlainText,
                    _pendingTranslateContext.SelectionRange,
                    _pendingTranslateContext.SelectionText);
                return;
            }

            string? html = _pageEditor?.GetContent();
            string plain = PlainTextMapper.ToPlainText(html ?? string.Empty);
            TextRange selectionRange = new(0, 0);
            string selection = string.Empty;
            if (action.RequiresSelection && _currentSelectionRange is not null)
            {
                selectionRange = NormalizeRange(_currentSelectionRange, plain.Length);
                selection = ExtractRangeText(plain, selectionRange);
            }

            OpenTranslateModal(action, plain, selectionRange, selection);
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
            if (IsTranslationActionKey(pending.ActionKey))
            {
                await ApplyTranslationProposalAsync(pending);
                return;
            }
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

        private async Task ApplyTranslationProposalAsync(PendingAiProposal pending)
        {
            if (_activeSection is null || string.IsNullOrWhiteSpace(pending.ProposedText))
            {
                _pendingAiProposal = null;
                await InvokeAsync(StateHasChanged);
                return;
            }

            string? beforeContent = _pageEditor is null ? null : await _pageEditor.GetContentAsync();
            string translatedText = pending.ProposedText ?? string.Empty;
            DateTimeOffset appliedAt = DateTimeOffset.UtcNow;

            if (string.Equals(pending.ActionKey, "translate.selection", StringComparison.OrdinalIgnoreCase))
            {
                await InvokePageCommandAsync("replaceSelection", translatedText);
            }
            else if (string.Equals(pending.ActionKey, "translate.section", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(_translationApplyMode, "duplicate-section", StringComparison.OrdinalIgnoreCase))
                {
                    await DuplicateTranslatedSectionAsync(translatedText);
                }
                else
                {
                    string html = PlainTextToHtml(translatedText);
                    if (_pageEditor is not null)
                    {
                        await _pageEditor.SetContentAsync(html);
                    }
                }
            }
            else if (string.Equals(pending.ActionKey, "translate.document", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(_translationApplyMode, "duplicate-document", StringComparison.OrdinalIgnoreCase))
                {
                    await DuplicateTranslatedDocumentAsync(translatedText);
                }
                else
                {
                    await ReplaceTranslatedDocumentAsync(translatedText);
                }
            }

            string? afterContent = _pageEditor is null ? null : await _pageEditor.GetContentAsync();
            UpdateAiHistoryAppliedState(pending.ProposalId, appliedAt);
            _expandedAiHistoryId = pending.ProposalId;
            _pendingDetailsExpanded = false;
            _pendingAiProposal = null;
            _ = RecordAppliedEventAsync(pending.ProposalId, appliedAt, beforeContent, afterContent);
            UpdateAiUndoRedoAvailability();
            await InvokeAsync(StateHasChanged);
        }

        private async Task DuplicateTranslatedSectionAsync(string translatedText)
        {
            if (_activeSection is null)
            {
                return;
            }

            string html = PlainTextToHtml(translatedText);
            TranslationDuplicateSectionRequest payload = new(
                html,
                _translateTargetLanguage,
                _translateSourceLanguage,
                BuildTranslatedTitle(_activeSection.Title, _translateTargetLanguage));

            using HttpResponseMessage response =
                await Http.PostAsJsonAsync($"api/sections/{_activeSection.Id}/translations", payload);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Translate duplicate section failed: {Status}", response.StatusCode);
                return;
            }

            TranslationDuplicateSectionResponse? result =
                await response.Content.ReadFromJsonAsync<TranslationDuplicateSectionResponse>();
            if (result is null)
            {
                return;
            }

            Navigation.NavigateTo($"/documents/{result.Section.DocumentId}/sections/{result.Section.Id}");
        }

        private async Task DuplicateTranslatedDocumentAsync(string translatedText)
        {
            if (_sections.Count == 0)
            {
                return;
            }

            List<TranslatedSectionPayload> sections = BuildTranslatedSectionsPayload(translatedText);
            TranslationDuplicateDocumentRequest payload = new(
                BuildTranslatedTitle(_documentTitle, _translateTargetLanguage),
                _translateTargetLanguage,
                _translateSourceLanguage,
                sections);

            using HttpResponseMessage response =
                await Http.PostAsJsonAsync($"api/documents/{DocumentId}/translations/duplicate", payload);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Translate duplicate document failed: {Status}", response.StatusCode);
                return;
            }

            TranslationDuplicateDocumentResponse? result =
                await response.Content.ReadFromJsonAsync<TranslationDuplicateDocumentResponse>();
            if (result is null)
            {
                return;
            }

            Guid? targetSectionId = result.DefaultSectionId;
            if (!targetSectionId.HasValue)
            {
                List<SectionDto>? sectionsList =
                    await Http.GetFromJsonAsync<List<SectionDto>>($"api/documents/{result.Document.Id}/sections");
                targetSectionId = sectionsList?.OrderBy(section => section.OrderIndex).FirstOrDefault()?.Id;
            }

            if (targetSectionId.HasValue)
            {
                Navigation.NavigateTo($"/documents/{result.Document.Id}/sections/{targetSectionId.Value}");
            }
        }

        private async Task ReplaceTranslatedDocumentAsync(string translatedText)
        {
            Dictionary<Guid, string> mapping = ParseTranslatedSections(translatedText);
            foreach (SectionDto section in _sections)
            {
                if (!mapping.TryGetValue(section.Id, out string? sectionText))
                {
                    continue;
                }

                string html = PlainTextToHtml(sectionText);
                if (_activeSection is not null && section.Id == _activeSection.Id)
                {
                    if (_pageEditor is not null)
                    {
                        await _pageEditor.SetContentAsync(html);
                    }

                    if (_pagesBySection.TryGetValue(section.Id, out List<PageDto>? activePages) && activePages.Count > 0)
                    {
                        activePages[0] = activePages[0] with { Content = html };
                    }

                    continue;
                }

                if (_pagesBySection.TryGetValue(section.Id, out List<PageDto>? pages) && pages.Count > 0)
                {
                    PageDto page = pages[0] with { Content = html };
                    using HttpResponseMessage response = await Http.PutAsJsonAsync(
                        $"api/pages/{page.Id}",
                        new PageUpdateRequest(page.Title, page.Content));
                    if (response.IsSuccessStatusCode)
                    {
                        pages[0] = page;
                    }
                }
            }
        }

        private List<TranslatedSectionPayload> BuildTranslatedSectionsPayload(string translatedText)
        {
            Dictionary<Guid, string> mapping = ParseTranslatedSections(translatedText);
            List<TranslatedSectionPayload> result = new();
            foreach (SectionDto section in _sections.OrderBy(item => item.OrderIndex))
            {
                string content = mapping.TryGetValue(section.Id, out string? sectionText)
                    ? PlainTextToHtml(sectionText)
                    : string.Empty;
                result.Add(new TranslatedSectionPayload(section.Id, content, BuildTranslatedTitle(section.Title, _translateTargetLanguage)));
            }

            return result;
        }

        private static Dictionary<Guid, string> ParseTranslatedSections(string translatedText)
        {
            Dictionary<Guid, string> result = new();
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                return result;
            }

            Regex markerRegex = new(@"\[\[SECTION:(?<id>[0-9a-fA-F\-]{36})\]\]", RegexOptions.Compiled);
            MatchCollection matches = markerRegex.Matches(translatedText);
            if (matches.Count == 0)
            {
                return result;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (!Guid.TryParse(match.Groups["id"].Value, out Guid sectionId))
                {
                    continue;
                }

                int startIndex = match.Index + match.Length;
                int endIndex = i + 1 < matches.Count ? matches[i + 1].Index : translatedText.Length;
                string sectionText = translatedText.Substring(startIndex, Math.Max(0, endIndex - startIndex)).Trim();
                result[sectionId] = sectionText;
            }

            return result;
        }

        private static string PlainTextToHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .TrimEnd();
            string[] paragraphs = Regex.Split(normalized, @"\n\s*\n");
            StringBuilder builder = new();
            foreach (string paragraph in paragraphs)
            {
                string trimmed = paragraph.TrimEnd();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                string encoded = WebUtility.HtmlEncode(trimmed);
                encoded = encoded.Replace("\n", "<br />\n");
                builder.Append("<p>").Append(encoded).Append("</p>");
            }

            return builder.ToString();
        }

        private static string BuildTranslatedTitle(string title, string? languageCode)
        {
            string normalized = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
            string lang = string.IsNullOrWhiteSpace(languageCode) ? "" : languageCode.Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(lang) ? normalized : $"{normalized} ({lang})";
        }

        private static bool IsTranslationActionKey(string? actionKey)
        {
            return !string.IsNullOrWhiteSpace(actionKey)
                && actionKey.StartsWith("translate.", StringComparison.OrdinalIgnoreCase);
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

        private IEnumerable<TranslationApplyOption> GetTranslationApplyOptions()
        {
            string actionKey = _pendingAiProposal?.ActionKey ?? _pendingTranslateAction?.ActionKey ?? string.Empty;
            if (string.Equals(actionKey, "translate.selection", StringComparison.OrdinalIgnoreCase))
            {
                yield return new TranslationApplyOption("replace", "Replace selection");
                yield break;
            }

            if (string.Equals(actionKey, "translate.section", StringComparison.OrdinalIgnoreCase))
            {
                yield return new TranslationApplyOption("replace", "Replace section");
                yield return new TranslationApplyOption("duplicate-section", "Duplicate as new section");
                yield break;
            }

            if (string.Equals(actionKey, "translate.document", StringComparison.OrdinalIgnoreCase))
            {
                yield return new TranslationApplyOption("replace", "Replace document");
                yield return new TranslationApplyOption("duplicate-document", "Duplicate as new document");
                yield break;
            }

            yield return new TranslationApplyOption("replace", "Apply");
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

            if (string.Equals(actionKey, "translate.selection", StringComparison.OrdinalIgnoreCase))
            {
                return "Translate selection";
            }

            if (string.Equals(actionKey, "translate.section", StringComparison.OrdinalIgnoreCase))
            {
                return "Translate section";
            }

            if (string.Equals(actionKey, "translate.document", StringComparison.OrdinalIgnoreCase))
            {
                return "Translate document";
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

        private async Task LoadPageVersionsAsync()
        {
            if (_activePage is null)
            {
                _pageVersions.Clear();
                _versionsError = null;
                return;
            }

            _versionsLoading = true;
            _versionsError = null;
            try
            {
                List<PageVersionListItemDto>? versions =
                    await Http.GetFromJsonAsync<List<PageVersionListItemDto>>(
                        $"api/pages/{_activePage.Id}/versions");
                _pageVersions.Clear();
                if (versions is not null)
                {
                    _pageVersions.AddRange(versions);
                    UpdateVersionStatusMessage(versions);
                }
            }
            catch (Exception ex)
            {
                _pageVersions.Clear();
                _versionsError = $"Failed to load versions: {ex.Message}";
            }
            finally
            {
                _versionsLoading = false;
            }
        }

        private void UpdateVersionStatusMessage(IReadOnlyList<PageVersionListItemDto> versions)
        {
            if (versions.Count == 0)
            {
                return;
            }

            PageVersionListItemDto latest = versions[0];
            if (_lastVersionSeenAt is null)
            {
                _lastVersionSeenAt = latest.CreatedAt;
                return;
            }

            if (latest.CreatedAt <= _lastVersionSeenAt.Value)
            {
                return;
            }

            _lastVersionSeenAt = latest.CreatedAt;
            string reason = string.IsNullOrWhiteSpace(latest.Reason) ? "snapshot" : latest.Reason.Trim();
            _versionStatusMessage = $"Version saved ({reason})";

            _versionStatusCts?.Cancel();
            _versionStatusCts?.Dispose();
            _versionStatusCts = new CancellationTokenSource();
            _ = ClearVersionStatusMessageAsync(_versionStatusCts, TimeSpan.FromSeconds(6));
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task ClearVersionStatusMessageAsync(CancellationTokenSource cts, TimeSpan delay)
        {
            try
            {
                await Task.Delay(delay, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (cts.IsCancellationRequested)
            {
                return;
            }

            _versionStatusMessage = null;
            await InvokeAsync(StateHasChanged);
        }

        private void ResetVersionStatusTracking()
        {
            _lastVersionSeenAt = null;
            _versionStatusMessage = null;
            _versionStatusCts?.Cancel();
            _versionStatusCts?.Dispose();
            _versionStatusCts = null;
        }

        private async Task LoadAnnotationsAsync()
        {
            if (_activePage is null)
            {
                _annotations.Clear();
                _annotationsError = null;
                return;
            }

            _annotationsLoading = true;
            _annotationsError = null;

            try
            {
                string status = string.IsNullOrWhiteSpace(_annotationFilterStatus) ? "open" : _annotationFilterStatus;
                string url = $"api/pages/{_activePage.Id}/annotations?status={Uri.EscapeDataString(status)}";
                if (!string.IsNullOrWhiteSpace(_annotationFilterKind) && !_annotationFilterKind.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    url += $"&kind={Uri.EscapeDataString(_annotationFilterKind)}";
                }

                List<PageAnnotationDto>? annotations = await Http.GetFromJsonAsync<List<PageAnnotationDto>>(url);
                _annotations.Clear();
                if (annotations is not null)
                {
                    _annotations.AddRange(annotations);
                }
            }
            catch (Exception ex)
            {
                _annotations.Clear();
                _annotationsError = $"Failed to load annotations: {ex.Message}";
            }
            finally
            {
                _annotationsLoading = false;
            }

            if (_annotationFocusedId.HasValue)
            {
                await ScrollAnnotationIntoViewAsync(_annotationFocusedId.Value);
            }
        }

        private async Task LoadQualityIssuesAsync()
        {
            if (_activePage is null)
            {
                _qualityIssues.Clear();
                _qualityError = null;
                return;
            }

            _qualityLoading = true;
            _qualityError = null;
            _qualityFromCache = false;

            try
            {
                List<PageQualityIssueDto>? issues =
                    await Http.GetFromJsonAsync<List<PageQualityIssueDto>>(
                        $"api/pages/{_activePage.Id}/quality-checks/issues");
                _qualityIssues.Clear();
                if (issues is not null)
                {
                    _qualityIssues.AddRange(issues);
                }
            }
            catch (Exception ex)
            {
                _qualityIssues.Clear();
                _qualityError = $"Failed to load quality issues: {ex.Message}";
            }
            finally
            {
                _qualityLoading = false;
            }
        }

        private async Task OnAnnotationStatusFilterChanged(ChangeEventArgs args)
        {
            _annotationFilterStatus = args.Value?.ToString() ?? "open";
            await LoadAnnotationsAsync();
        }

        private async Task OnAnnotationKindFilterChanged(ChangeEventArgs args)
        {
            _annotationFilterKind = args.Value?.ToString() ?? "all";
            await LoadAnnotationsAsync();
        }

        private Task OnQualityScopeChanged(ChangeEventArgs args)
        {
            _qualityScope = args.Value?.ToString() ?? "page";
            return Task.CompletedTask;
        }

        private Task OnQualitySeverityChanged(ChangeEventArgs args)
        {
            _qualityFilterSeverity = args.Value?.ToString() ?? "all";
            return Task.CompletedTask;
        }

        private Task OnQualityKindChanged(ChangeEventArgs args)
        {
            _qualityFilterKind = args.Value?.ToString() ?? "all";
            return Task.CompletedTask;
        }

        private IEnumerable<PageQualityIssueDto> FilterQualityIssues()
        {
            IEnumerable<PageQualityIssueDto> query = _qualityIssues;
            if (!string.Equals(_qualityFilterSeverity, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(issue => string.Equals(issue.Severity, _qualityFilterSeverity, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(_qualityFilterKind, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(issue => string.Equals(issue.Kind, _qualityFilterKind, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        private static string GetQualityIssueClass(PageQualityIssueDto issue)
        {
            string severity = issue.Severity?.ToLowerInvariant() ?? "info";
            return $"quality-item--{severity}";
        }

        private async Task RunQualityChecksAsync()
        {
            if (_activePage is null)
            {
                return;
            }

            _qualityLoading = true;
            _qualityError = null;
            _qualityFromCache = false;

            try
            {
                string scope = string.IsNullOrWhiteSpace(_qualityScope) ? "page" : _qualityScope;
                string? selectionText = null;
                if (string.Equals(scope, "selection", StringComparison.OrdinalIgnoreCase))
                {
                    selectionText = await GetSelectionTextAsync();
                    if (string.IsNullOrWhiteSpace(selectionText))
                    {
                        _qualityError = "Select text in the editor first.";
                        return;
                    }
                }

                QualityCheckRunRequest request = new(
                    scope,
                    selectionText,
                    false);

                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/pages/{_activePage.Id}/quality-checks/run", request);
                if (!response.IsSuccessStatusCode)
                {
                    _qualityError = "Quality checks failed.";
                    return;
                }

                QualityCheckRunResultDto? result = await response.Content.ReadFromJsonAsync<QualityCheckRunResultDto>();
                if (result is null)
                {
                    _qualityError = "Quality checks failed.";
                    return;
                }

                _qualityFromCache = result.FromCache;
                _qualityIssues.Clear();
                if (result.Issues.Count > 0)
                {
                    _qualityIssues.AddRange(result.Issues);
                }
            }
            catch (Exception ex)
            {
                _qualityError = $"Quality checks failed: {ex.Message}";
            }
            finally
            {
                _qualityLoading = false;
            }
        }

        private async Task DismissQualityIssueAsync(PageQualityIssueDto issue)
        {
            if (_activePage is null)
            {
                return;
            }

            try
            {
                using HttpResponseMessage response =
                    await Http.PostAsync(
                        $"api/pages/{_activePage.Id}/quality-checks/issues/{Uri.EscapeDataString(issue.IssueKey)}/dismiss",
                        null);
                if (!response.IsSuccessStatusCode)
                {
                    _qualityError = "Failed to dismiss issue.";
                    return;
                }

                _qualityIssues.RemoveAll(item => item.IssueKey == issue.IssueKey);
            }
            catch (Exception ex)
            {
                _qualityError = $"Failed to dismiss issue: {ex.Message}";
            }
        }

        private void OnAnnotationDraftInput(ChangeEventArgs args)
        {
            _annotationDraftContent = args.Value?.ToString() ?? string.Empty;
        }

        private string GetAnnotationSelectionLabel()
        {
            if (_currentSelectionRange is null || _currentSelectionRange.End <= _currentSelectionRange.Start)
            {
                return "Select text in the editor to add a comment, TODO, or highlight.";
            }

            int length = Math.Abs(_currentSelectionRange.End - _currentSelectionRange.Start);
            return $"Selected {length} characters.";
        }

        private async Task CreateAnnotationAsync(string kind)
        {
            if (_activePage is null || _currentSelectionRange is null)
            {
                _annotationActionError = "Select text in the editor first.";
                return;
            }

            if (_annotationSaving)
            {
                return;
            }

            _annotationActionError = null;

            SelectionDocRange docRange = await GetSelectionDocRangeAsync();
            int from = docRange.From;
            int to = docRange.To;
            if (to <= from)
            {
                _annotationActionError = "Select text in the editor first.";
                return;
            }

            bool isHighlight = string.Equals(kind, "highlight", StringComparison.OrdinalIgnoreCase);
            string content = isHighlight ? string.Empty : _annotationDraftContent.Trim();
            if (!isHighlight && string.IsNullOrWhiteSpace(content))
            {
                _annotationActionError = "Enter a comment or TODO before saving.";
                return;
            }

            string? anchorText = await GetSelectionTextAsync();
            if (string.IsNullOrWhiteSpace(anchorText))
            {
                anchorText = null;
            }

            _annotationSaving = true;
            try
            {
                PageAnnotationCreateRequest request = new(
                    kind,
                    from,
                    to,
                    anchorText,
                    content);

                using HttpResponseMessage response =
                    await Http.PostAsJsonAsync($"api/pages/{_activePage.Id}/annotations", request);
                if (!response.IsSuccessStatusCode)
                {
                    _annotationActionError = "Failed to create annotation.";
                    return;
                }

                PageAnnotationDto? created = await response.Content.ReadFromJsonAsync<PageAnnotationDto>();
                if (created is null)
                {
                    _annotationActionError = "Failed to create annotation.";
                    return;
                }

                _annotationDraftContent = string.Empty;
                _annotationFocusedId = created.Id;
                await LoadAnnotationsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Create annotation failed.");
                _annotationActionError = "Failed to create annotation.";
            }
            finally
            {
                _annotationSaving = false;
            }
        }

        private async Task ResolveAnnotationAsync(PageAnnotationDto annotation)
        {
            if (_activePage is null)
            {
                return;
            }

            try
            {
                using HttpResponseMessage response =
                    await Http.PostAsync($"api/pages/{_activePage.Id}/annotations/{annotation.Id}/resolve", null);
                if (!response.IsSuccessStatusCode)
                {
                    _annotationActionError = "Failed to resolve annotation.";
                    return;
                }

                await LoadAnnotationsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Resolve annotation failed.");
                _annotationActionError = "Failed to resolve annotation.";
            }
        }

        private async Task ReopenAnnotationAsync(PageAnnotationDto annotation)
        {
            if (_activePage is null)
            {
                return;
            }

            try
            {
                using HttpResponseMessage response =
                    await Http.PostAsync($"api/pages/{_activePage.Id}/annotations/{annotation.Id}/reopen", null);
                if (!response.IsSuccessStatusCode)
                {
                    _annotationActionError = "Failed to reopen annotation.";
                    return;
                }

                await LoadAnnotationsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Reopen annotation failed.");
                _annotationActionError = "Failed to reopen annotation.";
            }
        }

        private async Task UpdateAnnotationAnchorsAsync()
        {
            if (_annotationAnchorsUpdating || _activePage is null || _pageEditor is null || _annotations.Count == 0)
            {
                return;
            }

            _annotationAnchorsUpdating = true;
            try
            {
                IReadOnlyList<PageAnnotationAnchorUpdateRequest> updates = await _pageEditor.GetAnnotationAnchorsAsync();
                if (updates.Count == 0)
                {
                    return;
                }

                using HttpResponseMessage response =
                    await Http.PutAsJsonAsync($"api/pages/{_activePage.Id}/annotations/anchors", updates);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                foreach (PageAnnotationAnchorUpdateRequest update in updates)
                {
                    int index = _annotations.FindIndex(item => item.Id == update.Id);
                    if (index >= 0)
                    {
                        PageAnnotationDto existing = _annotations[index];
                        _annotations[index] = existing with
                        {
                            AnchorFrom = update.AnchorFrom,
                            AnchorTo = update.AnchorTo,
                            AnchorText = update.AnchorText
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Update annotation anchors failed.");
            }
            finally
            {
                _annotationAnchorsUpdating = false;
            }
        }

        private async Task<SelectionDocRange> GetSelectionDocRangeAsync()
        {
            if (_pageEditor is null)
            {
                return new SelectionDocRange(0, 0);
            }

            SelectionDocRange? range = await _pageEditor.GetSelectionDocRangeAsync();
            if (range is null)
            {
                return new SelectionDocRange(0, 0);
            }

            int from = Math.Min(range.From, range.To);
            int to = Math.Max(range.From, range.To);
            return new SelectionDocRange(from, to);
        }

        private async Task<string?> GetSelectionTextAsync()
        {
            if (_pageEditor is null)
            {
                return null;
            }

            return await _pageEditor.GetSelectionTextAsync();
        }

        private async Task ScrollAnnotationIntoViewAsync(Guid annotationId)
        {
            string elementId = $"annotation-item-{annotationId}";
            try
            {
                await JSRuntime.InvokeVoidAsync("tiptapEditor.scrollToElement", elementId);
            }
            catch (JSException)
            {
            }
        }

        private async Task OnAnnotationClickedAsync(Guid annotationId)
        {
            _annotationFocusedId = annotationId;
            _annotationFilterStatus = "all";
            _annotationFilterKind = "all";
            _activeContextTab = ContextTab.Annotations;
            await LoadAnnotationsAsync();
        }

        private static string GetAnnotationElementId(PageAnnotationDto annotation)
        {
            return $"annotation-item-{annotation.Id}";
        }

        private string GetAnnotationFocusClass(PageAnnotationDto annotation)
        {
            return _annotationFocusedId.HasValue && _annotationFocusedId.Value == annotation.Id
                ? "annotation-item--focused"
                : string.Empty;
        }

        private static string GetAnnotationItemClass(PageAnnotationDto annotation)
        {
            string kind = annotation.Kind?.ToLowerInvariant() ?? "comment";
            string status = annotation.Status?.ToLowerInvariant() ?? "open";
            return $"annotation-item--{kind} annotation-item--{status}";
        }

        private static string GetAnnotationKindLabel(string kind)
        {
            if (string.Equals(kind, "todo", StringComparison.OrdinalIgnoreCase))
            {
                return "TODO";
            }

            if (string.Equals(kind, "highlight", StringComparison.OrdinalIgnoreCase))
            {
                return "Highlight";
            }

            return "Comment";
        }

        private bool _canCompareVersions => _diffBaseVersionId.HasValue;

        private async Task SelectDiffBaseAsync(PageVersionListItemDto version)
        {
            _diffBaseVersionId = version.Id;
            _diffCompareVersionId = null;
            await LoadVersionDiffAsync();
        }

        private async Task LoadVersionDiffAsync()
        {
            if (_activePage is null || !_diffBaseVersionId.HasValue)
            {
                _diffError = "Select a base version to compare.";
                return;
            }

            _isDiffMode = true;
            _diffLoading = true;
            _diffError = null;
            _diffResult = null;
            _diffChangeBlocks.Clear();
            _diffSummary = DiffSummary.Empty;
            _diffChangeIndex = -1;

            try
            {
                string url = $"api/pages/{_activePage.Id}/versions/diff?fromVersionId={_diffBaseVersionId.Value}";
                if (_diffCompareVersionId.HasValue)
                {
                    url += $"&toVersionId={_diffCompareVersionId.Value}";
                }

                url += $"&granularity={_diffGranularity}";
                PageVersionDiffResultDto? result = await Http.GetFromJsonAsync<PageVersionDiffResultDto>(url);
                if (result is null)
                {
                    _diffError = "Diff not available.";
                    return;
                }

                _diffResult = result;
                RebuildDiffChangeList(result);
                await FocusDiffHeaderAsync();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to load version diff.");
                _diffError = "Failed to load diff.";
            }
            finally
            {
                _diffLoading = false;
            }
        }

        private void OnDiffBaseChanged(ChangeEventArgs args)
        {
            _diffBaseVersionId = TryParseGuid(args.Value);
        }

        private void OnDiffCompareChanged(ChangeEventArgs args)
        {
            _diffCompareVersionId = TryParseGuid(args.Value);
        }

        private async Task OnDiffGranularityChanged(ChangeEventArgs args)
        {
            _diffGranularity = args.Value?.ToString() ?? "word";
            if (_diffBaseVersionId.HasValue)
            {
                await LoadVersionDiffAsync();
            }
        }

        private async Task OnDiffViewModeChanged(ChangeEventArgs args)
        {
            _diffViewMode = args.Value?.ToString() ?? "inline";
            await InvokeAsync(StateHasChanged);
        }

        private void OnDiffShowDeletionsChanged(ChangeEventArgs args)
        {
            if (args.Value is bool flag)
            {
                _diffShowDeletions = flag;
                return;
            }

            if (args.Value is string text)
            {
                _diffShowDeletions = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "on", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void OnDiffSyncScrollChanged(ChangeEventArgs args)
        {
            if (args.Value is bool flag)
            {
                _diffSyncScroll = flag;
                return;
            }

            if (args.Value is string text)
            {
                _diffSyncScroll = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "on", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void CloseDiffMode()
        {
            _isDiffMode = false;
        }

        private async Task SwapDiffVersionsAsync()
        {
            if (!_diffBaseVersionId.HasValue)
            {
                return;
            }

            Guid? temp = _diffCompareVersionId;
            _diffCompareVersionId = _diffBaseVersionId;
            _diffBaseVersionId = temp;

            await LoadVersionDiffAsync();
        }

        private async Task FocusDiffHeaderAsync()
        {
            try
            {
                await _diffHeaderRef.FocusAsync();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private async Task OnDiffPaneScrolled(string sourceId)
        {
            if (!_diffSyncScroll || _diffSyncInProgress)
            {
                return;
            }

            _diffSyncInProgress = true;
            try
            {
                string targetId = string.Equals(sourceId, "diff-pane-left", StringComparison.Ordinal)
                    ? "diff-pane-right"
                    : "diff-pane-left";
                await JSRuntime.InvokeVoidAsync("tiptapEditor.syncDiffScroll", sourceId, targetId);
            }
            catch (JSException)
            {
            }
            finally
            {
                _diffSyncInProgress = false;
            }
        }

        private async Task OnDiffKeyDown(KeyboardEventArgs args)
        {
            if (args.Key == "Escape")
            {
                CloseDiffMode();
                return;
            }

            if (args.Key == "F7" && args.ShiftKey)
            {
                await GoToPreviousChange();
                return;
            }

            if (args.Key == "F7")
            {
                await GoToNextChange();
            }
        }

        private void PromptRestoreVersion(PageVersionListItemDto version)
        {
            _pendingRestoreVersion = version;
            _restoreError = null;
            _isRestoreDialogOpen = true;
        }

        private void CancelRestoreVersion()
        {
            _pendingRestoreVersion = null;
            _restoreError = null;
            _isRestoreDialogOpen = false;
        }

        private async Task ConfirmRestoreVersionAsync()
        {
            if (_pendingRestoreVersion is null || _activePage is null || _restoreInFlight)
            {
                CancelRestoreVersion();
                return;
            }

            _restoreInFlight = true;
            _restoreError = null;

            try
            {
                using HttpResponseMessage response = await Http.PostAsync(
                    $"api/pages/{_activePage.Id}/versions/{_pendingRestoreVersion.Id}/restore",
                    null);

                if (!response.IsSuccessStatusCode)
                {
                    _restoreError = "Restore failed.";
                    return;
                }

                PageDto? updated = await response.Content.ReadFromJsonAsync<PageDto>();
                if (updated is null)
                {
                    _restoreError = "Restore failed.";
                    return;
                }

                _activePage = updated;
                if (_pagesBySection.TryGetValue(updated.SectionId, out List<PageDto>? pages) && pages.Count > 0)
                {
                    pages[0] = updated;
                }

                if (_pageEditor is not null)
                {
                    await _pageEditor.SetContentAsync(updated.Content ?? string.Empty, markDirty: false);
                }

                await LoadPageVersionsAsync();
                CancelRestoreVersion();
            }
            catch (Exception ex)
            {
                _restoreError = $"Restore failed: {ex.Message}";
            }
            finally
            {
                _restoreInFlight = false;
            }
        }

        private static string GetVersionReasonLabel(string reason)
        {
            if (string.Equals(reason, "pre-ai", StringComparison.OrdinalIgnoreCase))
            {
                return "Pre-AI";
            }

            if (string.Equals(reason, "pre-restore", StringComparison.OrdinalIgnoreCase))
            {
                return "Pre-restore";
            }

            if (string.Equals(reason, "autosnap", StringComparison.OrdinalIgnoreCase))
            {
                return "Autosnap";
            }

            return string.IsNullOrWhiteSpace(reason) ? "Snapshot" : reason;
        }

        private static Guid? TryParseGuid(object? value)
        {
            if (value is Guid guid)
            {
                return guid;
            }

            if (value is string text && Guid.TryParse(text, out Guid parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string GetDiffClass(string? kind)
        {
            return kind?.ToLowerInvariant() switch
            {
                "added" => "is-added",
                "removed" => "is-removed",
                "changed" => "is-changed",
                "empty" => "is-empty",
                _ => "is-unchanged"
            };
        }

        private static string GetDiffPrefix(string? kind)
        {
            return kind?.ToLowerInvariant() switch
            {
                "added" => "+",
                "removed" => "-",
                _ => " "
            };
        }

        private static string GetDiffSpanClass(string? kind)
        {
            return kind?.ToLowerInvariant() switch
            {
                "added" => "is-added",
                "removed" => "is-removed",
                _ => "is-unchanged"
            };
        }

        private RenderFragment RenderDiffSegments(
            IReadOnlyList<PageVersionDiffSpanDto>? segments,
            bool hideRemoved = false) => builder =>
        {
            if (segments is null || segments.Count == 0)
            {
                return;
            }

            int seq = 0;
            foreach (PageVersionDiffSpanDto span in segments)
            {
                if (hideRemoved && string.Equals(span.Kind, "removed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                builder.OpenElement(seq++, "span");
                builder.AddAttribute(seq++, "class", $"version-diff-span {GetDiffSpanClass(span.Kind)}");
                builder.AddContent(seq++, span.Text);
                builder.CloseElement();
            }
        };

        private static IReadOnlyList<PageVersionDiffSpanDto> GetInlineSegments(PageVersionDiffBlockDto block)
        {
            if (block.InlineSegments is not null && block.InlineSegments.Count > 0)
            {
                return block.InlineSegments;
            }

            if (block.Compare is not null && !string.IsNullOrWhiteSpace(block.Compare.Text))
            {
                string kind = string.Equals(block.Status, "added", StringComparison.OrdinalIgnoreCase)
                    ? "added"
                    : "unchanged";
                return new[] { new PageVersionDiffSpanDto(kind, block.Compare.Text) };
            }

            if (block.Base is not null && !string.IsNullOrWhiteSpace(block.Base.Text))
            {
                return new[] { new PageVersionDiffSpanDto("removed", block.Base.Text) };
            }

            return Array.Empty<PageVersionDiffSpanDto>();
        }

        private static IReadOnlyList<PageVersionDiffSpanDto> GetBaseSegments(PageVersionDiffBlockDto block)
        {
            if (block.Base is null)
            {
                return Array.Empty<PageVersionDiffSpanDto>();
            }

            if (block.Base.Segments is not null && block.Base.Segments.Count > 0)
            {
                return block.Base.Segments;
            }

            string kind = string.Equals(block.Status, "removed", StringComparison.OrdinalIgnoreCase)
                ? "removed"
                : "unchanged";
            return new[] { new PageVersionDiffSpanDto(kind, block.Base.Text) };
        }

        private static IReadOnlyList<PageVersionDiffSpanDto> GetCompareSegments(PageVersionDiffBlockDto block)
        {
            if (block.Compare is null)
            {
                return Array.Empty<PageVersionDiffSpanDto>();
            }

            if (block.Compare.Segments is not null && block.Compare.Segments.Count > 0)
            {
                return block.Compare.Segments;
            }

            string kind = string.Equals(block.Status, "added", StringComparison.OrdinalIgnoreCase)
                ? "added"
                : "unchanged";
            return new[] { new PageVersionDiffSpanDto(kind, block.Compare.Text) };
        }

        private void RebuildDiffChangeList(PageVersionDiffResultDto result)
        {
            _diffChangeBlocks.Clear();
            _diffChangeBlocks.AddRange(BuildDiffChangeBlocks(result.Blocks));
            _diffSummary = new DiffSummary(
                result.Stats.AddedWords,
                result.Stats.RemovedWords,
                result.Stats.ChangedBlocks,
                result.Stats.AddedBlocks,
                result.Stats.RemovedBlocks);
            _diffChangeIndex = _diffChangeBlocks.Count > 0 ? 0 : -1;
        }

        private static IReadOnlyList<DiffChangeBlock> BuildDiffChangeBlocks(
            IReadOnlyList<PageVersionDiffBlockDto> blocks)
        {
            List<DiffChangeBlock> changes = new();

            foreach (PageVersionDiffBlockDto block in blocks)
            {
                if (string.Equals(block.Status, "unchanged", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                changes.Add(new DiffChangeBlock(block.Id, block.Status, BuildPreview(block.PreviewText)));
            }

            return changes;
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            int count = 0;
            bool inWord = false;
            foreach (char ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (!inWord)
                    {
                        count++;
                        inWord = true;
                    }
                }
                else
                {
                    inWord = false;
                }
            }

            return count;
        }

        private static string BuildPreview(string text)
        {
            string trimmed = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (trimmed.Length <= 120)
            {
                return trimmed;
            }

            return trimmed[..117] + "...";
        }

        private static string GetDiffAnchorId(string blockId)
        {
            return $"diff-block-{blockId}";
        }

        private string GetDiffPaneSubtitle(Guid? versionId)
        {
            if (versionId is null)
            {
                return "Current draft";
            }

            PageVersionListItemDto? version = _pageVersions.FirstOrDefault(item => item.Id == versionId.Value);
            if (version is null)
            {
                return "Version";
            }

            return $"{version.CreatedAt.ToLocalTime():g} � {GetVersionReasonLabel(version.Reason)} � {version.WordCount} words";
        }

        private static string FormatVersionLabel(PageVersionListItemDto version)
        {
            return $"{version.CreatedAt.ToLocalTime():g} � {GetVersionReasonLabel(version.Reason)} � {version.WordCount} words";
        }

        private async Task GoToNextChange()
        {
            if (_diffChangeBlocks.Count == 0)
            {
                return;
            }

            _diffChangeIndex = (_diffChangeIndex + 1) % _diffChangeBlocks.Count;
            await JumpToChange(_diffChangeBlocks[_diffChangeIndex]);
        }

        private async Task GoToPreviousChange()
        {
            if (_diffChangeBlocks.Count == 0)
            {
                return;
            }

            _diffChangeIndex = (_diffChangeIndex - 1 + _diffChangeBlocks.Count) % _diffChangeBlocks.Count;
            await JumpToChange(_diffChangeBlocks[_diffChangeIndex]);
        }

        private async Task JumpToChange(DiffChangeBlock block)
        {
            _diffChangeIndex = _diffChangeBlocks.IndexOf(block);
            if (!_diffShowDeletions && string.Equals(block.Kind, "removed", StringComparison.OrdinalIgnoreCase))
            {
                _diffShowDeletions = true;
                await InvokeAsync(StateHasChanged);
            }
            string anchorId = GetDiffAnchorId(block.BlockId);
            try
            {
                await JSRuntime.InvokeVoidAsync("tiptapEditor.scrollToElement", anchorId);
            }
            catch (JSException)
            {
            }
        }

        private string GetDiffChangeClass(DiffChangeBlock block)
        {
            int index = _diffChangeBlocks.IndexOf(block);
            string kindClass = block.Kind switch
            {
                "added" => "is-added",
                "removed" => "is-removed",
                _ => "is-changed"
            };

            return index == _diffChangeIndex ? $"is-active {kindClass}" : kindClass;
        }

        private void PromptRestoreVersionById(Guid versionId)
        {
            PageVersionListItemDto? version = _pageVersions.FirstOrDefault(item => item.Id == versionId);
            if (version is null)
            {
                return;
            }

            PromptRestoreVersion(version);
        }

        private async Task LoadTranslationLinksAsync()
        {
            _documentTranslationLinks.Clear();
            _sectionTranslationLinks.Clear();

            if (_documentTranslationGroupId is null && _sectionTranslationGroupId is null)
            {
                return;
            }

            try
            {
                if (_documentTranslationGroupId is not null)
                {
                    List<DocumentTranslationLinkDto>? documents =
                        await Http.GetFromJsonAsync<List<DocumentTranslationLinkDto>>(
                            $"api/documents/{DocumentId}/translations");
                    if (documents is not null)
                    {
                        _documentTranslationLinks.AddRange(documents);
                    }
                }

                if (_sectionTranslationGroupId is not null && _activeSection is not null)
                {
                    List<SectionTranslationLinkDto>? sections =
                        await Http.GetFromJsonAsync<List<SectionTranslationLinkDto>>(
                            $"api/sections/{_activeSection.Id}/translations");
                    if (sections is not null)
                    {
                        _sectionTranslationLinks.AddRange(sections);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Translation links load failed.");
            }
        }

        private async Task LoadHeadingPrefixCountersAsync()
        {
            if (_activePage is null)
            {
                _headingPrefixCounters = new int[7];
                return;
            }

            try
            {
                await DebugHeadingLogAsync("FETCH_PREFIX_START", new
                {
                    traceId = _headingTraceId,
                    documentId = DocumentId,
                    sectionId = _activeSection?.Id,
                    pageId = _activePage.Id
                });

                using HttpRequestMessage request = new(
                    HttpMethod.Get,
                    $"api/documents/{DocumentId}/heading-outline?upToPageId={_activePage.Id}");
                request.Headers.Add("X-Trace-Id", _headingTraceId);
                using HttpResponseMessage response = await Http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                HeadingPrefixCountersDto? payload = await response.Content.ReadFromJsonAsync<HeadingPrefixCountersDto>();
                _headingPrefixCounters = payload?.Counters?.ToArray() ?? new int[7];

                await DebugHeadingLogAsync("FETCH_PREFIX_SUCCESS", new
                {
                    traceId = _headingTraceId,
                    counters = _headingPrefixCounters.Skip(1).ToArray()
                });

                Logger.LogDebug(
                    "HeadingPrefix ClientLoaded TraceId={TraceId} DocumentId={DocumentId} PageId={PageId} Length={Length}",
                    _headingTraceId,
                    DocumentId,
                    _activePage.Id,
                    _activePage.Content?.Length ?? 0);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Heading prefix counters load failed.");
                _headingPrefixCounters = new int[7];
                await DebugHeadingLogAsync("FETCH_PREFIX_FAIL", new
                {
                    traceId = _headingTraceId,
                    error = ex.Message
                });
            }
        }

        private async Task DebugHeadingLogAsync(string stage, object payload)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("tiptapEditor.debugLog", stage, payload);
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (JSException)
            {
            }
        }

        private static string ComputeShortHash(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "0";
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash.AsSpan(0, 4));
        }

        private IEnumerable<TranslationLinkItem> GetTranslationLinks()
        {
            if (_sectionTranslationLinks.Count > 0)
            {
                foreach (SectionTranslationLinkDto link in _sectionTranslationLinks)
                {
                    bool isActive = _activeSection is not null && link.SectionId == _activeSection.Id;
                    string label = BuildLanguageLabel(link.LanguageCode);
                    yield return new TranslationLinkItem(
                        link.DocumentId,
                        link.SectionId,
                        label,
                        link.Title,
                        isActive);
                }

                yield break;
            }

            foreach (DocumentTranslationLinkDto link in _documentTranslationLinks)
            {
                bool isActive = link.DocumentId == DocumentId;
                string label = BuildLanguageLabel(link.LanguageCode);
                yield return new TranslationLinkItem(
                    link.DocumentId,
                    null,
                    label,
                    link.Title,
                    isActive);
            }
        }

        private async Task NavigateToTranslation(TranslationLinkItem item)
        {
            if (item.SectionId.HasValue)
            {
                Navigation.NavigateTo($"/documents/{item.DocumentId}/sections/{item.SectionId.Value}");
                return;
            }

            List<SectionDto>? sections = await Http.GetFromJsonAsync<List<SectionDto>>(
                $"api/documents/{item.DocumentId}/sections");
            SectionDto? target = sections?.OrderBy(section => section.OrderIndex).FirstOrDefault();
            if (target is not null)
            {
                Navigation.NavigateTo($"/documents/{item.DocumentId}/sections/{target.Id}");
            }
        }

        private static string BuildLanguageLabel(string? languageCode)
        {
            return string.IsNullOrWhiteSpace(languageCode)
                ? "??"
                : languageCode.Trim().ToUpperInvariant();
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

        private sealed record TranslationApplyOption(string Value, string Label);

        private sealed record TranslationLinkItem(
            Guid DocumentId,
            Guid? SectionId,
            string Label,
            string Title,
            bool IsActive);

        private sealed record TranslateContext(
            string PlainText,
            TextRange SelectionRange,
            string SelectionText);

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

        private sealed record DiffChangeBlock(string BlockId, string Kind, string Preview)
        {
            public string KindLabel => Kind switch
            {
                "added" => "Added",
                "removed" => "Removed",
                _ => "Changed"
            };
        }

        private sealed record DiffSummary(
            int AddedWords,
            int RemovedWords,
            int ChangedBlocks,
            int AddedBlocks,
            int RemovedBlocks)
        {
            public static DiffSummary Empty => new(0, 0, 0, 0, 0);
        }

        private enum ContextTab
        {
            Notes,
            Scene,
            Outline,
            Ai,
            Annotations,
            Quality,
            History
        }
    }
}

