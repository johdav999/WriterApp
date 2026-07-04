using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Continuity;
using WriterApp.Application.Documents;
using WriterApp.Application.Exporting;
using WriterApp.Application.Subscriptions;
using WriterApp.Client.Diagnostics;
using WriterApp.Client.State;
using WriterApp.Application.Usage;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Services;
using WriterApp.Shared;
using WriterApp.Shared.Localization;
using SelectionDocRange = WriterApp.Client.Components.Editor.PageEditor.SelectionDocRange;

namespace WriterApp.Client.Pages
{
    public partial class DocumentEditor : ComponentBase, IDisposable
    {
        private const string ContextPanelStateStoragePrefix = "writerapp.editor.contextpanel.v1";
        private const int OnboardingMinTypedCharacters = 100;
        private const string OnboardingDemoSceneHtml =
            "<p>The Café was quiet that afternoon, the kind of quiet that settles softly between the clink of cups and the low murmur of strangers. Outside, the street moved slowly through a pale autumn light.</p>"
            + "<p>He had chosen the table by the window without thinking much about it. It was simply where he always sat when he came here - close enough to watch the world passing by, far enough away from everyone else.</p>"
            + "<p>He noticed her only after she had already been sitting there for several minutes.</p>"
            + "<p>She was across the room, near the bookshelf, a cup of coffee resting untouched in front of her. She was reading something on her phone, though from time to time her eyes lifted, drifting around the room as if searching for something she couldn't quite name.</p>"
            + "<p>At one of those moments their eyes met.</p>";
        private const string OnboardingDemoAiInstruction =
            "Tighten the character description. Focus especially on the woman across the room. Sharpen the visual details and make the prose more precise without rewriting the whole scene. Return only the revised section text.";

        [Parameter]
        public Guid DocumentId { get; set; }

        [Parameter]
        public Guid SectionId { get; set; }

        [Parameter]
        public Guid ProjectId { get; set; }

        [Parameter]
        public Guid SceneNodeId { get; set; }

        [SupplyParameterFromQuery(Name = "search")]
        public string? SearchQuery { get; set; }

        [Inject]
        public GlobalSearchNavigationService GlobalSearchNavigationService { get; set; } = default!;

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
        public CurrentSceneStateService CurrentSceneStateService { get; set; } = default!;

        [Inject]
        public CurrentProjectStateService CurrentProjectStateService { get; set; } = default!;

        [Inject]
        public ProjectProgressCacheService ProjectProgressCacheService { get; set; } = default!;

        [Inject]
        public AuthMeStateService AuthMeStateService { get; set; } = default!;

        [Inject]
        internal FeatureAccessService FeatureAccessService { get; set; } = default!;

        [Inject]
        public LastOpenedDocumentStateService LastOpenedDocumentStateService { get; set; } = default!;

        [Inject]
        public IJSRuntime JSRuntime { get; set; } = default!;

        [Inject]
        public IConfiguration Configuration { get; set; } = default!;

        [Inject]
        public CoachRecommendationService CoachRecommendationService { get; set; } = default!;

        [Inject]
        public OnboardingService OnboardingService { get; set; } = default!;

        [Inject]
        public AiCommandStatusService AiCommandStatusService { get; set; } = default!;

        [Inject]
        public OnboardingStateStore OnboardingStateStore { get; set; } = default!;

        [Inject]
        public OnboardingOverlayStateService OnboardingOverlayStateService { get; set; } = default!;

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
        private bool _isImportDialogOpen;
        private Guid _importTargetSectionId;
        private string _importMode = "replace";
        private bool _importNormalizeWhitespace = true;
        private bool _importPreserveTxtLineBreaks;
        private IBrowserFile? _importFile;
        private string? _importFileName;
        private bool _isImporting;
        private string? _importError;
        private string? _importSummary;
        private Guid _loadedDocumentId;
        private string _documentTitle = string.Empty;
        private string? _documentLanguageCode;
        private Guid? _documentTranslationGroupId;
        private string? _sectionLanguageCode;
        private Guid? _sectionTranslationGroupId;
        private readonly List<DocumentTranslationLinkDto> _documentTranslationLinks = new();
        private readonly List<SectionTranslationLinkDto> _sectionTranslationLinks = new();
        private bool _layoutStateInitialized;
        private bool _isPreviewMode;
        private PageEditor? _pageEditor;
        private Projects? _navigatorPanel;
        private PageEditor.EditorStatusSnapshot _editorStatus = PageEditor.EditorStatusSnapshot.Empty;
        private DateTimeOffset _nextNavigatorRefreshUtc = DateTimeOffset.MinValue;
        private string _headingTraceId = string.Empty;
        private string _activeSearchQuery = string.Empty;
        private bool _pendingSearchTargetFocus;
        private Guid _loadedRouteProjectId;
        private Guid _loadedRouteSceneNodeId;
        private Guid? _draggedSectionId;
        private bool _isReorderingSections;
        private bool _dndDebugEnabled;
        private string _dndDebugStatus = "idle";
        private Guid? _dndDebugLastTargetId;
        private Guid? _dndDebugLastDraggedId;
        private DateTimeOffset? _dndDebugLastAt;
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
        private bool _isLinkContextMenuOpen;
        private double _linkContextMenuX;
        private double _linkContextMenuY;
        private string? _linkContextMenuHref;
        private bool _isToolbarOverflowOpen;
        private bool _toolbarOverflowNeedsPositioning;
        private bool _toolbarOverflowAlignLeft;
        private bool _toolbarOverflowOpenUpward;
        private ElementReference _toolbarOverflowButtonRef;
        private ElementReference _toolbarOverflowPanelRef;
        private AiCommandStatusSnapshot _aiCommandStatus = AiCommandStatusSnapshot.Empty;
        private bool _isDocumentMenuOpen;
        private string _imageUploadInputKey = Guid.NewGuid().ToString("N");
        private bool _isFeedbackDialogOpen;
        private bool _feedbackSubmitting;
        private string _feedbackType = "bug";
        private string _feedbackSubject = string.Empty;
        private string _feedbackDescription = string.Empty;
        private bool _feedbackIncludeDiagnostics = true;
        private string? _feedbackErrorMessage;
        private string? _feedbackBannerMessage;
        private bool _focusFeedbackDialogOnRender;
        private ElementReference _feedbackTypeSelectRef;
        private ElementReference _feedbackSubmitButtonRef;
        private string? _imageUploadError;
        private bool _isExportDialogOpen;
        private bool _isTemplateManagerOpen;
        private bool _isTemplateEditorOpen;
        private bool _isTemplatesLoading;
        private bool _isTemplateSaving;
        private bool _isTemplateDeleting;
        private bool _docxExportEnabled;
        private bool _epubExportEnabled;
        private int[] _headingPrefixCounters = new int[7];
        private string? _templateLoadError;
        private string? _templateActionError;
        private readonly List<ExportTemplateDto> _exportTemplates = new();
        private readonly List<ExportPresetDto> _exportPresets = new();
        private const string NoTemplateOptionValue = "__default__";
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
        private Guid? _synopsisPreviewCacheDocumentId;
        private string? _synopsisPreviewCacheHtml;
        private DotNetObjectReference<DocumentEditor>? _previewScrollRef;
        private string _exportContentSelection = "document";
        private string _exportScopeType = "document";
        private bool _exportIncludeTitlePage = true;
        private bool _exportIncludeCover = true;
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
                "Generate a 10-12 sentence continuation based on current section + scene beats."),
            new AiActionOption(
                "expand.selection",
                "Expand selection",
                "Expand selection",
                true,
                new Dictionary<string, object?>(),
                "Add detail while preserving intent.",
                true),
            new AiActionOption(
                "expand.section",
                "Expand section",
                "Expand section",
                false,
                new Dictionary<string, object?>(),
                "Add detail while preserving intent.",
                true),
            new AiActionOption(
                "show_dont_tell.selection",
                "Show, don't tell (selection)",
                "Show, don't tell",
                true,
                new Dictionary<string, object?>(),
                "Rewrite abstractions into concrete, sensory prose.",
                true),
            new AiActionOption(
                "show_dont_tell.section",
                "Show, don't tell (section)",
                "Show, don't tell",
                false,
                new Dictionary<string, object?>(),
                "Rewrite abstractions into concrete, sensory prose.",
                true)
        };
        private readonly List<PromptPresetDto> _promptPresets = new();
        private readonly List<Guid> _pinnedPromptPresetIds = new();
        private string? _promptStatus;
        private Guid? _promptEditingId;
        private string _promptNameDraft = string.Empty;
        private string _promptCategoryDraft = string.Empty;
        private string _promptKindDraft = "builtin";
        private string _promptBuiltinActionIdDraft = "rewrite.selection";
        private string _promptTemplateDraft = string.Empty;
        private string _promptParametersDraft = "{}";
        private string _promptRunScope = "selection";
        private ContinuityReport? _continuityReport;
        private string _continuitySeverityFilter = "all";
        private string? _continuityStatus;
        private bool _continuityBusy;
        private string? _selectedContinuityIssueKey;
        private bool _pendingContinuityHighlights;
        private bool _isContinuityProposalOpen;
        private ContinuityIssue? _pendingContinuityIssue;
        private ContinuityProposalPreview? _continuityProposalPreview;
        private string? _continuityProposalError;
        private bool _isApplyingContinuityProposal;
        private ContinuityApplyRange? _pendingContinuityRange;
        private BibleSnapshotDto? _characterBibleSnapshot;
        private BibleSnapshotDto? _placeBibleSnapshot;
        private BibleSnapshotDto? _timelineBibleSnapshot;
        private readonly HashSet<string> _availableActionKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<AiHistoryEntry> _aiHistoryEntries = new();
        private Guid? _expandedAiHistoryId;
        private HistoryFilter _historyFilter = HistoryFilter.All;
        private AiUsageStatusDto? _aiUsageStatus;
        private bool _aiUsageRefreshInProgress;
        private bool _canShowAiMenu;
        private bool? _lastAiMenuVisibility;
        private bool _isTranslateModalOpen;
        private AiActionOption? _pendingTranslateAction;
        private string _translateSourceLanguage = "auto";
        private string _translateTargetLanguage = "en";
        private string _translateSourceLanguageQuery = string.Empty;
        private string _translateTargetLanguageQuery = string.Empty;
        private string _translateStyle = "natural";
        private string _translationAlignmentMode = "paragraph";
        private string _translationApplyMode = "replace";
        private TranslateContext? _pendingTranslateContext;
        private ContextTab _activeContextTab = ContextTab.Ai;
        private PanelCategory _activePanelCategory = PanelCategory.Coach;
        private bool _showOnboardingWalkthrough;
        private int _onboardingWalkthroughIndex;
        private bool _onboardingWalkthroughBusy;
        private string? _onboardingWalkthroughStatus;
        private bool _onboardingStarterTextEnsured;
        private bool _onboardingAiDemoAttempted;
        private bool _onboardingProjectCreated;
        private bool _onboardingTypedEnough;
        private bool _onboardingSavedOnce;
        private bool _onboardingAiRequirementMet;
        private bool _onboardingCompletionInFlight;
        private DateTimeOffset _onboardingLastTypingProbeUtc = DateTimeOffset.MinValue;
        private int _onboardingMeasuredCharacterCount;
        private IReadOnlyList<OnboardingWalkthroughTip> _onboardingWalkthroughTips = new List<OnboardingWalkthroughTip>
        {
            new(3, "Welcome", "Welcome — let's start writing.", "#onboarding-editor-scene", false),
            new(4, "Project structure", "Your starter structure is ready in this project.", "#onboarding-project-structure", false),
            new(5, "AI Coach example", "Use Writing tools to try a rewrite or guided AI action.", "#onboarding-tab-ai", true)
        };
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
        private string _diffViewMode = "side";
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
        private string? _qualityStatus;
        private bool _qualityFromCache;
        private bool _qualityHasRunOnce;
        private string _qualityScope = "page";
        private string _qualityFilterSeverity = "all";
        private string _qualityFilterKind = "all";
        private bool _isStyleQualityTabActive;
        private string? _selectedQualityIssueKey;
        private readonly HashSet<string> _qualityApplyingIssueKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _qualityAppliedIssueKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _qualityMetaLeakWarnedIssueKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _qualityIssueActionErrors = new(StringComparer.Ordinal);
        private bool _isQualityProposalOpen;
        private PageQualityIssueDto? _proposalIssue;
        private string? _proposalError;
        private bool _isProposalApplying;
        private QualityProposalPreview? _proposalPreview;
        private string _notesDraft = string.Empty;
        private string? _notesStatus;
        private string? _notesError;
        private DateTimeOffset? _notesLastSavedAtUtc;
        private CancellationTokenSource? _notesAutosaveCts;
        private CancellationTokenSource? _notesRetryCts;
        private bool _notesSaveInFlight;
        private bool _notesSaveQueued;
        private int _notesEditVersion;
        private int _notesSavedVersion;
        private int _notesRetryAttempt;
        private string _sceneNarrativeRole = string.Empty;
        private string _sceneNarrativeIntent = string.Empty;
        private string _sceneSummary = string.Empty;
        private string _sceneCardMetadataStatus = "Draft";
        private string _sceneEmotionalBeat = string.Empty;
        private string _sceneKeyEvents = string.Empty;
        private string _sceneOpenQuestions = string.Empty;
        private string _scenePovCharacterId = string.Empty;
        private string _sceneSubplotTagsText = string.Empty;
        private string _scenePlaceId = string.Empty;
        private string _sceneTimelineEventId = string.Empty;
        private string _sceneTimeRef = string.Empty;
        private string _sceneTagsText = string.Empty;
        private string _sceneReferencesJson = string.Empty;
        private string? _sceneStatus;
        private bool _sceneSaveInFlight;
        private CancellationTokenSource? _sceneAutosaveCts;
        private Guid? _sceneCardSectionId;
        private SectionSceneCardProposalDto? _sceneAiProposal;
        private string? _sceneAiExplanation;
        private Guid? _sceneAiProposalId;
        private string _sceneAiSelectedField = "summary";
        private string? _sceneAiProposalFieldKey;
        private string? _sceneAiError;
        private bool _sceneAiInFlight;

        private bool IsExportDialogLoading => _isTemplatesLoading || _isPresetsLoading;
        private PendingAiProposal? _pendingAiProposal;
        private AiSelectionSnapshot? _lastAiSelectionSnapshot;
        private bool _pendingDetailsExpanded;
        private bool _aiUndoRedoInFlight;
        private bool _hasAiUndoHistory;
        private bool _hasAiRedoHistory;
        private bool _isAiQuotaDialogOpen;
        private string _aiQuotaPlanName = "Free";
        private int _aiQuotaBudget;
        private int _aiQuotaUsed;
        private string _aiQuotaMessage = "AI quota exceeded. Upgrade to continue.";
        private bool _isEntitlementUpgradeDialogOpen;
        private string _entitlementUpgradeUrl = "/upgrade?feature=ai.bibles.refresh";
        private string _entitlementUserMessage = "Upgrade to enable this feature.";
        private string _entitlementFeatureKey = "ai.bibles.refresh";
        private string? _lastReorderStatus;
        private int _lastReorderCount;
        private string? _lastReorderCorrelationId;
        private bool _sectionReorderDiagnosticsEnabled;
        private IJSObjectReference? _exportModule;
        private const int SectionTitleMaxLength = 120;
        private const int PageBreakHeightPx = 980;
        private const int PageBreakGutterOffsetPx = 28;
        private const int PageBreakGapPx = 0;
        private const int PagePaddingX = 20;
        private const int PagePaddingY = 24;
        private static readonly TimeSpan NotesAutosaveDebounce = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan[] NotesAutosaveRetryDelays =
        {
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30)
        };
        private static readonly TimeSpan SceneCardAutosaveDebounce = TimeSpan.FromSeconds(2.5);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly string[] SceneCardStatusOptions = ["Idea", "Draft", "Revised", "Final"];
        private static readonly string[] SceneNarrativeRoleOptions =
            [string.Empty, .. SceneNarrativeRoleCatalog.Values];

        private PageEditor.PageBreakOptions PageBreaks
        {
            get
            {
                LayoutState state = LayoutStateService.State;
                string mode = state.PrintLayoutEnabled ? "print" : "simple";
                bool showRule = false;
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
        private IEnumerable<AiActionOption> RecommendedAiActions =>
            _aiActions.Where(action => action.IsRecommended && action.IncludeInLists);
        private IEnumerable<AiActionOption> SelectionAiActions =>
            _aiActions.Where(action => action.RequiresSelection && action.IncludeInLists && !action.IsRecommended);
        private IEnumerable<AiActionOption> SectionAiActions =>
            _aiActions.Where(action => !action.RequiresSelection && action.IncludeInLists && !action.IsRecommended);
        private bool CanShowContinuityCoach =>
            HasAction("continuity.extract_character_bible")
            || HasAction("continuity.extract_place_bible")
            || HasAction("continuity.extract_timeline_bible")
            || HasAction("continuity.check_section");
        private bool CanShowContinuityCoachFixes =>
            HasAction("continuity.apply_fix");
        private bool CanRunExtractCharacterBible => HasAction("continuity.extract_character_bible");
        private bool CanRunExtractPlaceBible => HasAction("continuity.extract_place_bible");
        private bool CanRunExtractTimelineBible => HasAction("continuity.extract_timeline_bible");
        private bool CanRunContinuityCheck => HasAction("continuity.check_section");
        private bool CanRunBibleUpdate =>
            CanRunExtractCharacterBible
            && CanRunExtractPlaceBible
            && CanRunExtractTimelineBible;
        private bool CanDisplayContinuityCoach =>
            CanShowContinuityCoach
            || !CanUseFeature(FeatureKey.ContinuityCheck)
            || !CanUseFeature(FeatureKey.CanonRefresh);
        private bool CanShowPromptLibrary =>
            HasAction("custom_transform")
            && CanUseFeature(FeatureKey.PromptLibrary);
        private bool CanDisplayPromptLibrary =>
            HasAction("custom_transform")
            || !CanUseFeature(FeatureKey.PromptLibrary);
        private bool CanShowQualityChecks =>
            CanUseFeature(FeatureKey.QualityChecks);
        private bool CanDisplayQualityChecks => true;
        private bool CanShowVersionHistory =>
            CanUseFeature(FeatureKey.VersionHistory);
        private bool CanDisplayVersionHistory => true;
        private IReadOnlyList<PromptPresetDto> PinnedPromptPresets =>
            _pinnedPromptPresetIds
                .Select(id => _promptPresets.FirstOrDefault(preset => preset.Id == id))
                .Where(preset => preset is not null)
                .Cast<PromptPresetDto>()
                .ToList();
        private IEnumerable<ContinuityIssue> FilteredContinuityIssues =>
            (_continuityReport?.Issues ?? Array.Empty<ContinuityIssue>())
            .Where(issue => string.Equals(_continuitySeverityFilter, "all", StringComparison.OrdinalIgnoreCase)
                || string.Equals(issue.Severity, _continuitySeverityFilter, StringComparison.OrdinalIgnoreCase));
        private bool IsTranslationProposal => IsTranslationActionKey(_pendingAiProposal?.ActionKey);
        private bool ShowTranslationSwitcher => GetTranslationLinks().Any(item => !item.IsActive);
        private bool IsSceneRoute => ProjectId != Guid.Empty && SceneNodeId != Guid.Empty;

        protected override async Task OnInitializedAsync()
        {
            AuthMeStateService.Changed += OnAuthMeStateChanged;
            CurrentSceneStateService.Changed += HandleCurrentSceneStateChanged;
            GlobalSearchNavigationService.Changed += OnGlobalSearchNavigationChanged;
            AiCommandStatusService.Changed += OnAiCommandStatusChanged;
            _aiCommandStatus = AiCommandStatusService.Current;
            await AuthMeStateService.RefreshAsync();
            await LoadAiUsageStatusAsync();
            await LoadAiActionsAsync();
            _sectionReorderDiagnosticsEnabled = SectionReorderDiagnostics.IsEnabled(Configuration);
            _dndDebugEnabled = IsDndDebugEnabled();
            _docxExportEnabled = Configuration.GetValue<bool?>("Exports:DocxEnabled") ?? false;
            _epubExportEnabled = Configuration.GetValue<bool?>("Exports:EpubEnabled") ?? false;
        }

        private bool IsDndDebugEnabled()
        {
            try
            {
                Uri uri = new(Navigation.Uri);
                string query = uri.Query;
                if (!string.IsNullOrWhiteSpace(query))
                {
                    string trimmed = query.TrimStart('?');
                    foreach (string segment in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = segment.Split('=', 2);
                        if (parts.Length == 0)
                        {
                            continue;
                        }

                        string key = Uri.UnescapeDataString(parts[0]);
                        if (!string.Equals(key, "dndDebug", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                        if (string.Equals(value, "1", StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
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

            if (_pendingContinuityHighlights && _pageEditor is not null)
            {
                _pendingContinuityHighlights = false;
                await ApplyContinuityHighlightsAsync();
            }

            if (_focusFeedbackDialogOnRender && _isFeedbackDialogOpen)
            {
                _focusFeedbackDialogOnRender = false;
                await _feedbackTypeSelectRef.FocusAsync();
            }

            if (_isToolbarOverflowOpen && _toolbarOverflowNeedsPositioning)
            {
                _toolbarOverflowNeedsPositioning = false;
                ToolbarDropdownPlacement placement;
                try
                {
                    placement = await JSRuntime.InvokeAsync<ToolbarDropdownPlacement>(
                        "tiptapEditor.getDropdownPosition",
                        _toolbarOverflowButtonRef,
                        _toolbarOverflowPanelRef);
                }
                catch (JSException)
                {
                    placement = new ToolbarDropdownPlacement(false, false);
                }

                _toolbarOverflowAlignLeft = placement.AlignLeft;
                _toolbarOverflowOpenUpward = placement.OpenUpward;

                await InvokeAsync(StateHasChanged);
            }

            if (_pendingSearchTargetFocus && !_isLoading && _pageEditor is not null)
            {
                _pendingSearchTargetFocus = false;
                await _pageEditor.FocusSearchAsync(_activeSearchQuery);
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            if (ProjectId != Guid.Empty && SceneNodeId != Guid.Empty)
            {
                CurrentProjectStateService.SetCurrent(ProjectId);
                CurrentSceneStateService.SetCurrent(ProjectId, SceneNodeId);
                bool resolved = await EnsureLegacySectionTargetForSceneRouteAsync();
                if (!resolved)
                {
                    _isLoading = false;
                    _loadError = "This scene is not yet mapped to a legacy section route.";
                    return;
                }
            }
            else if (DocumentId != Guid.Empty && SectionId != Guid.Empty)
            {
                bool redirected = await TryRedirectLegacySectionRouteAsync();
                if (redirected)
                {
                    return;
                }

                CurrentSceneStateService.Clear();
            }

            CurrentDocumentStateService.SetCurrent(DocumentId, SectionId);
            if (CanReuseLoadedRouteTarget())
            {
                ApplyActiveSearchQuery(SearchQuery, focus: !string.IsNullOrWhiteSpace(SearchQuery));
                ConsumePendingGlobalSearchTarget();
            }
            else if (CanActivateLoadedSection())
            {
                await ActivateLoadedSectionAsync(SectionId);
                ConsumePendingGlobalSearchTarget();
            }
            else
            {
                await LoadDocumentAsync();
                ConsumePendingGlobalSearchTarget();
            }

            await RefreshOnboardingWalkthroughAsync();
            await EnsureOnboardingStarterTextAsync();
        }

        private bool CanReuseLoadedRouteTarget()
        {
            if (_loadedDocumentId == Guid.Empty || _activeSection is null)
            {
                return false;
            }

            if (_loadedDocumentId != DocumentId || _activeSection.Id != SectionId)
            {
                return false;
            }

            if (IsSceneRoute)
            {
                return _loadedRouteProjectId == ProjectId && _loadedRouteSceneNodeId == SceneNodeId;
            }

            return _loadedRouteProjectId == Guid.Empty && _loadedRouteSceneNodeId == Guid.Empty;
        }

        private bool CanActivateLoadedSection()
        {
            return !IsSceneRoute
                && _loadedDocumentId == DocumentId
                && DocumentId != Guid.Empty
                && SectionId != Guid.Empty
                && _sections.Any(section => section.Id == SectionId);
        }

        private void ApplyActiveSearchQuery(string? query, bool focus)
        {
            _activeSearchQuery = query?.Trim() ?? string.Empty;
            if (focus && !string.IsNullOrWhiteSpace(_activeSearchQuery))
            {
                _pendingSearchTargetFocus = true;
            }
        }

        private void ConsumePendingGlobalSearchTarget()
        {
            if (DocumentId == Guid.Empty || SectionId == Guid.Empty)
            {
                ApplyActiveSearchQuery(SearchQuery, focus: false);
                return;
            }

            if (GlobalSearchNavigationService.TryConsume(DocumentId, SectionId, out GlobalSearchNavigationTarget? target)
                && target is not null)
            {
                Logger.LogDebug(
                    "Global search target consumed. DocumentId={DocumentId} SectionId={SectionId} PageId={PageId} EntityType={EntityType}",
                    target.DocumentId,
                    target.SectionId,
                    target.PageId,
                    target.EntityType);
                ApplyActiveSearchQuery(target.Query, focus: true);
                return;
            }

            ApplyActiveSearchQuery(SearchQuery, focus: false);
        }

        private void OnGlobalSearchNavigationChanged(GlobalSearchNavigationTarget? target)
        {
            if (target is null
                || target.DocumentId != DocumentId
                || target.SectionId != SectionId)
            {
                return;
            }

            _ = InvokeAsync(async () =>
            {
                ApplyActiveSearchQuery(target.Query, focus: true);
                if (!_isLoading)
                {
                    await InvokeAsync(StateHasChanged);
                }
            });
        }

        private async Task<bool> EnsureLegacySectionTargetForSceneRouteAsync()
        {
            if (DocumentId != Guid.Empty && SectionId != Guid.Empty)
            {
                return true;
            }

            try
            {
                using HttpResponseMessage response = await Http.PostAsync(
                    $"api/projects/{ProjectId}/nodes/{SceneNodeId}/open-scene",
                    null);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                ProjectSceneOpenTargetDto? target = await response.Content.ReadFromJsonAsync<ProjectSceneOpenTargetDto>();
                if (target is null || !target.DocumentId.HasValue || !target.SectionId.HasValue)
                {
                    return false;
                }

                CurrentSceneStateService.SetCurrent(ProjectId, SceneNodeId, target.SceneTitle);
                DocumentId = target.DocumentId.Value;
                SectionId = target.SectionId.Value;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TryRedirectLegacySectionRouteAsync()
        {
            if (DocumentId == Guid.Empty || SectionId == Guid.Empty)
            {
                return false;
            }

            try
            {
                ProjectSceneOpenTargetDto? sceneTarget = await Http.GetFromJsonAsync<ProjectSceneOpenTargetDto>(
                    $"api/sections/{SectionId}/scene-target");
                if (sceneTarget is null || sceneTarget.ProjectId == Guid.Empty || sceneTarget.SceneNodeId == Guid.Empty)
                {
                    return false;
                }

                Navigation.NavigateTo($"/projects/{sceneTarget.ProjectId}/scenes/{sceneTarget.SceneNodeId}", replace: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task LoadDocumentAsync()
        {
            _isLoading = true;
            _loadError = null;
            ResetSectionRename();
            CancelDeleteSection();
            _continuityReport = null;
            _selectedContinuityIssueKey = null;
            await ClearContinuityHighlightsAsync();

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
                if (IsSceneRoute && ProjectId != Guid.Empty)
                {
                    CurrentProjectStateService.SetCurrent(ProjectId);
                }
                else if (document.ProjectId != Guid.Empty)
                {
                    CurrentProjectStateService.SetCurrent(document.ProjectId);
                }
                else
                {
                    CurrentProjectStateService.Clear();
                }

                if (_loadedDocumentId != DocumentId)
                {
                    _sections.Clear();
                    _pagesBySection.Clear();
                    _synopsisPreviewCacheDocumentId = null;
                    _synopsisPreviewCacheHtml = null;
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
                _qualityHasRunOnce = false;
                if (_activePage is null)
                {
                    _loadError = "No pages available.";
                    return;
                }

                if (IsSceneRoute)
                {
                    await LoadSceneContentIntoActivePageAsync();
                }
                SyncActiveSceneTitle();
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
                await RestoreContextPanelStateAsync();

                await LoadHeadingPrefixCountersAsync();
                ResetNotesAutosaveState();
                _notesDraft = await LoadSectionNotesAsync(_activeSection.Id, CancellationToken.None);
                _notesStatus = null;
                await LoadSceneCardAsync(_activeSection.Id);
                await LoadAiHistoryAsync();
                await LoadBibleSnapshotsAsync();
                await LoadPageVersionsAsync();
                await LoadAnnotationsAsync();
                await LoadQualityIssuesAsync();
                await LoadTranslationLinksAsync();
                _loadedRouteProjectId = IsSceneRoute ? ProjectId : Guid.Empty;
                _loadedRouteSceneNodeId = IsSceneRoute ? SceneNodeId : Guid.Empty;
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

        private async Task ActivateLoadedSectionAsync(Guid sectionId)
        {
            _isLoading = true;
            _loadError = null;
            ResetSectionRename();
            CancelDeleteSection();
            _continuityReport = null;
            _selectedContinuityIssueKey = null;
            await ClearContinuityHighlightsAsync();

            try
            {
                SectionDto? targetSection = _sections.FirstOrDefault(section => section.Id == sectionId);
                if (targetSection is null)
                {
                    await LoadDocumentAsync();
                    return;
                }

                _headingTraceId = Guid.NewGuid().ToString("N");
                _activeSection = targetSection;
                await EnsureSectionHasPageAsync(targetSection.Id);
                _sectionLanguageCode = targetSection.LanguageCode;
                _sectionTranslationGroupId = targetSection.TranslationGroupId;

                _activePage = GetPrimaryPage(targetSection.Id);
                _qualityHasRunOnce = false;
                if (_activePage is null)
                {
                    _loadError = "No pages available.";
                    return;
                }

                SyncActiveSceneTitle();
                ResetVersionStatusTracking();

                await LastOpenedDocumentStateService.SaveAsync(DocumentId, targetSection.Id);
                await RestoreContextPanelStateAsync();
                await LoadHeadingPrefixCountersAsync();
                ResetNotesAutosaveState();
                _notesDraft = await LoadSectionNotesAsync(targetSection.Id, CancellationToken.None);
                _notesStatus = null;
                await LoadSceneCardAsync(targetSection.Id);
                await LoadAiHistoryAsync();
                await LoadBibleSnapshotsAsync();
                await LoadPageVersionsAsync();
                await LoadAnnotationsAsync();
                await LoadQualityIssuesAsync();
                await LoadTranslationLinksAsync();
                _loadedRouteProjectId = Guid.Empty;
                _loadedRouteSceneNodeId = Guid.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Document editor section activation failed.");
                _loadError = "Failed to open the search result.";
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

        private async Task LoadSceneContentIntoActivePageAsync()
        {
            if (!IsSceneRoute || _activePage is null)
            {
                return;
            }

            try
            {
                SceneContentDto? sceneContent = await Http.GetFromJsonAsync<SceneContentDto>(
                    $"api/projects/{ProjectId}/scenes/{SceneNodeId}/content");
                if (sceneContent is null)
                {
                    return;
                }

                string content = sceneContent.ContentJson ?? string.Empty;
                _activePage = _activePage with { Content = content, UpdatedAt = sceneContent.UpdatedAtUtc };
                if (_activeSection is not null
                    && _pagesBySection.TryGetValue(_activeSection.Id, out List<PageDto>? pages)
                    && pages.Count > 0)
                {
                    pages[0] = pages[0] with { Content = content, UpdatedAt = sceneContent.UpdatedAtUtc };
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Scene content load failed.");
            }
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
            await FlushNotesSaveAsync();

            await FlushActiveEditorAsync("navigate");

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

        private void PromptImportSection(Guid sectionId)
        {
            SectionDto? section = _sections.FirstOrDefault(item => item.Id == sectionId);
            if (section is null)
            {
                return;
            }

            _sectionMenuOpenId = null;
            _isImportDialogOpen = true;
            _importTargetSectionId = section.Id;
            _importMode = "replace";
            _importNormalizeWhitespace = true;
            _importPreserveTxtLineBreaks = false;
            _importFile = null;
            _importFileName = null;
            _importError = null;
        }

        private void PromptImportCurrentSection()
        {
            _isDocumentMenuOpen = false;
            if (_activeSection is null)
            {
                _importError = "No active section to import into.";
                return;
            }

            PromptImportSection(_activeSection.Id);
        }

        private void CancelImportSection()
        {
            _isImportDialogOpen = false;
            _isImporting = false;
            _importFile = null;
            _importFileName = null;
            _importError = null;
        }

        private void OnImportFileSelected(InputFileChangeEventArgs args)
        {
            _importError = null;
            _importFile = args.File;
            _importFileName = _importFile?.Name;
        }

        private async Task ConfirmImportSectionAsync()
        {
            if (_importFile is null)
            {
                _importError = "Select a file to import.";
                return;
            }

            _isImporting = true;
            _importError = null;
            try
            {
                using MultipartFormDataContent form = new();
                using Stream fileStream = _importFile.OpenReadStream(10 * 1024 * 1024);
                using StreamContent fileContent = new(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_importFile.ContentType);
                form.Add(fileContent, "file", _importFile.Name);
                form.Add(new StringContent(_importTargetSectionId.ToString()), "targetSectionId");
                form.Add(new StringContent(_importMode), "mode");
                form.Add(new StringContent(_importNormalizeWhitespace.ToString()), "normalizeWhitespace");
                form.Add(new StringContent(_importPreserveTxtLineBreaks.ToString()), "preserveTxtLineBreaks");

                using HttpResponseMessage response = await Http.PostAsync(
                    $"api/documents/{DocumentId}/sections/{_importTargetSectionId}/import",
                    form);
                if (!response.IsSuccessStatusCode)
                {
                    _importError = await TryReadMessageAsync(response) ?? $"Import failed ({response.StatusCode}).";
                    return;
                }

                SectionImportResponseDto? result = await response.Content.ReadFromJsonAsync<SectionImportResponseDto>();
                if (result is null)
                {
                    _importError = "Import failed.";
                    return;
                }

                _importSummary =
                    $"Imported {result.Format.ToUpperInvariant()}: {result.Stats.Paragraphs} paragraphs, {result.Stats.Headings} headings, {result.Stats.Lists} lists, {result.Stats.Characters} chars.";
                if (result.Warnings.Count > 0)
                {
                    _importSummary += " Warnings: " + string.Join(" ", result.Warnings);
                }

                if (_activeSection?.Id == result.TargetSectionId && _activePage is not null)
                {
                    bool appendMode = QualityFixClientHelpers.IsAppendMode(_importMode);
                    string existingHtml = _activePage.Content ?? string.Empty;
                    string mergedHtml = appendMode
                        ? QualityFixClientHelpers.MergeImportedHtmlForAppend(existingHtml, result.Html)
                        : result.Html;
                    _activePage = _activePage with { Content = mergedHtml, UpdatedAt = DateTimeOffset.UtcNow };
                    if (_pagesBySection.TryGetValue(result.TargetSectionId, out List<PageDto>? pages) && pages.Count > 0)
                    {
                        pages[0] = pages[0] with { Content = mergedHtml, UpdatedAt = DateTimeOffset.UtcNow };
                        for (int i = 1; i < pages.Count; i++)
                        {
                            pages[i] = pages[i] with { Content = string.Empty, UpdatedAt = DateTimeOffset.UtcNow };
                        }
                    }

                    if (_pageEditor is not null)
                    {
                        await _pageEditor.SetContentAsync(mergedHtml, markDirty: false);
                        await _pageEditor.RefreshPageBreaksAsync();
                    }
                }
                else
                {
                    await LoadDocumentAsync();
                }

                CancelImportSection();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Import section failed.");
                _importError = "Import failed.";
            }
            finally
            {
                _isImporting = false;
                await InvokeAsync(StateHasChanged);
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
                if (_dndDebugEnabled)
                {
                    _dndDebugStatus = "dragstart ignored: reordering";
                    _dndDebugLastDraggedId = null;
                    _dndDebugLastTargetId = null;
                    _dndDebugLastAt = DateTimeOffset.Now;
                    Logger.LogInformation(
                        "[DND] dragstart ignored: reordering DocId={DocumentId} SectionId={SectionId}",
                        DocumentId,
                        sectionId);
                }
                return;
            }

            _draggedSectionId = sectionId;
            if (_dndDebugEnabled)
            {
                _dndDebugStatus = "dragstart";
                _dndDebugLastDraggedId = sectionId;
                _dndDebugLastTargetId = null;
                _dndDebugLastAt = DateTimeOffset.Now;
                Logger.LogInformation(
                    "[DND] dragstart DocId={DocumentId} SectionId={SectionId} Reordering={IsReordering} Dragged={DraggedSectionId}",
                    DocumentId,
                    sectionId,
                    _isReorderingSections,
                    _draggedSectionId);
            }
            SectionReorderDiagnostics.LogDebug(
                Logger,
                Configuration,
                "UI drag start DocId={DocumentId} SectionId={SectionId}",
                DocumentId,
                sectionId);
        }

        private async Task OnSectionDrop(Guid targetSectionId)
        {
            if (_dndDebugEnabled)
            {
                Logger.LogInformation(
                    "[DND] drop entry DocId={DocumentId} TargetId={TargetSectionId} Reordering={IsReordering} Dragged={DraggedSectionId}",
                    DocumentId,
                    targetSectionId,
                    _isReorderingSections,
                    _draggedSectionId);
            }

            if (_isReorderingSections || _draggedSectionId is null)
            {
                if (_dndDebugEnabled)
                {
                    _dndDebugStatus = _isReorderingSections
                        ? "drop ignored: reordering"
                        : "drop ignored: no dragged id";
                    _dndDebugLastTargetId = targetSectionId;
                    _dndDebugLastAt = DateTimeOffset.Now;
                    Logger.LogInformation(
                        "[DND] drop ignored DocId={DocumentId} TargetId={TargetSectionId} Reordering={IsReordering} Dragged={DraggedSectionId}",
                        DocumentId,
                        targetSectionId,
                        _isReorderingSections,
                        _draggedSectionId);
                }
                return;
            }

            Guid sourceSectionId = _draggedSectionId.Value;
            _draggedSectionId = null;
            if (sourceSectionId == targetSectionId)
            {
                if (_dndDebugEnabled)
                {
                    _dndDebugStatus = "drop ignored: same target";
                    _dndDebugLastTargetId = targetSectionId;
                    _dndDebugLastDraggedId = sourceSectionId;
                    _dndDebugLastAt = DateTimeOffset.Now;
                    Logger.LogInformation(
                        "[DND] drop ignored: same target DocId={DocumentId} SectionId={SectionId}",
                        DocumentId,
                        sourceSectionId);
                }
                return;
            }

            int sourceIndex = _sections.FindIndex(section => section.Id == sourceSectionId);
            int targetIndex = _sections.FindIndex(section => section.Id == targetSectionId);
            if (sourceIndex < 0 || targetIndex < 0)
            {
                if (_dndDebugEnabled)
                {
                    _dndDebugStatus = "drop ignored: invalid indices";
                    _dndDebugLastTargetId = targetSectionId;
                    _dndDebugLastDraggedId = sourceSectionId;
                    _dndDebugLastAt = DateTimeOffset.Now;
                    Logger.LogInformation(
                        "[DND] drop ignored: invalid indices DocId={DocumentId} SourceIndex={SourceIndex} TargetIndex={TargetIndex}",
                        DocumentId,
                        sourceIndex,
                        targetIndex);
                }
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
            if (_dndDebugEnabled)
            {
                _dndDebugStatus = "drop: reordered";
                _dndDebugLastTargetId = targetSectionId;
                _dndDebugLastDraggedId = sourceSectionId;
                _dndDebugLastAt = DateTimeOffset.Now;
                Guid[] head = _sections.Take(3).Select(section => section.Id).ToArray();
                Logger.LogInformation(
                    "[DND] drop reorder DocId={DocumentId} SourceIndex={SourceIndex} TargetIndex={TargetIndex} HeadIds={HeadIds}",
                    DocumentId,
                    sourceIndex,
                    targetIndex,
                    head);
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

        private Task OnSectionDropAfterLast()
        {
            if (_isReorderingSections || _draggedSectionId is null || _sections.Count == 0)
            {
                if (_dndDebugEnabled)
                {
                    _dndDebugStatus = _isReorderingSections
                        ? "drop-end ignored: reordering"
                        : _draggedSectionId is null
                            ? "drop-end ignored: no dragged id"
                            : "drop-end ignored: empty list";
                    _dndDebugLastTargetId = null;
                    _dndDebugLastAt = DateTimeOffset.Now;
                    Logger.LogInformation(
                        "[DND] drop-end ignored DocId={DocumentId} Reordering={IsReordering} Dragged={DraggedSectionId} Count={Count}",
                        DocumentId,
                        _isReorderingSections,
                        _draggedSectionId,
                        _sections.Count);
                }
                return Task.CompletedTask;
            }

            Guid sourceSectionId = _draggedSectionId.Value;
            _draggedSectionId = null;
            int sourceIndex = _sections.FindIndex(section => section.Id == sourceSectionId);
            if (sourceIndex < 0)
            {
                if (_dndDebugEnabled)
                {
                    _dndDebugStatus = "drop-end ignored: invalid source";
                    _dndDebugLastAt = DateTimeOffset.Now;
                    Logger.LogInformation(
                        "[DND] drop-end ignored: invalid source DocId={DocumentId} SourceIndex={SourceIndex}",
                        DocumentId,
                        sourceIndex);
                }
                return Task.CompletedTask;
            }

            SectionDto moved = _sections[sourceIndex];
            _sections.RemoveAt(sourceIndex);
            _sections.Add(moved);
            for (int index = 0; index < _sections.Count; index++)
            {
                _sections[index] = _sections[index] with { OrderIndex = index };
            }

            if (_dndDebugEnabled)
            {
                _dndDebugStatus = "drop-end: reordered";
                _dndDebugLastDraggedId = sourceSectionId;
                _dndDebugLastTargetId = null;
                _dndDebugLastAt = DateTimeOffset.Now;
                Guid[] head = _sections.Take(3).Select(section => section.Id).ToArray();
                Logger.LogInformation(
                    "[DND] drop-end reorder DocId={DocumentId} SourceIndex={SourceIndex} HeadIds={HeadIds}",
                    DocumentId,
                    sourceIndex,
                    head);
            }

            SectionReorderDiagnostics.LogDebug(
                Logger,
                Configuration,
                "UI drop end DocId={DocumentId} Count={Count} FirstId={FirstId} LastId={LastId}",
                DocumentId,
                _sections.Count,
                _sections.FirstOrDefault()?.Id,
                _sections.LastOrDefault()?.Id);

            return SaveSectionOrderAsync();
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
            InvalidateActiveProjectProgressCache("content-save");

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
                _qualityHasRunOnce = false;
                await LoadPageVersionsAsync();
                await UpdateAnnotationAnchorsAsync();
            }

            if (IsSceneRoute)
            {
                await RefreshNavigatorInspectorAsync();
            }

            _onboardingSavedOnce = true;
            await EvaluateOnboardingCompletionAsync(forceTypingProbe: false);
            await InvokeAsync(StateHasChanged);
        }

        private void InvalidateActiveProjectProgressCache(string reason)
        {
            Guid projectId = ProjectId != Guid.Empty
                ? ProjectId
                : CurrentProjectStateService.ProjectId ?? Guid.Empty;
            if (projectId == Guid.Empty)
            {
                return;
            }

            ProjectProgressCacheService.InvalidateProject(projectId, reason);
        }

        private async Task FlushActiveEditorAsync(string reason)
        {
            if (_pageEditor is null)
            {
                return;
            }

            await _pageEditor.ForceSaveIfDifferentAsync(reason);
        }

        private async Task OnEditorStatusChanged(PageEditor.EditorStatusSnapshot status)
        {
            _editorStatus = status;
            await EvaluateOnboardingCompletionAsync(forceTypingProbe: false);
            await InvokeAsync(StateHasChanged);
        }

        private void OnAiCommandStatusChanged(AiCommandStatusSnapshot status)
        {
            _aiCommandStatus = status;
            _ = InvokeAsync(StateHasChanged);
        }

        private string GetAiCommandStatusClass()
        {
            return _aiCommandStatus.IsInProgress ? "ai-command-status" : "ai-command-status is-complete";
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
            if (_isPreviewMode)
            {
                return "is-panels-collapsed is-preview-mode";
            }

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

        private string? GetPanelCategoryTooltip(PanelCategory category)
        {
            string description = category switch
            {
                PanelCategory.Coach => "Writing: Run AI edits and guidance for the current draft.",
                PanelCategory.Story => "Story: Capture scene intent and story metadata.",
                PanelCategory.Navigator => "Navigator: Browse and open manuscript structure.",
                PanelCategory.NotesTasks => "Notes & Tasks: Track notes, comments, and TODOs.",
                PanelCategory.History => "History: Review and undo/redo AI actions.",
                PanelCategory.Advanced => "Advanced: Use reusable prompt presets.",
                _ => "Open panel."
            };
            return GetTooltip(description);
        }

        private string? GetContextTabTooltip(ContextTab tab)
        {
            string description = tab switch
            {
                ContextTab.Ai => "Writing tools: Run AI edits on the selection or section.",
                ContextTab.Continuity => "Consistency: Check characters, places, and timeline for contradictions.",
                ContextTab.Quality => "Style & quality: Find clarity, pacing, and readability issues.",
                ContextTab.Scene => "Scene card: Capture narrative role, intent, beats, and open questions.",
                ContextTab.Navigator => "Project navigator: Open and move sections in the manuscript tree.",
                ContextTab.Notes => "Notes: Save drafting reminders for this section.",
                ContextTab.Annotations => "Annotations: Manage inline comments and TODO highlights.",
                ContextTab.History => "History: Review previous versions and AI proposals.",
                ContextTab.PromptLibrary => "Prompt library: Run saved custom edit prompts.",
                _ => "Open tab."
            };
            return GetTooltip(description);
        }

        private string? GetAiActionTooltip(AiActionOption action)
        {
            if (action is null)
            {
                return null;
            }

            string text = !string.IsNullOrWhiteSpace(action.Description)
                ? $"{action.Label}: {action.Description}"
                : $"{action.Label}: Apply this AI transformation to your draft.";
            return GetTooltip(text);
        }

        private string GetExportPreviewButtonLabel()
        {
            return string.Equals(_exportContentSelection, "synopsis", StringComparison.OrdinalIgnoreCase)
                ? "Preview Synopsis"
                : "Preview";
        }

        private string? GetExportPreviewButtonTooltip()
        {
            if (string.Equals(_exportContentSelection, "synopsis", StringComparison.OrdinalIgnoreCase))
            {
                return GetTooltip("Preview Synopsis: View the synopsis before exporting.");
            }

            return GetTooltip("Preview: View the current export output before downloading.");
        }

        private string? GetExportSubmitButtonTooltip()
        {
            if (string.Equals(_exportContentSelection, "synopsis", StringComparison.OrdinalIgnoreCase)
                && string.Equals(_exportFormatSelection, "docx", StringComparison.OrdinalIgnoreCase))
            {
                return GetTooltip("Export Synopsis to DOCX: Download the synopsis as a Word document.");
            }

            return GetTooltip("Export: Download the selected content in the chosen format.");
        }

        private string? GetExportSubmitLockedTooltip()
        {
            if (!TryGetExportSubmitRequiredFeature(out FeatureKey feature))
            {
                return null;
            }

            return FeatureAccessService.GetRequiredPlanMessage(feature);
        }

        private string? GetExportSubmitFeatureName()
        {
            return TryGetExportSubmitRequiredFeature(out FeatureKey feature)
                ? feature.ToString()
                : null;
        }

        private bool TryGetExportSubmitRequiredFeature(out FeatureKey feature)
        {
            if (_selectedTemplateId.HasValue && !CanUseFeature(FeatureKey.ExportTemplates))
            {
                feature = FeatureKey.ExportTemplates;
                return true;
            }

            if (string.Equals(_exportContentSelection, "synopsis", StringComparison.OrdinalIgnoreCase)
                && !CanUseFeature(FeatureKey.SynopsisExport))
            {
                feature = FeatureKey.SynopsisExport;
                return true;
            }

            if (!CanUseFeature(FeatureKey.ExportDocument))
            {
                feature = FeatureKey.ExportDocument;
                return true;
            }

            if (!CanUseFeature(FeatureKey.ExportFormats))
            {
                feature = FeatureKey.ExportFormats;
                return true;
            }

            feature = default;
            return false;
        }

        private string GetExportPreviewTitle()
        {
            return string.Equals(_exportContentSelection, "synopsis", StringComparison.OrdinalIgnoreCase)
                ? "Synopsis preview"
                : "Export preview";
        }

        private string? GetExportPreviewCoverNote()
        {
            if (string.Equals(_exportContentSelection, "synopsis", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!IsCoverSupportedForFormat(_exportFormatSelection))
            {
                return "Cover is not included for this export format.";
            }

            return _exportIncludeCover
                ? "This format includes the project cover."
                : "Cover is not included for this export.";
        }

        private void OpenFeedbackDialog()
        {
            _isFeedbackDialogOpen = true;
            _feedbackErrorMessage = null;
            _feedbackBannerMessage = null;
            _focusFeedbackDialogOnRender = true;
        }

        private void CloseFeedbackDialog()
        {
            if (_feedbackSubmitting)
            {
                return;
            }

            _isFeedbackDialogOpen = false;
            _feedbackErrorMessage = null;
            _focusFeedbackDialogOnRender = false;
        }

        private void OnFeedbackDialogKeyDown(KeyboardEventArgs args)
        {
            if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            {
                CloseFeedbackDialog();
            }
        }

        private async Task FocusFeedbackFirstAsync(FocusEventArgs _)
        {
            await _feedbackTypeSelectRef.FocusAsync();
        }

        private async Task FocusFeedbackLastAsync(FocusEventArgs _)
        {
            await _feedbackSubmitButtonRef.FocusAsync();
        }

        private async Task SubmitFeedbackAsync()
        {
            _feedbackErrorMessage = null;

            if (string.IsNullOrWhiteSpace(_feedbackType))
            {
                _feedbackErrorMessage = "Type is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_feedbackSubject))
            {
                _feedbackErrorMessage = "Title is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_feedbackDescription))
            {
                _feedbackErrorMessage = "Description is required.";
                return;
            }

            _feedbackSubmitting = true;
            _feedbackBannerMessage = null;
            try
            {
                string? userAgent = null;
                if (_feedbackIncludeDiagnostics)
                {
                    try
                    {
                        userAgent = await JSRuntime.InvokeAsync<string?>("tiptapEditor.getBrowserUserAgent");
                    }
                    catch
                    {
                        userAgent = null;
                    }
                }

                FeedbackSubmitRequest payload = new(
                    _feedbackType.Trim(),
                    _feedbackSubject.Trim(),
                    _feedbackDescription.Trim(),
                    _feedbackIncludeDiagnostics,
                    _feedbackIncludeDiagnostics
                        ? new FeedbackDiagnosticsPayload(
                            Navigation.Uri,
                            typeof(DocumentEditor).Assembly.GetName().Version?.ToString() ?? "unknown",
                            userAgent)
                        : null);

                using HttpResponseMessage response = await Http.PostAsJsonAsync("/api/feedback", payload);
                if (!response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    await LogFeedbackErrorToConsoleAsync(response, responseBody);
                    _feedbackErrorMessage = ExtractFeedbackErrorMessage(response, responseBody)
                        ?? "Could not send feedback. Please retry.";
                    return;
                }

                _feedbackBannerMessage = "Thanks - feedback sent.";
                _isFeedbackDialogOpen = false;
                _feedbackSubject = string.Empty;
                _feedbackDescription = string.Empty;
                _feedbackType = "bug";
                _feedbackIncludeDiagnostics = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Feedback submit failed.");
                await LogFeedbackExceptionToConsoleAsync(ex);
                _feedbackErrorMessage = "Could not send feedback. Please retry.";
            }
            finally
            {
                _feedbackSubmitting = false;
            }
        }

        private async Task LogFeedbackErrorToConsoleAsync(HttpResponseMessage response, string? responseBody)
        {
            string status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
            string body = string.IsNullOrWhiteSpace(responseBody) ? "<empty>" : responseBody;
            Logger.LogWarning("Feedback request failed. Status={Status} Body={Body}", status, body);
            try
            {
                await JSRuntime.InvokeVoidAsync("console.error", $"Feedback request failed: {status}", body);
            }
            catch
            {
            }
        }

        private async Task LogFeedbackExceptionToConsoleAsync(Exception exception)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("console.error", "Feedback request threw an exception.", exception.ToString());
            }
            catch
            {
            }
        }

        private static string? ExtractFeedbackErrorMessage(HttpResponseMessage response, string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                JsonElement root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("message", out JsonElement messageElement)
                        && messageElement.ValueKind == JsonValueKind.String)
                    {
                        return messageElement.GetString();
                    }

                    if (root.TryGetProperty("title", out JsonElement titleElement)
                        && titleElement.ValueKind == JsonValueKind.String)
                    {
                        if (root.TryGetProperty("errors", out JsonElement errorsElement)
                            && errorsElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (JsonProperty property in errorsElement.EnumerateObject())
                            {
                                if (property.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (JsonElement arrayItem in property.Value.EnumerateArray())
                                    {
                                        if (arrayItem.ValueKind == JsonValueKind.String)
                                        {
                                            return $"{titleElement.GetString()}: {arrayItem.GetString()}";
                                        }
                                    }
                                }
                            }
                        }

                        return titleElement.GetString();
                    }
                }
            }
            catch
            {
            }

            return responseBody.Trim();
        }

        private async Task ToggleContextPanel()
        {
            LayoutState current = LayoutStateService.State;
            await LayoutStateService.SetStateAsync(current with { ContextCollapsed = !current.ContextCollapsed });
        }

        public void Dispose()
        {
            LayoutStateService.Changed -= OnLayoutStateChanged;
            AuthMeStateService.Changed -= OnAuthMeStateChanged;
            CurrentSceneStateService.Changed -= HandleCurrentSceneStateChanged;
            GlobalSearchNavigationService.Changed -= OnGlobalSearchNavigationChanged;
            AiCommandStatusService.Changed -= OnAiCommandStatusChanged;
            OnboardingOverlayStateService.Clear();
            _notesAutosaveCts?.Cancel();
            _notesAutosaveCts?.Dispose();
            _notesAutosaveCts = null;
            _notesRetryCts?.Cancel();
            _notesRetryCts?.Dispose();
            _notesRetryCts = null;
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

        private void HandleCurrentSceneStateChanged()
        {
            _ = InvokeAsync(StateHasChanged);
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
            if (_isPreviewMode)
            {
                classes.Add("is-preview-mode");
            }

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
            CloseLinkContextMenu();
            _contextMenuX = request.X;
            _contextMenuY = request.Y;
            _isContextMenuOpen = true;
            _shouldFocusContextMenu = true;
            return InvokeAsync(StateHasChanged);
        }

        private void OnAuthMeStateChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        private Task OnEditorLinkContextMenuRequested(SectionEditor.EditorLinkContextMenuRequest request)
        {
            CloseContextMenu();
            _linkContextMenuX = request.X;
            _linkContextMenuY = request.Y;
            _linkContextMenuHref = request.Href;
            _isLinkContextMenuOpen = true;
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

        private void CloseLinkContextMenu()
        {
            _isLinkContextMenuOpen = false;
            _linkContextMenuHref = null;
        }

        private string GetContextMenuStyle()
        {
            string left = _contextMenuX.ToString(CultureInfo.InvariantCulture);
            string top = _contextMenuY.ToString(CultureInfo.InvariantCulture);
            return $"left: {left}px; top: {top}px;";
        }

        private string GetLinkContextMenuStyle()
        {
            string left = _linkContextMenuX.ToString(CultureInfo.InvariantCulture);
            string top = _linkContextMenuY.ToString(CultureInfo.InvariantCulture);
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

        private Task OnLinkContextMenuKeyDown(KeyboardEventArgs args)
        {
            if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            {
                CloseLinkContextMenu();
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

        private Task OnInsertTableRequested()
        {
            return InvokePageCommandAsync("insertTable", 3, 3, true);
        }

        private Task OnAddTableRowBeforeRequested()
        {
            return InvokePageCommandAsync("addTableRowBefore");
        }

        private Task OnAddTableRowAfterRequested()
        {
            return InvokePageCommandAsync("addTableRowAfter");
        }

        private Task OnDeleteTableRowRequested()
        {
            return InvokePageCommandAsync("deleteTableRow");
        }

        private Task OnAddTableColumnBeforeRequested()
        {
            return InvokePageCommandAsync("addTableColumnBefore");
        }

        private Task OnAddTableColumnAfterRequested()
        {
            return InvokePageCommandAsync("addTableColumnAfter");
        }

        private Task OnDeleteTableColumnRequested()
        {
            return InvokePageCommandAsync("deleteTableColumn");
        }

        private Task OnDeleteTableRequested()
        {
            return InvokePageCommandAsync("deleteTable");
        }

        private Task OnToggleTableHeaderRowRequested()
        {
            return InvokePageCommandAsync("toggleTableHeaderRow");
        }

        private Task OnToggleTableHeaderColumnRequested()
        {
            return InvokePageCommandAsync("toggleTableHeaderColumn");
        }

        private Task OnMergeTableCellsRequested()
        {
            return InvokePageCommandAsync("mergeTableCells");
        }

        private Task OnSplitTableCellRequested()
        {
            return InvokePageCommandAsync("splitTableCell");
        }

        private async Task OnImageFileSelected(InputFileChangeEventArgs args)
        {
            _imageUploadError = null;
            try
            {
                IBrowserFile? file = args.File;
                if (file is null)
                {
                    return;
                }

                long maxBytes = Math.Clamp(Configuration.GetValue<long?>("Images:MaxUploadBytes") ?? (5 * 1024 * 1024), 256 * 1024, 10 * 1024 * 1024);
                if (file.Size <= 0 || file.Size > maxBytes)
                {
                    _imageUploadError = $"Image must be between 1 byte and {maxBytes} bytes.";
                    return;
                }

                if (!IsSupportedImageMimeType(file.ContentType))
                {
                    _imageUploadError = "Unsupported image type. Use PNG, JPEG, GIF, or WEBP.";
                    return;
                }

                await using Stream readStream = file.OpenReadStream(maxBytes);
                using StreamContent streamContent = new(readStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

                using MultipartFormDataContent form = new();
                form.Add(streamContent, "file", file.Name);

                using HttpResponseMessage response = await Http.PostAsync(
                    $"api/documents/{DocumentId:D}/assets/images",
                    form);

                if (!response.IsSuccessStatusCode)
                {
                    string message = await response.Content.ReadAsStringAsync();
                    _imageUploadError = string.IsNullOrWhiteSpace(message)
                        ? "Image upload failed."
                        : $"Image upload failed: {message}";
                    return;
                }

                ImageUploadResponse? payload = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
                if (payload is null || string.IsNullOrWhiteSpace(payload.DataUri))
                {
                    _imageUploadError = "Image upload response was invalid.";
                    return;
                }

                string altText = Path.GetFileNameWithoutExtension(file.Name);
                await InvokePageCommandAsync(
                    "replaceSelectedImage",
                    payload.DataUri,
                    altText,
                    string.Empty,
                    null,
                    payload.Url,
                    payload.ImageId.ToString("D", CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _imageUploadError = "Image upload failed.";
                Logger.LogWarning(ex, "Image upload failed for document {DocumentId}.", DocumentId);
            }
            finally
            {
                _imageUploadInputKey = Guid.NewGuid().ToString("N");
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task OnInsertImageRequested()
        {
            if (!_formattingState.CanInsertImage)
            {
                return;
            }

            await JSRuntime.InvokeVoidAsync("tiptapEditor.openFilePicker", "editor-image-upload");
        }

        private async Task OnInsertImageFromUrlRequested()
        {
            string? input = await JSRuntime.InvokeAsync<string?>("prompt", "Image URL", string.Empty);
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            if (!TryNormalizeImageUrl(input, out string? normalized))
            {
                _imageUploadError = "Image URL must be https://, http://, or data:image/*";
                await InvokeAsync(StateHasChanged);
                return;
            }

            _imageUploadError = null;
            await InvokePageCommandAsync("replaceSelectedImage", normalized, string.Empty, string.Empty, null, null, null);
        }

        private Task OnRemoveImageRequested()
        {
            return InvokePageCommandAsync("removeSelectedImage");
        }

        private Task OnLinkRequested()
        {
            return PromptLinkAsync();
        }

        private async Task PromptLinkAsync(string? initialLink = null)
        {
            if (_pageEditor is null)
            {
                return;
            }

            string originalLink = !string.IsNullOrWhiteSpace(initialLink)
                ? initialLink.Trim()
                : (_formattingState.IsLink ? _formattingState.LinkHref?.Trim() ?? string.Empty : string.Empty);
            string defaultLink = string.IsNullOrWhiteSpace(originalLink) ? string.Empty : originalLink;
            string? link = await JSRuntime.InvokeAsync<string?>("prompt", "Link URL", defaultLink);
            if (link is null)
            {
                return;
            }

            string normalized = link.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (string.Equals(normalized, originalLink, StringComparison.Ordinal))
            {
                return;
            }

            await InvokePageCommandAsync("setLink", normalized);
        }

        private Task OnLinkShortcutRequested()
        {
            return PromptLinkAsync();
        }

        private async Task OpenLinkFromContextMenu()
        {
            string? href = _linkContextMenuHref;
            CloseLinkContextMenu();
            if (string.IsNullOrWhiteSpace(href))
            {
                return;
            }

            await JSRuntime.InvokeVoidAsync("tiptapEditor.openInNewTab", href);
        }

        private async Task EditLinkFromContextMenu()
        {
            string? href = _linkContextMenuHref;
            CloseLinkContextMenu();
            await PromptLinkAsync(href);
        }

        private async Task RemoveLinkFromContextMenu()
        {
            CloseLinkContextMenu();
            await InvokePageCommandAsync("unsetLink");
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
            if (_isToolbarOverflowOpen)
            {
                _toolbarOverflowNeedsPositioning = true;
                _toolbarOverflowAlignLeft = false;
                _toolbarOverflowOpenUpward = false;
            }
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

        private async Task TogglePreviewModeAsync()
        {
            if (!_isPreviewMode)
            {
                await FlushActiveEditorAsync("preview-mode");
            }

            _isPreviewMode = !_isPreviewMode;
            _isToolbarOverflowOpen = false;
            _isDocumentMenuOpen = false;
            _selectionBubbleVisible = false;
            _isContextMenuOpen = false;
            _isLinkContextMenuOpen = false;
            await InvokeAsync(StateHasChanged);
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

        private string GetToolbarOverflowPanelClass()
        {
            if (_toolbarOverflowAlignLeft && _toolbarOverflowOpenUpward)
            {
                return "editor-toolbar-overflow-panel is-align-left is-open-upward";
            }

            if (_toolbarOverflowAlignLeft)
            {
                return "editor-toolbar-overflow-panel is-align-left";
            }

            if (_toolbarOverflowOpenUpward)
            {
                return "editor-toolbar-overflow-panel is-open-upward";
            }

            return "editor-toolbar-overflow-panel";
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
                ? "Switch to standard layout"
                : "Switch to print layout";
        }

        private async Task SetContextTabAsync(
            ContextTab tab,
            bool persistSelection = true,
            bool loadTabData = true)
        {
            bool wasStyleQualityActive = _isStyleQualityTabActive;
            _activeContextTab = tab;
            _activePanelCategory = GetCategoryForTab(tab);
            _isStyleQualityTabActive = tab == ContextTab.Quality;

            if (wasStyleQualityActive && !_isStyleQualityTabActive && _pageEditor is not null)
            {
                _selectedQualityIssueKey = null;
                await _pageEditor.ClearAllQualityIssueHighlightsAsync();
            }

            if (!loadTabData)
            {
                if (persistSelection)
                {
                    await PersistContextPanelStateAsync();
                }

                return;
            }

            if (!CanAccessContextTab(tab))
            {
                if (persistSelection)
                {
                    await PersistContextPanelStateAsync();
                }

                return;
            }

            if (tab == ContextTab.Annotations)
            {
                await LoadAnnotationsAsync();
            }
            else if (tab == ContextTab.Quality)
            {
                await LoadQualityIssuesAsync();
            }
            else if (tab == ContextTab.PromptLibrary)
            {
                await LoadPromptPresetsAsync();
            }
            else if (tab == ContextTab.Continuity)
            {
                await LoadBibleSnapshotsAsync();
            }

            if (persistSelection)
            {
                await PersistContextPanelStateAsync();
            }

            if (_isStyleQualityTabActive)
            {
                await SyncQualityIssueHighlightAsync();
            }
        }

        private string GetContextTabClass(ContextTab tab)
        {
            return _activeContextTab == tab ? "is-active" : string.Empty;
        }

        private bool ShowContextCoachCard => !_isDiffMode;

        private CoachCardRecommendation BuildContextCoachRecommendation()
        {
            RightPanelCoachContext context = BuildRightPanelCoachContext();
            return context.PanelCategory switch
            {
                PanelCategory.Coach when context.ContextTab == ContextTab.Ai => BuildWritingToolsCoachRecommendation(context),
                PanelCategory.Coach when context.ContextTab == ContextTab.Continuity => BuildConsistencyCoachRecommendation(context),
                PanelCategory.Coach when context.ContextTab == ContextTab.Quality => BuildStyleQualityCoachRecommendation(context),
                PanelCategory.History => BuildHistoryCoachRecommendation(context),
                PanelCategory.Navigator => BuildNavigatorCoachRecommendation(context),
                PanelCategory.Story => BuildStoryCoachRecommendation(context),
                PanelCategory.NotesTasks => BuildNotesTasksCoachRecommendation(context),
                PanelCategory.Advanced => BuildAdvancedCoachRecommendation(context),
                _ => BuildWritingToolsCoachRecommendation(context)
            };
        }

        private RightPanelCoachContext BuildRightPanelCoachContext()
        {
            int missingFields = 0;
            if (string.IsNullOrWhiteSpace(_sceneNarrativeRole))
            {
                missingFields++;
            }

            if (string.IsNullOrWhiteSpace(_sceneEmotionalBeat))
            {
                missingFields++;
            }

            if (string.IsNullOrWhiteSpace(_sceneKeyEvents))
            {
                missingFields++;
            }

            if (string.IsNullOrWhiteSpace(_sceneOpenQuestions))
            {
                missingFields++;
            }

            bool hasSelection = _currentSelectionRange is not null && _currentSelectionRange.End > _currentSelectionRange.Start;
            bool hasContinuityReport = _continuityReport is not null;
            bool hasContinuityIssues = (_continuityReport?.Issues.Count ?? 0) > 0;
            bool hasStructure = _sections.Count > 0;
            bool isSceneContext = IsSceneRoute;

            return new RightPanelCoachContext(
                _activePanelCategory,
                _activeContextTab,
                isSceneContext,
                hasSelection,
                missingFields,
                _qualityIssues.Count,
                hasContinuityReport,
                hasContinuityIssues,
                hasStructure,
                _aiHistoryEntries.Count,
                _pageVersions.Count,
                _sections.Count,
                _editorStatus.WordCount);
        }

        private CoachCardRecommendation BuildWritingToolsCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new();
            if (context.HasSelection)
            {
                observations.Add("Selection is active, so targeted rewrite actions are ready.");
            }
            else
            {
                observations.Add("Select a paragraph to enable rewrite actions.");
            }

            if (context.QualityIssueCount > 0)
            {
                observations.Add($"{context.QualityIssueCount} quality issue(s) are available for quick fixes.");
            }
            else if (context.WordCount >= 120)
            {
                observations.Add("Use Rewrite/Shorten actions to shape tone and pacing.");
            }
            else
            {
                observations.Add("Use the prompt box to request the next paragraph.");
            }

            if (context.IsSceneContext)
            {
                observations.Add("Writing tools apply directly to your current scene text.");
            }
            else
            {
                observations.Add("Writing tools apply directly to the active section.");
            }

            List<CoachTipCandidate> candidates = new()
            {
                new(
                    CoachTipScope.WritingTools,
                    CoachPrimaryAction.RunQualityCheck,
                    "Run quality check",
                    "A quick quality pass gives concrete rewrite targets in Writing tools.",
                    context.QualityIssueCount > 0 ? 120 : 80),
                new(
                    CoachTipScope.GenericWriting,
                    CoachPrimaryAction.RunQualityCheck,
                    "Run quality check",
                    "Use short quality loops to improve clarity while staying in Writing tools.",
                    60)
            };
            CoachTipCandidate selected = SelectCoachTipCandidate(candidates, CoachTipScope.WritingTools);

            return new CoachCardRecommendation(
                context.IsSceneContext ? "Writing" : "Writing tools",
                observations.Take(3).ToList(),
                selected.PrimaryActionLabel,
                selected.Why,
                selected.PrimaryAction,
                null,
                null,
                nameof(FeatureKey.QualityChecks));
        }

        private CoachCardRecommendation BuildConsistencyCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new()
            {
                $"Character canon: {GetBibleStatusLabel(_characterBibleSnapshot)}",
                $"Place canon: {GetBibleStatusLabel(_placeBibleSnapshot)}",
                $"Timeline canon: {GetBibleStatusLabel(_timelineBibleSnapshot)}"
            };
            if (context.HasContinuityIssues)
            {
                observations.Add("Continuity report has open issues in the current scope.");
            }
            else if (!context.HasContinuityReport)
            {
                observations.Add("No continuity report has been generated yet.");
            }

            return new CoachCardRecommendation(
                "Consistency Coach",
                observations.Take(3).ToList(),
                "Run continuity check",
                "Continuity checks protect timeline, location, and entity consistency.",
                CoachPrimaryAction.RunContinuityCheck,
                null,
                null,
                nameof(FeatureKey.ContinuityCheck));
        }

        private CoachCardRecommendation BuildStyleQualityCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new();
            if (context.QualityIssueCount > 0)
            {
                observations.Add($"{context.QualityIssueCount} quality issue(s) are currently listed.");
                observations.Add("Apply proposal previews to accept targeted fixes with context.");
            }
            else
            {
                observations.Add("No quality issues are currently listed for this scope.");
                observations.Add("Run a scan after major edits to catch regressions early.");
            }

            observations.Add("Use scope/severity filters to narrow findings before applying changes.");

            return new CoachCardRecommendation(
                "Style & quality Coach",
                observations.Take(3).ToList(),
                "Run quality check",
                "Short quality loops keep tone and readability consistent.",
                CoachPrimaryAction.RunQualityCheck,
                null,
                null,
                nameof(FeatureKey.QualityChecks));
        }

        private CoachCardRecommendation BuildHistoryCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new();
            if (context.AiHistoryCount > 0)
            {
                observations.Add($"AI history contains {context.AiHistoryCount} command entr{(context.AiHistoryCount == 1 ? "y" : "ies")}.");
            }
            else
            {
                observations.Add("No AI command history has been captured yet.");
            }

            if (context.VersionCount > 0)
            {
                observations.Add($"Version history contains {context.VersionCount} saved version(s).");
            }
            else
            {
                observations.Add("No saved versions are available yet.");
            }

            observations.Add("Use AI undo/redo or diff compare to apply or roll back safely.");

            return new CoachCardRecommendation(
                "History",
                observations.Take(3).ToList(),
                "Refresh history",
                "Frequent history review reduces risk when applying large rewrites.",
                CoachPrimaryAction.OpenOutline,
                null,
                null,
                nameof(FeatureKey.VersionHistory));
        }

        private CoachCardRecommendation BuildNavigatorCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new();
            if (context.SectionCount > 0)
            {
                observations.Add($"Navigator currently reflects {context.SectionCount} section(s).");
            }
            else
            {
                observations.Add("No sections are available in navigator yet.");
            }

            observations.Add("Drag to reorder nodes and keep manuscript flow intentional.");
            observations.Add("Open a target section from navigator to continue drafting in order.");

            return new CoachCardRecommendation(
                "Navigator",
                observations.Take(3).ToList(),
                "Refresh navigator",
                "Keeping structure clean makes drafting and revision faster.",
                CoachPrimaryAction.OpenOutline,
                null,
                null);
        }

        private CoachCardRecommendation BuildStoryCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new();
            if (context.IsSceneContext)
            {
                observations.Add("Scene card fields directly shape story beats and drafting focus.");
                if (context.MissingSceneCardFields > 0)
                {
                    observations.Add($"{context.MissingSceneCardFields} scene-card field(s) are still empty.");
                }
            }
            else
            {
                observations.Add("Story tools are available for the current section.");
                observations.Add("Capture narrative intent and beats to guide revisions.");
            }

            observations.Add("Use narrative role, narrative intent, emotional beat, and key events to anchor revisions.");
            List<CoachTipCandidate> candidates = new()
            {
                new(
                    CoachTipScope.Story,
                    context.IsSceneContext ? CoachPrimaryAction.SuggestSceneCardFromText : CoachPrimaryAction.OpenOutline,
                    context.IsSceneContext ? "Suggest scene card from text" : "Open navigator",
                    "Story metadata keeps chapter-level intent visible while drafting.",
                    context.IsSceneContext ? 120 : 80),
                new(
                    CoachTipScope.GenericWriting,
                    CoachPrimaryAction.OpenOutline,
                    "Open navigator",
                    "Use structure context to keep story beats aligned.",
                    40)
            };
            CoachTipCandidate selected = SelectCoachTipCandidate(candidates, CoachTipScope.Story);

            return new CoachCardRecommendation(
                "Story",
                observations.Take(3).ToList(),
                selected.PrimaryActionLabel,
                selected.Why,
                selected.PrimaryAction,
                null,
                null,
                context.IsSceneContext ? nameof(FeatureKey.SceneAiSuggestions) : null);
        }

        private static CoachTipCandidate SelectCoachTipCandidate(
            IReadOnlyList<CoachTipCandidate> candidates,
            CoachTipScope activeScope)
        {
            CoachTipCandidate? scoped = candidates
                .Where(candidate => candidate.Scope == activeScope)
                .OrderByDescending(candidate => candidate.Priority)
                .FirstOrDefault();
            if (scoped is not null)
            {
                return scoped;
            }

            CoachTipCandidate? generic = candidates
                .Where(candidate => candidate.Scope == CoachTipScope.GenericWriting)
                .OrderByDescending(candidate => candidate.Priority)
                .FirstOrDefault();
            if (generic is not null)
            {
                return generic;
            }

            return new CoachTipCandidate(
                CoachTipScope.GenericWriting,
                CoachPrimaryAction.RunQualityCheck,
                "Run quality check",
                "Keep revision loops small and actionable.",
                0);
        }

        private CoachCardRecommendation BuildNotesTasksCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new()
            {
                "Use notes for local drafting context and next-pass reminders.",
                "Use annotations to pin TODOs and comments to exact text ranges."
            };
            if (!context.HasSelection)
            {
                observations.Add("Select text before creating annotation highlights.");
            }

            return new CoachCardRecommendation(
                "Notes & tasks",
                observations.Take(3).ToList(),
                "Open annotations",
                "Fast note capture prevents context loss between revision sessions.",
                CoachPrimaryAction.OpenOutline,
                null,
                null,
                null);
        }

        private CoachCardRecommendation BuildAdvancedCoachRecommendation(RightPanelCoachContext context)
        {
            List<string> observations = new()
            {
                "Prompt Library presets can standardize common rewrite workflows.",
                "Use parameterized prompts to keep custom transformations repeatable."
            };
            if (context.HasSelection)
            {
                observations.Add("Current selection is ready for selection-scoped prompt presets.");
            }

            return new CoachCardRecommendation(
                "Advanced",
                observations.Take(3).ToList(),
                "Open prompt library",
                "Reusable prompts reduce repetitive editing overhead.",
                CoachPrimaryAction.OpenOutline,
                null,
                null,
                nameof(FeatureKey.PromptLibrary));
        }

        private async Task OnContextCoachPrimaryActionAsync()
        {
            CoachCardRecommendation recommendation = BuildContextCoachRecommendation();
            if (TryResolveCoachRequiredFeature(recommendation, out FeatureKey requiredFeature)
                && !CanUseFeature(requiredFeature))
            {
                NavigateToUpgradeForFeature(requiredFeature);
                return;
            }

            switch (recommendation.PrimaryAction)
            {
                case CoachPrimaryAction.SuggestSceneCardFromText:
                    await RunSceneAiAsync("scene.suggest", "Suggest scene card fields based on the section text.");
                    break;

                case CoachPrimaryAction.RunQualityCheck:
                    await RunQualityChecksAsync();
                    break;

                case CoachPrimaryAction.RunContinuityCheck:
                    await OnCheckContinuityAsync();
                    break;

                case CoachPrimaryAction.OpenOutline:
                    if (_activePanelCategory == PanelCategory.History)
                    {
                        await LoadAiHistoryAsync();
                    }
                    else if (_activePanelCategory == PanelCategory.Navigator)
                    {
                        await RefreshNavigatorInspectorAsync();
                    }
                    else if (_activePanelCategory == PanelCategory.NotesTasks)
                    {
                        await SetContextTabAsync(ContextTab.Annotations);
                    }
                    else if (_activePanelCategory == PanelCategory.Advanced)
                    {
                        await SetContextTabAsync(ContextTab.PromptLibrary);
                    }
                    else
                    {
                        await SetContextTabAsync(IsSceneRoute ? ContextTab.Scene : ContextTab.Navigator);
                    }
                    break;

                case CoachPrimaryAction.OpenNextScene:
                    await OpenNextSectionFromCoachAsync();
                    break;
            }
        }

        private sealed record RightPanelCoachContext(
            PanelCategory PanelCategory,
            ContextTab ContextTab,
            bool IsSceneContext,
            bool HasSelection,
            int MissingSceneCardFields,
            int QualityIssueCount,
            bool HasContinuityReport,
            bool HasContinuityIssues,
            bool HasStructure,
            int AiHistoryCount,
            int VersionCount,
            int SectionCount,
            int WordCount);

        private async Task OpenNextSectionFromCoachAsync()
        {
            if (_activeSection is null)
            {
                return;
            }

            int index = _sections.FindIndex(section => section.Id == _activeSection.Id);
            if (index < 0 || index >= _sections.Count - 1)
            {
                return;
            }

            await OnSectionSelected(_sections[index + 1].Id);
        }

        private Guid? GetNavigatorProjectId()
        {
            if (ProjectId != Guid.Empty)
            {
                return ProjectId;
            }

            return CurrentSceneStateService.ProjectId;
        }

        private async Task OpenSceneFromNavigatorAsync(Guid sceneNodeId)
        {
            Guid? projectId = GetNavigatorProjectId();
            if (!projectId.HasValue || projectId.Value == Guid.Empty || sceneNodeId == Guid.Empty)
            {
                return;
            }

            if (ProjectId == projectId.Value && SceneNodeId == sceneNodeId)
            {
                return;
            }

            await FlushNotesSaveAsync();
            await FlushActiveEditorAsync("navigate");

            string target = SceneRouteBuilder.BuildRelativeSceneEditorPath(projectId.Value, sceneNodeId);
            Navigation.NavigateTo(target);
        }

        private string GetActiveEditorTitle()
        {
            if (IsSceneRoute
                && CurrentSceneStateService.ProjectId == ProjectId
                && CurrentSceneStateService.SceneNodeId == SceneNodeId
                && !string.IsNullOrWhiteSpace(CurrentSceneStateService.SceneTitle))
            {
                return CurrentSceneStateService.SceneTitle!;
            }

            return _activeSection?.Title ?? "Section";
        }

        private string GetPreviewDocumentTitle()
        {
            return string.IsNullOrWhiteSpace(_documentTitle)
                ? "Untitled manuscript"
                : _documentTitle.Trim();
        }

        private IReadOnlyList<DocumentPreviewSectionItem> GetPreviewSections()
        {
            return _sections
                .OrderBy(section => section.OrderIndex)
                .Select((section, index) =>
                {
                    string kindLabel = GetPreviewSectionKindLabel(section);
                    string title = string.IsNullOrWhiteSpace(section.Title)
                        ? $"{kindLabel} {index + 1}"
                        : section.Title.Trim();
                    string contentHtml = GetPrimaryPage(section.Id)?.Content ?? string.Empty;
                    return new DocumentPreviewSectionItem(kindLabel, title, contentHtml);
                })
                .ToList();
        }

        private string GetPreviewSectionKindLabel(SectionDto section)
        {
            string title = section.Title?.Trim() ?? string.Empty;
            if (title.StartsWith("Part ", StringComparison.OrdinalIgnoreCase))
            {
                return "Part";
            }

            if (title.StartsWith("Chapter ", StringComparison.OrdinalIgnoreCase))
            {
                return "Chapter";
            }

            if (title.StartsWith("Scene ", StringComparison.OrdinalIgnoreCase) || IsSceneRoute)
            {
                return "Scene";
            }

            return "Section";
        }

        private void SyncActiveSceneTitle()
        {
            if (!IsSceneRoute || ProjectId == Guid.Empty || SceneNodeId == Guid.Empty)
            {
                return;
            }

            string title = !string.IsNullOrWhiteSpace(CurrentSceneStateService.SceneTitle)
                ? CurrentSceneStateService.SceneTitle!
                : _activeSection?.Title ?? string.Empty;
            CurrentSceneStateService.SetCurrent(ProjectId, SceneNodeId, title);
        }

        private async Task RefreshNavigatorInspectorAsync(bool force = false)
        {
            if (!IsSceneRoute || _navigatorPanel is null)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!force && now < _nextNavigatorRefreshUtc)
            {
                return;
            }

            _nextNavigatorRefreshUtc = now.AddSeconds(5);
            try
            {
                await _navigatorPanel.RefreshActiveProjectTreeAsync(
                    forceRefresh: force,
                    invalidateCache: false,
                    includeProgress: false,
                    reason: force ? "navigator-panel-open" : "content-save");
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Unable to refresh embedded navigator tree after scene save.");
            }
        }

        private string GetPanelCategoryClass(PanelCategory category)
        {
            return _activePanelCategory == category ? "is-active" : string.Empty;
        }

        private static string GetPanelCategoryLabel(PanelCategory category)
        {
            return category switch
            {
                PanelCategory.Coach => "Writing",
                PanelCategory.NotesTasks => "Notes & Tasks",
                _ => category.ToString()
            };
        }

        private async Task SetPanelCategoryAsync(PanelCategory category)
        {
            _activePanelCategory = category;
            ContextTab preferred = ResolvePreferredTabForCategory(category);
            await SetContextTabAsync(preferred);
        }

        private IReadOnlyList<PanelCategory> GetAvailablePanelCategories()
        {
            List<PanelCategory> categories = new()
            {
                PanelCategory.Coach,
                PanelCategory.Story,
                PanelCategory.Navigator,
                PanelCategory.NotesTasks,
                PanelCategory.History
            };

            if (CanDisplayPromptLibrary)
            {
                categories.Add(PanelCategory.Advanced);
            }

            return categories;
        }

        private IReadOnlyList<ContextTab> GetTabsForActiveCategory()
        {
            return GetTabsForCategory(_activePanelCategory);
        }

        private IReadOnlyList<ContextTab> GetTabsForCategory(PanelCategory category)
        {
            List<ContextTab> tabs = new();

            switch (category)
            {
                case PanelCategory.Coach:
                    tabs.Add(ContextTab.Ai);
                    if (CanDisplayContinuityCoach)
                    {
                        tabs.Add(ContextTab.Continuity);
                    }

                    tabs.Add(ContextTab.Quality);
                    break;

                case PanelCategory.Story:
                    tabs.Add(ContextTab.Scene);
                    break;

                case PanelCategory.Navigator:
                    tabs.Add(ContextTab.Navigator);
                    break;

                case PanelCategory.NotesTasks:
                    tabs.Add(ContextTab.Notes);
                    tabs.Add(ContextTab.Annotations);
                    break;

                case PanelCategory.History:
                    tabs.Add(ContextTab.History);
                    break;

                case PanelCategory.Advanced:
                    if (CanDisplayPromptLibrary)
                    {
                        tabs.Add(ContextTab.PromptLibrary);
                    }

                    break;
            }

            return tabs;
        }

        private static string GetContextTabLabel(ContextTab tab)
        {
            return tab switch
            {
                ContextTab.Ai => "Writing tools",
                ContextTab.Continuity => "Consistency Coach",
                ContextTab.Quality => "Style & quality Coach",
                ContextTab.Scene => "Scene card Coach",
                ContextTab.Navigator => "Project navigator",
                ContextTab.PromptLibrary => "Prompt Library",
                _ => tab.ToString()
            };
        }

        private static string GetSecondaryTabsHeading(PanelCategory category)
        {
            return category switch
            {
                PanelCategory.Coach => "Writing",
                PanelCategory.Story => "Story tools",
                PanelCategory.Navigator => "Navigator",
                PanelCategory.NotesTasks => "Notes & tasks",
                PanelCategory.History => "History tools",
                PanelCategory.Advanced => "Advanced tools",
                _ => "Tools"
            };
        }

        private static string? GetSecondaryTabHelperText(ContextTab tab)
        {
            return tab switch
            {
                ContextTab.Ai => "Rewrite, shorten, translate, propose next paragraph.",
                ContextTab.Continuity => "Check characters, places, and timeline consistency.",
                ContextTab.Quality => "Find repetition, clarity issues, pacing, and other quality checks.",
                ContextTab.Scene => "Capture scene intent, beats, and metadata before drafting.",
                ContextTab.Annotations => "Track notes, comments, TODOs, and highlights.",
                ContextTab.Notes => "Keep personal notes for this section.",
                ContextTab.History => "Review versions and compare content changes.",
                ContextTab.Navigator => "Parts, chapters, and scenes in manuscript order.",
                ContextTab.PromptLibrary => "Run reusable prompt presets for edits and planning.",
                _ => null
            };
        }

        private string GetContextPanelStorageKey()
        {
            return $"{ContextPanelStateStoragePrefix}.{DocumentId:D}.{SectionId:D}";
        }

        private async Task RestoreContextPanelStateAsync()
        {
            ContextTab defaultTab = ResolvePreferredTabForCategory(PanelCategory.Coach);
            ContextPanelStateStorage? stored = await TryLoadContextPanelStateAsync();

            if (stored is null
                || !Enum.TryParse(stored.Tab, ignoreCase: true, out ContextTab storedTab)
                || !IsContextTabAvailable(storedTab))
            {
                await SetContextTabAsync(defaultTab, persistSelection: false, loadTabData: false);
                return;
            }

            await SetContextTabAsync(storedTab, persistSelection: false, loadTabData: false);
        }

        private async Task<ContextPanelStateStorage?> TryLoadContextPanelStateAsync()
        {
            string? json = null;
            try
            {
                json = await JSRuntime.InvokeAsync<string>("localStorage.getItem", GetContextPanelStorageKey());
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (JSException)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ContextPanelStateStorage>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task PersistContextPanelStateAsync()
        {
            ContextPanelStateStorage payload = new(
                _activePanelCategory.ToString(),
                _activeContextTab.ToString());
            string json = JsonSerializer.Serialize(payload);

            try
            {
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", GetContextPanelStorageKey(), json);
            }
            catch (JSException)
            {
            }
        }

        private bool IsContextTabAvailable(ContextTab tab)
        {
            return tab switch
            {
                ContextTab.Continuity => CanDisplayContinuityCoach,
                ContextTab.Quality => CanDisplayQualityChecks,
                ContextTab.History => CanDisplayVersionHistory,
                ContextTab.PromptLibrary => CanDisplayPromptLibrary,
                _ => true
            };
        }

        private FeatureKey? GetRequiredFeatureForContextTab(ContextTab tab)
        {
            return tab switch
            {
                ContextTab.Continuity => FeatureKey.ContinuityCheck,
                ContextTab.Quality => FeatureKey.QualityChecks,
                ContextTab.History => FeatureKey.VersionHistory,
                ContextTab.PromptLibrary => FeatureKey.PromptLibrary,
                _ => null
            };
        }

        private bool CanAccessContextTab(ContextTab tab)
        {
            FeatureKey? feature = GetRequiredFeatureForContextTab(tab);
            return !feature.HasValue || CanUseFeature(feature.Value);
        }

        private void NavigateToUpgradeForFeature(FeatureKey feature)
        {
            Navigation.NavigateTo(FeatureAccessService.GetUpgradePathWithCurrentReturn(feature));
        }

        private string AppendUpgradeReturnUrl(string upgradePath)
        {
            return FeatureAccessService.AppendReturnUrl(upgradePath);
        }

        private static bool TryResolveCoachRequiredFeature(CoachCardRecommendation recommendation, out FeatureKey feature)
        {
            return Enum.TryParse(recommendation.RequiredFeature, ignoreCase: true, out feature);
        }

        private PanelCategory GetCategoryForTab(ContextTab tab)
        {
            return tab switch
            {
                ContextTab.Scene => PanelCategory.Story,
                ContextTab.Navigator => PanelCategory.Navigator,
                ContextTab.Notes => PanelCategory.NotesTasks,
                ContextTab.Annotations => PanelCategory.NotesTasks,
                ContextTab.History => PanelCategory.History,
                ContextTab.PromptLibrary => PanelCategory.Advanced,
                _ => PanelCategory.Coach
            };
        }

        private ContextTab ResolvePreferredTabForCategory(PanelCategory category)
        {
            IReadOnlyList<ContextTab> tabs = GetTabsForCategory(category);
            if (tabs.Count == 0)
            {
                return ContextTab.Ai;
            }

            if (tabs.Contains(_activeContextTab))
            {
                return _activeContextTab;
            }

            return tabs[0];
        }

        private async Task OnPrimaryTabsKeyDown(KeyboardEventArgs args, PanelCategory current)
        {
            List<PanelCategory> categories = GetAvailablePanelCategories().ToList();
            int currentIndex = categories.IndexOf(current);
            if (currentIndex < 0)
            {
                return;
            }

            PanelCategory? target = args.Key switch
            {
                "ArrowRight" => categories[(currentIndex + 1) % categories.Count],
                "ArrowLeft" => categories[(currentIndex - 1 + categories.Count) % categories.Count],
                "Home" => categories[0],
                "End" => categories[^1],
                "Enter" => current,
                " " => current,
                _ => null
            };

            if (target.HasValue)
            {
                await SetPanelCategoryAsync(target.Value);
            }
        }

        private async Task OnSecondaryTabsKeyDown(KeyboardEventArgs args, ContextTab current)
        {
            List<ContextTab> tabs = GetTabsForActiveCategory().ToList();
            int currentIndex = tabs.IndexOf(current);
            if (currentIndex < 0 || tabs.Count == 0)
            {
                return;
            }

            ContextTab? target = args.Key switch
            {
                "ArrowRight" => tabs[(currentIndex + 1) % tabs.Count],
                "ArrowLeft" => tabs[(currentIndex - 1 + tabs.Count) % tabs.Count],
                "Home" => tabs[0],
                "End" => tabs[^1],
                "Enter" => current,
                " " => current,
                _ => null
            };

            if (target.HasValue)
            {
                await SetContextTabAsync(target.Value);
            }
        }

        private async Task OnNotesSave()
        {
            await FlushNotesSaveAsync();
        }

        private void OnNotesInput(ChangeEventArgs args)
        {
            _notesDraft = args.Value?.ToString() ?? string.Empty;
            _notesError = null;
            _notesEditVersion++;
            QueueNotesAutosave();
        }

        private bool HasAction(string actionKey)
        {
            return _availableActionKeys.Contains(actionKey);
        }

        private async Task OnRefreshCharacterBibleAsync() => await RefreshBibleAsync("character", fullRebuild: false);

        private async Task OnRefreshPlaceBibleAsync() => await RefreshBibleAsync("place", fullRebuild: false);

        private async Task OnRefreshTimelineBibleAsync() => await RefreshBibleAsync("timeline", fullRebuild: false);

        private async Task OnFullRebuildCharacterBibleAsync() => await RefreshBibleAsync("character", fullRebuild: true);

        private async Task OnFullRebuildPlaceBibleAsync() => await RefreshBibleAsync("place", fullRebuild: true);

        private async Task OnFullRebuildTimelineBibleAsync() => await RefreshBibleAsync("timeline", fullRebuild: true);

        private async Task OnCheckContinuityAsync()
        {
            await ExecuteContinuityActionAsync("continuity.check_section", "Continuity check complete.", null);
        }

        private async Task OnUpdateAllBiblesAsync()
        {
            if (_activeSection is null || _continuityBusy || !CanRunBibleUpdate)
            {
                return;
            }

            _continuityBusy = true;
            List<string> failures = new();
            try
            {
                (string Type, Func<BibleSnapshotDto?> Snapshot)[] steps =
                {
                    ("character", () => _characterBibleSnapshot),
                    ("place", () => _placeBibleSnapshot),
                    ("timeline", () => _timelineBibleSnapshot)
                };

                for (int index = 0; index < steps.Length; index++)
                {
                    (string bibleType, Func<BibleSnapshotDto?> getSnapshot) = steps[index];
                    bool fullRebuild = NeedsFullBibleBuild(getSnapshot());
                    string phase = fullRebuild ? "building" : "refreshing";
                    _continuityStatus = $"Updating Story Canon: {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(bibleType)} ({index + 1}/{steps.Length})...";
                    await InvokeAsync(StateHasChanged);

                    try
                    {
                        RefreshBibleRequest request = new(fullRebuild, _activeSection.Id);
                        using HttpResponseMessage response = await Http.PostAsJsonAsync(
                            $"api/documents/{DocumentId}/bibles/{bibleType}/refresh",
                            request);
                        if (!response.IsSuccessStatusCode)
                        {
                            if (await TryHandleEntitlementDeniedAsync(response, "ai.bibles.refresh", "Upgrade to enable Story Canon updates."))
                            {
                                _continuityStatus = _entitlementUserMessage;
                                return;
                            }

                            failures.Add($"{bibleType} ({(int)response.StatusCode})");
                            Logger.LogWarning(
                                "Update Story Canon request failed. Type={BibleType}, Phase={Phase}, Status={Status}",
                                bibleType,
                                phase,
                                response.StatusCode);
                            continue;
                        }

                        BibleSnapshotDto? snapshot = await response.Content.ReadFromJsonAsync<BibleSnapshotDto>();
                        if (snapshot is not null)
                        {
                            SetBibleSnapshot(snapshot);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(bibleType);
                        Logger.LogWarning(ex, "Update Story Canon request failed. Type={BibleType}, Phase={Phase}", bibleType, phase);
                    }
                }

                _continuityStatus = failures.Count == 0
                    ? "Story Canon updated successfully."
                    : $"Story Canon update completed with errors: {string.Join(", ", failures)}";
                await LoadAiHistoryAsync();
            }
            finally
            {
                _continuityBusy = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private static bool NeedsFullBibleBuild(BibleSnapshotDto? snapshot)
        {
            return snapshot is null || snapshot.LastRefreshUtc is null;
        }

        private async Task RefreshBibleAsync(string bibleType, bool fullRebuild)
        {
            if (_activeSection is null || _continuityBusy)
            {
                return;
            }

            _continuityBusy = true;
            _continuityStatus = null;
            try
            {
                RefreshBibleRequest request = new(fullRebuild, _activeSection.Id);
                using HttpResponseMessage response = await Http.PostAsJsonAsync(
                    $"api/documents/{DocumentId}/bibles/{bibleType}/refresh",
                    request);
                if (!response.IsSuccessStatusCode)
                {
                    if (await TryHandleEntitlementDeniedAsync(response, "ai.bibles.refresh", "Upgrade to enable Story Canon updates."))
                    {
                        _continuityStatus = _entitlementUserMessage;
                        return;
                    }

                    _continuityStatus = $"Story Canon update failed ({response.StatusCode}).";
                    return;
                }

                BibleSnapshotDto? snapshot = await response.Content.ReadFromJsonAsync<BibleSnapshotDto>();
                if (snapshot is not null)
                {
                    SetBibleSnapshot(snapshot);
                }

                _continuityStatus = fullRebuild
                    ? $"{bibleType} Story Canon rebuilt."
                    : $"{bibleType} Story Canon updated incrementally.";
                await LoadAiHistoryAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Story Canon update failed for type {BibleType}.", bibleType);
                _continuityStatus = $"{bibleType} Story Canon update failed.";
            }
            finally
            {
                _continuityBusy = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task<AiActionExecuteResponseDto?> ExecuteContinuityActionAsync(string actionKey, string successMessage, Dictionary<string, object?>? options)
        {
            if (_activeSection is null || _continuityBusy || !HasAction(actionKey))
            {
                return null;
            }

            _continuityBusy = true;
            _continuityStatus = null;
            try
            {
                string plain = string.Empty;
                if (_pageEditor is not null)
                {
                    plain = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(plain))
                    {
                        string htmlFromEditor = await _pageEditor.GetContentAsync();
                        plain = PlainTextMapper.ToPlainText(htmlFromEditor);
                    }
                }
                else
                {
                    plain = PlainTextMapper.ToPlainText(_activePage?.Content ?? string.Empty);
                }

                Logger.LogWarning(
                    "Continuity action source text resolved. Action={Action}, SectionId={SectionId}, PageId={PageId}, PlainLength={PlainLength}",
                    actionKey,
                    _activeSection.Id,
                    _activePage?.Id,
                    plain.Length);

                Dictionary<string, object?> resolvedOptions = options is null
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?>(options);
                if (string.Equals(actionKey, "continuity.check_section", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedOptions["character_bible_json"] = _characterBibleSnapshot?.ContentJson ?? "{}";
                    resolvedOptions["place_bible_json"] = _placeBibleSnapshot?.ContentJson ?? "{}";
                    resolvedOptions["timeline_bible_json"] = _timelineBibleSnapshot?.ContentJson ?? "{}";
                }

                AiActionExecuteRequestDto request = new(
                    DocumentId,
                    _activeSection.Id,
                    _activePage?.Id,
                    0,
                    plain.Length,
                    plain,
                    plain,
                    GetOutlineTextForAi(),
                    resolvedOptions);

                using HttpResponseMessage result = await PostAiActionAsync(actionKey, request);
                if (!result.IsSuccessStatusCode)
                {
                    if (await TryHandleEntitlementDeniedAsync(result, "ai.actions", "Upgrade to continue using AI features."))
                    {
                        _continuityStatus = _entitlementUserMessage;
                        return null;
                    }

                    if (await TryHandlePlanUpgradeRequiredAsync(result))
                    {
                        return null;
                    }

                    if (await TryHandleAiQuotaExceededAsync(result))
                    {
                        _continuityStatus = _aiQuotaMessage;
                        return null;
                    }

                    _continuityStatus = "Continuity action failed.";
                    return null;
                }

                AiActionExecuteResponseDto? response = await result.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                bool hasOperations = response?.Operations is { Count: > 0 };
                if (response is null || (string.IsNullOrWhiteSpace(response.ProposedText) && !hasOperations))
                {
                    _continuityStatus = "Continuity action returned no output.";
                    return null;
                }

                if (string.Equals(actionKey, "continuity.apply_fix", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning(
                        "Continuity apply_fix response received. ProposalId={ProposalId}, Operations={Operations}, ProposedTextLength={ProposedTextLength}, SectionId={SectionId}, PageId={PageId}",
                        response.ProposalId,
                        response.Operations?.Count ?? 0,
                        response.ProposedText?.Length ?? 0,
                        _activeSection.Id,
                        _activePage?.Id);
                }

                if (string.Equals(actionKey, "continuity.check_section", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseContinuityReport(response.ProposedText, out ContinuityReport? report) || report is null)
                    {
                        _continuityStatus = "Continuity report parsing failed.";
                        return null;
                    }

                    _continuityReport = NormalizeContinuityReport(report, plain.Length);
                    _selectedContinuityIssueKey = FilteredContinuityIssues.Select(GetContinuityIssueKey).FirstOrDefault();
                    _pendingContinuityHighlights = true;
                    await ApplyContinuityHighlightsAsync();
                }

                _continuityStatus = successMessage;
                await LoadAiHistoryAsync();
                return response;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Continuity action failed.");
                _continuityStatus = "Continuity action failed.";
                return null;
            }
            finally
            {
                await RefreshPlanUsageAsync();
                _continuityBusy = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task OnContinuitySeverityChanged(ChangeEventArgs args)
        {
            _continuitySeverityFilter = args.Value?.ToString() ?? "all";
            _selectedContinuityIssueKey = FilteredContinuityIssues.Select(GetContinuityIssueKey).FirstOrDefault();
            _pendingContinuityHighlights = true;
            await ApplyContinuityHighlightsAsync();
            await InvokeAsync(StateHasChanged);
        }

        private async Task OnJumpToContinuityIssueAsync(ContinuityIssue issue)
        {
            if (_pageEditor is null)
            {
                return;
            }

            _selectedContinuityIssueKey = GetContinuityIssueKey(issue);
            _pendingContinuityHighlights = true;
            await ApplyContinuityHighlightsAsync();
            await InvokePageCommandAsync("scrollToPosition", Math.Max(0, issue.Anchor.PlainTextStart));
            await InvokeAsync(StateHasChanged);
        }

        private async Task OpenContinuityProposalAsync(ContinuityIssue issue)
        {
            if (!CanShowContinuityCoachFixes || _continuityBusy || _activeSection is null)
            {
                return;
            }

            string plain = _pageEditor is null
                ? string.Empty
                : (await _pageEditor.GetPlainTextAsync() ?? string.Empty);
            ContinuityApplyRange? applyRange = await BuildContinuityApplyRangeAsync(issue, plain);
            if (applyRange is null)
            {
                _continuityStatus = "Can't apply automatically; text changed.";
                await InvokeAsync(StateHasChanged);
                return;
            }

            ContinuityIssue resolvedIssue = await EnsureContinuityIssueHasRevisedFixAsync(issue, plain, applyRange);
            string fixText = ResolveContinuityFixText(resolvedIssue);
            if (string.IsNullOrWhiteSpace(fixText) && !IsLikelyDuplicateContinuityIssue(resolvedIssue))
            {
                _continuityStatus = "The suggestion didn't contain revised prose. Please regenerate.";
                await InvokeAsync(StateHasChanged);
                return;
            }

            _pendingContinuityIssue = resolvedIssue;
            _pendingContinuityRange = applyRange;
            _continuityProposalPreview = BuildContinuityProposalPreview(applyRange, fixText);
            Logger.LogInformation(
                "Continuity proposal prepared. IssueKey={IssueKey}, IsDeletion={IsDeletion}, BeforeLength={BeforeLength}, AfterLength={AfterLength}",
                GetContinuityIssueKey(resolvedIssue),
                string.IsNullOrEmpty(fixText),
                applyRange.Before.Length,
                fixText.Length);
            _continuityProposalError = null;
            _isApplyingContinuityProposal = false;
            _isContinuityProposalOpen = true;

            _selectedContinuityIssueKey = GetContinuityIssueKey(resolvedIssue);
            _pendingContinuityHighlights = true;
            await ApplyContinuityHighlightsAsync();
            await InvokeAsync(StateHasChanged);
        }

        private static ContinuityProposalPreview BuildContinuityProposalPreview(ContinuityApplyRange range, string fixText)
        {
            bool isDeletion = string.IsNullOrEmpty(fixText);
            string after = isDeletion ? string.Empty : fixText;
            return new ContinuityProposalPreview(
                range.Before,
                after,
                range.Prefix,
                range.Suffix,
                range.PlainFrom,
                Math.Max(0, range.PlainTo - range.PlainFrom),
                isDeletion,
                range.Before);
        }

        private async Task ConfirmContinuityProposalApplyAsync()
        {
            if (_pendingContinuityIssue is null || _isApplyingContinuityProposal)
            {
                return;
            }

            _isApplyingContinuityProposal = true;
            _continuityProposalError = null;
            try
            {
                bool applied = await ApplyContinuityFixCoreAsync(_pendingContinuityIssue, _pendingContinuityRange);
                if (!applied)
                {
                    _continuityProposalError = _continuityStatus ?? "Continuity fix could not be applied.";
                    return;
                }

                RemoveContinuityIssueFromCurrentReport(_pendingContinuityIssue);
                await OnClearContinuityHighlightsAsync();
                CloseContinuityProposal();
            }
            finally
            {
                _isApplyingContinuityProposal = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task<bool> ApplyContinuityFixCoreAsync(ContinuityIssue issue, ContinuityApplyRange? proposalRange = null)
        {
            if (_activeSection is null || _pageEditor is null)
            {
                return false;
            }

            await FlushActiveEditorAsync("continuity-apply");

            string beforePlain = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
            ContinuityApplyRange? applyRange = proposalRange ?? await BuildContinuityApplyRangeAsync(issue, beforePlain);
            if (applyRange is null)
            {
                _continuityStatus = "Can't apply automatically; text changed.";
                return false;
            }

            bool staleRange = beforePlain.Length < applyRange.PlainTo
                || !string.Equals(
                    beforePlain.Substring(applyRange.PlainFrom, applyRange.PlainTo - applyRange.PlainFrom),
                    applyRange.Before,
                    StringComparison.Ordinal);
            if (staleRange)
            {
                Logger.LogInformation(
                    "Continuity apply detected stale plain range; delegating recovery to editor range resolver. IssueKey={IssueKey}, PlainFrom={PlainFrom}, PlainTo={PlainTo}",
                    GetContinuityIssueKey(issue),
                    applyRange.PlainFrom,
                    applyRange.PlainTo);
            }

            ContinuityIssue resolvedIssue = await EnsureContinuityIssueHasRevisedFixAsync(issue, beforePlain, applyRange);
            string fixText = ResolveContinuityFixText(resolvedIssue);
            if (string.IsNullOrWhiteSpace(fixText) && !IsLikelyDuplicateContinuityIssue(resolvedIssue))
            {
                _continuityStatus = "The suggestion didn't contain revised prose. Please regenerate.";
                return false;
            }

            string issueKey = GetContinuityIssueKey(resolvedIssue);
            Guid? pageId = _activePage?.Id;

            if (!string.IsNullOrEmpty(fixText)
                && !ContinuityRewriteValidator.ValidateReplacement(
                    applyRange.Prefix,
                    fixText,
                    applyRange.Suffix,
                    applyRange.StartsSentence,
                    applyRange.EndsSentence,
                    applyRange.Before.Length,
                    out string? replacementError))
            {
                _continuityStatus = "Suggestion didn't integrate cleanly; please regenerate.";
                Logger.LogWarning("Continuity replacement validation failed before apply. IssueKey={IssueKey}, Error={Error}", issueKey, replacementError);
                return false;
            }

            string kind = string.IsNullOrEmpty(fixText) ? "delete" : "replace";
            QualityIssueFixDto continuityFix = new(
                kind,
                applyRange.PlainFrom,
                applyRange.PlainTo,
                fixText,
                applyRange.Before,
                issueKey,
                applyRange.DocFrom,
                applyRange.DocTo,
                applyRange.Before,
                applyRange.Prefix,
                applyRange.Suffix,
                BuildNeedle(applyRange.Before));

            Logger.LogWarning(
                "Continuity direct apply start. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, IssueKey={IssueKey}, Kind={Kind}, PlainFrom={PlainFrom}, PlainTo={PlainTo}, DocFrom={DocFrom}, DocTo={DocTo}, ReplacementLength={ReplacementLength}",
                DocumentId,
                _activeSection.Id,
                pageId,
                issueKey,
                kind,
                applyRange.PlainFrom,
                applyRange.PlainTo,
                applyRange.DocFrom,
                applyRange.DocTo,
                fixText.Length);

            bool applySucceeded = await _pageEditor.ApplyQualityIssueFixAsync(continuityFix, applyRange.Before, issueKey);
            if (!applySucceeded)
            {
                _continuityStatus = GetQualityFixFailureMessage();
                return false;
            }

            string afterPlain = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
            if (string.Equals(beforePlain, afterPlain, StringComparison.Ordinal))
            {
                _continuityStatus = "Couldn't apply because text changed; please rerun continuity check.";
                Logger.LogWarning(
                    "Continuity fix produced no text change. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, ProposalId={ProposalId}, IssueKey={IssueKey}, BeforeLength={BeforeLength}, AfterLength={AfterLength}",
                    DocumentId,
                    _activeSection.Id,
                    pageId,
                    Guid.Empty,
                    issueKey,
                    beforePlain.Length,
                    afterPlain.Length);
                return false;
            }

            await _pageEditor.ForceSaveIfDifferentAsync("continuity-apply");
            _continuityStatus = "Continuity fix applied.";
            Logger.LogWarning(
                "Continuity fix applied. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, ProposalId={ProposalId}, IssueKey={IssueKey}, BeforeLength={BeforeLength}, AfterLength={AfterLength}",
                DocumentId,
                _activeSection.Id,
                pageId,
                Guid.Empty,
                issueKey,
                beforePlain.Length,
                afterPlain.Length);
            return true;
        }

        private async Task<ContinuityIssue> EnsureContinuityIssueHasRevisedFixAsync(ContinuityIssue issue, string plainText, ContinuityApplyRange applyRange)
        {
            string normalizedFix = ResolveContinuityFixText(issue);
            if (!string.IsNullOrWhiteSpace(normalizedFix)
                && ContinuityRewriteValidator.ValidateReplacement(
                    applyRange.Prefix,
                    normalizedFix,
                    applyRange.Suffix,
                    applyRange.StartsSentence,
                    applyRange.EndsSentence,
                    applyRange.Before.Length,
                    out _))
            {
                return issue with { SuggestedFix = normalizedFix };
            }

            if (IsLikelyDuplicateContinuityIssue(issue))
            {
                return issue with { SuggestedFix = string.Empty };
            }

            string firstAttempt = await GenerateContinuityRewriteAsync(issue, plainText, applyRange, strictMode: false);
            if (!string.IsNullOrWhiteSpace(firstAttempt))
            {
                Logger.LogWarning(
                    "Continuity rewrite retry succeeded. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, Strict={Strict}, IssueKey={IssueKey}, TextLength={TextLength}",
                    DocumentId,
                    _activeSection?.Id,
                    _activePage?.Id,
                    false,
                    GetContinuityIssueKey(issue),
                    firstAttempt.Length);
                return issue with { SuggestedFix = firstAttempt };
            }

            string strictAttempt = await GenerateContinuityRewriteAsync(issue, plainText, applyRange, strictMode: true);
            if (!string.IsNullOrWhiteSpace(strictAttempt))
            {
                Logger.LogWarning(
                    "Continuity rewrite retry succeeded. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, Strict={Strict}, IssueKey={IssueKey}, TextLength={TextLength}",
                    DocumentId,
                    _activeSection?.Id,
                    _activePage?.Id,
                    true,
                    GetContinuityIssueKey(issue),
                    strictAttempt.Length);
                return issue with { SuggestedFix = strictAttempt };
            }

            Logger.LogWarning(
                "Continuity rewrite retry failed. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, IssueKey={IssueKey}",
                DocumentId,
                _activeSection?.Id,
                _activePage?.Id,
                GetContinuityIssueKey(issue));
            return issue with { SuggestedFix = string.Empty };
        }

        private async Task<string> GenerateContinuityRewriteAsync(ContinuityIssue issue, string plainText, ContinuityApplyRange applyRange, bool strictMode)
        {
            if (_activeSection is null)
            {
                return string.Empty;
            }

            string source = plainText ?? string.Empty;
            int start = applyRange.PlainFrom;
            int end = applyRange.PlainTo;
            int length = Math.Max(0, end - start);
            if (length <= 0 || source.Length < end)
            {
                return string.Empty;
            }

            string selectedText = source.Substring(start, length);
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                return string.Empty;
            }

            string instruction = strictMode
                ? $"Rewrite exactly the selected span to resolve this continuity issue while preserving voice and style: {issue.Message}. Output rewritten span text only. Do not include prefix or suffix. Do not output instructions, analysis, labels, markdown, bullets, or quotes around the full answer. The output must integrate cleanly with prefix and suffix, must not start/end mid-word, should start with a capital letter when the span starts a sentence, and should end with sentence punctuation when the span ends a sentence."
                : $"Rewrite exactly the selected span to resolve this continuity issue while preserving voice and style: {issue.Message}. Return revised span only (no prefix/suffix, no explanation). Avoid repeating prefix/suffix text at boundaries.";

            Dictionary<string, object?> parameters = new()
            {
                ["instruction"] = instruction,
                ["tone"] = "Neutral",
                ["length"] = "Same",
                ["preserve_terms"] = true
            };

            AiActionExecuteRequestDto request = new(
                DocumentId,
                _activeSection.Id,
                _activePage?.Id,
                start,
                end,
                selectedText,
                source,
                GetOutlineTextForAi(),
                parameters);

            try
            {
                using HttpResponseMessage result = await PostAiActionAsync("rewrite.selection", request, commandLabel: "Rewrite selection");
                if (!result.IsSuccessStatusCode)
                {
                    await TryHandleAiQuotaExceededAsync(result);
                    Logger.LogWarning(
                        "Continuity rewrite retry request failed. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, Strict={Strict}, StatusCode={StatusCode}, IssueKey={IssueKey}",
                        DocumentId,
                        _activeSection.Id,
                        _activePage?.Id,
                        strictMode,
                        result.StatusCode,
                        GetContinuityIssueKey(issue));
                    return string.Empty;
                }

                AiActionExecuteResponseDto? response = await result.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                string candidate = NormalizeContinuityRewriteCandidate(response?.ProposedText);
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    Logger.LogWarning(
                        "Continuity rewrite retry returned invalid prose. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, Strict={Strict}, IssueKey={IssueKey}, ProposedPreview={ProposedPreview}",
                        DocumentId,
                        _activeSection.Id,
                        _activePage?.Id,
                        strictMode,
                        GetContinuityIssueKey(issue),
                        CreateLogPreview(response?.ProposedText, 160));
                    return string.Empty;
                }

                if (!ContinuityRewriteValidator.ValidateReplacement(
                    applyRange.Prefix,
                    candidate,
                    applyRange.Suffix,
                    applyRange.StartsSentence,
                    applyRange.EndsSentence,
                    applyRange.Before.Length,
                    out string? validationError))
                {
                    Logger.LogWarning(
                        "Continuity rewrite retry rejected by join validation. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, Strict={Strict}, IssueKey={IssueKey}, Error={Error}, CandidatePreview={CandidatePreview}",
                        DocumentId,
                        _activeSection.Id,
                        _activePage?.Id,
                        strictMode,
                        GetContinuityIssueKey(issue),
                        validationError,
                        CreateLogPreview(candidate, 160));
                    return string.Empty;
                }

                return candidate;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    ex,
                    "Continuity rewrite retry threw. DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, Strict={Strict}, IssueKey={IssueKey}",
                    DocumentId,
                    _activeSection.Id,
                    _activePage?.Id,
                    strictMode,
                    GetContinuityIssueKey(issue));
                return string.Empty;
            }
            finally
            {
                await RefreshPlanUsageAsync();
            }
        }

        private async Task<ContinuityApplyRange?> BuildContinuityApplyRangeAsync(ContinuityIssue issue, string plainText)
        {
            if (_pageEditor is null)
            {
                return null;
            }

            ContinuityRewriteSpan expanded = ContinuityRewriteSpanResolver.ExpandToSentenceSpan(
                plainText,
                issue.Anchor.PlainTextStart,
                issue.Anchor.PlainTextLength,
                contextRadius: 56);

            if (expanded.Length <= 0 || string.IsNullOrWhiteSpace(expanded.Before))
            {
                return null;
            }

            PageEditor.QualityIssueRangeResolution? resolved = await _pageEditor.ResolvePlainRangeAsync(
                expanded.Start,
                expanded.Start + expanded.Length,
                expanded.Before);
            if (resolved is null
                || !resolved.Resolved
                || !resolved.DocFrom.HasValue
                || !resolved.DocTo.HasValue
                || !resolved.From.HasValue
                || !resolved.To.HasValue)
            {
                Logger.LogWarning(
                    "Continuity range resolution failed. IssueKey={IssueKey}, Reason={Reason}, Source={Source}",
                    GetContinuityIssueKey(issue),
                    resolved?.Reason,
                    resolved?.Source);
                return null;
            }

            int plainFrom = resolved.From.Value;
            int plainTo = resolved.To.Value;
            int docFrom = resolved.DocFrom.Value;
            int docTo = resolved.DocTo.Value;
            string source = resolved.Source ?? "resolved";
            if (plainTo <= plainFrom || plainText.Length < plainTo)
            {
                return null;
            }

            ContinuityRewriteSpan sentenceAligned = ContinuityRewriteSpanResolver.ExpandToSentenceSpan(
                plainText,
                plainFrom,
                plainTo - plainFrom,
                contextRadius: 56);

            bool needsSentenceRealignment = sentenceAligned.Start != plainFrom
                || sentenceAligned.Length != (plainTo - plainFrom);
            if (needsSentenceRealignment)
            {
                PageEditor.QualityIssueRangeResolution? sentenceResolution = await _pageEditor.ResolvePlainRangeAsync(
                    sentenceAligned.Start,
                    sentenceAligned.Start + sentenceAligned.Length,
                    sentenceAligned.Before);
                if (sentenceResolution is null
                    || !sentenceResolution.Resolved
                    || !sentenceResolution.DocFrom.HasValue
                    || !sentenceResolution.DocTo.HasValue
                    || !sentenceResolution.From.HasValue
                    || !sentenceResolution.To.HasValue
                    || sentenceResolution.To.Value <= sentenceResolution.From.Value)
                {
                    Logger.LogWarning(
                        "Continuity sentence realignment failed. IssueKey={IssueKey}, Reason={Reason}, Source={Source}",
                        GetContinuityIssueKey(issue),
                        sentenceResolution?.Reason,
                        sentenceResolution?.Source);
                    return null;
                }

                plainFrom = sentenceResolution.From.Value;
                plainTo = sentenceResolution.To.Value;
                docFrom = sentenceResolution.DocFrom.Value;
                docTo = sentenceResolution.DocTo.Value;
                source = sentenceResolution.Source ?? "resolved-sentence";
            }

            if (plainTo <= plainFrom || plainText.Length < plainTo)
            {
                return null;
            }

            ContinuityRewriteSpan finalContext = ContinuityRewriteSpanResolver.BuildFromRange(
                plainText,
                plainFrom,
                plainTo - plainFrom,
                contextRadius: 56);

            Logger.LogWarning(
                "Continuity apply range resolved. IssueKey={IssueKey}, Source={Source}, PlainFrom={PlainFrom}, PlainTo={PlainTo}, DocFrom={DocFrom}, DocTo={DocTo}, StartsSentence={StartsSentence}, EndsSentence={EndsSentence}, BeforeLength={BeforeLength}",
                GetContinuityIssueKey(issue),
                source,
                plainFrom,
                plainTo,
                docFrom,
                docTo,
                finalContext.StartsSentence,
                finalContext.EndsSentence,
                finalContext.Before.Length);

            return new ContinuityApplyRange(
                plainFrom,
                plainTo,
                docFrom,
                docTo,
                finalContext.Before,
                finalContext.Prefix,
                finalContext.Suffix,
                finalContext.StartsSentence,
                finalContext.EndsSentence,
                source);
        }

        private static bool TryRemapContinuityOperations(
            IReadOnlyList<AiTextOperationDto> operations,
            string plainText,
            out List<AiTextOperationDto> remapped,
            out string? error)
        {
            remapped = new List<AiTextOperationDto>();
            error = null;
            if (operations is null || operations.Count == 0)
            {
                error = "No operations to apply.";
                return false;
            }

            string text = plainText ?? string.Empty;
            int textLength = text.Length;
            int lastEnd = 0;
            List<AiTextOperationDto> ascending = operations
                .OrderBy(operation => operation.From)
                .ThenBy(operation => operation.To)
                .ToList();

            foreach (AiTextOperationDto operation in ascending)
            {
                int from = Math.Clamp(operation.From, 0, textLength);
                int to = Math.Clamp(operation.To, from, textLength);
                string expected = operation.ExpectedText ?? string.Empty;
                string kind = operation.Type?.Trim().ToLowerInvariant() ?? string.Empty;
                bool needsSpan = string.Equals(kind, "replace", StringComparison.Ordinal)
                    || string.Equals(kind, "delete", StringComparison.Ordinal);

                bool exactMatch = string.IsNullOrEmpty(expected)
                    || (to >= from
                        && to <= textLength
                        && string.Equals(text.Substring(from, to - from), expected, StringComparison.Ordinal));

                if (!exactMatch && !string.IsNullOrEmpty(expected))
                {
                    List<int> candidates = FindAllOccurrences(text, expected);
                    if (candidates.Count == 0)
                    {
                        error = "Can't apply automatically; text changed.";
                        return false;
                    }

                    int chosenStart = candidates
                        .OrderBy(candidate => candidate < lastEnd ? 1 : 0)
                        .ThenBy(candidate => Math.Abs(candidate - operation.From))
                        .First();
                    from = chosenStart;
                    to = Math.Min(textLength, chosenStart + expected.Length);
                }

                if (needsSpan && to <= from)
                {
                    error = "Can't apply automatically; text changed.";
                    return false;
                }

                if (from < lastEnd)
                {
                    error = "Overlapping continuity operations are not supported.";
                    return false;
                }

                remapped.Add(new AiTextOperationDto(
                    operation.Type?.Trim() ?? string.Empty,
                    from,
                    to,
                    operation.Text,
                    operation.ExpectedText));
                lastEnd = Math.Max(lastEnd, to);
            }

            return true;
        }

        private static List<int> FindAllOccurrences(string source, string value)
        {
            List<int> positions = new();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            {
                return positions;
            }

            int start = 0;
            while (start <= source.Length - value.Length)
            {
                int index = source.IndexOf(value, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                positions.Add(index);
                start = index + Math.Max(1, value.Length);
            }

            return positions;
        }

        private static string ResolveContinuityFixText(ContinuityIssue issue)
        {
            string suggested = issue.SuggestedFix?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(suggested))
            {
                return string.Empty;
            }

            string normalized = NormalizeContinuityRewriteCandidate(suggested);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            if (IsLikelyDuplicateContinuityIssue(issue))
            {
                // For duplicate/repeated paragraph issues, instruction-like fix text means "remove duplicate span".
                return string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizeContinuityRewriteCandidate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string candidate = text.Trim();
            if (TryExtractRevisedTextCandidate(candidate, out string extracted))
            {
                candidate = extracted.Trim();
            }

            return LooksLikeInstructionLeak(candidate) ? string.Empty : candidate;
        }

        private static bool TryExtractRevisedTextCandidate(string source, out string revised)
        {
            revised = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string value = source.Trim();
            int revisedStart = value.IndexOf("<<REVISED>>", StringComparison.OrdinalIgnoreCase);
            int revisedEnd = value.IndexOf("<<END>>", StringComparison.OrdinalIgnoreCase);
            if (revisedStart >= 0 && revisedEnd > revisedStart)
            {
                int contentStart = revisedStart + "<<REVISED>>".Length;
                revised = value.Substring(contentStart, revisedEnd - contentStart).Trim();
                return !string.IsNullOrWhiteSpace(revised);
            }

            if (value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(value);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("revisedText", out JsonElement revisedText)
                        && revisedText.ValueKind == JsonValueKind.String)
                    {
                        revised = revisedText.GetString()?.Trim() ?? string.Empty;
                        return !string.IsNullOrWhiteSpace(revised);
                    }
                }
                catch (JsonException)
                {
                }
            }

            MatchCollection quotedMatches = Regex.Matches(value, "\"([^\"]{24,})\"");
            if (quotedMatches.Count > 0)
            {
                Match longest = quotedMatches
                    .Cast<Match>()
                    .OrderByDescending(match => match.Groups[1].Value.Length)
                    .First();
                revised = longest.Groups[1].Value.Trim();
                return !string.IsNullOrWhiteSpace(revised);
            }

            return false;
        }

        private static bool IsLikelyDuplicateContinuityIssue(ContinuityIssue issue)
        {
            string type = issue.Type?.Trim().ToLowerInvariant() ?? string.Empty;
            string message = issue.Message?.Trim().ToLowerInvariant() ?? string.Empty;
            return type.Contains("repeat", StringComparison.Ordinal)
                || type.Contains("duplicate", StringComparison.Ordinal)
                || message.Contains("repeat", StringComparison.Ordinal)
                || message.Contains("duplicate", StringComparison.Ordinal)
                || message.Contains("same paragraph", StringComparison.Ordinal)
                || message.Contains("repeated paragraph", StringComparison.Ordinal);
        }

        private static string CreateLogPreview(string? value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (normalized.Length <= maxChars)
            {
                return normalized;
            }

            return normalized.Substring(0, Math.Max(0, maxChars)) + "...";
        }

        private static bool TryValidateContinuityOperations(
            IReadOnlyList<AiTextOperationDto> operations,
            string beforePlain,
            out string? error)
        {
            error = null;
            int length = beforePlain.Length;

            List<AiTextOperationDto> ascending = operations
                .OrderBy(operation => operation.From)
                .ThenBy(operation => operation.To)
                .ToList();

            for (int index = 0; index < ascending.Count; index++)
            {
                AiTextOperationDto operation = ascending[index];
                string kind = operation.Type?.Trim().ToLowerInvariant() ?? string.Empty;
                if (!string.Equals(kind, "replace", StringComparison.Ordinal)
                    && !string.Equals(kind, "delete", StringComparison.Ordinal)
                    && !string.Equals(kind, "insert", StringComparison.Ordinal))
                {
                    error = "Unsupported continuity operation.";
                    return false;
                }

                if (operation.From < 0 || operation.To < 0 || operation.From > operation.To || operation.To > length)
                {
                    error = "Can't apply automatically; text changed.";
                    return false;
                }

                if (index > 0)
                {
                    AiTextOperationDto prev = ascending[index - 1];
                    if (operation.From < prev.To)
                    {
                        error = "Overlapping continuity operations are not supported.";
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(operation.ExpectedText))
                {
                    string actual = beforePlain.Substring(operation.From, operation.To - operation.From);
                    if (!string.Equals(actual, operation.ExpectedText, StringComparison.Ordinal))
                    {
                        error = "Can't apply automatically; text changed.";
                        return false;
                    }
                }

                if ((string.Equals(kind, "replace", StringComparison.Ordinal) || string.Equals(kind, "insert", StringComparison.Ordinal))
                    && LooksLikeInstructionLeak(operation.Text))
                {
                    error = "Continuity fix was rejected because replacement text looked like instructions.";
                    return false;
                }
            }

            return true;
        }

        private static bool LooksLikeInstructionLeak(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim();
            string lowered = normalized.ToLowerInvariant();
            string[] imperativeStarts =
            {
                "adjust ",
                "change ",
                "fix ",
                "rewrite ",
                "update ",
                "make ",
                "ensure ",
                "move ",
                "remove ",
                "replace ",
                "delete ",
                "insert "
            };

            if (imperativeStarts.Any(prefix => lowered.StartsWith(prefix, StringComparison.Ordinal)))
            {
                return true;
            }

            if (Regex.IsMatch(normalized, @"^\s*(?:-|\*|\d+\.)\s+", RegexOptions.Multiline))
            {
                return true;
            }

            string firstLine = normalized.Split('\n')[0].Trim();
            if (firstLine.Length <= 80 && firstLine.EndsWith(":", StringComparison.Ordinal))
            {
                return true;
            }

            if (lowered.Contains("remove duplicate paragraph", StringComparison.Ordinal)
                || lowered.Contains("highlighted range", StringComparison.Ordinal)
                || lowered.Contains("remove the repeated paragraphs", StringComparison.Ordinal)
                || lowered.Contains("remove repeated paragraph", StringComparison.Ordinal)
                || lowered.Contains("maintain narrative clarity", StringComparison.Ordinal)
                || lowered.Contains("improve narrative clarity", StringComparison.Ordinal)
                || lowered.Contains("as an ai", StringComparison.Ordinal)
                || lowered.Contains("openai", StringComparison.Ordinal)
                || lowered.Contains("responses", StringComparison.Ordinal)
                || lowered.Contains("tool:", StringComparison.Ordinal)
                || lowered.Contains("system:", StringComparison.Ordinal)
                || lowered.Contains("assistant:", StringComparison.Ordinal)
                || lowered.Contains("you are ", StringComparison.Ordinal)
                || lowered.Contains("\"model\"", StringComparison.Ordinal)
                || lowered.Contains("\"input\"", StringComparison.Ordinal)
                || lowered.Contains("arrives before", StringComparison.Ordinal)
                || lowered.Contains("arrives after", StringComparison.Ordinal)
                || lowered.StartsWith("instruction:", StringComparison.Ordinal)
                || lowered.StartsWith("analysis:", StringComparison.Ordinal)
                || lowered.StartsWith("explanation:", StringComparison.Ordinal))
            {
                return true;
            }

            if (normalized.Length <= 220
                && (normalized.StartsWith("Please ", StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith("Use ", StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith("Return ", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static string BuildNeedle(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            string normalized = source.Trim();
            int maxLength = Math.Min(80, normalized.Length);
            if (maxLength <= 0)
            {
                return string.Empty;
            }

            int minLength = Math.Min(30, maxLength);
            int length = Math.Max(minLength, maxLength);
            int start = Math.Max(0, (normalized.Length - length) / 2);
            return normalized.Substring(start, length);
        }

        private async Task RecomputeContinuityRangeAsync()
        {
            if (_pendingContinuityIssue is null || _pageEditor is null)
            {
                return;
            }

            string plain = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
            ContinuityApplyRange? recalculated = await BuildContinuityApplyRangeAsync(_pendingContinuityIssue, plain);
            if (recalculated is null)
            {
                _continuityProposalError = "The text changed and we couldn't safely locate the target range. Click 'Show in text' then try again.";
                await InvokeAsync(StateHasChanged);
                return;
            }

            _pendingContinuityRange = recalculated;
            string fixText = ResolveContinuityFixText(_pendingContinuityIssue);
            _continuityProposalPreview = BuildContinuityProposalPreview(recalculated, fixText);
            _continuityProposalError = null;
            await InvokeAsync(StateHasChanged);
        }

        private async Task ShowPendingContinuityIssueInTextAsync()
        {
            if (_pendingContinuityIssue is null)
            {
                return;
            }

            await OnJumpToContinuityIssueAsync(_pendingContinuityIssue);
        }

        private void CancelContinuityProposal()
        {
            if (_isApplyingContinuityProposal)
            {
                return;
            }

            CloseContinuityProposal();
        }

        private void CloseContinuityProposal()
        {
            _isContinuityProposalOpen = false;
            _pendingContinuityIssue = null;
            _pendingContinuityRange = null;
            _continuityProposalPreview = null;
            _continuityProposalError = null;
            _isApplyingContinuityProposal = false;
        }

        private void OnContinuityProposalKeyDown(KeyboardEventArgs args)
        {
            if (!string.Equals(args.Key, "Escape", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CancelContinuityProposal();
        }

        private void RemoveContinuityIssueFromCurrentReport(ContinuityIssue issue)
        {
            if (_continuityReport is null || _continuityReport.Issues.Count == 0)
            {
                return;
            }

            string targetKey = GetContinuityIssueKey(issue);
            List<ContinuityIssue> remaining = _continuityReport.Issues
                .Where(item => !string.Equals(GetContinuityIssueKey(item), targetKey, StringComparison.Ordinal))
                .ToList();

            if (remaining.Count == _continuityReport.Issues.Count)
            {
                return;
            }

            _continuityReport = _continuityReport with { Issues = remaining };
            _selectedContinuityIssueKey = remaining.Select(GetContinuityIssueKey).FirstOrDefault();
            _pendingContinuityHighlights = true;
        }

        private async Task OnClearContinuityHighlightsAsync()
        {
            _selectedContinuityIssueKey = null;
            _pendingContinuityHighlights = false;
            await ClearContinuityHighlightsAsync();
            await InvokeAsync(StateHasChanged);
        }

        private async Task ApplyContinuityHighlightsAsync()
        {
            if (_pageEditor is null)
            {
                _pendingContinuityHighlights = true;
                return;
            }

            List<PageEditor.AiDecorationRange> ranges = FilteredContinuityIssues
                .Select(issue =>
                {
                    int start = Math.Max(0, issue.Anchor.PlainTextStart);
                    int end = start + Math.Max(1, issue.Anchor.PlainTextLength);
                    return new PageEditor.AiDecorationRange(
                        start,
                        end,
                        GetContinuityIssueCssClass(issue),
                        string.Equals(_selectedContinuityIssueKey, GetContinuityIssueKey(issue), StringComparison.Ordinal));
                })
                .ToList();

            await _pageEditor.SetAiDecorationsAsync(ranges);
        }

        private async Task ClearContinuityHighlightsAsync()
        {
            if (_pageEditor is null)
            {
                return;
            }

            await _pageEditor.ClearAiDecorationsAsync();
        }

        private async Task LoadBibleSnapshotsAsync()
        {
            try
            {
                _characterBibleSnapshot = await Http.GetFromJsonAsync<BibleSnapshotDto>(
                    $"api/documents/{DocumentId}/bibles/character");
                _placeBibleSnapshot = await Http.GetFromJsonAsync<BibleSnapshotDto>(
                    $"api/documents/{DocumentId}/bibles/place");
                _timelineBibleSnapshot = await Http.GetFromJsonAsync<BibleSnapshotDto>(
                    $"api/documents/{DocumentId}/bibles/timeline");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Bible snapshot load failed.");
            }
        }

        private void SetBibleSnapshot(BibleSnapshotDto snapshot)
        {
            if (string.Equals(snapshot.BibleType, "character", StringComparison.OrdinalIgnoreCase))
            {
                _characterBibleSnapshot = snapshot;
            }
            else if (string.Equals(snapshot.BibleType, "place", StringComparison.OrdinalIgnoreCase))
            {
                _placeBibleSnapshot = snapshot;
            }
            else if (string.Equals(snapshot.BibleType, "timeline", StringComparison.OrdinalIgnoreCase))
            {
                _timelineBibleSnapshot = snapshot;
            }
        }

        private static string GetBibleStatusLabel(BibleSnapshotDto? snapshot)
        {
            if (snapshot is null || snapshot.LastRefreshUtc is null)
            {
                return "Not built yet";
            }

            return $"Last refresh {snapshot.LastRefreshUtc.Value.ToLocalTime():g}, changed sections pending {snapshot.ChangedSectionsSinceLastRefresh}";
        }

        private async Task LoadPromptPresetsAsync()
        {
            if (!CanShowPromptLibrary)
            {
                _promptPresets.Clear();
                return;
            }

            try
            {
                List<PromptPresetDto>? presets = await Http.GetFromJsonAsync<List<PromptPresetDto>>(
                    $"api/ai/presets?projectId={DocumentId}");
                _promptPresets.Clear();
                if (presets is not null)
                {
                    _promptPresets.AddRange(presets);
                }

                _pinnedPromptPresetIds.RemoveAll(id => _promptPresets.All(preset => preset.Id != id));
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Prompt presets load failed.");
                _promptStatus = "Failed to load presets.";
            }
        }

        private void BeginCreatePromptPreset()
        {
            _promptEditingId = null;
            _promptNameDraft = string.Empty;
            _promptCategoryDraft = string.Empty;
            _promptKindDraft = "builtin";
            _promptBuiltinActionIdDraft = "rewrite.selection";
            _promptTemplateDraft = string.Empty;
            _promptParametersDraft = "{}";
            _promptStatus = null;
        }

        private void BeginEditPromptPreset(PromptPresetDto preset)
        {
            _promptEditingId = preset.Id;
            _promptNameDraft = preset.Name;
            _promptCategoryDraft = preset.Category ?? string.Empty;
            _promptKindDraft = preset.Kind;
            _promptBuiltinActionIdDraft = preset.BuiltinActionId ?? "rewrite.selection";
            _promptTemplateDraft = preset.TemplateText ?? string.Empty;
            _promptParametersDraft = JsonSerializer.Serialize(preset.Parameters ?? new Dictionary<string, object?>(), JsonOptions);
            _promptStatus = null;
        }

        private async Task SavePromptPresetAsync()
        {
            if (!CanShowPromptLibrary)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_promptNameDraft))
            {
                _promptStatus = "Preset name is required.";
                return;
            }

            if (!TryParsePromptParameters(_promptParametersDraft, out Dictionary<string, object?> parameters, out string? parseError))
            {
                _promptStatus = parseError;
                return;
            }

            UpsertPromptPresetRequest request = new(
                DocumentId,
                _promptNameDraft.Trim(),
                string.IsNullOrWhiteSpace(_promptCategoryDraft) ? null : _promptCategoryDraft.Trim(),
                _promptKindDraft,
                _promptKindDraft == "builtin" ? _promptBuiltinActionIdDraft : null,
                _promptKindDraft == "custom" ? _promptTemplateDraft : null,
                parameters);

            try
            {
                HttpResponseMessage response;
                if (_promptEditingId.HasValue)
                {
                    response = await Http.PutAsJsonAsync($"api/ai/presets/{_promptEditingId.Value}", request);
                }
                else
                {
                    response = await Http.PostAsJsonAsync("api/ai/presets", request);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _promptStatus = $"Save failed ({response.StatusCode}).";
                    return;
                }

                _promptStatus = "Preset saved.";
                await LoadPromptPresetsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Prompt preset save failed.");
                _promptStatus = "Preset save failed.";
            }
        }

        private async Task DeletePromptPresetAsync(Guid presetId)
        {
            try
            {
                using HttpResponseMessage response = await Http.DeleteAsync($"api/ai/presets/{presetId}");
                if (!response.IsSuccessStatusCode)
                {
                    _promptStatus = $"Delete failed ({response.StatusCode}).";
                    return;
                }

                _pinnedPromptPresetIds.RemoveAll(id => id == presetId);
                if (_promptEditingId == presetId)
                {
                    BeginCreatePromptPreset();
                }

                _promptStatus = "Preset deleted.";
                await LoadPromptPresetsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Prompt preset delete failed.");
                _promptStatus = "Preset delete failed.";
            }
        }

        private void TogglePinPromptPreset(Guid presetId)
        {
            int existingIndex = _pinnedPromptPresetIds.FindIndex(id => id == presetId);
            if (existingIndex >= 0)
            {
                _pinnedPromptPresetIds.RemoveAt(existingIndex);
                return;
            }

            if (_pinnedPromptPresetIds.Count >= 3)
            {
                _promptStatus = "You can pin up to 3 presets.";
                return;
            }

            _pinnedPromptPresetIds.Add(presetId);
        }

        private bool IsPromptPresetPinned(Guid presetId)
        {
            return _pinnedPromptPresetIds.Contains(presetId);
        }

        private async Task RunPromptPresetAsync(PromptPresetDto preset)
        {
            if (!CanShowPromptLibrary)
            {
                _promptStatus = GetFeatureTooltip(FeatureKey.PromptLibrary);
                return;
            }

            string scope = _promptRunScope;
            if (scope == "selection" && _currentSelectionRange is null)
            {
                _promptStatus = "Select text to run this on selection scope.";
                return;
            }

            string actionKey = ResolvePresetActionKey(preset, scope);
            if (string.IsNullOrWhiteSpace(actionKey) || !HasAction(actionKey))
            {
                _promptStatus = $"Action '{actionKey}' is not available.";
                return;
            }

            Dictionary<string, object?> parameters = NormalizePromptParameters(preset.Parameters);
            if (string.Equals(preset.Kind, "custom", StringComparison.OrdinalIgnoreCase))
            {
                parameters["template"] = preset.TemplateText ?? string.Empty;
                parameters["scope"] = scope;
            }

            bool requiresSelection = scope == "selection";
            AiActionOption option = new(
                actionKey,
                preset.Name,
                preset.Name,
                requiresSelection,
                parameters,
                preset.Category,
                false);

            await OnAiActionSelected(option);
            _promptStatus = "Preset executed. Review preview and apply.";
        }

        private async Task<HttpResponseMessage> PostAiActionAsync(
            string actionKey,
            AiActionExecuteRequestDto request,
            bool trackStatus = true,
            string? commandLabel = null)
        {
            string label = string.IsNullOrWhiteSpace(commandLabel) ? GetActionLabel(actionKey) : commandLabel.Trim();
            if (trackStatus)
            {
                AiCommandStatusService.Start(label);
            }

            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync($"api/ai/actions/{actionKey}/execute", request);
                if (trackStatus)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        AiCommandStatusService.Complete(label);
                    }
                    else
                    {
                        AiCommandStatusService.Clear();
                    }
                }

                return response;
            }
            catch
            {
                if (trackStatus)
                {
                    AiCommandStatusService.Clear();
                }

                throw;
            }
        }

        private string ResolvePresetActionKey(PromptPresetDto preset, string scope)
        {
            if (string.Equals(preset.Kind, "custom", StringComparison.OrdinalIgnoreCase))
            {
                return "custom_transform";
            }

            string actionKey = preset.BuiltinActionId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                return string.Empty;
            }

            if (scope == "selection" && actionKey.EndsWith(".section", StringComparison.Ordinal))
            {
                string selectionActionKey = actionKey.Substring(0, actionKey.Length - ".section".Length) + ".selection";
                if (HasAction(selectionActionKey))
                {
                    return selectionActionKey;
                }
            }

            if (scope == "section" && actionKey.EndsWith(".selection", StringComparison.Ordinal))
            {
                string sectionActionKey = actionKey.Substring(0, actionKey.Length - ".selection".Length) + ".section";
                if (HasAction(sectionActionKey))
                {
                    return sectionActionKey;
                }
            }

            return actionKey;
        }

        private static bool TryParsePromptParameters(
            string json,
            out Dictionary<string, object?> parameters,
            out string? error)
        {
            parameters = new Dictionary<string, object?>();
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                Dictionary<string, object?>? parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
                parameters = parsed ?? new Dictionary<string, object?>();
                return true;
            }
            catch (JsonException)
            {
                error = "Parameters must be valid JSON object.";
                return false;
            }
        }

        private static Dictionary<string, object?> NormalizePromptParameters(Dictionary<string, object?> parameters)
        {
            Dictionary<string, object?> normalized = new();
            foreach ((string key, object? value) in parameters)
            {
                if (value is JsonElement element)
                {
                    normalized[key] = ConvertJsonElement(element);
                }
                else
                {
                    normalized[key] = value;
                }
            }

            return normalized;
        }

        private static object? ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out long longValue)
                    ? longValue
                    : (element.TryGetDouble(out double doubleValue) ? doubleValue : element.GetRawText()),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText()
            };
        }

        private void OnSceneNarrativeRoleChanged(ChangeEventArgs args)
        {
            _sceneNarrativeRole = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneNarrativeIntentInput(ChangeEventArgs args)
        {
            _sceneNarrativeIntent = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneSummaryInput(ChangeEventArgs args)
        {
            _sceneSummary = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneMetadataStatusChanged(ChangeEventArgs args)
        {
            _sceneCardMetadataStatus = NormalizeSceneCardStatus(args.Value?.ToString());
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

        private void OnScenePovInput(ChangeEventArgs args)
        {
            _scenePovCharacterId = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneSubplotTagsInput(ChangeEventArgs args)
        {
            _sceneSubplotTagsText = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnScenePlaceInput(ChangeEventArgs args)
        {
            _scenePlaceId = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneTimelineEventInput(ChangeEventArgs args)
        {
            _sceneTimelineEventId = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneTimeRefInput(ChangeEventArgs args)
        {
            _sceneTimeRef = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneTagsInput(ChangeEventArgs args)
        {
            _sceneTagsText = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneReferencesJsonInput(ChangeEventArgs args)
        {
            _sceneReferencesJson = args.Value?.ToString() ?? string.Empty;
            OnSceneCardInputChanged();
        }

        private void OnSceneCardInputChanged()
        {
            _sceneStatus = null;
            QueueSceneCardAutosave();
        }

        private async Task OnSceneCardSave()
        {
            if (_activeSection is null && !IsSceneRoute)
            {
                return;
            }

            await SaveSceneCardAsync(_activeSection?.Id ?? Guid.Empty, isAutosave: false);
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
                if (IsSceneRoute)
                {
                    SceneCardDto? card =
                        await Http.GetFromJsonAsync<SceneCardDto>($"api/scenes/{SceneNodeId}/scene-card");
                    _sceneSummary = card?.Summary ?? string.Empty;
                    _sceneCardMetadataStatus = NormalizeSceneCardStatus(card?.Status);
                    _sceneNarrativeRole = card?.NarrativeRole ?? GetNormalizedLegacyNarrativeRole(card?.NarrativePurpose) ?? string.Empty;
                    _sceneNarrativeIntent = card?.NarrativeIntent ?? GetLegacyNarrativeIntent(card?.NarrativePurpose) ?? string.Empty;
                    _sceneEmotionalBeat = card?.EmotionalBeat ?? string.Empty;
                    _sceneKeyEvents = card?.KeyEvents ?? string.Empty;
                    _sceneOpenQuestions = card?.OpenQuestions ?? string.Empty;
                    _scenePovCharacterId = card?.PovCharacterId ?? string.Empty;
                    _sceneSubplotTagsText = string.Join(", ", card?.SubplotTags ?? Array.Empty<string>());
                    _scenePlaceId = card?.PlaceId ?? string.Empty;
                    _sceneTimelineEventId = card?.TimelineEventId ?? string.Empty;
                    _sceneTimeRef = card?.TimeRef ?? string.Empty;
                    _sceneTagsText = string.Join(", ", card?.Tags ?? Array.Empty<string>());
                    _sceneReferencesJson = SerializeSceneReferences(card?.References);
                }
                else
                {
                    SectionSceneCardDto? card =
                        await Http.GetFromJsonAsync<SectionSceneCardDto>($"api/sections/{sectionId}/scene-card");

                    _sceneSummary = card?.Summary ?? string.Empty;
                    _sceneCardMetadataStatus = NormalizeSceneCardStatus(card?.Status);
                    _sceneNarrativeRole = card?.NarrativeRole ?? GetNormalizedLegacyNarrativeRole(card?.NarrativePurpose) ?? string.Empty;
                    _sceneNarrativeIntent = card?.NarrativeIntent ?? GetLegacyNarrativeIntent(card?.NarrativePurpose) ?? string.Empty;
                    _sceneEmotionalBeat = card?.EmotionalBeat ?? string.Empty;
                    _sceneKeyEvents = card?.KeyEvents ?? string.Empty;
                    _sceneOpenQuestions = card?.OpenQuestions ?? string.Empty;
                    _scenePovCharacterId = card?.PovCharacterId ?? string.Empty;
                    _sceneSubplotTagsText = string.Join(", ", card?.SubplotTags ?? Array.Empty<string>());
                    _scenePlaceId = card?.PlaceId ?? string.Empty;
                    _sceneTimelineEventId = card?.TimelineEventId ?? string.Empty;
                    _sceneTimeRef = card?.TimeRef ?? string.Empty;
                    _sceneTagsText = string.Join(", ", card?.Tags ?? Array.Empty<string>());
                    _sceneReferencesJson = SerializeSceneReferences(card?.References);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Scene card load failed.");
                _sceneStatus = "Failed to load scene card.";
            }
        }

        private void QueueSceneCardAutosave()
        {
            if (_activeSection is null && !IsSceneRoute)
            {
                return;
            }

            _sceneAutosaveCts?.Cancel();
            _sceneAutosaveCts = new CancellationTokenSource();
            _ = DebouncedSceneCardSaveAsync(_sceneAutosaveCts, _activeSection?.Id ?? Guid.Empty);
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
            if (_sceneSaveInFlight || (!IsSceneRoute && _sceneCardSectionId != sectionId))
            {
                return;
            }

            _sceneSaveInFlight = true;
            try
            {
                SceneCardUpdateRequest scenePayload = new(
                    GetLegacyNarrativePurposeForSave(),
                    _sceneEmotionalBeat,
                    _sceneKeyEvents,
                    _sceneOpenQuestions,
                    NormalizeOptional(_scenePovCharacterId),
                    NormalizeOptional(_scenePlaceId),
                    NormalizeOptional(_sceneTimelineEventId),
                    NormalizeOptional(_sceneTimeRef),
                    ParseTags(_sceneTagsText),
                    ParseSceneReferences(_sceneReferencesJson),
                    NormalizeOptional(_sceneSummary),
                    NormalizeSceneCardStatus(_sceneCardMetadataStatus),
                    ParseTags(_sceneSubplotTagsText),
                    NormalizeNarrativeRole(_sceneNarrativeRole),
                    NormalizeOptional(_sceneNarrativeIntent));

                HttpResponseMessage response;
                if (IsSceneRoute)
                {
                    response = await Http.PutAsJsonAsync($"api/scenes/{SceneNodeId}/scene-card", scenePayload);
                }
                else
                {
                    SectionSceneCardUpdateRequest payload = new(
                        scenePayload.NarrativePurpose,
                        scenePayload.EmotionalBeat,
                        scenePayload.KeyEvents,
                        scenePayload.OpenQuestions,
                        scenePayload.PovCharacterId,
                        scenePayload.PlaceId,
                        scenePayload.TimelineEventId,
                        scenePayload.TimeRef,
                        scenePayload.Tags,
                        scenePayload.References,
                        scenePayload.Summary,
                        scenePayload.Status,
                        scenePayload.SubplotTags,
                        scenePayload.NarrativeRole,
                        scenePayload.NarrativeIntent);
                    response = await Http.PutAsJsonAsync($"api/sections/{sectionId}/scene-card", payload);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _sceneStatus = "Failed to save scene card.";
                    return;
                }

                SceneCardDto? updated;
                if (IsSceneRoute)
                {
                    updated = await response.Content.ReadFromJsonAsync<SceneCardDto>();
                }
                else
                {
                    SectionSceneCardDto? legacy = await response.Content.ReadFromJsonAsync<SectionSceneCardDto>();
                    updated = legacy is null
                        ? null
                        : new SceneCardDto(
                            SceneNodeId,
                            legacy.NarrativePurpose,
                            legacy.EmotionalBeat,
                            legacy.KeyEvents,
                            legacy.OpenQuestions,
                            legacy.UpdatedUtc,
                            legacy.PovCharacterId,
                            legacy.PlaceId,
                            legacy.TimelineEventId,
                            legacy.TimeRef,
                            legacy.Tags,
                            legacy.References,
                            legacy.Summary,
                            legacy.Status,
                            legacy.SubplotTags,
                            legacy.NarrativeRole,
                            legacy.NarrativeIntent);
                }
                if (updated is not null)
                {
                    _sceneSummary = updated.Summary ?? string.Empty;
                    _sceneCardMetadataStatus = NormalizeSceneCardStatus(updated.Status);
                    _sceneNarrativeRole = updated.NarrativeRole ?? GetNormalizedLegacyNarrativeRole(updated.NarrativePurpose) ?? string.Empty;
                    _sceneNarrativeIntent = updated.NarrativeIntent ?? GetLegacyNarrativeIntent(updated.NarrativePurpose) ?? string.Empty;
                    _sceneEmotionalBeat = updated.EmotionalBeat ?? string.Empty;
                    _sceneKeyEvents = updated.KeyEvents ?? string.Empty;
                    _sceneOpenQuestions = updated.OpenQuestions ?? string.Empty;
                    _scenePovCharacterId = updated.PovCharacterId ?? string.Empty;
                    _sceneSubplotTagsText = string.Join(", ", updated.SubplotTags ?? Array.Empty<string>());
                    _scenePlaceId = updated.PlaceId ?? string.Empty;
                    _sceneTimelineEventId = updated.TimelineEventId ?? string.Empty;
                    _sceneTimeRef = updated.TimeRef ?? string.Empty;
                    _sceneTagsText = string.Join(", ", updated.Tags ?? Array.Empty<string>());
                    _sceneReferencesJson = SerializeSceneReferences(updated.References);
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
            await RunSceneAiAsync(actionKey, instruction, proposalFieldKey: null);
        }

        private async Task RunSceneAiAsync(string actionKey, string instruction, string? proposalFieldKey)
        {
            if (!CanUseFeature(FeatureKey.SceneAiSuggestions))
            {
                _sceneAiError = GetFeatureTooltip(FeatureKey.SceneAiSuggestions);
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (_sceneAiInFlight || _activeSection is null)
            {
                return;
            }

            _sceneAiInFlight = true;
            _sceneAiProposal = null;
            _sceneAiExplanation = null;
            _sceneAiProposalId = null;
            _sceneAiProposalFieldKey = null;
            _sceneAiError = null;
            try
            {
                string originalSnapshot = BuildSceneCardSnapshotJson();
                string sectionPlainText = await GetCurrentAiPlainTextAsync();
                AiActionExecuteRequestDto payload = new(
                    DocumentId,
                    _activeSection.Id,
                    _activePage?.Id,
                    null,
                    null,
                    originalSnapshot,
                    sectionPlainText,
                    null,
                    new Dictionary<string, object?>
                    {
                        ["instruction"] = instruction
                    });

                using HttpResponseMessage response =
                    await PostAiActionAsync(actionKey, payload);
                if (!response.IsSuccessStatusCode)
                {
                    if (await TryHandleEntitlementDeniedAsync(response, "ai.actions", "Upgrade to continue using AI features."))
                    {
                        _sceneAiError = _entitlementUserMessage;
                        return;
                    }

                    if (await TryHandlePlanUpgradeRequiredAsync(response))
                    {
                        return;
                    }

                    if (await TryHandleAiQuotaExceededAsync(response))
                    {
                        _sceneAiError = _aiQuotaMessage;
                        return;
                    }

                    _sceneAiError = await ReadApiErrorMessageAsync(response, "AI action failed.");
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
                _sceneAiProposalFieldKey = proposalFieldKey;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Scene card AI failed.");
                _sceneAiError = "AI action failed.";
            }
            finally
            {
                await RefreshPlanUsageAsync();
                _sceneAiInFlight = false;
            }
        }

        private Task RunScopedSceneAiAsync()
        {
            SceneAiFieldOption option = GetSelectedSceneAiFieldOption();
            string actionKey = string.Equals(option.Key, "openQuestions", StringComparison.Ordinal)
                ? "scene.find-open-questions"
                : "scene.suggest";

            return RunSceneAiAsync(actionKey, BuildScopedSceneAiInstruction(option), option.Key);
        }

        private async Task ApplySceneAiProposalAsync()
        {
            if (_sceneAiProposal is null || _activeSection is null || !_sceneAiProposalId.HasValue)
            {
                return;
            }

            string beforeSnapshot = BuildSceneCardSnapshotJson();
            if (IsScopedSceneAiProposal())
            {
                ApplyScopedSceneAiProposal(_sceneAiProposalFieldKey!, _sceneAiProposal);
            }
            else
            {
                ApplySuggestedValue(ref _sceneSummary, _sceneAiProposal.Summary);
                ApplySuggestedValue(ref _sceneCardMetadataStatus, _sceneAiProposal.Status);
                ApplySuggestedValue(ref _sceneNarrativeRole, GetSceneProposalNarrativeRole(_sceneAiProposal));
                ApplySuggestedValue(ref _sceneNarrativeIntent, GetSceneProposalNarrativeIntent(_sceneAiProposal));
                ApplySuggestedValue(ref _sceneEmotionalBeat, _sceneAiProposal.EmotionalBeat);
                ApplySuggestedValue(ref _sceneKeyEvents, _sceneAiProposal.KeyEvents);
                ApplySuggestedValue(ref _sceneOpenQuestions, _sceneAiProposal.OpenQuestions);
                ApplySuggestedValue(ref _scenePovCharacterId, _sceneAiProposal.PovCharacterId);
                ApplySuggestedValue(ref _scenePlaceId, _sceneAiProposal.PlaceId);
                ApplySuggestedValue(ref _sceneTimelineEventId, _sceneAiProposal.TimelineEventId);
                ApplySuggestedValue(ref _sceneTimeRef, _sceneAiProposal.TimeRef);

                IReadOnlyList<string> normalizedTags = NormalizeTagList(_sceneAiProposal.Tags);
                if (normalizedTags.Count > 0)
                {
                    _sceneTagsText = string.Join(", ", normalizedTags);
                }

                IReadOnlyList<string> normalizedSubplotTags = NormalizeTagList(_sceneAiProposal.SubplotTags);
                if (normalizedSubplotTags.Count > 0)
                {
                    _sceneSubplotTagsText = string.Join(", ", normalizedSubplotTags);
                }

                if (_sceneAiProposal.References is not null && _sceneAiProposal.References.Count > 0)
                {
                    _sceneReferencesJson = SerializeSceneReferences(_sceneAiProposal.References);
                }
            }

            await SaveSceneCardAsync(_activeSection.Id, isAutosave: false);

            string afterSnapshot = BuildSceneCardSnapshotJson();
            await RecordAiSceneCardAppliedAsync(_sceneAiProposalId.Value, beforeSnapshot, afterSnapshot);
            _sceneAiProposal = null;
            _sceneAiExplanation = null;
            _sceneAiProposalId = null;
            _sceneAiProposalFieldKey = null;
            await LoadAiHistoryAsync();
        }

        private void DiscardSceneAiProposal()
        {
            _sceneAiProposal = null;
            _sceneAiExplanation = null;
            _sceneAiProposalId = null;
            _sceneAiProposalFieldKey = null;
            _sceneAiError = null;
        }

        private string BuildSceneCardSnapshotJson()
        {
            SectionSceneCardProposalDto snapshot = new(
                GetLegacyNarrativePurposeForSave(),
                _sceneEmotionalBeat,
                _sceneKeyEvents,
                _sceneOpenQuestions,
                NormalizeOptional(_scenePovCharacterId),
                NormalizeOptional(_scenePlaceId),
                NormalizeOptional(_sceneTimelineEventId),
                NormalizeOptional(_sceneTimeRef),
                ParseTags(_sceneTagsText),
                ParseSceneReferences(_sceneReferencesJson),
                NormalizeOptional(_sceneSummary),
                NormalizeSceneCardStatus(_sceneCardMetadataStatus),
                ParseTags(_sceneSubplotTagsText),
                NormalizeNarrativeRole(_sceneNarrativeRole),
                NormalizeOptional(_sceneNarrativeIntent));
            return JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static IReadOnlyList<string> ParseTags(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return text
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<string> NormalizeTagList(IReadOnlyList<string>? tags)
        {
            if (tags is null || tags.Count == 0)
            {
                return Array.Empty<string>();
            }

            return tags
                .Select(tag => tag?.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string GetSceneProposalTagsDisplay(IReadOnlyList<string>? tags)
        {
            IReadOnlyList<string> normalized = NormalizeTagList(tags);
            if (normalized.Count == 0)
            {
                return "(not provided)";
            }

            return string.Join(", ", normalized);
        }

        private IEnumerable<SceneAiFieldOption> GetSceneAiFieldOptions()
        {
            yield return new SceneAiFieldOption("summary", "Summary");
            yield return new SceneAiFieldOption("status", "Status");
            yield return new SceneAiFieldOption("narrativeRole", "Narrative role");
            yield return new SceneAiFieldOption("narrativeIntent", "Narrative intent");
            yield return new SceneAiFieldOption("emotionalBeat", "Emotional beat");
            yield return new SceneAiFieldOption("keyEvents", "Key events");
            yield return new SceneAiFieldOption("openQuestions", "Open questions");
            yield return new SceneAiFieldOption("povCharacterId", "POV");
            yield return new SceneAiFieldOption("placeId", "Setting / place");
            yield return new SceneAiFieldOption("timeRef", "Timeline marker");
            yield return new SceneAiFieldOption("subplotTags", "Subplot tags");
            yield return new SceneAiFieldOption("tags", "Tags");
        }

        private SceneAiFieldOption GetSelectedSceneAiFieldOption()
        {
            return GetSceneAiFieldOptions().FirstOrDefault(option => string.Equals(option.Key, _sceneAiSelectedField, StringComparison.Ordinal))
                ?? new SceneAiFieldOption("summary", "Summary");
        }

        private static string GetSceneAiFieldPlaceholder(string key)
        {
            return key switch
            {
                "status" => "Draft",
                _ => "(not provided)"
            };
        }

        private bool IsScopedSceneAiProposal()
        {
            return _sceneAiProposal is not null && !string.IsNullOrWhiteSpace(_sceneAiProposalFieldKey);
        }

        private string GetSceneAiPreviewFieldLabel()
        {
            return GetSelectedSceneAiFieldOption().Label;
        }

        private string GetCurrentSceneAiFieldDisplay()
        {
            return GetSceneFieldDisplay(_sceneAiProposalFieldKey, proposal: null);
        }

        private string GetProposedSceneAiFieldDisplay()
        {
            return GetSceneFieldDisplay(_sceneAiProposalFieldKey, _sceneAiProposal);
        }

        private string GetSceneFieldDisplay(string? key, SectionSceneCardProposalDto? proposal)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "(not provided)";
            }

            string? value = key switch
            {
                "summary" => proposal?.Summary ?? NormalizeOptional(_sceneSummary),
                "status" => proposal is null ? NormalizeOptional(_sceneCardMetadataStatus) : NormalizeOptional(NormalizeSceneCardStatus(proposal.Status)),
                "narrativeRole" => proposal is null ? NormalizeOptional(_sceneNarrativeRole) : GetSceneProposalNarrativeRole(proposal),
                "narrativeIntent" => proposal is null ? NormalizeOptional(_sceneNarrativeIntent) : GetSceneProposalNarrativeIntent(proposal),
                "emotionalBeat" => proposal?.EmotionalBeat ?? NormalizeOptional(_sceneEmotionalBeat),
                "keyEvents" => proposal?.KeyEvents ?? NormalizeOptional(_sceneKeyEvents),
                "openQuestions" => proposal?.OpenQuestions ?? NormalizeOptional(_sceneOpenQuestions),
                "povCharacterId" => proposal?.PovCharacterId ?? NormalizeOptional(_scenePovCharacterId),
                "placeId" => proposal?.PlaceId ?? NormalizeOptional(_scenePlaceId),
                "timeRef" => proposal?.TimeRef ?? NormalizeOptional(_sceneTimeRef),
                "subplotTags" => proposal is null ? NormalizeOptional(_sceneSubplotTagsText) : NormalizeOptional(GetSceneProposalTagsDisplay(proposal.SubplotTags)),
                "tags" => proposal is null ? NormalizeOptional(_sceneTagsText) : NormalizeOptional(GetSceneProposalTagsDisplay(proposal.Tags)),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "(not provided)", StringComparison.Ordinal))
            {
                return GetSceneAiFieldPlaceholder(key);
            }

            return value;
        }

        private string BuildScopedSceneAiInstruction(SceneAiFieldOption option)
        {
            string currentValue = GetCurrentSceneAiFieldDisplay();
            return option.Key switch
            {
                "summary" => $"Suggest ONLY an improved Summary for this scene card based on the section text. Return a concise 1-2 sentence summary. Leave every other scene card field unchanged. Current Summary: {currentValue}",
                "status" => $"Suggest ONLY the Status field for this scene card based on the section text. Return a single best-fit status value. Leave every other scene card field unchanged. Current Status: {currentValue}",
                "narrativeRole" => $"Suggest ONLY the Narrative role for this scene based on the section text. Treat it as the structural role of the scene in the story. Leave every other scene card field unchanged. Current Narrative role: {currentValue}",
                "narrativeIntent" => $"Suggest ONLY the Narrative intent for this scene based on the section text. Treat it as the emotional or dramatic aim of the scene. Leave every other scene card field unchanged. Current Narrative intent: {currentValue}",
                "emotionalBeat" => $"Suggest ONLY the Emotional beat for this scene based on the section text. Describe how the emotional energy shifts. Leave every other scene card field unchanged. Current Emotional beat: {currentValue}",
                "keyEvents" => $"Suggest ONLY the Key events field for this scene based on the section text. Focus on the most important beats that happen in the scene. Leave every other scene card field unchanged. Current Key events: {currentValue}",
                "openQuestions" => $"Find open questions in this section and update ONLY the Open questions field. Do not change any other scene card fields. Current Open questions: {currentValue}",
                "povCharacterId" => $"Suggest ONLY the POV character for this scene based on the section text. Leave every other scene card field unchanged. Current POV: {currentValue}",
                "placeId" => $"Suggest ONLY the Setting / place field for this scene based on the section text. Leave every other scene card field unchanged. Current Setting / place: {currentValue}",
                "timeRef" => $"Suggest ONLY the Timeline marker field for this scene based on the section text. Leave every other scene card field unchanged. Current Timeline marker: {currentValue}",
                "subplotTags" => $"Suggest ONLY the Subplot tags for this scene based on the section text. Return concise comma-separated style tag values. Leave every other scene card field unchanged. Current Subplot tags: {currentValue}",
                "tags" => $"Suggest ONLY the Tags field for this scene based on the section text. Return concise comma-separated style tag values. Leave every other scene card field unchanged. Current Tags: {currentValue}",
                _ => $"Suggest ONLY the {option.Label} field for this scene card based on the section text. Leave every other scene card field unchanged. Current value: {currentValue}"
            };
        }

        private void ApplyScopedSceneAiProposal(string fieldKey, SectionSceneCardProposalDto proposal)
        {
            switch (fieldKey)
            {
                case "summary":
                    ApplySuggestedValue(ref _sceneSummary, proposal.Summary);
                    break;
                case "status":
                    ApplySuggestedValue(ref _sceneCardMetadataStatus, proposal.Status);
                    break;
                case "narrativeRole":
                    ApplySuggestedValue(ref _sceneNarrativeRole, GetSceneProposalNarrativeRole(proposal));
                    break;
                case "narrativeIntent":
                    ApplySuggestedValue(ref _sceneNarrativeIntent, GetSceneProposalNarrativeIntent(proposal));
                    break;
                case "emotionalBeat":
                    ApplySuggestedValue(ref _sceneEmotionalBeat, proposal.EmotionalBeat);
                    break;
                case "keyEvents":
                    ApplySuggestedValue(ref _sceneKeyEvents, proposal.KeyEvents);
                    break;
                case "openQuestions":
                    ApplySuggestedValue(ref _sceneOpenQuestions, proposal.OpenQuestions);
                    break;
                case "povCharacterId":
                    ApplySuggestedValue(ref _scenePovCharacterId, proposal.PovCharacterId);
                    break;
                case "placeId":
                    ApplySuggestedValue(ref _scenePlaceId, proposal.PlaceId);
                    break;
                case "timeRef":
                    ApplySuggestedValue(ref _sceneTimeRef, proposal.TimeRef);
                    break;
                case "subplotTags":
                    IReadOnlyList<string> subplotTags = NormalizeTagList(proposal.SubplotTags);
                    if (subplotTags.Count > 0)
                    {
                        _sceneSubplotTagsText = string.Join(", ", subplotTags);
                    }
                    break;
                case "tags":
                    IReadOnlyList<string> tags = NormalizeTagList(proposal.Tags);
                    if (tags.Count > 0)
                    {
                        _sceneTagsText = string.Join(", ", tags);
                    }
                    break;
            }
        }

        private static void ApplySuggestedValue(ref string target, string? suggestion)
        {
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                return;
            }

            target = suggestion.Trim();
        }

        private static string NormalizeSceneCardStatus(string? status)
        {
            string normalized = status?.Trim() ?? string.Empty;
            foreach (string option in SceneCardStatusOptions)
            {
                if (string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return "Draft";
        }

        private static string? NormalizeNarrativeRole(string? value)
        {
            return SceneNarrativeRoleCatalog.TryNormalize(value, out string? normalizedRole)
                ? normalizedRole
                : null;
        }

        private string? GetLegacyNarrativePurposeForSave()
        {
            return SceneNarrativeRoleCatalog.ToLegacyPurpose(
                NormalizeNarrativeRole(_sceneNarrativeRole),
                NormalizeOptional(_sceneNarrativeIntent));
        }

        private static string? GetNormalizedLegacyNarrativeRole(string? legacyNarrativePurpose)
        {
            return SceneNarrativeRoleCatalog.TryNormalize(legacyNarrativePurpose, out string? normalizedRole)
                ? normalizedRole
                : null;
        }

        private static string? GetLegacyNarrativeIntent(string? legacyNarrativePurpose)
        {
            return GetNormalizedLegacyNarrativeRole(legacyNarrativePurpose) is null
                ? NormalizeOptional(legacyNarrativePurpose)
                : null;
        }

        private static string? GetSceneProposalNarrativeRole(SectionSceneCardProposalDto? proposal)
        {
            if (proposal is null)
            {
                return null;
            }

            return NormalizeNarrativeRole(proposal.NarrativeRole)
                ?? GetNormalizedLegacyNarrativeRole(proposal.NarrativePurpose);
        }

        private static string? GetSceneProposalNarrativeIntent(SectionSceneCardProposalDto? proposal)
        {
            if (proposal is null)
            {
                return null;
            }

            return NormalizeOptional(proposal.NarrativeIntent)
                ?? GetLegacyNarrativeIntent(proposal.NarrativePurpose);
        }

        private static string GetSceneProposalNarrativeRoleDisplay(SectionSceneCardProposalDto? proposal)
        {
            string? role = GetSceneProposalNarrativeRole(proposal);
            return string.IsNullOrWhiteSpace(role) ? "(not provided)" : role;
        }

        private static string GetSceneProposalNarrativeIntentDisplay(SectionSceneCardProposalDto? proposal)
        {
            string? intent = GetSceneProposalNarrativeIntent(proposal);
            return string.IsNullOrWhiteSpace(intent) ? "(not provided)" : intent;
        }

        private IReadOnlyList<string> GetNarrativeRoleOptions()
        {
            if (string.IsNullOrWhiteSpace(_sceneNarrativeRole)
                || SceneNarrativeRoleOptions.Contains(_sceneNarrativeRole, StringComparer.OrdinalIgnoreCase))
            {
                return SceneNarrativeRoleOptions;
            }

            return [.. SceneNarrativeRoleOptions, _sceneNarrativeRole];
        }


        private static IReadOnlyList<SceneCardReferenceDto> ParseSceneReferences(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<SceneCardReferenceDto>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<SceneCardReferenceDto>>(json, JsonOptions)
                    ?? new List<SceneCardReferenceDto>();
            }
            catch (JsonException)
            {
                return Array.Empty<SceneCardReferenceDto>();
            }
        }

        private static string SerializeSceneReferences(IReadOnlyList<SceneCardReferenceDto>? references)
        {
            if (references is null || references.Count == 0)
            {
                return "[]";
            }

            return JsonSerializer.Serialize(references, JsonOptions);
        }

        private async Task RecordAiSceneCardAppliedAsync(Guid proposalId, string before, string after)
        {
            var payload = new
            {
                DocumentId,
                SectionId = IsSceneRoute ? (Guid?)null : _activeSection?.Id,
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
        private async Task<string> LoadSectionNotesAsync(Guid sectionId, CancellationToken ct)
        {
            try
            {
                if (IsSceneRoute)
                {
                    SceneNotesDto? result = await Http.GetFromJsonAsync<SceneNotesDto>($"api/scenes/{SceneNodeId}/notes", ct);
                    _notesLastSavedAtUtc = result?.UpdatedAtUtc;
                    return result?.NotesText ?? string.Empty;
                }

                SectionNotesDto? legacy = await Http.GetFromJsonAsync<SectionNotesDto>($"api/sections/{sectionId}/notes", ct);
                _notesLastSavedAtUtc = legacy?.UpdatedAtUtc;
                return legacy?.NotesText ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Notes load failed.");
                _notesError = "Failed to load notes. Changes will retry on edit.";
                return string.Empty;
            }
        }

        private async Task<DateTimeOffset?> SaveSectionNotesAsync(Guid sectionId, string value, CancellationToken ct)
        {
            if (IsSceneRoute)
            {
                SceneNotesUpdateRequest payload = new(value ?? string.Empty);
                using HttpResponseMessage response = await Http.PutAsJsonAsync($"api/scenes/{SceneNodeId}/notes", payload, ct);
                if (!response.IsSuccessStatusCode)
                {
                    string message = await ReadApiErrorMessageAsync(response, "Could not save notes.");
                    throw new HttpRequestException(message, null, response.StatusCode);
                }
                SceneNotesDto? saved = await response.Content.ReadFromJsonAsync<SceneNotesDto>(cancellationToken: ct);
                return saved?.UpdatedAtUtc;
            }

            SectionNotesDto legacyPayload = new(sectionId, value ?? string.Empty, DateTimeOffset.UtcNow);
            using HttpResponseMessage legacyResponse = await Http.PutAsJsonAsync($"api/sections/{sectionId}/notes", legacyPayload, ct);
            if (!legacyResponse.IsSuccessStatusCode)
            {
                string message = await ReadApiErrorMessageAsync(legacyResponse, "Could not save notes.");
                throw new HttpRequestException(message, null, legacyResponse.StatusCode);
            }
            SectionNotesDto? legacySaved = await legacyResponse.Content.ReadFromJsonAsync<SectionNotesDto>(cancellationToken: ct);
            return legacySaved?.UpdatedAtUtc;
        }

        private void ResetNotesAutosaveState()
        {
            _notesAutosaveCts?.Cancel();
            _notesAutosaveCts?.Dispose();
            _notesAutosaveCts = null;
            _notesRetryCts?.Cancel();
            _notesRetryCts?.Dispose();
            _notesRetryCts = null;
            _notesSaveInFlight = false;
            _notesSaveQueued = false;
            _notesEditVersion = 0;
            _notesSavedVersion = 0;
            _notesRetryAttempt = 0;
            _notesStatus = null;
            _notesError = null;
            _notesLastSavedAtUtc = null;
        }

        private void QueueNotesAutosave()
        {
            if (_activeSection is null)
            {
                return;
            }

            _notesAutosaveCts?.Cancel();
            _notesAutosaveCts?.Dispose();
            _notesAutosaveCts = new CancellationTokenSource();
            _notesRetryCts?.Cancel();
            _notesRetryCts?.Dispose();
            _notesRetryCts = null;
            Guid sectionId = _activeSection.Id;
            int version = _notesEditVersion;
            _notesStatus = "Saving...";
            _ = DebouncedNotesSaveAsync(sectionId, version, _notesAutosaveCts.Token);
        }

        private async Task DebouncedNotesSaveAsync(Guid sectionId, int version, CancellationToken ct)
        {
            try
            {
                await Task.Delay(NotesAutosaveDebounce, ct);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await SaveNotesCoreAsync(sectionId, version, ct);
        }

        private async Task FlushNotesSaveAsync()
        {
            if (_activeSection is null)
            {
                return;
            }

            _notesAutosaveCts?.Cancel();
            _notesAutosaveCts?.Dispose();
            _notesAutosaveCts = null;
            _notesRetryCts?.Cancel();
            _notesRetryCts?.Dispose();
            _notesRetryCts = null;
            _notesEditVersion++;
            await SaveNotesCoreAsync(_activeSection.Id, _notesEditVersion, CancellationToken.None);
        }

        private async Task SaveNotesCoreAsync(Guid sectionId, int version, CancellationToken ct)
        {
            if (_activeSection?.Id != sectionId)
            {
                return;
            }

            _notesSaveQueued = true;
            if (_notesSaveInFlight)
            {
                return;
            }

            while (_notesSaveQueued)
            {
                _notesSaveQueued = false;
                bool queueImmediateRetry = true;
                int saveVersion = Math.Max(version, _notesEditVersion);
                string snapshot = _notesDraft ?? string.Empty;
                _notesSaveInFlight = true;
                _notesStatus = "Saving...";
                await InvokeAsync(StateHasChanged);

                try
                {
                    DateTimeOffset? savedAt = await SaveSectionNotesAsync(sectionId, snapshot, ct);
                    _notesRetryAttempt = 0;
                    _notesRetryCts?.Cancel();
                    _notesRetryCts?.Dispose();
                    _notesRetryCts = null;
                    if (saveVersion >= _notesSavedVersion)
                    {
                        _notesSavedVersion = saveVersion;
                        _notesLastSavedAtUtc = savedAt ?? DateTimeOffset.UtcNow;
                        _notesStatus = "Saved";
                        _notesError = null;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (HttpRequestException ex) when (ShouldRetryNotesSave(ex.StatusCode))
                {
                    Logger.LogWarning(ex, "Notes save transient failure: {StatusCode}", ex.StatusCode);
                    TimeSpan retryDelay = GetNextNotesRetryDelay();
                    _notesStatus = "Server unavailable";
                    _notesError = $"Could not save notes. Retrying in {(int)retryDelay.TotalSeconds}s.";
                    ScheduleNotesRetry(sectionId, retryDelay);
                    queueImmediateRetry = false;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Notes save failed.");
                    _notesStatus = "Save failed";
                    _notesError = "Could not save notes. Changes will retry on next edit.";
                    queueImmediateRetry = false;
                }
                finally
                {
                    _notesSaveInFlight = false;
                    await InvokeAsync(StateHasChanged);
                }

                if (_activeSection?.Id != sectionId)
                {
                    return;
                }

                if (queueImmediateRetry && _notesSavedVersion < _notesEditVersion)
                {
                    _notesSaveQueued = true;
                }
            }
        }

        private static bool ShouldRetryNotesSave(HttpStatusCode? statusCode)
        {
            return statusCode is null
                || statusCode == HttpStatusCode.BadGateway
                || statusCode == HttpStatusCode.ServiceUnavailable
                || statusCode == HttpStatusCode.GatewayTimeout
                || statusCode == HttpStatusCode.TooManyRequests;
        }

        private TimeSpan GetNextNotesRetryDelay()
        {
            int index = Math.Min(_notesRetryAttempt, NotesAutosaveRetryDelays.Length - 1);
            _notesRetryAttempt++;
            return NotesAutosaveRetryDelays[index];
        }

        private void ScheduleNotesRetry(Guid sectionId, TimeSpan delay)
        {
            if (_activeSection?.Id != sectionId)
            {
                return;
            }

            _notesRetryCts?.Cancel();
            _notesRetryCts?.Dispose();
            _notesRetryCts = new CancellationTokenSource();
            CancellationToken retryToken = _notesRetryCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, retryToken);
                    if (retryToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await InvokeAsync(async () =>
                    {
                        if (_activeSection?.Id != sectionId)
                        {
                            return;
                        }

                        _notesEditVersion++;
                        await SaveNotesCoreAsync(sectionId, _notesEditVersion, retryToken);
                    });
                }
                catch (TaskCanceledException)
                {
                }
            }, CancellationToken.None);
        }

        private string GetOutlineTextForAi()
        {
            if (_sections.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            foreach (SectionDto section in _sections.OrderBy(section => section.OrderIndex))
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                {
                    builder.Append("- ").AppendLine(section.Title.Trim());
                }
            }

            return builder.ToString().TrimEnd();
        }

        private async Task OnExportRequested(string kind, string format)
        {
            _isDocumentMenuOpen = false;
            try
            {
                await FlushActiveEditorAsync("export");

                if (!string.Equals(kind, "document", StringComparison.OrdinalIgnoreCase))
                {
                    string templateQuery = string.Empty;
                    if ((string.Equals(format, "html", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase))
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
                    string.IsNullOrWhiteSpace(_titlePageDate) ? null : _titlePageDate,
                    _exportIncludeCover);

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
                await FlushActiveEditorAsync("export-pdf");

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
                    string.IsNullOrWhiteSpace(_titlePageDate) ? null : _titlePageDate,
                    _exportIncludeCover);

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
                await FlushActiveEditorAsync("export-preview");

                if (string.Equals(_exportContentSelection, "synopsis", StringComparison.OrdinalIgnoreCase))
                {
                    _previewSidebarOpen = false;
                    string synopsisHtml = await GetSynopsisPreviewHtmlAsync();
                    _previewHtml = BuildPreviewHtml(synopsisHtml);
                    _previewZoom = 1.0;
                    _previewInitialized = false;
                    _previewFrameLoaded = false;
                    _previewHasFrontMatter = false;
                    _previewSearchTerm = string.Empty;
                    _previewPageCount = 1;
                    _previewCurrentPage = 1;
                    return;
                }

                _previewSidebarOpen = true;
                if (!ValidateScope(out string? error))
                {
                    _previewError = error;
                    return;
                }

                ExportPreviewRequest request = new(
                    DocumentId,
                    _selectedTemplateId,
                    _exportFormatSelection,
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
                    string.IsNullOrWhiteSpace(_titlePageDate) ? null : _titlePageDate,
                    _exportIncludeCover);

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

        private async Task<string> GetSynopsisPreviewHtmlAsync()
        {
            if (_synopsisPreviewCacheDocumentId == DocumentId
                && !string.IsNullOrWhiteSpace(_synopsisPreviewCacheHtml))
            {
                return _synopsisPreviewCacheHtml;
            }

            using HttpResponseMessage response = await Http.GetAsync(
                $"api/documents/{DocumentId}/export?kind=synopsis&format=html");
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Synopsis preview failed.");
            }

            string html = await response.Content.ReadAsStringAsync();
            _synopsisPreviewCacheDocumentId = DocumentId;
            _synopsisPreviewCacheHtml = html;
            return html;
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
            Logger.LogInformation(
                "Export preview scroll update. PageCount={PageCount} CurrentPage={CurrentPage} HasFrontMatter={HasFrontMatter}",
                _previewPageCount,
                _previewCurrentPage,
                _previewHasFrontMatter);
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
            Logger.LogInformation(
                "Export preview initialized. PageCount={PageCount} CurrentPage={CurrentPage} HasFrontMatter={HasFrontMatter} RenderOrder={RenderOrder}",
                _previewPageCount,
                _previewCurrentPage,
                _previewHasFrontMatter,
                string.Join(",", Enumerable.Range(1, _previewPageCount)));
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

            int targetPage = Math.Clamp(page, 1, Math.Max(1, _previewPageCount));
            Logger.LogInformation(
                "Export preview jump request. RequestedPage={RequestedPage} TargetPage={TargetPage} ZeroBasedIndex={ZeroBasedIndex} TotalPages={TotalPages}",
                page,
                targetPage,
                targetPage - 1,
                _previewPageCount);
            await _exportModule.InvokeVoidAsync("scrollPreviewToPage", "export-preview-frame", targetPage);
            _previewCurrentPage = targetPage;
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
    table { border-collapse: collapse; border-spacing: 0; width: 100%; border: 1px solid #9aa4b2; }
    th, td { border: 1px solid #9aa4b2; padding: 6px 8px; vertical-align: top; }
    td:empty::after { content: ""\00a0""; }
    .preview-pagebreak-overlay { position: absolute; left: 0; right: 0; top: 0; pointer-events: none; }
    .preview-pagebreak-line { position: absolute; left: 0; right: 0; border-top: 1px dashed rgba(148, 163, 184, 0.7); }
    mark.preview-search-hit { background: #fde68a; padding: 0 2px; border-radius: 3px; }
    html, body { scroll-behavior: smooth; }
</style>
<script id=""__WRITER_PREVIEW__"">window.__writerPreviewReady=true;</script>";

        private sealed record PreviewMetrics(int PageCount, int CurrentPage, bool HasFrontMatter);
        private sealed record PreviewFit(double FitWidth, double FitPage);
        private sealed record ToolbarDropdownPlacement(bool AlignLeft, bool OpenUpward);
        private sealed record SceneAiFieldOption(string Key, string Label);

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
            _exportContentSelection = "document";
            _exportIncludeTitlePage = true;
            _exportIncludeCover = GetDefaultIncludeCoverForCurrentSelection();
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
            if (!CanUseFeature(FeatureKey.ExportTemplates))
            {
                _templateActionError = GetFeatureTooltip(FeatureKey.ExportTemplates);
                return;
            }

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
            if (TryGetExportSubmitRequiredFeature(out FeatureKey requiredFeature))
            {
                NavigateToUpgradeForFeature(requiredFeature);
                return;
            }

            if (string.Equals(_exportFormatSelection, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(_exportContentSelection, "document", StringComparison.OrdinalIgnoreCase))
                {
                    _templateActionError = "PDF export is available for document content.";
                    return;
                }

                await OnExportPdfRequested();
                _isExportDialogOpen = false;
                return;
            }

            string format = _exportFormatSelection.ToLowerInvariant() switch
            {
                "markdown" => "markdown",
                "html" => "html",
                "docx" => "docx",
                "epub" => "epub",
                _ => "html"
            };

            await OnExportRequested(_exportContentSelection, format);
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

                if (_selectedTemplateId.HasValue
                    && _exportTemplates.All(template => template.Id != _selectedTemplateId.Value))
                {
                    _selectedTemplateId = null;
                }

                ApplyDefaultTemplateSelection();
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

            bool appliedPreset = false;
            if (presetId.HasValue)
            {
                ExportPresetDto? preset = _exportPresets.FirstOrDefault(item => item.Id == presetId.Value);
                if (preset is not null)
                {
                    _selectedExportPresetId = preset.Id;
                    ApplyExportPreset(preset);
                    appliedPreset = true;
                }
            }

            if (!appliedPreset)
            {
                ApplyDefaultTemplateSelection();
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

            if (templateId.HasValue && CanUseFeature(FeatureKey.ExportTemplates))
            {
                _selectedTemplateId = templateId;
            }
            else
            {
                _selectedTemplateId = null;
            }

            NormalizeExportFormatSelection();
            _exportIncludeCover = settings.IncludeCover ?? GetDefaultIncludeCoverForCurrentSelection();
            if (!CanConfigureExportCover)
            {
                _exportIncludeCover = false;
            }
        }

        private void NormalizeExportFormatSelection()
        {
            if (!string.Equals(_exportContentSelection, "document", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(_exportFormatSelection, "pdf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(_exportFormatSelection, "epub", StringComparison.OrdinalIgnoreCase)))
            {
                _exportFormatSelection = "html";
            }

            if (string.Equals(_exportFormatSelection, "docx", StringComparison.OrdinalIgnoreCase) && !_docxExportEnabled)
            {
                _exportFormatSelection = "html";
            }

            if (string.Equals(_exportFormatSelection, "epub", StringComparison.OrdinalIgnoreCase) && !_epubExportEnabled)
            {
                _exportFormatSelection = "html";
            }

            if (!CanConfigureExportCover)
            {
                _exportIncludeCover = false;
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
                null,
                CanConfigureExportCover ? _exportIncludeCover : null);
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
            NormalizeExportFormatSelection();
        }

        private void OnExportContentSelectionChanged()
        {
            NormalizeExportFormatSelection();
            _exportIncludeCover = GetDefaultIncludeCoverForCurrentSelection();
            _selectedExportPresetId = null;
        }

        private void OnExportFormatSelectionChanged()
        {
            NormalizeExportFormatSelection();
            _exportIncludeCover = GetDefaultIncludeCoverForCurrentSelection();
            _selectedExportPresetId = null;
        }

        private bool CanConfigureExportCover =>
            string.Equals(_exportContentSelection, "document", StringComparison.OrdinalIgnoreCase)
            && IsCoverSupportedForFormat(_exportFormatSelection);

        private bool GetDefaultIncludeCoverForCurrentSelection()
        {
            if (!CanConfigureExportCover)
            {
                return false;
            }

            return GetDefaultIncludeCoverForFormat(_exportFormatSelection);
        }

        private static bool GetDefaultIncludeCoverForFormat(string? format)
        {
            return (format ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "docx" => false,
                "markdown" => false,
                "pdf" => true,
                "epub" => true,
                "html" => true,
                _ => false
            };
        }

        private static bool IsCoverSupportedForFormat(string? format)
        {
            return (format ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "html" => true,
                "pdf" => true,
                "docx" => true,
                "epub" => true,
                _ => false
            };
        }

        private void OpenPresetSave()
        {
            if (!CanUseFeature(FeatureKey.ExportPresets))
            {
                _presetActionError = GetFeatureTooltip(FeatureKey.ExportPresets);
                return;
            }

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
            get => _selectedTemplateId?.ToString() ?? NoTemplateOptionValue;
            set => _selectedTemplateId = Guid.TryParse(value, out Guid parsed) ? parsed : null;
        }

        private void ApplyDefaultTemplateSelection()
        {
            if (_selectedTemplateId.HasValue)
            {
                return;
            }

            if (!CanUseFeature(FeatureKey.ExportTemplates) || _exportTemplates.Count == 0)
            {
                _selectedTemplateId = null;
                return;
            }

            ExportTemplateDto? manuscript = _exportTemplates
                .FirstOrDefault(template => string.Equals(template.PresetKey, "manuscript", StringComparison.OrdinalIgnoreCase));
            _selectedTemplateId = manuscript?.Id ?? _exportTemplates[0].Id;
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
                    _selectedTemplateId = null;
                    ApplyDefaultTemplateSelection();
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
            => await OnAiActionSelected(action, allowOnboardingDemoBypass: false);

        private async Task OnAiActionSelected(AiActionOption action, bool allowOnboardingDemoBypass)
        {
            bool onboardingDemoBypass = OnboardingAiDemoRequest.ShouldBypassClientGates(
                allowOnboardingDemoBypass,
                action.ActionKey,
                action.Parameters);
            if (!IsAiAvailable && !onboardingDemoBypass)
            {
                ShowAiMessage(GetAiBlockedMessage());
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (!CanUseAiAction(action) && !onboardingDemoBypass)
            {
                ShowAiMessage(GetAiActionUpgradeTooltip(action));
                await InvokeAsync(StateHasChanged);
                return;
            }

            if (_activeSection is null)
            {
                return;
            }

            await FlushActiveEditorAsync($"ai-request:{action.ActionKey}");

            string plain = await GetCurrentAiPlainTextAsync();
            TextRange selectionRange = new(0, 0);
            string selection = string.Empty;
            AiSelectionSnapshot? selectionSnapshot = null;

            if (action.RequiresSelection)
            {
                selectionSnapshot = await BuildAiSelectionSnapshotAsync(plain);
                if (selectionSnapshot is null)
                {
                    ShowAiMessage("Select text to run this action.");
                    await InvokeAsync(StateHasChanged);
                    return;
                }

                _lastAiSelectionSnapshot = selectionSnapshot;
                selectionRange = selectionSnapshot.PlainRange;
                selection = selectionSnapshot.SelectionText;
            }

            if (IsTranslationActionKey(action.ActionKey))
            {
                OpenTranslateModal(action, plain, selectionRange, selection, selectionSnapshot);
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
            LogAiSelectionDiagnostics(action.ActionKey, originalText);

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
                using HttpResponseMessage result = await PostAiActionAsync(
                    action.ActionKey,
                    request,
                    commandLabel: action.Label);
                if (!result.IsSuccessStatusCode)
                {
                    if (!onboardingDemoBypass
                        && await TryHandleEntitlementDeniedAsync(result, "ai.actions", "Upgrade to continue using AI features."))
                    {
                        ShowAiMessage(_entitlementUserMessage);
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    if (!onboardingDemoBypass
                        && await TryHandlePlanUpgradeRequiredAsync(result))
                    {
                        return;
                    }

                    if (onboardingDemoBypass
                        && (result.StatusCode == HttpStatusCode.PaymentRequired
                            || result.StatusCode == HttpStatusCode.Forbidden))
                    {
                        Logger.LogWarning(
                            "Onboarding AI demo request was not granted by the server. ActionKey={ActionKey}, StatusCode={StatusCode}, DocumentId={DocumentId}, SectionId={SectionId}",
                            action.ActionKey,
                            (int)result.StatusCode,
                            DocumentId,
                            _activeSection?.Id);
                    }

                    if (await TryHandleAiQuotaExceededAsync(result))
                    {
                        ShowAiMessage(_aiQuotaMessage);
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    string errorMessage = await ReadApiErrorMessageAsync(result, "AI action failed.");
                    ShowAiMessage(errorMessage);
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
                Logger.LogWarning(ex, "AI action request failed for {ActionKey}.", action.ActionKey);
                ShowAiMessage("AI action failed.");
                await InvokeAsync(StateHasChanged);
                return;
            }
            finally
            {
                await RefreshPlanUsageAsync();
            }

            string? proposedText = response.ProposedText;
            if (string.Equals(action.ActionKey, "propose.next-paragraph", StringComparison.OrdinalIgnoreCase))
            {
                proposedText = NormalizeSingleParagraph(proposedText ?? string.Empty);
            }

            string? originalForProposal = action.RequiresSelection ? selection : response.OriginalText;
            if (IsTightenAction(action.ActionKey))
            {
                proposedText = await EnsureMeaningfulTightenAsync(action, request, originalForProposal, proposedText);
            }

            bool appendOnly = IsAppendOnlyCustomTransform(action);
            string scope = ResolveActionScope(action);
            _translationApplyMode = "replace";
            _pendingAiProposal = new PendingAiProposal(
                response.ProposalId,
                action.ActionKey,
                action.Instruction,
                originalForProposal,
                proposedText,
                response.ChangesSummary,
                null,
                response.CreatedUtc,
                new PendingAiProposalContext(
                    action.RequiresSelection,
                    _activeSection.Id,
                    _activePage?.Id,
                    selectionSnapshot,
                    scope,
                    appendOnly,
                    plain));
            _pendingDetailsExpanded = false;
            await MarkOnboardingAiSignalAsync("onboarding_first_ai_success", action.ActionKey);
            await LoadAiHistoryAsync();
            await InvokeAsync(StateHasChanged);
        }

        private OnboardingWalkthroughTip CurrentOnboardingWalkthroughTip
        {
            get
            {
                if (_onboardingWalkthroughTips.Count == 0)
                {
                    return new OnboardingWalkthroughTip(4, "Onboarding", "Continue onboarding.", null, false);
                }

                int index = Math.Clamp(_onboardingWalkthroughIndex, 0, _onboardingWalkthroughTips.Count - 1);
                return _onboardingWalkthroughTips[index];
            }
        }

        private bool ShowOnboardingAiActionCta =>
            _showOnboardingWalkthrough && CurrentOnboardingWalkthroughTip.ShowAiAction;

        private void SyncOnboardingOverlayState()
        {
            OnboardingWalkthroughTip tip = CurrentOnboardingWalkthroughTip;
            OnboardingOverlayStateService.Set(
                _showOnboardingWalkthrough,
                _onboardingWalkthroughIndex,
                _onboardingWalkthroughTips.Count,
                tip.Title,
                tip.Description,
                tip.TargetSelector,
                _onboardingWalkthroughStatus,
                _onboardingWalkthroughBusy,
                ShowOnboardingAiActionCta,
                "Run AI demo",
                "Next",
                OnOnboardingWalkthroughNextAsync,
                OnOnboardingWalkthroughSkipAsync,
                OnOnboardingAiDemoAsync);
        }

        private async Task RefreshOnboardingWalkthroughAsync()
        {
            try
            {
                await OnboardingStateStore.RefreshAsync();
                OnboardingState state = OnboardingStateStore.Current;
                if (state.HasCompletedOnboarding)
                {
                    _showOnboardingWalkthrough = false;
                    _onboardingProjectCreated = true;
                    _onboardingTypedEnough = true;
                    _onboardingSavedOnce = true;
                    _onboardingAiRequirementMet = true;
                    return;
                }

                _showOnboardingWalkthrough = true;
                _onboardingWalkthroughTips = BuildOnboardingWalkthroughTips(ResolveOnboardingIntentKey(state.PrimaryWritingIntent));
                _onboardingProjectCreated = ProjectId != Guid.Empty || state.OnboardingStep >= 3;
                _onboardingAiRequirementMet = state.OnboardingStep >= 8;
                _onboardingAiDemoAttempted = state.OnboardingStep >= 8;
                _onboardingWalkthroughIndex = ResolveWalkthroughIndex(state.OnboardingStep);
                _onboardingWalkthroughStatus = null;

                await EnsureWalkthroughContextAsync();
                await EvaluateOnboardingCompletionAsync(forceTypingProbe: true);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to refresh onboarding walkthrough state.");
                _showOnboardingWalkthrough = false;
            }
            finally
            {
                SyncOnboardingOverlayState();
            }
        }

        private static int ResolveWalkthroughIndex(int onboardingStep)
        {
            if (onboardingStep <= 2)
            {
                return 0;
            }

            if (onboardingStep <= 3)
            {
                return 1;
            }

            return 2;
        }

        private static string ResolveOnboardingIntentKey(string? raw)
        {
            string normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "novel" => "Novel",
                "short story" => "ShortStory",
                "shortstory" => "ShortStory",
                "non-fiction" => "NonFiction",
                "non fiction" => "NonFiction",
                "nonfiction" => "NonFiction",
                "blog" => "Blog",
                _ => "Other"
            };
        }

        private static AiActionOption CreateRecommendedToolOption(WritingToolDefinition definition)
        {
            return new AiActionOption(
                "custom_transform",
                definition.DisplayName,
                definition.PromptTemplate.UserTemplate,
                false,
                new Dictionary<string, object?>
                {
                    ["template"] = definition.PromptTemplate.UserTemplate,
                    ["systemTemplate"] = definition.PromptTemplate.SystemTemplate,
                    ["scope"] = "section",
                    ["tone"] = "Neutral",
                    ["length"] = "Same",
                    ["strictTokens"] = false,
                    ["recommendedToolId"] = definition.Id
                },
                definition.Description,
                true,
                definition.IsIntentRecommended,
                definition.IsIntentRecommended ? "Recommended" : null);
        }

        private static string ResolveWritingToolsIntentKey(string? raw)
        {
            string normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "novel" => "Novel",
                "short story" => "ShortStory",
                "shortstory" => "ShortStory",
                "non-fiction" => "NonFiction",
                "non fiction" => "NonFiction",
                "nonfiction" => "NonFiction",
                "blog" => "Blog",
                _ => "Other"
            };
        }

        private static class PromptStrategyResolver
        {
            private const string WritingToolsCategory = "WritingTools";
            private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> IntentToolOrder =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Novel"] = new[] { "novel.continue_scene", "novel.deepen_character", "novel.raise_stakes" },
                    ["ShortStory"] = new[] { "short_story.tighten_prose", "short_story.sharpen_ending", "short_story.heighten_theme" },
                    ["NonFiction"] = new[] { "non_fiction.clarify_simplify", "non_fiction.strengthen_argument", "non_fiction.add_structure" },
                    ["Blog"] = new[] { "blog.improve_hook", "blog.improve_readability", "blog.generate_headlines" },
                    ["Other"] = new[] { "other.improve_flow", "other.expand_idea", "other.summarize_clearly" }
                };

            private static readonly IReadOnlyDictionary<string, WritingToolDefinition> Registry =
                new Dictionary<string, WritingToolDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["novel.continue_scene"] = Create("novel.continue_scene", "Continue Scene", "Continue the scene while preserving POV and momentum.", "You are a fiction writing assistant focused on scene-level craft and continuity.", "Write ONLY the next paragraph that should follow this scene context. Do NOT repeat, paraphrase, or recap any existing text from context. Do NOT include any preamble, labels, or explanation. Return exactly one new paragraph only.\n\nContext:\n{context}"),
                    ["novel.deepen_character"] = Create("novel.deepen_character", "Deepen Character", "Increase character motivation and internal conflict signals.", "You are a fiction writing assistant focused on character depth and emotional clarity.", "Revise this section to deepen the main character's motivation and inner conflict using concrete cues. Context:\n{context}"),
                    ["novel.raise_stakes"] = Create("novel.raise_stakes", "Raise Stakes", "Increase urgency and consequences while preserving events.", "You are a fiction writing assistant focused on narrative stakes and tension.", "Revise this section to raise narrative stakes with clearer consequences and urgency while preserving events. Context:\n{context}"),
                    ["short_story.tighten_prose"] = Create("short_story.tighten_prose", "Tighten Prose", "Compress language while keeping tone and intent.", "You are a short-story writing assistant focused on economy and precision.", "Tighten this section by removing filler, sharpening verbs, and keeping the same meaning and tone. Context:\n{context}"),
                    ["short_story.sharpen_ending"] = Create("short_story.sharpen_ending", "Sharpen Ending", "Strengthen the final beat and emotional impact.", "You are a short-story writing assistant focused on strong endings and resonance.", "Revise this section to sharpen ending momentum and leave a stronger final emotional beat. Context:\n{context}"),
                    ["short_story.heighten_theme"] = Create("short_story.heighten_theme", "Heighten Theme", "Make thematic through-lines clearer in concrete prose.", "You are a short-story writing assistant focused on thematic clarity through scene detail.", "Revise this section to make the core theme more visible through concrete phrasing, not exposition. Context:\n{context}"),
                    ["non_fiction.clarify_simplify"] = Create("non_fiction.clarify_simplify", "Clarify & Simplify", "Improve clarity with concise, plain language.", "You are a non-fiction writing assistant focused on clarity and reader comprehension.", "Rewrite this section for clarity and simplicity with short precise sentences and plain language. Context:\n{context}"),
                    ["non_fiction.strengthen_argument"] = Create("non_fiction.strengthen_argument", "Strengthen Argument", "Improve logical flow and evidence framing.", "You are a non-fiction writing assistant focused on argument quality and structure.", "Revise this section to strengthen logic with clearer claims, support, and transitions. Context:\n{context}"),
                    ["non_fiction.add_structure"] = Create("non_fiction.add_structure", "Add Structure", "Improve organization using clear signposting.", "You are a non-fiction writing assistant focused on structure and readability.", "Re-structure this section with a clearer flow using concise headings or signpost transitions. Context:\n{context}"),
                    ["blog.improve_hook"] = Create("blog.improve_hook", "Improve Hook", "Create a stronger opening for audience attention.", "You are a blog writing assistant focused on engagement and retention.", "Rewrite the opening to create a stronger hook in 1-3 sentences while preserving topic and voice. Context:\n{context}"),
                    ["blog.improve_readability"] = Create("blog.improve_readability", "Improve Readability", "Make content easier to scan and read online.", "You are a blog writing assistant focused on scannability and readability.", "Revise this section for web readability with shorter sentences and scannable phrasing. Context:\n{context}"),
                    ["blog.generate_headlines"] = Create("blog.generate_headlines", "Generate Headlines", "Generate title ideas tailored to topic and audience.", "You are a blog writing assistant focused on compelling headline options.", "Generate 5 concise headline options tailored to this section's topic and audience. Context:\n{context}"),
                    ["other.improve_flow"] = Create("other.improve_flow", "Improve Flow", "Smooth transitions and coherence across ideas.", "You are a writing assistant focused on clarity, flow, and coherence.", "Revise this section to improve flow between ideas and sentence transitions. Context:\n{context}"),
                    ["other.expand_idea"] = Create("other.expand_idea", "Expand Idea", "Develop the strongest point with concise detail.", "You are a writing assistant focused on developing ideas with concise support.", "Expand the strongest idea in this section with one concise supporting paragraph. Context:\n{context}"),
                    ["other.summarize_clearly"] = Create("other.summarize_clearly", "Summarize Clearly", "Provide concise summaries with clear wording.", "You are a writing assistant focused on concise, accurate summaries.", "Produce a clear concise summary of this section in 2-3 sentences. Context:\n{context}")
                };

            public static IReadOnlyList<WritingToolDefinition> GetTopWritingToolsForIntent(string? intent)
            {
                string intentKey = ResolveWritingToolsIntentKey(intent);
                if (!IntentToolOrder.TryGetValue(intentKey, out IReadOnlyList<string>? toolIds))
                {
                    toolIds = IntentToolOrder["Other"];
                }

                List<WritingToolDefinition> result = new(toolIds.Count);
                foreach (string id in toolIds)
                {
                    if (Registry.TryGetValue(id, out WritingToolDefinition? definition))
                    {
                        result.Add(definition);
                    }
                }

                return result;
            }

            private static WritingToolDefinition Create(
                string id,
                string displayName,
                string description,
                string systemTemplate,
                string userTemplate)
            {
                return new WritingToolDefinition(
                    id,
                    displayName,
                    description,
                    new WritingToolPromptTemplate(systemTemplate, userTemplate),
                    WritingToolsCategory,
                    true);
            }
        }

        private static IReadOnlyList<OnboardingWalkthroughTip> BuildOnboardingWalkthroughTips(string intentKey)
        {
            (string welcome, string structure, string coach) = intentKey switch
            {
                "Novel" => (
                    "Welcome — let's start your novel.",
                    "We created Act I with Scene 1 and loaded a sample Café scene so you can begin drafting right away.",
                    "Watch AI tighten the character description, then compare the before and after."),
                "ShortStory" => (
                    "Welcome — let's start your short story.",
                    "We created a Draft with Scene 1, loaded a sample Café scene, and added Ending Notes for your closing idea.",
                    "Watch AI tighten the character description, then compare the before and after."),
                "NonFiction" => (
                    "Welcome — let's start your non-fiction draft.",
                    "We created Chapter 1 with Scene 1, loaded a sample scene, and added a Research section for source notes.",
                    "Watch AI tighten the character description, then compare the before and after."),
                "Blog" => (
                    "Welcome — let's start your blog post.",
                    "We created a Draft with Scene 1, loaded a sample scene, and added Headline Ideas.",
                    "Watch AI tighten the character description, then compare the before and after."),
                _ => (
                    "Welcome — let's start writing.",
                    "We created a clean Draft with Scene 1 and loaded a sample Café scene so you can jump in quickly.",
                    "Watch AI tighten the character description, then compare the before and after.")
            };

            return new List<OnboardingWalkthroughTip>
            {
                new(3, "Welcome", welcome, "#onboarding-editor-scene", false),
                new(4, "Project structure", structure, "#onboarding-project-structure", false),
                new(5, "AI Coach example", coach, "#onboarding-tab-ai", true)
            };
        }

        private async Task EnsureWalkthroughContextAsync()
        {
            OnboardingWalkthroughTip tip = CurrentOnboardingWalkthroughTip;
            if (string.Equals(tip.TargetSelector, "#onboarding-tab-ai", StringComparison.Ordinal))
            {
                await SetContextTabAsync(ContextTab.Ai);
                if (!_onboardingAiDemoAttempted)
                {
                    await OnOnboardingAiDemoAsync();
                }
            }
        }

        private async Task OnOnboardingWalkthroughNextAsync()
        {
            if (_onboardingWalkthroughBusy || !_showOnboardingWalkthrough)
            {
                return;
            }

            _onboardingWalkthroughBusy = true;
            SyncOnboardingOverlayState();
            try
            {
                int persistedStep = CurrentOnboardingWalkthroughTip.ServerStep;
                await OnboardingService.SetStepAsync(persistedStep);

                if (_onboardingWalkthroughIndex >= _onboardingWalkthroughTips.Count - 1)
                {
                    await OnboardingStateStore.RefreshAsync();
                    _onboardingWalkthroughStatus = "Write 100+ characters and run one AI action to finish onboarding.";
                    await EvaluateOnboardingCompletionAsync(forceTypingProbe: true);
                    return;
                }

                _onboardingWalkthroughIndex++;
                _onboardingWalkthroughStatus = null;
                await EnsureWalkthroughContextAsync();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to advance onboarding walkthrough step.");
                _onboardingWalkthroughStatus = "Could not save onboarding progress. Please try again.";
            }
            finally
            {
                _onboardingWalkthroughBusy = false;
                SyncOnboardingOverlayState();
            }
        }

        private Task OnOnboardingWalkthroughSkipAsync()
        {
            if (_onboardingWalkthroughBusy)
            {
                return Task.CompletedTask;
            }

            _onboardingWalkthroughBusy = true;
            SyncOnboardingOverlayState();
            try
            {
                _showOnboardingWalkthrough = false;
                _onboardingWalkthroughStatus = null;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to skip onboarding walkthrough.");
                _onboardingWalkthroughStatus = "Could not complete onboarding right now. You can keep editing.";
            }
            finally
            {
                _onboardingWalkthroughBusy = false;
                SyncOnboardingOverlayState();
            }

            return Task.CompletedTask;
        }

        private async Task EnsureOnboardingStarterTextAsync()
        {
            if (_onboardingStarterTextEnsured || !_showOnboardingWalkthrough || _activePage is null)
            {
                return;
            }

            string plain = await GetCurrentAiPlainTextAsync();
            if (!string.IsNullOrWhiteSpace(plain))
            {
                _onboardingStarterTextEnsured = true;
                return;
            }

            string starterHtml = OnboardingDemoSceneHtml;
            try
            {
                using HttpResponseMessage response = await Http.PutAsJsonAsync(
                    $"api/pages/{_activePage.Id}",
                    new PageUpdateRequest(_activePage.Title, starterHtml));
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                _activePage = _activePage with { Content = starterHtml, UpdatedAt = DateTimeOffset.UtcNow };
                if (_pagesBySection.TryGetValue(_activePage.SectionId, out List<PageDto>? pages))
                {
                    int pageIndex = pages.FindIndex(item => item.Id == _activePage.Id);
                    if (pageIndex >= 0)
                    {
                        pages[pageIndex] = _activePage;
                    }
                }

                if (_pageEditor is not null)
                {
                    await _pageEditor.SetContentAsync(starterHtml, markDirty: false);
                }

                _onboardingStarterTextEnsured = true;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to initialize onboarding starter scene text.");
            }
        }

        private async Task OnOnboardingAiDemoAsync()
        {
            if (_onboardingWalkthroughBusy)
            {
                return;
            }

            _onboardingAiDemoAttempted = true;
            _onboardingWalkthroughBusy = true;
            SyncOnboardingOverlayState();
            try
            {
                await SetContextTabAsync(ContextTab.Ai);

                if (!HasAction("tighten.section"))
                {
                    _onboardingWalkthroughStatus = "AI tighten is not available right now.";
                    return;
                }

                await EnsureOnboardingStarterTextAsync();
                Guid beforeProposalId = _pendingAiProposal?.ProposalId ?? Guid.Empty;
                Logger.LogInformation(
                    "Onboarding AI demo requested. DocumentId={DocumentId}, SectionId={SectionId}, AiUiEnabled={AiUiEnabled}, AiEntitled={AiEntitled}, QuotaExceeded={QuotaExceeded}",
                    DocumentId,
                    _activeSection?.Id,
                    IsAiUiEnabled,
                    IsAiEntitled,
                    IsAiQuotaExceeded);

                _onboardingWalkthroughStatus = "Running AI demo: \"tighten the character description\".";
                AiActionOption onboardingDemo = new(
                    "tighten.section",
                    "Tighten character description",
                    "tighten the character description",
                    false,
                    new Dictionary<string, object?>
                    {
                        ["onboarding_demo"] = true
                    },
                    OnboardingDemoAiInstruction,
                    false);

                await OnAiActionSelected(onboardingDemo, allowOnboardingDemoBypass: true);

                bool aiSucceeded = _pendingAiProposal is not null
                    && _pendingAiProposal.ProposalId != beforeProposalId
                    && !string.IsNullOrWhiteSpace(_pendingAiProposal.ProposedText);

                if (aiSucceeded)
                {
                    Logger.LogInformation(
                        "Onboarding AI demo completed. DocumentId={DocumentId}, SectionId={SectionId}, ProposalId={ProposalId}",
                        DocumentId,
                        _activeSection?.Id,
                        _pendingAiProposal?.ProposalId);
                    await OnboardingService.SetStepAsync(8);
                    await OnboardingStateStore.RefreshAsync();
                    await EvaluateOnboardingCompletionAsync(forceTypingProbe: false);
                    _onboardingWalkthroughStatus = "AI demo ready below. Compare the original and revised text to see how AI can tighten descriptions, improve pacing, and sharpen focus without rewriting the whole scene.";
                }
                else if (IsAiQuotaExceeded)
                {
                    Logger.LogWarning(
                        "Onboarding AI demo did not complete because quota is exceeded. DocumentId={DocumentId}, SectionId={SectionId}",
                        DocumentId,
                        _activeSection?.Id);
                    _onboardingWalkthroughStatus = "AI demo did not complete. You can continue onboarding and try again.";
                }
                else
                {
                    Logger.LogWarning(
                        "Onboarding AI demo did not complete without quota exhaustion. DocumentId={DocumentId}, SectionId={SectionId}",
                        DocumentId,
                        _activeSection?.Id);
                    _onboardingWalkthroughStatus = "AI demo did not complete. You can continue without AI.";
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Onboarding AI demo action failed.");
                _onboardingWalkthroughStatus = "AI demo failed. You can continue onboarding.";
            }
            finally
            {
                _onboardingWalkthroughBusy = false;
                SyncOnboardingOverlayState();
            }
        }

        private async Task MarkOnboardingAiSignalAsync(string eventName, string source)
        {
            if (_onboardingAiRequirementMet)
            {
                return;
            }

            _onboardingAiRequirementMet = true;
            try
            {
                await OnboardingService.TrackEventAsync(
                    eventName,
                    new Dictionary<string, object?>
                    {
                        ["source"] = source
                    });
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to track onboarding AI signal event {EventName}.", eventName);
            }

            await EvaluateOnboardingCompletionAsync(forceTypingProbe: false);
        }

        private async Task EvaluateOnboardingCompletionAsync(bool forceTypingProbe)
        {
            if (_onboardingCompletionInFlight || !_showOnboardingWalkthrough)
            {
                return;
            }

            if (OnboardingStateStore.Current.HasCompletedOnboarding)
            {
                return;
            }

            if (!_onboardingProjectCreated)
            {
                return;
            }

            if (!_onboardingSavedOnce && !_onboardingTypedEnough)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (forceTypingProbe || now - _onboardingLastTypingProbeUtc >= TimeSpan.FromSeconds(2))
                {
                    _onboardingLastTypingProbeUtc = now;
                    string plainText = await GetCurrentAiPlainTextAsync();
                    _onboardingMeasuredCharacterCount = plainText?.Trim().Length ?? 0;
                    _onboardingTypedEnough = _onboardingMeasuredCharacterCount >= OnboardingMinTypedCharacters;
                }
            }

            bool hasWritingSignal = _onboardingSavedOnce || _onboardingTypedEnough;
            if (!hasWritingSignal || !_onboardingAiRequirementMet)
            {
                return;
            }

            _onboardingCompletionInFlight = true;
            try
            {
                await OnboardingService.CompleteAsync();
                await OnboardingStateStore.RefreshAsync();
                _showOnboardingWalkthrough = false;
                _onboardingWalkthroughStatus = null;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to complete onboarding after criteria evaluation.");
            }
            finally
            {
                _onboardingCompletionInFlight = false;
                SyncOnboardingOverlayState();
            }
        }

        private async Task<string?> EnsureMeaningfulTightenAsync(
            AiActionOption action,
            AiActionExecuteRequestDto request,
            string? originalText,
            string? proposedText)
        {
            if (!IsLowImpactTighten(originalText, proposedText))
            {
                return proposedText;
            }

            Dictionary<string, object?> retryParameters = new(action.Parameters)
            {
                ["instruction"] =
                    "Tighten this passage. Keep meaning, voice, and facts. Remove redundancy and filler. Target 10-25% fewer words. Return only revised text.",
                ["target_reduction_pct"] = 20,
                ["min_reduction_pct"] = 10,
                ["preserve_meaning"] = true
            };

            AiActionExecuteRequestDto retryRequest = request with
            {
                Parameters = retryParameters
            };

            try
            {
                using HttpResponseMessage retryResult = await PostAiActionAsync(
                    action.ActionKey,
                    retryRequest,
                    trackStatus: false,
                    commandLabel: action.Label);
                if (!retryResult.IsSuccessStatusCode)
                {
                    await TryHandleAiQuotaExceededAsync(retryResult);
                    return proposedText;
                }

                AiActionExecuteResponseDto? retryResponse = await retryResult.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                if (retryResponse is null || string.IsNullOrWhiteSpace(retryResponse.ProposedText))
                {
                    return proposedText;
                }

                string candidate = retryResponse.ProposedText;
                return IsLowImpactTighten(originalText, candidate) ? proposedText : candidate;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Tighten retry failed.");
                return proposedText;
            }
            finally
            {
                await RefreshPlanUsageAsync();
            }
        }

        private void OpenTranslateModal(
            AiActionOption action,
            string plainText,
            TextRange selectionRange,
            string selectionText,
            AiSelectionSnapshot? selectionSnapshot = null)
        {
            AiSelectionSnapshot? effectiveSnapshot = selectionSnapshot;
            if (action.RequiresSelection
                && effectiveSnapshot is null
                && !string.IsNullOrWhiteSpace(selectionText))
            {
                effectiveSnapshot = BuildFallbackSelectionSnapshot(selectionText, selectionRange);
            }

            _pendingTranslateAction = action;
            _pendingTranslateContext = new TranslateContext(plainText, selectionRange, selectionText, effectiveSnapshot);
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

        private IEnumerable<TranslationLanguageOption> GetPopularTranslationLanguages(string? query, string? selectedCode, bool includeAuto = false)
        {
            HashSet<string> popularCodes = TranslationLanguages.Popular
                .Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return GetFilteredTranslationLanguages(query, selectedCode, includeAuto)
                .Where(item => popularCodes.Contains(item.Code));
        }

        private IEnumerable<TranslationLanguageOption> GetAdditionalTranslationLanguages(string? query, string? selectedCode, bool includeAuto = false)
        {
            HashSet<string> popularCodes = TranslationLanguages.Popular
                .Select(item => item.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return GetFilteredTranslationLanguages(query, selectedCode, includeAuto)
                .Where(item => !popularCodes.Contains(item.Code));
        }

        private IEnumerable<TranslationLanguageOption> GetFilteredTranslationLanguages(string? query, string? selectedCode, bool includeAuto = false)
        {
            string normalizedQuery = query?.Trim() ?? string.Empty;
            string normalizedSelected = NormalizeTranslationLanguageSelection(selectedCode, allowAuto: includeAuto, fallbackCode: includeAuto ? "auto" : "en");

            IEnumerable<TranslationLanguageOption> matches = TranslationLanguages.All
                .Where(item =>
                    string.IsNullOrWhiteSpace(normalizedQuery)
                    || item.Code.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || item.DisplayName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(item.NativeName) && item.NativeName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)));

            List<TranslationLanguageOption> result = matches.ToList();
            if (!string.IsNullOrWhiteSpace(normalizedSelected)
                && !string.Equals(normalizedSelected, "auto", StringComparison.OrdinalIgnoreCase)
                && result.All(item => !string.Equals(item.Code, normalizedSelected, StringComparison.OrdinalIgnoreCase)))
            {
                TranslationLanguageOption? selected = TranslationLanguages.Find(normalizedSelected);
                if (selected is not null)
                {
                    result.Insert(0, selected);
                }
                else
                {
                    result.Insert(0, new TranslationLanguageOption(normalizedSelected, TranslationLanguages.GetDisplayNameOrValue(normalizedSelected)));
                }
            }

            return result
                .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        private static string GetTranslationLanguageOptionLabel(TranslationLanguageOption language)
        {
            return string.IsNullOrWhiteSpace(language.NativeName)
                ? language.DisplayName
                : $"{language.DisplayName} ({language.NativeName})";
        }

        private static bool ShouldShowTranslationLanguagePlaceholder(string? selectedCode)
        {
            return string.IsNullOrWhiteSpace(TranslationLanguages.NormalizeRequestedLanguage(selectedCode));
        }

        private static string NormalizeTranslationLanguageSelection(string? value, bool allowAuto, string fallbackCode)
        {
            string? normalized = TranslationLanguages.NormalizeRequestedLanguage(value, allowAuto);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return fallbackCode;
            }

            if (allowAuto && string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return "auto";
            }

            return normalized;
        }

        private async Task ConfirmTranslateAsync()
        {
            if (_pendingTranslateAction is null || _pendingTranslateContext is null || _activeSection is null)
            {
                CloseTranslateModal();
                return;
            }

            _translateSourceLanguage = NormalizeTranslationLanguageSelection(_translateSourceLanguage, allowAuto: true, fallbackCode: "auto");
            _translateTargetLanguage = NormalizeTranslationLanguageSelection(_translateTargetLanguage, allowAuto: false, fallbackCode: "en");

            if (_pendingTranslateAction.RequiresSelection && string.IsNullOrWhiteSpace(_pendingTranslateContext.SelectionText))
            {
                ShowAiMessage("Select text to translate.");
                _isTranslateModalOpen = false;
                await InvokeAsync(StateHasChanged);
                return;
            }

            await ExecuteTranslateActionAsync(_pendingTranslateAction, _pendingTranslateContext);
            _isTranslateModalOpen = false;
            await InvokeAsync(StateHasChanged);
        }

        private async Task ExecuteTranslateActionAsync(AiActionOption action, TranslateContext context)
        {
            if (action.RequiresSelection && string.IsNullOrWhiteSpace(context.SelectionText))
            {
                ShowAiMessage("Select text to translate.");
                await InvokeAsync(StateHasChanged);
                return;
            }

            string sourceLanguageCode = NormalizeTranslationLanguageSelection(_translateSourceLanguage, allowAuto: true, fallbackCode: "auto");
            string targetLanguageCode = NormalizeTranslationLanguageSelection(_translateTargetLanguage, allowAuto: false, fallbackCode: "en");
            string sourceLanguagePrompt = TranslationLanguages.GetDisplayNameOrValue(sourceLanguageCode, allowAuto: true);
            string targetLanguagePrompt = TranslationLanguages.GetDisplayNameOrValue(targetLanguageCode);
            _translateSourceLanguage = sourceLanguageCode;
            _translateTargetLanguage = targetLanguageCode;

            Dictionary<string, object?> parameters = new(action.Parameters)
            {
                ["instruction"] = action.Instruction,
                ["source_language"] = sourceLanguageCode,
                ["target_language"] = targetLanguageCode,
                ["source_language_display"] = sourceLanguagePrompt,
                ["target_language_display"] = targetLanguagePrompt,
                ["style"] = _translateStyle
            };

            int? selectionStart = action.RequiresSelection ? context.SelectionRange.Start : null;
            int? selectionEnd = action.RequiresSelection ? context.SelectionRange.Start + context.SelectionRange.Length : null;
            string? originalText = action.RequiresSelection ? context.SelectionText : null;
            LogAiSelectionDiagnostics(action.ActionKey, originalText);

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
                using HttpResponseMessage result = await PostAiActionAsync(
                    action.ActionKey,
                    request,
                    commandLabel: action.Label);
                if (!result.IsSuccessStatusCode)
                {
                    if (await TryHandleEntitlementDeniedAsync(result, "ai.actions", "Upgrade to continue using AI features."))
                    {
                        ShowAiMessage(_entitlementUserMessage);
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    if (await TryHandlePlanUpgradeRequiredAsync(result))
                    {
                        return;
                    }

                    if (await TryHandleAiQuotaExceededAsync(result))
                    {
                        ShowAiMessage(_aiQuotaMessage);
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    string errorMessage = await ReadApiErrorMessageAsync(result, "AI translation failed.");
                    ShowAiMessage(errorMessage);
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
                Logger.LogWarning(ex, "AI translation request failed for {ActionKey}.", action.ActionKey);
                ShowAiMessage("AI translation failed.");
                await InvokeAsync(StateHasChanged);
                return;
            }
            finally
            {
                await RefreshPlanUsageAsync();
            }

            _pendingAiProposal = new PendingAiProposal(
                response.ProposalId,
                action.ActionKey,
                action.Instruction,
                action.RequiresSelection ? context.SelectionText : response.OriginalText,
                response.ProposedText,
                response.ChangesSummary,
                null,
                response.CreatedUtc,
                new PendingAiProposalContext(
                    action.RequiresSelection,
                    _activeSection!.Id,
                    _activePage?.Id,
                    context.SelectionSnapshot,
                    ResolveActionScope(action)));
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

        private async Task OpenTranslateModalFromProposal()
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
                    _pendingTranslateContext.SelectionText,
                    _pendingTranslateContext.SelectionSnapshot);
                await InvokeAsync(StateHasChanged);
                return;
            }

            string plain = await GetCurrentAiPlainTextAsync();
            TextRange selectionRange = new(0, 0);
            string selection = string.Empty;
            AiSelectionSnapshot? selectionSnapshot = null;
            if (action.RequiresSelection && _currentSelectionRange is not null)
            {
                selectionRange = NormalizeRange(_currentSelectionRange, plain.Length);
                selection = await GetSelectionTextOrFallbackAsync(plain, selectionRange);
                if (!string.IsNullOrWhiteSpace(selection))
                {
                    SelectionDocRange range = await GetSelectionDocRangeAsync();
                    selectionSnapshot = new AiSelectionSnapshot(
                        selection,
                        selectionRange,
                        range.From,
                        range.To,
                        ComputeShortHash(selection));
                }
            }
            else if (action.RequiresSelection)
            {
                selectionSnapshot = _lastAiSelectionSnapshot;
                if (selectionSnapshot is not null)
                {
                    selectionRange = selectionSnapshot.PlainRange;
                    selection = selectionSnapshot.SelectionText;
                }
            }

            OpenTranslateModal(action, plain, selectionRange, selection, selectionSnapshot);
            await InvokeAsync(StateHasChanged);
        }

        private static async Task<string> ReadApiErrorMessageAsync(HttpResponseMessage response, string fallback)
        {
            try
            {
                string payload = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return fallback;
                }

                using JsonDocument doc = JsonDocument.Parse(payload);
                JsonElement root = doc.RootElement;

                string? detail = GetJsonString(root, "detail");
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    return detail.Trim();
                }

                string? message = GetJsonString(root, "message");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message.Trim();
                }

                string? title = GetJsonString(root, "title");
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title.Trim();
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static string? GetJsonString(JsonElement root, string name)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.GetRawText();
            }

            return null;
        }

        private async Task<bool> TryHandleAiQuotaExceededAsync(HttpResponseMessage response)
        {
            if (response is null)
            {
                return false;
            }

            int statusCode = (int)response.StatusCode;
            if (statusCode != 402
                && statusCode != 429)
            {
                return false;
            }

            try
            {
                string payload = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return false;
                }

                using JsonDocument doc = JsonDocument.Parse(payload);
                JsonElement root = doc.RootElement;
                string? code = GetJsonString(root, "code");
                if (!string.Equals(code, "AI_QUOTA_EXCEEDED", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string planName = GetJsonString(root, "planKey") ?? "Free";
                int budget = GetJsonInt(root, "budget") ?? 0;
                int used = GetJsonInt(root, "used") ?? budget;
                string message = GetJsonString(root, "detail")
                    ?? GetJsonString(root, "message")
                    ?? "AI quota exceeded. Upgrade to continue.";

                _aiQuotaPlanName = planName;
                _aiQuotaBudget = Math.Max(0, budget);
                _aiQuotaUsed = Math.Max(0, used);
                _aiQuotaMessage = message;
                _isAiQuotaDialogOpen = true;
                _aiUsageStatus = new AiUsageStatusDto
                {
                    PlanKey = _aiQuotaPlanName,
                    Plan = _aiQuotaPlanName,
                    AiEnabled = _aiUsageStatus?.AiEnabled ?? true,
                    UiEnabled = _aiUsageStatus?.UiEnabled ?? true,
                    QuotaTotal = _aiQuotaBudget,
                    QuotaRemaining = Math.Max(0, _aiQuotaBudget - _aiQuotaUsed)
                };
                await InvokeAsync(StateHasChanged);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TryHandleEntitlementDeniedAsync(
            HttpResponseMessage response,
            string fallbackFeatureKey,
            string fallbackUserMessage)
        {
            if (response is null)
            {
                return false;
            }

            int statusCode = (int)response.StatusCode;
            if (statusCode != (int)HttpStatusCode.PaymentRequired
                && statusCode != (int)HttpStatusCode.Forbidden)
            {
                return false;
            }

            try
            {
                string payload = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return false;
                }

                using JsonDocument doc = JsonDocument.Parse(payload);
                JsonElement root = doc.RootElement;
                string? code = GetJsonString(root, "code");
                string? problemType = GetJsonString(root, "type");
                bool isEntitlementDenied =
                    string.Equals(code, "entitlement_denied", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(problemType, "https://prosa-app.com/problems/entitlement-denied", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(GetJsonString(root, "featureKey"));
                if (!isEntitlementDenied)
                {
                    return false;
                }

                _entitlementFeatureKey = GetJsonString(root, "featureKey")
                    ?? fallbackFeatureKey;
                _entitlementUserMessage = GetJsonString(root, "userMessage")
                    ?? GetJsonString(root, "detail")
                    ?? fallbackUserMessage;
                _entitlementUpgradeUrl = GetJsonString(root, "upgradePath")
                    ?? GetJsonString(root, "upgradeUrl")
                    ?? $"/upgrade?feature={WebUtility.UrlEncode(_entitlementFeatureKey)}";
                _entitlementUpgradeUrl = AppendUpgradeReturnUrl(_entitlementUpgradeUrl);

                Navigation.NavigateTo(_entitlementUpgradeUrl, forceLoad: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TryHandlePlanUpgradeRequiredAsync(HttpResponseMessage response)
        {
            if (response is null)
            {
                return false;
            }

            if (response.StatusCode != HttpStatusCode.PaymentRequired
                && response.StatusCode != HttpStatusCode.Forbidden)
            {
                return false;
            }

            try
            {
                string payload = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return false;
                }

                using JsonDocument doc = JsonDocument.Parse(payload);
                JsonElement root = doc.RootElement;
                string? problemUpgradePath = GetJsonString(root, "upgradePath");
                if (IsProblemDetailsResponse(response)
                    && !string.IsNullOrWhiteSpace(problemUpgradePath))
                {
                    Navigation.NavigateTo(AppendUpgradeReturnUrl(problemUpgradePath), forceLoad: true);
                    return true;
                }

                string? code = GetJsonString(root, "code");
                if (!string.Equals(code, "plan_upgrade_required", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string upgradePath = problemUpgradePath ?? "/upgrade?feature=ai.actions";
                upgradePath = AppendUpgradeReturnUrl(upgradePath);
                Navigation.NavigateTo(upgradePath, forceLoad: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProblemDetailsResponse(HttpResponseMessage response)
        {
            string? mediaType = response.Content?.Headers?.ContentType?.MediaType;
            return string.Equals(mediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase);
        }

        private static int? GetJsonInt(JsonElement root, string name)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetInt32(out int intValue))
                {
                    return intValue;
                }

                if (property.Value.ValueKind == JsonValueKind.String
                    && int.TryParse(property.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    return parsed;
                }

                return null;
            }

            return null;
        }

        private void CloseAiQuotaDialog()
        {
            _isAiQuotaDialogOpen = false;
        }

        private void NavigateToUpgradeFromQuotaDialog()
        {
            _isAiQuotaDialogOpen = false;
            Navigation.NavigateTo(FeatureAccessService.AppendReturnUrl("/upgrade"), forceLoad: true);
        }

        private void CloseEntitlementUpgradeDialog()
        {
            _isEntitlementUpgradeDialogOpen = false;
        }

        private void NavigateToUpgradeFromEntitlementDialog()
        {
            _isEntitlementUpgradeDialogOpen = false;
            string target = string.IsNullOrWhiteSpace(_entitlementUpgradeUrl)
                ? FeatureAccessService.AppendReturnUrl($"/upgrade?feature={WebUtility.UrlEncode(_entitlementFeatureKey)}")
                : _entitlementUpgradeUrl;
            Navigation.NavigateTo(target, forceLoad: true);
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
            await FlushActiveEditorAsync($"ai-apply:{pending.ActionKey}");
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

            if (!await ValidatePendingProposalSelectionAsync(pending))
            {
                return;
            }

            string? beforeContent = _pageEditor is null ? null : await _pageEditor.GetContentAsync();
            bool appendAtEnd = string.Equals(
                pending.ActionKey,
                "propose.next-paragraph",
                StringComparison.OrdinalIgnoreCase)
                || pending.Context?.AppendAtEnd == true;
            string applyMode = ResolveAiApplyMode(
                pending.Context?.Scope,
                pending.ActionKey,
                appendAtEnd);
            string proposedText = appendAtEnd
                ? NormalizeSingleParagraph(pending.ProposedText)
                : pending.ProposedText;

            if (string.Equals(applyMode, "section", StringComparison.OrdinalIgnoreCase))
            {
                string sectionPlainText = await GetCurrentAiPlainTextAsync();
                await InvokePageCommandAsync("replaceTextRange", 0, sectionPlainText.Length, proposedText);
            }
            else if (string.Equals(applyMode, "cursor", StringComparison.OrdinalIgnoreCase))
            {
                string contextText = pending.Context?.ContextText ?? await GetCurrentAiPlainTextAsync();
                if (appendAtEnd)
                {
                    proposedText = TrimLeadingEchoFromGeneratedParagraph(proposedText, contextText);
                    if (string.IsNullOrWhiteSpace(proposedText))
                    {
                        proposedText = NormalizeSingleParagraph(pending.ProposedText);
                    }

                    await InvokePageCommandAsync("appendParagraph", proposedText);
                }
                else
                {
                    await InvokePageCommandAsync("replaceSelection", proposedText);
                }
            }
            else if (pending.Context?.RequiresSelection == true && pending.Context.SelectionSnapshot is not null)
            {
                await InvokePageCommandAsync(
                    "replaceTextRange",
                    pending.Context.SelectionSnapshot.DocFrom,
                    pending.Context.SelectionSnapshot.DocTo,
                    proposedText);
            }
            else if (IsSectionScopeAction(pending.ActionKey))
            {
                if (_pageEditor is not null)
                {
                    await _pageEditor.SetContentAsync(PlainTextToHtml(proposedText));
                }
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
                if (!await ValidatePendingProposalSelectionAsync(pending))
                {
                    return;
                }

                if (pending.Context?.SelectionSnapshot is not null)
                {
                    await InvokePageCommandAsync(
                        "replaceTextRange",
                        pending.Context.SelectionSnapshot.DocFrom,
                        pending.Context.SelectionSnapshot.DocTo,
                        translatedText);
                }
                else
                {
                    await InvokePageCommandAsync("replaceSelection", translatedText);
                }
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
            string targetLanguageCode = NormalizeTranslationLanguageSelection(_translateTargetLanguage, allowAuto: false, fallbackCode: "en");
            string sourceLanguageCode = NormalizeTranslationLanguageSelection(_translateSourceLanguage, allowAuto: true, fallbackCode: "auto");
            TranslationDuplicateSectionRequest payload = new(
                html,
                targetLanguageCode,
                sourceLanguageCode,
                BuildTranslatedTitle(_activeSection.Title, targetLanguageCode));

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
            string targetLanguageCode = NormalizeTranslationLanguageSelection(_translateTargetLanguage, allowAuto: false, fallbackCode: "en");
            string sourceLanguageCode = NormalizeTranslationLanguageSelection(_translateSourceLanguage, allowAuto: true, fallbackCode: "auto");
            TranslationDuplicateDocumentRequest payload = new(
                BuildTranslatedTitle(_documentTitle, targetLanguageCode),
                targetLanguageCode,
                sourceLanguageCode,
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
            string targetLanguageCode = NormalizeTranslationLanguageSelection(_translateTargetLanguage, allowAuto: false, fallbackCode: "en");
            foreach (SectionDto section in _sections.OrderBy(item => item.OrderIndex))
            {
                string content = mapping.TryGetValue(section.Id, out string? sectionText)
                    ? PlainTextToHtml(sectionText)
                    : string.Empty;
                result.Add(new TranslatedSectionPayload(section.Id, content, BuildTranslatedTitle(section.Title, targetLanguageCode)));
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
            string lang = TranslationLanguages.GetDisplayNameOrValue(languageCode);
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

        private string? GetOnboardingContextTabId(ContextTab tab)
        {
            return tab switch
            {
                ContextTab.Ai => "onboarding-tab-ai",
                ContextTab.Continuity => "onboarding-tab-continuity",
                ContextTab.Quality => "onboarding-tab-quality",
                _ => null
            };
        }

        private string PlanStatusLabel => $"Plan: {AuthMeStateService.PlanKey}";

        private bool CanUseFeature(FeatureKey feature)
        {
            return FeatureAccessService.CanUse(feature);
        }

        private bool CanUseAiAction(AiActionOption action)
        {
            FeatureKey? feature = ResolveFeatureForAction(action.ActionKey);
            return !feature.HasValue || CanUseFeature(feature.Value);
        }

        private string GetFeatureTooltip(FeatureKey feature)
        {
            return FeatureAccessService.GetUpgradeMessage(feature);
        }

        private string GetAiActionUpgradeTooltip(AiActionOption action)
        {
            FeatureKey? feature = ResolveFeatureForAction(action.ActionKey);
            return feature.HasValue ? GetFeatureTooltip(feature.Value) : string.Empty;
        }

        private string? GetFeatureNameForAction(AiActionOption action)
        {
            return ResolveFeatureForAction(action.ActionKey)?.ToString();
        }

        private static FeatureKey? ResolveFeatureForAction(string actionKey)
        {
            return actionKey switch
            {
                "rewrite.selection" => FeatureKey.RewriteSelection,
                "translate.selection" => FeatureKey.TranslateText,
                "translate.section" => FeatureKey.TranslateText,
                "translate.document" => FeatureKey.TranslateText,
                "propose.next-paragraph" => FeatureKey.NextParagraph,
                "scene.suggest" => FeatureKey.SceneAiSuggestions,
                "scene.refine" => FeatureKey.SceneAiSuggestions,
                "scene.find-open-questions" => FeatureKey.SceneAiSuggestions,
                "custom_transform" => FeatureKey.PromptLibrary,
                "expand.selection" => FeatureKey.AdvancedReviseTools,
                "expand.section" => FeatureKey.AdvancedReviseTools,
                "tighten.selection" => FeatureKey.AdvancedReviseTools,
                "tighten.section" => FeatureKey.AdvancedReviseTools,
                "change_tone.selection" => FeatureKey.AdvancedReviseTools,
                "change_tone.section" => FeatureKey.AdvancedReviseTools,
                "show_dont_tell.selection" => FeatureKey.AdvancedReviseTools,
                "show_dont_tell.section" => FeatureKey.AdvancedReviseTools,
                _ => null
            };
        }

        private string AiUsageStatusLabel
        {
            get
            {
                int budget = AuthMeStateService.AiMonthlyTokenBudget;
                if (budget <= 0)
                {
                    return "AI: not included";
                }

                if (string.Equals(AuthMeStateService.PlanKey, "Standard", StringComparison.Ordinal)
                    || string.Equals(AuthMeStateService.PlanKey, "Professional", StringComparison.Ordinal))
                {
                    int used = Math.Max(0, AuthMeStateService.AiTokensUsedThisPeriod);
                    int percentage = (int)Math.Round(Math.Clamp(used / (double)budget, 0d, 1d) * 100d);
                    return $"AI: {percentage}% / 100%";
                }

                return $"AI: {AuthMeStateService.AiTokensUsedThisPeriod} / {budget} tokens";
            }
        }

        private bool ShowPlanUpgrade => GetPlanUpgradeHref() is not null;

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
            _hasAiUndoHistory = _aiHistoryEntries.Any(entry => entry.IsApplied);
            _hasAiRedoHistory = _aiHistoryEntries.Any(entry => entry.AppliedCount > 0 && !entry.IsApplied);
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
                return "Story guidance";
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

            if (string.Equals(actionKey, "tighten.selection", StringComparison.OrdinalIgnoreCase))
            {
                return "Tighten selection";
            }

            if (string.Equals(actionKey, "tighten.section", StringComparison.OrdinalIgnoreCase))
            {
                return "Tighten section";
            }

            if (string.Equals(actionKey, "expand.selection", StringComparison.OrdinalIgnoreCase))
            {
                return "Expand selection";
            }

            if (string.Equals(actionKey, "expand.section", StringComparison.OrdinalIgnoreCase))
            {
                return "Expand section";
            }

            if (string.Equals(actionKey, "change_tone.selection", StringComparison.OrdinalIgnoreCase))
            {
                return "Change tone (selection)";
            }

            if (string.Equals(actionKey, "change_tone.section", StringComparison.OrdinalIgnoreCase))
            {
                return "Change tone (section)";
            }

            if (string.Equals(actionKey, "show_dont_tell.selection", StringComparison.OrdinalIgnoreCase))
            {
                return "Show, don't tell (selection)";
            }

            if (string.Equals(actionKey, "show_dont_tell.section", StringComparison.OrdinalIgnoreCase))
            {
                return "Show, don't tell (section)";
            }

            if (string.Equals(actionKey, "continuity.extract_character_bible", StringComparison.OrdinalIgnoreCase))
            {
                return "Build character canon";
            }

            if (string.Equals(actionKey, "continuity.extract_place_bible", StringComparison.OrdinalIgnoreCase))
            {
                return "Build place canon";
            }

            if (string.Equals(actionKey, "continuity.extract_timeline_bible", StringComparison.OrdinalIgnoreCase))
            {
                return "Build timeline canon";
            }

            if (string.Equals(actionKey, "continuity.refresh_character_bible", StringComparison.OrdinalIgnoreCase))
            {
                return "Refresh character canon";
            }

            if (string.Equals(actionKey, "continuity.refresh_place_bible", StringComparison.OrdinalIgnoreCase))
            {
                return "Refresh place canon";
            }

            if (string.Equals(actionKey, "continuity.refresh_timeline_bible", StringComparison.OrdinalIgnoreCase))
            {
                return "Refresh timeline canon";
            }

            if (string.Equals(actionKey, "continuity.check_section", StringComparison.OrdinalIgnoreCase))
            {
                return "Check continuity";
            }

            if (string.Equals(actionKey, "continuity.apply_fix", StringComparison.OrdinalIgnoreCase))
            {
                return "Apply continuity fix";
            }

            if (string.Equals(actionKey, "custom_transform", StringComparison.OrdinalIgnoreCase))
            {
                return "Run custom prompt";
            }

            return "AI";
        }

        private static string FormatHistoryText(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? "No content captured." : text;
        }

        private bool ShouldShowTightenMetrics()
        {
            return _pendingAiProposal is not null
                && IsTightenAction(_pendingAiProposal.ActionKey)
                && !string.IsNullOrWhiteSpace(_pendingAiProposal.OriginalText)
                && !string.IsNullOrWhiteSpace(_pendingAiProposal.ProposedText);
        }

        private bool ShouldShowTightenLowImpactWarning()
        {
            if (_pendingAiProposal is null || !IsTightenAction(_pendingAiProposal.ActionKey))
            {
                return false;
            }

            return IsLowImpactTighten(_pendingAiProposal.OriginalText, _pendingAiProposal.ProposedText);
        }

        private int GetPendingOriginalWordCount()
        {
            return CountWords(_pendingAiProposal?.OriginalText ?? string.Empty);
        }

        private int GetPendingProposedWordCount()
        {
            return CountWords(_pendingAiProposal?.ProposedText ?? string.Empty);
        }

        private string GetPendingTightenDeltaLabel()
        {
            int originalWords = GetPendingOriginalWordCount();
            int proposedWords = GetPendingProposedWordCount();
            if (originalWords <= 0)
            {
                return "0%";
            }

            double percent = ((double)(originalWords - proposedWords) / originalWords) * 100d;
            return percent.ToString("+#0.0;-#0.0;0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static bool IsTightenAction(string? actionKey)
        {
            return !string.IsNullOrWhiteSpace(actionKey)
                && actionKey.StartsWith("tighten.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLowImpactTighten(string? originalText, string? proposedText)
        {
            int originalWords = CountWords(originalText ?? string.Empty);
            if (originalWords <= 0)
            {
                return false;
            }

            int proposedWords = CountWords(proposedText ?? string.Empty);
            double reduction = ((double)(originalWords - proposedWords) / originalWords) * 100d;
            return reduction < 2d;
        }

        private static bool TryParseContinuityReport(string? json, out ContinuityReport? report)
        {
            report = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            foreach (string candidate in EnumerateContinuityJsonCandidates(json))
            {
                try
                {
                    ContinuityReport? parsed = JsonSerializer.Deserialize<ContinuityReport>(candidate, JsonOptions);
                    if (parsed is null || parsed.Issues is null)
                    {
                        continue;
                    }

                    report = parsed;
                    return true;
                }
                catch (JsonException)
                {
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateContinuityJsonCandidates(string raw)
        {
            string trimmed = raw.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }

            string withoutFence = trimmed;
            if (withoutFence.StartsWith("```", StringComparison.Ordinal))
            {
                int firstNewline = withoutFence.IndexOf('\n');
                if (firstNewline >= 0 && firstNewline < withoutFence.Length - 1)
                {
                    withoutFence = withoutFence[(firstNewline + 1)..];
                }

                if (withoutFence.EndsWith("```", StringComparison.Ordinal))
                {
                    withoutFence = withoutFence[..^3];
                }

                withoutFence = withoutFence.Trim();
                if (!string.IsNullOrWhiteSpace(withoutFence))
                {
                    yield return withoutFence;
                }
            }

            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                string objectSlice = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                if (!string.IsNullOrWhiteSpace(objectSlice))
                {
                    yield return objectSlice;
                }
            }
        }

        private static ContinuityReport NormalizeContinuityReport(ContinuityReport report, int plainTextLength)
        {
            List<ContinuityIssue> normalized = new();
            foreach (ContinuityIssue issue in report.Issues ?? Array.Empty<ContinuityIssue>())
            {
                int start = Math.Clamp(issue.Anchor.PlainTextStart, 0, Math.Max(0, plainTextLength));
                int maxLen = Math.Max(0, plainTextLength - start);
                int length = Math.Clamp(issue.Anchor.PlainTextLength, 0, maxLen);
                string normalizedFix = ResolveContinuityFixText(issue);
                normalized.Add(issue with
                {
                    Anchor = new ContinuityAnchor(start, length),
                    SuggestedFix = normalizedFix
                });
            }

            return report with { Issues = normalized };
        }

        private static string GetContinuityIssueKey(ContinuityIssue issue)
        {
            return $"{issue.Type}|{issue.Anchor.PlainTextStart}|{issue.Anchor.PlainTextLength}|{issue.Message}";
        }

        private static string GetContinuityIssueCssClass(ContinuityIssue issue)
        {
            string type = issue.Type?.Trim() ?? string.Empty;
            if (type.Contains("character", StringComparison.OrdinalIgnoreCase))
            {
                return "continuity-issue continuity-character";
            }

            if (type.Contains("place", StringComparison.OrdinalIgnoreCase)
                || type.Contains("location", StringComparison.OrdinalIgnoreCase))
            {
                return "continuity-issue continuity-place";
            }

            if (type.Contains("timeline", StringComparison.OrdinalIgnoreCase)
                || type.Contains("time", StringComparison.OrdinalIgnoreCase)
                || type.Contains("date", StringComparison.OrdinalIgnoreCase))
            {
                return "continuity-issue continuity-timeline";
            }

            return "continuity-issue";
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
                    Status = CommandHistoryStatus.Applied,
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
                CommandHistoryStatus.Applied,
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

            if (_aiUsageStatus?.ShouldShowAiLimitMessage == true)
            {
                return "You've reached your monthly AI limit.";
            }

            if (_aiUsageStatus?.ShouldShowAiUpgradeHint == true)
            {
                return "Upgrade to Standard or Professional to use AI features.";
            }

            return "AI usage is not available.";
        }

        private string? GetPlanUpgradeHref()
        {
            if (string.Equals(AuthMeStateService.PlanKey, "Free", StringComparison.OrdinalIgnoreCase))
            {
                return "/start?plan=standard";
            }

            if (string.Equals(AuthMeStateService.PlanKey, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                return "/start?plan=pro";
            }

            return null;
        }

        private string GetPlanUpgradeLabel()
        {
            return string.Equals(AuthMeStateService.PlanKey, "Free", StringComparison.OrdinalIgnoreCase)
                ? "Upgrade to Standard"
                : "Upgrade to Professional";
        }

        private void NavigateToPlanUpgrade()
        {
            string? href = GetPlanUpgradeHref();
            if (string.IsNullOrWhiteSpace(href))
            {
                return;
            }

            Navigation.NavigateTo(href, forceLoad: true);
        }

        private async Task RefreshPlanUsageAsync()
        {
            try
            {
                await AuthMeStateService.RefreshAsync(force: true);
            }
            catch
            {
            }
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

            string writingIntent = "Other";
            try
            {
                await OnboardingStateStore.RefreshAsync();
                writingIntent = ResolveWritingToolsIntentKey(OnboardingStateStore.Current.PrimaryWritingIntent);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Onboarding intent resolution failed for writing tools.");
            }

            _aiActions.Clear();
            if (_availableActionKeys.Count == 0)
            {
                return;
            }

            IReadOnlyList<WritingToolDefinition> recommendedDefinitions =
                PromptStrategyResolver.GetTopWritingToolsForIntent(writingIntent);
            List<AiActionOption> recommendedTools = recommendedDefinitions
                .Select(CreateRecommendedToolOption)
                .Where(tool => _availableActionKeys.Contains(tool.ActionKey))
                .ToList();
            foreach (AiActionOption tool in recommendedTools)
            {
                _aiActions.Add(tool);
            }

            foreach (AiActionOption preset in _aiActionPresets)
            {
                if (_availableActionKeys.Contains(preset.ActionKey))
                {
                    if (recommendedTools.Any(tool =>
                        string.Equals(tool.ActionKey, preset.ActionKey, StringComparison.Ordinal)
                        && string.Equals(tool.Label, preset.Label, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

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
                            ResolveHistoryStatus(entry),
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
            string reasonLabel = GetVersionReasonLabel(latest.Reason);
            _versionStatusMessage = $"{reasonLabel} saved";

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
                _qualityStatus = null;
                _qualityHasRunOnce = false;
                _selectedQualityIssueKey = null;
                _qualityIssueActionErrors.Clear();
                _qualityAppliedIssueKeys.Clear();
                _qualityApplyingIssueKeys.Clear();
                return;
            }

            _qualityLoading = true;
            _qualityError = null;
            _qualityStatus = null;
            _qualityFromCache = false;
            _qualityIssueActionErrors.Clear();
            _qualityAppliedIssueKeys.Clear();
            _qualityApplyingIssueKeys.Clear();

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
                if (_qualityIssues.Count > 0)
                {
                    _qualityHasRunOnce = true;
                }

                ReconcileQualityIssueStateAfterRefresh();
                await SyncQualityIssueHighlightAsync();
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
            _qualityHasRunOnce = false;
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

        private string GetQualityRunButtonLabel()
        {
            return _qualityHasRunOnce ? "Re-run check" : "Run check";
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
            _qualityStatus = null;
            _qualityFromCache = false;
            _qualityIssueActionErrors.Clear();
            _qualityAppliedIssueKeys.Clear();
            _qualityApplyingIssueKeys.Clear();

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

                _qualityHasRunOnce = true;
                _qualityFromCache = result.FromCache;
                _qualityIssues.Clear();
                if (result.Issues.Count > 0)
                {
                    _qualityIssues.AddRange(result.Issues);
                }

                int applyableCount = _qualityIssues.Count(CanApplyQualityIssue);
                Logger.LogInformation(
                    "Quality checks loaded for page {PageId}. Issues={IssueCount}, Applyable={ApplyableCount}, Scope={Scope}, FromCache={FromCache}",
                    _activePage.Id,
                    _qualityIssues.Count,
                    applyableCount,
                    scope,
                    _qualityFromCache);

                foreach (PageQualityIssueDto issue in _qualityIssues.Where(item => !CanApplyQualityIssue(item)))
                {
                    Logger.LogDebug(
                        "Quality issue is not applyable. Key={IssueKey}, Rule={RuleId}, Kind={Kind}, Severity={Severity}, HasFix={HasFix}, FixKind={FixKind}, Reason={Reason}",
                        issue.IssueKey,
                        issue.RuleId,
                        issue.Kind,
                        issue.Severity,
                        issue.Fix is not null,
                        issue.Fix?.Kind,
                        GetQualityIssueApplyUnavailableReason(issue));
                }

                foreach (PageQualityIssueDto issue in _qualityIssues)
                {
                    Logger.LogDebug(
                        "Quality issue card. Key={IssueKey}, Rule={RuleId}, Kind={Kind}, Severity={Severity}, HasFix={HasFix}, Applyable={Applyable}",
                        issue.IssueKey,
                        issue.RuleId,
                        issue.Kind,
                        issue.Severity,
                        issue.Fix is not null,
                        CanApplyQualityIssue(issue));
                }

                ReconcileQualityIssueStateAfterRefresh();
                await SyncQualityIssueHighlightAsync();
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
                _qualityIssueActionErrors.Remove(issue.IssueKey);
                _qualityAppliedIssueKeys.Remove(issue.IssueKey);
                _qualityApplyingIssueKeys.Remove(issue.IssueKey);

                if (_selectedQualityIssueKey == issue.IssueKey)
                {
                    _selectedQualityIssueKey = null;
                    if (_pageEditor is not null)
                    {
                        await _pageEditor.ClearQualityIssueHighlightAsync(issue.IssueKey);
                    }
                }

                await SyncQualityIssueHighlightAsync();
            }
            catch (Exception ex)
            {
                _qualityError = $"Failed to dismiss issue: {ex.Message}";
            }
        }

        private async Task ShowQualityIssueInTextAsync(PageQualityIssueDto issue)
        {
            _selectedQualityIssueKey = issue.IssueKey;
            _qualityIssueActionErrors.Remove(issue.IssueKey);

            if (_pageEditor is null)
            {
                return;
            }

            await _pageEditor.SetActiveQualityIssueAsync(issue.IssueKey);

            bool highlighted = await _pageEditor.ScrollToQualityIssueAsync(issue.IssueKey);
            if (!highlighted)
            {
                highlighted = await _pageEditor.HighlightQualityIssueAsync(
                    issue.IssueKey,
                    issue.StartOffset,
                    issue.EndOffset,
                    issue.AnchorText);
            }

            if (!highlighted)
            {
                _qualityIssueActionErrors[issue.IssueKey] = "Can't locate this issue in the current text.";
            }
        }

        private async Task OpenQualityProposalAsync(PageQualityIssueDto issue)
        {
            if (_activePage is null || _pageEditor is null || !CanApplyQualityIssue(issue))
            {
                Logger.LogInformation(
                    "Quality apply request ignored. PageReady={PageReady}, EditorReady={EditorReady}, IssueKey={IssueKey}, Reason={Reason}",
                    _activePage is not null,
                    _pageEditor is not null,
                    issue.IssueKey,
                    GetQualityIssueApplyUnavailableReason(issue));
                return;
            }

            PageQualityIssueDto effectiveIssue = await EnsureAutoProposableFixAsync(issue);
            if (QualityIssueCapabilities.IsAutoProposable(effectiveIssue) && !HasValidAutoProposableFix(effectiveIssue))
            {
                _qualityIssueActionErrors[effectiveIssue.IssueKey] = GetAutoProposableFailureMessage(effectiveIssue);
                return;
            }

            _proposalError = null;
            _proposalIssue = effectiveIssue;
            _proposalPreview = await BuildQualityProposalPreviewAsync(effectiveIssue);
            _isQualityProposalOpen = true;
            _isProposalApplying = false;
            _qualityIssueActionErrors.Remove(effectiveIssue.IssueKey);

            await ShowQualityIssueInTextAsync(effectiveIssue);
        }

        private async Task ConfirmQualityProposalApplyAsync()
        {
            if (_proposalIssue is null || _proposalIssue.Fix is null || _activePage is null || _pageEditor is null)
            {
                return;
            }

            if (_isProposalApplying)
            {
                return;
            }

            _proposalError = null;
            _isProposalApplying = true;

            (bool applied, string? error) = await ApplyQualityIssueChangeCoreAsync(_proposalIssue);
            if (!applied)
            {
                _proposalError = string.IsNullOrWhiteSpace(error)
                    ? GetQualityFixFailureMessage()
                    : error;
                _isProposalApplying = false;
                return;
            }

            _isProposalApplying = false;
            CloseQualityProposal();
        }

        private void CancelQualityProposal()
        {
            if (_isProposalApplying)
            {
                return;
            }

            CloseQualityProposal();
        }

        private void CloseQualityProposal()
        {
            _isQualityProposalOpen = false;
            _proposalIssue = null;
            _proposalPreview = null;
            _proposalError = null;
            _isProposalApplying = false;
        }

        private async Task<QualityProposalPreview> BuildQualityProposalPreviewAsync(PageQualityIssueDto issue)
        {
            string before = issue.Fix?.AnchorText ?? issue.AnchorText ?? string.Empty;
            string after = QualityFixClientHelpers.BuildProposalAfterText(issue.Fix);
            if (issue.Fix is not null
                && string.IsNullOrWhiteSpace(after)
                && !string.IsNullOrWhiteSpace(issue.Fix.Text)
                && QualityFixClientHelpers.LooksLikeProposalMetaLeak(issue.Fix.Text)
                && _qualityMetaLeakWarnedIssueKeys.Add(issue.IssueKey))
            {
                Logger.LogWarning(
                    "Quality proposal text looked like meta/prompt payload and was suppressed. IssueKey={IssueKey}, RuleId={RuleId}, Kind={Kind}",
                    issue.IssueKey,
                    issue.RuleId,
                    issue.Kind);
            }
            string prefix = string.Empty;
            string suffix = string.Empty;

            if (_pageEditor is null || issue.Fix is null)
            {
                return new QualityProposalPreview(before, after, prefix, suffix);
            }

            string plainText = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return new QualityProposalPreview(before, after, prefix, suffix);
            }

            int from = Math.Clamp(issue.Fix.From, 0, plainText.Length);
            int to = Math.Clamp(issue.Fix.To, from, plainText.Length);
            if (string.IsNullOrWhiteSpace(before) && to > from)
            {
                before = plainText[from..to];
            }

            int snippetStart = from;
            int snippetEnd = to;

            if (!string.IsNullOrWhiteSpace(before))
            {
                int nearest = FindNearestOccurrence(plainText, before, from);
                if (nearest >= 0)
                {
                    snippetStart = nearest;
                    snippetEnd = Math.Min(plainText.Length, nearest + before.Length);
                }
            }

            int contextStart = Math.Max(0, snippetStart - 40);
            int contextEnd = Math.Min(plainText.Length, snippetEnd + 40);
            prefix = plainText[contextStart..snippetStart];
            suffix = plainText[snippetEnd..contextEnd];

            return new QualityProposalPreview(before, after, prefix, suffix);
        }

        private static int FindNearestOccurrence(string source, string value, int targetIndex)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
            {
                return -1;
            }

            List<int> matches = new();
            int searchFrom = 0;
            while (searchFrom <= source.Length - value.Length)
            {
                int found = source.IndexOf(value, searchFrom, StringComparison.Ordinal);
                if (found < 0)
                {
                    break;
                }

                matches.Add(found);
                searchFrom = found + Math.Max(1, value.Length);
            }

            if (matches.Count == 0)
            {
                return -1;
            }

            return matches
                .OrderBy(index => Math.Abs(index - targetIndex))
                .First();
        }

        private async Task ApplyQualityIssueChangeAsync(PageQualityIssueDto issue)
        {
            (bool applied, string? error) = await ApplyQualityIssueChangeCoreAsync(issue);
            if (applied)
            {
                return;
            }

            _qualityIssueActionErrors[issue.IssueKey] = string.IsNullOrWhiteSpace(error)
                ? GetQualityFixFailureMessage()
                : error;
        }

        private string GetQualityFixFailureMessage()
        {
            string? reason = _pageEditor?.LastQualityFixFailureReason;
            if (string.Equals(reason, "doc_expected_text_mismatch", StringComparison.OrdinalIgnoreCase))
            {
                return "The text changed and we couldn't safely locate the target range. Click 'Show in text' then try again.";
            }
            if (string.Equals(reason, "could_not_resolve_range", StringComparison.OrdinalIgnoreCase))
            {
                return "The text changed and we couldn't safely locate the target range. Click 'Show in text' then try again.";
            }

            return "Can't apply automatically; text changed.";
        }

        private async Task<(bool Applied, string? Error)> ApplyQualityIssueChangeCoreAsync(PageQualityIssueDto issue)
        {
            if (_activePage is null || _pageEditor is null)
            {
                return (false, "Editor is not ready.");
            }

            if (issue.Fix is null || !CanApplyQualityIssue(issue) || _qualityApplyingIssueKeys.Contains(issue.IssueKey))
            {
                return (false, "Can't apply this issue.");
            }

            _qualityIssueActionErrors.Remove(issue.IssueKey);
            _qualityAppliedIssueKeys.Remove(issue.IssueKey);
            _qualityApplyingIssueKeys.Add(issue.IssueKey);
            _qualityStatus = null;

            try
            {
                await FlushActiveEditorAsync($"quality-apply:{issue.IssueKey}");

                PageQualityIssueDto effectiveIssue = await EnsureAutoProposableFixAsync(issue);
                if (effectiveIssue.Fix is null)
                {
                    return (false, "Can't apply this issue.");
                }

                if (QualityIssueCapabilities.IsAutoProposable(effectiveIssue) && !HasValidAutoProposableFix(effectiveIssue))
                {
                    return (false, GetAutoProposableFailureMessage(effectiveIssue));
                }

                bool applied = await _pageEditor.ApplyQualityIssueFixAsync(effectiveIssue.Fix, effectiveIssue.AnchorText, effectiveIssue.IssueKey);
                if (!applied)
                {
                    return (false, GetQualityFixFailureMessage());
                }

                await _pageEditor.SaveNowAsync();
                await _pageEditor.ClearQualityIssueHighlightAsync(issue.IssueKey);
                _qualityAppliedIssueKeys.Add(issue.IssueKey);
                _qualityStatus = "Applied.";

                if (string.Equals(_qualityScope, "page", StringComparison.OrdinalIgnoreCase))
                {
                    await RunQualityChecksAsync();
                }
                else
                {
                    _qualityIssues.RemoveAll(item => item.IssueKey == issue.IssueKey);
                    await SyncQualityIssueHighlightAsync();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Apply failed: {ex.Message}");
            }
            finally
            {
                _qualityApplyingIssueKeys.Remove(issue.IssueKey);
            }
        }

        private async Task<PageQualityIssueDto> EnsureAutoProposableFixAsync(PageQualityIssueDto issue)
        {
            if (!QualityIssueCapabilities.IsAutoProposable(issue))
            {
                return issue;
            }

            if (QualityIssueCapabilities.IsRepeatedWordIssue(issue))
            {
                return await EnsureRepeatedWordRewriteFixAsync(issue);
            }

            if (QualityIssueCapabilities.IsSentenceLengthIssue(issue))
            {
                return await EnsureSentenceLengthRewriteFixAsync(issue);
            }

            if (QualityIssueCapabilities.IsPassiveVoiceIssue(issue))
            {
                return await EnsurePassiveVoiceRewriteFixAsync(issue);
            }

            return issue;
        }

        private static string GetAutoProposableFailureMessage(PageQualityIssueDto issue)
        {
            if (QualityIssueCapabilities.IsRepeatedWordIssue(issue))
            {
                return "Couldn't reduce repetition automatically.";
            }

            if (QualityIssueCapabilities.IsSentenceLengthIssue(issue))
            {
                return "Couldn't split this sentence automatically.";
            }

            if (QualityIssueCapabilities.IsPassiveVoiceIssue(issue))
            {
                return "Couldn't rewrite this sentence into active voice automatically.";
            }

            return "Couldn't generate an automatic suggestion.";
        }

        private async Task<PageQualityIssueDto> EnsureRepeatedWordRewriteFixAsync(PageQualityIssueDto issue)
        {
            if (_pageEditor is null || _activeSection is null)
            {
                return issue;
            }

            bool alreadyRewrite = HasValidRepeatedWordRewriteFix(issue);
            if (alreadyRewrite)
            {
                return issue;
            }

            string plainText = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return issue;
            }

            RepeatedWordApplyRange? applyRange = await BuildRepeatedWordApplyRangeAsync(issue, plainText);
            if (applyRange is null || string.IsNullOrWhiteSpace(applyRange.Before))
            {
                return issue;
            }

            string anchor = issue.AnchorText ?? issue.Fix?.AnchorText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(anchor))
            {
                return issue;
            }

            string rewritten = BuildDeterministicRepeatedWordRewrite(applyRange.Before, anchor);
            if (string.IsNullOrWhiteSpace(rewritten)
                || string.Equals(rewritten.Trim(), applyRange.Before.Trim(), StringComparison.Ordinal))
            {
                rewritten = await GenerateRepeatedWordRewriteAsync(issue, plainText, applyRange, strictMode: false);
            }
            bool valid = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                applyRange.Before,
                rewritten,
                anchor,
                out int originalCount,
                out int candidateCount,
                out _);
            if (!valid)
            {
                rewritten = await GenerateRepeatedWordRewriteAsync(issue, plainText, applyRange, strictMode: true);
                valid = QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                    applyRange.Before,
                    rewritten,
                    anchor,
                    out originalCount,
                    out candidateCount,
                    out _);
            }

            string normalized = QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate(rewritten);
            if (!valid || !QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                    applyRange.Before,
                    normalized,
                    anchor,
                    out originalCount,
                    out candidateCount,
                    out _))
            {
                Logger.LogWarning(
                    "Repeated-word rewrite rejected. IssueKey={IssueKey}, Anchor={Anchor}, OriginalCount={OriginalCount}, CandidateCount={CandidateCount}",
                    issue.IssueKey,
                    anchor,
                    originalCount,
                    candidateCount);
                return issue;
            }

            QualityIssueFixDto updatedFix = new(
                "rewrite",
                applyRange.PlainFrom,
                applyRange.PlainTo,
                normalized,
                anchor,
                issue.IssueKey,
                applyRange.DocFrom,
                applyRange.DocTo,
                applyRange.Before);

            PageQualityIssueDto updatedIssue = issue with
            {
                Fix = updatedFix,
                AnchorText = anchor
            };

            UpsertQualityIssue(updatedIssue);
            return updatedIssue;
        }

        private static string BuildDeterministicRepeatedWordRewrite(string source, string anchor)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(anchor))
            {
                return string.Empty;
            }

            string escaped = Regex.Escape(anchor.Trim());
            string pattern = $@"(?<!\w)({escaped})(?:\s+\1)+(?!\w)";
            string collapsed = Regex.Replace(
                source,
                pattern,
                match => match.Groups[1].Value,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return collapsed;
        }

        private async Task<PageQualityIssueDto> EnsureSentenceLengthRewriteFixAsync(PageQualityIssueDto issue)
        {
            if (_pageEditor is null || _activeSection is null)
            {
                return issue;
            }

            bool alreadyRewrite =
                issue.Fix is not null
                && string.Equals(issue.Fix.Kind, "rewrite", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(issue.Fix.Text)
                && !string.IsNullOrWhiteSpace(issue.Fix.ExpectedText)
                && !LooksLikeInstructionLeak(issue.Fix.Text);
            if (alreadyRewrite)
            {
                return issue;
            }

            string plainText = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return issue;
            }

            RepeatedWordApplyRange? applyRange = await BuildRepeatedWordApplyRangeAsync(issue, plainText);
            if (applyRange is null || string.IsNullOrWhiteSpace(applyRange.Before))
            {
                return issue;
            }

            string rewritten = await GenerateSentenceLengthRewriteAsync(issue, plainText, applyRange, strictMode: false);
            string normalized = NormalizeContinuityRewriteCandidate(rewritten);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                rewritten = await GenerateSentenceLengthRewriteAsync(issue, plainText, applyRange, strictMode: true);
                normalized = NormalizeContinuityRewriteCandidate(rewritten);
            }

            if (string.IsNullOrWhiteSpace(normalized)
                || LooksLikeInstructionLeak(normalized)
                || string.Equals(normalized.Trim(), applyRange.Before.Trim(), StringComparison.Ordinal))
            {
                return issue;
            }

            QualityIssueFixDto updatedFix = new(
                "rewrite",
                applyRange.PlainFrom,
                applyRange.PlainTo,
                normalized,
                issue.AnchorText,
                issue.IssueKey,
                applyRange.DocFrom,
                applyRange.DocTo,
                applyRange.Before);

            PageQualityIssueDto updatedIssue = issue with
            {
                Fix = updatedFix,
                AnchorText = applyRange.Before
            };

            UpsertQualityIssue(updatedIssue);
            return updatedIssue;
        }

        private async Task<PageQualityIssueDto> EnsurePassiveVoiceRewriteFixAsync(PageQualityIssueDto issue)
        {
            if (_pageEditor is null || _activeSection is null)
            {
                return issue;
            }

            bool alreadyRewrite =
                issue.Fix is not null
                && string.Equals(issue.Fix.Kind, "rewrite", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(issue.Fix.Text)
                && !string.IsNullOrWhiteSpace(issue.Fix.ExpectedText)
                && !LooksLikeInstructionLeak(issue.Fix.Text);
            if (alreadyRewrite)
            {
                return issue;
            }

            string plainText = await _pageEditor.GetPlainTextAsync() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return issue;
            }

            RepeatedWordApplyRange? applyRange = await BuildRepeatedWordApplyRangeAsync(issue, plainText);
            if (applyRange is null || string.IsNullOrWhiteSpace(applyRange.Before))
            {
                return issue;
            }

            string rewritten = await GeneratePassiveVoiceRewriteAsync(issue, plainText, applyRange, strictMode: false);
            string normalized = NormalizeContinuityRewriteCandidate(rewritten);
            if (string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized.Trim(), applyRange.Before.Trim(), StringComparison.Ordinal))
            {
                rewritten = await GeneratePassiveVoiceRewriteAsync(issue, plainText, applyRange, strictMode: true);
                normalized = NormalizeContinuityRewriteCandidate(rewritten);
            }

            if (string.IsNullOrWhiteSpace(normalized)
                || LooksLikeInstructionLeak(normalized)
                || string.Equals(normalized.Trim(), applyRange.Before.Trim(), StringComparison.Ordinal))
            {
                Logger.LogWarning(
                    "Passive voice rewrite rejected. IssueKey={IssueKey}, Anchor={Anchor}",
                    issue.IssueKey,
                    issue.AnchorText ?? issue.Fix?.AnchorText ?? string.Empty);
                return issue;
            }

            QualityIssueFixDto updatedFix = new(
                "rewrite",
                applyRange.PlainFrom,
                applyRange.PlainTo,
                normalized,
                applyRange.Before,
                issue.IssueKey,
                applyRange.DocFrom,
                applyRange.DocTo,
                applyRange.Before);

            PageQualityIssueDto updatedIssue = issue with
            {
                Fix = updatedFix,
                AnchorText = applyRange.Before
            };

            UpsertQualityIssue(updatedIssue);
            return updatedIssue;
        }

        private async Task<string> GenerateSentenceLengthRewriteAsync(PageQualityIssueDto issue, string plainText, RepeatedWordApplyRange applyRange, bool strictMode)
        {
            if (_activeSection is null)
            {
                return string.Empty;
            }

            string instruction = "Rewrite the text below in the SAME LANGUAGE as the input. Preserve meaning and tone. Split the long/complex sentence into shorter clear sentences where helpful. Return ONLY the rewritten span text (no explanation, no bullets, no quotes).";
            if (strictMode)
            {
                instruction += " Your previous output was invalid. Return final rewritten prose only.";
            }

            Dictionary<string, object?> parameters = new()
            {
                ["instruction"] = instruction,
                ["tone"] = "Neutral",
                ["length"] = "Same",
                ["preserve_terms"] = true
            };

            AiActionExecuteRequestDto request = new(
                DocumentId,
                _activeSection.Id,
                _activePage?.Id,
                applyRange.PlainFrom,
                applyRange.PlainTo,
                applyRange.Before,
                plainText,
                GetOutlineTextForAi(),
                parameters);

            try
            {
                using HttpResponseMessage result = await PostAiActionAsync("rewrite.selection", request, commandLabel: "Rewrite selection");
                if (!result.IsSuccessStatusCode)
                {
                    await TryHandleAiQuotaExceededAsync(result);
                    return string.Empty;
                }

                AiActionExecuteResponseDto? response = await result.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                return response?.ProposedText ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                await RefreshPlanUsageAsync();
            }
        }

        private void UpsertQualityIssue(PageQualityIssueDto updatedIssue)
        {
            int index = _qualityIssues.FindIndex(item => string.Equals(item.IssueKey, updatedIssue.IssueKey, StringComparison.Ordinal));
            if (index >= 0)
            {
                _qualityIssues[index] = updatedIssue;
            }
        }

        private async Task<string> GenerateRepeatedWordRewriteAsync(PageQualityIssueDto issue, string plainText, RepeatedWordApplyRange applyRange, bool strictMode)
        {
            if (_activeSection is null)
            {
                return string.Empty;
            }

            string anchor = issue.AnchorText ?? issue.Fix?.AnchorText ?? string.Empty;
            int originalCount = QualityRewriteOutputValidator.CountOccurrences(applyRange.Before, anchor);
            string instruction = $"Rewrite the text below in the SAME LANGUAGE. Preserve meaning and tone. Reduce repetition of this word/phrase: '{anchor}'. In your rewrite, '{anchor}' must appear fewer times than in the original span (ideally once). Use synonyms or restructure. Return ONLY the rewritten span, no explanations.";
            if (strictMode)
            {
                instruction += $" Your output still repeats '{anchor}'. Rewrite again and ensure it appears at most once. Original count was {originalCount}.";
            }

            Dictionary<string, object?> parameters = new()
            {
                ["instruction"] = instruction,
                ["tone"] = "Neutral",
                ["length"] = "Same",
                ["preserve_terms"] = true
            };

            AiActionExecuteRequestDto request = new(
                DocumentId,
                _activeSection.Id,
                _activePage?.Id,
                applyRange.PlainFrom,
                applyRange.PlainTo,
                applyRange.Before,
                plainText,
                GetOutlineTextForAi(),
                parameters);

            try
            {
                using HttpResponseMessage result = await PostAiActionAsync("rewrite.selection", request, commandLabel: "Rewrite selection");
                if (!result.IsSuccessStatusCode)
                {
                    await TryHandleAiQuotaExceededAsync(result);
                    return string.Empty;
                }

                AiActionExecuteResponseDto? response = await result.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                return QualityRewriteOutputValidator.NormalizeRepeatedWordCandidate(response?.ProposedText);
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                await RefreshPlanUsageAsync();
            }
        }

        private async Task<string> GeneratePassiveVoiceRewriteAsync(PageQualityIssueDto issue, string plainText, RepeatedWordApplyRange applyRange, bool strictMode)
        {
            if (_activeSection is null)
            {
                return string.Empty;
            }

            string instruction = "Rewrite the text below in the SAME LANGUAGE as the input. Convert passive voice to active voice where possible while preserving meaning and tone. Return ONLY the rewritten span text (no explanation, no bullets, no quotes).";
            if (strictMode)
            {
                instruction += " Your previous output was invalid. Return final rewritten prose only.";
            }

            Dictionary<string, object?> parameters = new()
            {
                ["instruction"] = instruction,
                ["tone"] = "Neutral",
                ["length"] = "Same",
                ["preserve_terms"] = true
            };

            AiActionExecuteRequestDto request = new(
                DocumentId,
                _activeSection.Id,
                _activePage?.Id,
                applyRange.PlainFrom,
                applyRange.PlainTo,
                applyRange.Before,
                plainText,
                GetOutlineTextForAi(),
                parameters);

            try
            {
                using HttpResponseMessage result = await PostAiActionAsync("rewrite.selection", request, commandLabel: "Rewrite selection");
                if (!result.IsSuccessStatusCode)
                {
                    await TryHandleAiQuotaExceededAsync(result);
                    return string.Empty;
                }

                AiActionExecuteResponseDto? response = await result.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>();
                return response?.ProposedText ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                await RefreshPlanUsageAsync();
            }
        }

        private async Task<RepeatedWordApplyRange?> BuildRepeatedWordApplyRangeAsync(PageQualityIssueDto issue, string plainText)
        {
            if (_pageEditor is null || string.IsNullOrWhiteSpace(plainText))
            {
                return null;
            }

            int anchorFrom = Math.Clamp(issue.Fix?.From ?? issue.StartOffset, 0, plainText.Length);
            int anchorTo = Math.Clamp(issue.Fix?.To ?? issue.EndOffset, anchorFrom, plainText.Length);
            if (anchorTo <= anchorFrom)
            {
                anchorFrom = Math.Clamp(issue.StartOffset, 0, plainText.Length);
                anchorTo = Math.Clamp(issue.EndOffset, anchorFrom, plainText.Length);
                if (anchorTo <= anchorFrom)
                {
                    return null;
                }
            }

            ContinuityRewriteSpan sentenceSpan = ContinuityRewriteSpanResolver.ExpandToSentenceSpan(
                plainText,
                anchorFrom,
                anchorTo - anchorFrom,
                contextRadius: 56);
            if (sentenceSpan.Length <= 0 || string.IsNullOrWhiteSpace(sentenceSpan.Before))
            {
                return null;
            }

            PageEditor.QualityIssueRangeResolution? resolved = await _pageEditor.ResolvePlainRangeAsync(
                sentenceSpan.Start,
                sentenceSpan.Start + sentenceSpan.Length,
                sentenceSpan.Before);
            if (resolved is null
                || !resolved.Resolved
                || !resolved.DocFrom.HasValue
                || !resolved.DocTo.HasValue
                || !resolved.From.HasValue
                || !resolved.To.HasValue
                || resolved.To.Value <= resolved.From.Value)
            {
                return null;
            }

            int plainFrom = resolved.From.Value;
            int plainTo = resolved.To.Value;
            if (plainTo <= plainFrom || plainTo > plainText.Length)
            {
                return null;
            }

            ContinuityRewriteSpan aligned = ContinuityRewriteSpanResolver.BuildFromRange(
                plainText,
                plainFrom,
                plainTo - plainFrom,
                contextRadius: 56);

            return new RepeatedWordApplyRange(
                plainFrom,
                plainTo,
                resolved.DocFrom.Value,
                resolved.DocTo.Value,
                aligned.Before,
                aligned.Prefix,
                aligned.Suffix,
                aligned.StartsSentence,
                aligned.EndsSentence);
        }

        private static bool HasValidAutoProposableFix(PageQualityIssueDto issue)
        {
            if (QualityIssueCapabilities.IsRepeatedWordIssue(issue))
            {
                return HasValidRepeatedWordRewriteFix(issue);
            }

            if (QualityIssueCapabilities.IsSentenceLengthIssue(issue))
            {
                return issue.Fix is not null
                    && string.Equals(issue.Fix.Kind, "rewrite", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(issue.Fix.Text)
                    && !LooksLikeInstructionLeak(issue.Fix.Text);
            }

            if (QualityIssueCapabilities.IsPassiveVoiceIssue(issue))
            {
                return issue.Fix is not null
                    && string.Equals(issue.Fix.Kind, "rewrite", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(issue.Fix.Text)
                    && !string.IsNullOrWhiteSpace(issue.Fix.ExpectedText)
                    && !LooksLikeInstructionLeak(issue.Fix.Text)
                    && !string.Equals(issue.Fix.Text.Trim(), issue.Fix.ExpectedText.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            return issue.Fix is not null;
        }

        private static bool HasValidRepeatedWordRewriteFix(PageQualityIssueDto issue)
        {
            if (issue.Fix is null)
            {
                return false;
            }

            if (!string.Equals(issue.Fix.Kind, "rewrite", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string original = issue.Fix.ExpectedText ?? issue.AnchorText ?? string.Empty;
            string anchor = issue.Fix.AnchorText ?? issue.AnchorText ?? string.Empty;
            return QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(
                original,
                issue.Fix.Text ?? string.Empty,
                anchor,
                out _,
                out _,
                out _);
        }

        private bool IsQualityIssueSelected(PageQualityIssueDto issue)
        {
            return string.Equals(_selectedQualityIssueKey, issue.IssueKey, StringComparison.Ordinal);
        }

        private bool CanApplyQualityIssue(PageQualityIssueDto issue)
        {
            return GetQualityIssueApplyUnavailableReason(issue) is null;
        }

        private string? GetQualityIssueApplyUnavailableReason(PageQualityIssueDto issue)
        {
            if (_qualityApplyingIssueKeys.Contains(issue.IssueKey))
            {
                return "An apply operation is already running for this issue.";
            }

            if (QualityIssueCapabilities.IsAutoProposable(issue))
            {
                return null;
            }

            if (issue.Fix is null)
            {
                return "No automatic fix is available for this issue.";
            }

            if (string.IsNullOrWhiteSpace(issue.Fix.Kind))
            {
                return "Fix kind is missing.";
            }

            bool supported =
                string.Equals(issue.Fix.Kind, "replace", StringComparison.OrdinalIgnoreCase)
                || string.Equals(issue.Fix.Kind, "delete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(issue.Fix.Kind, "insert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(issue.Fix.Kind, "rewrite", StringComparison.OrdinalIgnoreCase);

            if (!supported)
            {
                return $"Unsupported fix kind: {issue.Fix.Kind}.";
            }

            return null;
        }

        private string GetQualityIssueApplyButtonTitle(PageQualityIssueDto issue)
        {
            return GetQualityIssueApplyUnavailableReason(issue) ?? "Apply suggested change.";
        }

        private void ReconcileQualityIssueStateAfterRefresh()
        {
            if (_qualityIssues.Count == 0)
            {
                _qualityAppliedIssueKeys.Clear();
                _qualityApplyingIssueKeys.Clear();
                _qualityIssueActionErrors.Clear();
                return;
            }

            HashSet<string> validKeys = _qualityIssues
                .Select(item => item.IssueKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.Ordinal);

            _qualityAppliedIssueKeys.RemoveWhere(key => !validKeys.Contains(key));
            _qualityApplyingIssueKeys.RemoveWhere(key => !validKeys.Contains(key));

            List<string> staleErrorKeys = _qualityIssueActionErrors.Keys
                .Where(key => !validKeys.Contains(key))
                .ToList();
            foreach (string staleKey in staleErrorKeys)
            {
                _qualityIssueActionErrors.Remove(staleKey);
            }
        }

        private string? GetQualityIssueActionError(PageQualityIssueDto issue)
        {
            if (_qualityIssueActionErrors.TryGetValue(issue.IssueKey, out string? message))
            {
                return message;
            }

            return null;
        }

        private bool IsQualityIssueApplying(PageQualityIssueDto issue)
        {
            return _qualityApplyingIssueKeys.Contains(issue.IssueKey);
        }

        private bool IsQualityIssueApplied(PageQualityIssueDto issue)
        {
            return _qualityAppliedIssueKeys.Contains(issue.IssueKey);
        }

        private async Task SyncQualityIssueHighlightAsync()
        {
            if (_pageEditor is null)
            {
                return;
            }

            if (!_isStyleQualityTabActive)
            {
                await _pageEditor.ClearAllQualityIssueHighlightsAsync();
                return;
            }

            if (_qualityIssues.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(_selectedQualityIssueKey))
                {
                    await _pageEditor.SetActiveQualityIssueAsync(null);
                }

                _selectedQualityIssueKey = null;
                return;
            }

            PageQualityIssueDto? issue = _qualityIssues
                .FirstOrDefault(item => string.Equals(item.IssueKey, _selectedQualityIssueKey, StringComparison.Ordinal));

            if (issue is not null)
            {
                await _pageEditor.SetActiveQualityIssueAsync(issue.IssueKey);
            }
            else
            {
                await _pageEditor.SetActiveQualityIssueAsync(null);
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

            try
            {
                return await _pageEditor.GetSelectionTextAsync();
            }
            catch (JSException ex)
            {
                Logger.LogDebug(ex, "GetSelectionText interop failed.");
                return null;
            }
        }

        private async Task<string> GetSelectionTextOrFallbackAsync(string plainText, TextRange range)
        {
            string? liveSelection = await GetSelectionTextAsync();
            if (!string.IsNullOrWhiteSpace(liveSelection))
            {
                return liveSelection;
            }

            return ExtractRangeText(plainText, range);
        }

        private async Task<AiSelectionSnapshot?> BuildAiSelectionSnapshotAsync(string plainText)
        {
            if (_pageEditor is null)
            {
                return null;
            }

            TextRange plainRange = _currentSelectionRange is null
                ? new TextRange(0, 0)
                : NormalizeRange(_currentSelectionRange, plainText.Length);

            string selectionText = _currentSelectionRange is null
                ? (await GetSelectionTextAsync() ?? string.Empty)
                : await GetSelectionTextOrFallbackAsync(plainText, plainRange);
            if (string.IsNullOrWhiteSpace(selectionText))
            {
                return null;
            }

            SelectionDocRange docRange = await GetSelectionDocRangeAsync();
            if (docRange.To <= docRange.From)
            {
                return null;
            }

            if (_currentSelectionRange is null)
            {
                plainRange = new TextRange(0, selectionText.Length);
            }

            return new AiSelectionSnapshot(
                selectionText,
                plainRange,
                docRange.From,
                docRange.To,
                ComputeShortHash(selectionText));
        }

        private static AiSelectionSnapshot BuildFallbackSelectionSnapshot(string selectionText, TextRange plainRange)
        {
            int docFrom = Math.Max(0, plainRange.Start);
            int docTo = Math.Max(docFrom, plainRange.Start + plainRange.Length);
            return new AiSelectionSnapshot(
                selectionText,
                plainRange,
                docFrom,
                docTo,
                ComputeShortHash(selectionText));
        }

        private async Task<bool> ValidatePendingProposalSelectionAsync(PendingAiProposal pending)
        {
            PendingAiProposalContext? context = pending.Context;
            if (context is null || !context.RequiresSelection)
            {
                return true;
            }

            if (_activeSection is null || _activeSection.Id != context.SectionId)
            {
                ShowAiMessage("This proposal was created for a different section. Re-run the AI action.");
                await InvokeAsync(StateHasChanged);
                return false;
            }

            if (context.SelectionSnapshot is null)
            {
                ShowAiMessage("Selection context is missing. Re-run the AI action.");
                await InvokeAsync(StateHasChanged);
                return false;
            }

            SelectionDocRange currentRange = await GetSelectionDocRangeAsync();
            if (currentRange.From != context.SelectionSnapshot.DocFrom
                || currentRange.To != context.SelectionSnapshot.DocTo)
            {
                ShowAiMessage("Selection changed since the proposal was created. Re-run the AI action.");
                await InvokeAsync(StateHasChanged);
                return false;
            }

            string currentSelection = await GetSelectionTextAsync() ?? string.Empty;
            if (!string.Equals(
                    ComputeShortHash(currentSelection),
                    context.SelectionSnapshot.SelectionHash,
                    StringComparison.Ordinal))
            {
                ShowAiMessage("Selected text changed since the proposal was created. Re-run the AI action.");
                await InvokeAsync(StateHasChanged);
                return false;
            }

            return true;
        }

        private async Task<string> GetCurrentAiPlainTextAsync()
        {
            if (_pageEditor is not null)
            {
                string? plain = await _pageEditor.GetPlainTextAsync();
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    return plain;
                }
            }

            string? html = _pageEditor?.GetContent();
            return PlainTextMapper.ToPlainText(html ?? string.Empty);
        }

        private void LogAiSelectionDiagnostics(string actionKey, string? selectionText)
        {
            int length = selectionText?.Length ?? 0;
            string preview = string.IsNullOrWhiteSpace(selectionText)
                ? "<empty>"
                : selectionText.Trim().Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
            if (preview.Length > 50)
            {
                preview = preview.Substring(0, 50);
            }

            Logger.LogDebug(
                "AI action request prepared. Action={ActionKey}, SelectionLength={SelectionLength}, SelectionPreview={SelectionPreview}",
                actionKey,
                length,
                preview);
        }

        private static bool IsSectionScopeAction(string actionKey)
        {
            return actionKey.EndsWith(".section", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveActionScope(AiActionOption action)
        {
            if (action.Parameters.TryGetValue("scope", out object? rawScope))
            {
                string? scope = rawScope?.ToString();
                if (!string.IsNullOrWhiteSpace(scope))
                {
                    return scope.Trim().ToLowerInvariant();
                }
            }

            if (action.RequiresSelection)
            {
                return "selection";
            }

            return IsSectionScopeAction(action.ActionKey) ? "section" : "selection";
        }

        public static string ResolveAiApplyMode(string? scope, string actionKey, bool appendAtEnd = false)
        {
            if (appendAtEnd)
            {
                return "cursor";
            }

            string normalizedScope = (scope ?? string.Empty).Trim().ToLowerInvariant();
            if (string.Equals(normalizedScope, "section", StringComparison.Ordinal))
            {
                return "section";
            }

            if (string.Equals(normalizedScope, "selection", StringComparison.Ordinal))
            {
                return "selection";
            }

            if (string.Equals(normalizedScope, "cursor", StringComparison.Ordinal))
            {
                return "cursor";
            }

            return IsSectionScopeAction(actionKey) ? "section" : "selection";
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
            await SetContextTabAsync(ContextTab.Annotations);
        }

        private async Task OnAnnotationCardClickedAsync(PageAnnotationDto annotation)
        {
            if (_pageEditor is null)
            {
                return;
            }

            _annotationFocusedId = annotation.Id;
            _annotationActionError = null;

            bool scrolled = await _pageEditor.ScrollToAnnotationAsync(
                annotation.Id.ToString(),
                annotation.AnchorFrom,
                annotation.AnchorTo);

            if (!scrolled)
            {
                _annotationActionError = "Could not locate annotation in document.";
            }
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
            _diffViewMode = args.Value?.ToString() ?? "side";
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
                await FlushActiveEditorAsync("restore-version");

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

        private void SetHistoryFilter(HistoryFilter filter)
        {
            _historyFilter = filter;
        }

        private string GetHistoryFilterClass(HistoryFilter filter)
        {
            return _historyFilter == filter ? "is-active" : string.Empty;
        }

        private IReadOnlyList<CombinedHistoryItem> GetFilteredHistoryItems()
        {
            IEnumerable<CombinedHistoryItem> items = GetCombinedHistoryItems();
            return _historyFilter switch
            {
                HistoryFilter.Snapshots => items.Where(item => item.Type == HistoryItemType.Snapshot).ToList(),
                HistoryFilter.Commands => items.Where(item => item.Type == HistoryItemType.Command).ToList(),
                _ => items.ToList()
            };
        }

        private IEnumerable<CombinedHistoryItem> GetCombinedHistoryItems()
        {
            IEnumerable<CombinedHistoryItem> snapshots = _pageVersions.Select(version =>
                new CombinedHistoryItem(
                    HistoryItemType.Snapshot,
                    version.CreatedAt,
                    version,
                    null));

            IEnumerable<CombinedHistoryItem> commands = _aiHistoryEntries.Select(entry =>
                new CombinedHistoryItem(
                    HistoryItemType.Command,
                    entry.Timestamp,
                    null,
                    entry));

            return snapshots
                .Concat(commands)
                .OrderByDescending(item => item.Timestamp);
        }

        private static string GetCommandScopeLabel(string actionKey)
        {
            if (actionKey.EndsWith(".selection", StringComparison.OrdinalIgnoreCase)
                || actionKey.Contains("selection", StringComparison.OrdinalIgnoreCase))
            {
                return "Selection";
            }

            if (actionKey.EndsWith(".document", StringComparison.OrdinalIgnoreCase)
                || actionKey.Contains("document", StringComparison.OrdinalIgnoreCase))
            {
                return "Document";
            }

            return "Section";
        }

        private static string GetCommandStatusLabel(AiHistoryEntry entry)
        {
            return entry.Status switch
            {
                CommandHistoryStatus.Applied => "Applied",
                CommandHistoryStatus.Succeeded => "Succeeded",
                CommandHistoryStatus.Pending => "Pending",
                CommandHistoryStatus.Failed => "Failed",
                _ => "Pending"
            };
        }

        private static string GetCommandStatusClass(AiHistoryEntry entry)
        {
            return entry.Status switch
            {
                CommandHistoryStatus.Applied => "is-applied",
                CommandHistoryStatus.Succeeded => "is-succeeded",
                CommandHistoryStatus.Pending => "is-pending",
                CommandHistoryStatus.Failed => "is-failed",
                _ => "is-pending"
            };
        }

        private static CommandHistoryStatus ResolveHistoryStatus(AiActionHistoryEntryDto entry)
        {
            if (entry.IsApplied)
            {
                return CommandHistoryStatus.Applied;
            }

            return entry.Status switch
            {
                AiCommandStatusDto.Applied => CommandHistoryStatus.Applied,
                AiCommandStatusDto.Succeeded => CommandHistoryStatus.Succeeded,
                AiCommandStatusDto.Failed => CommandHistoryStatus.Failed,
                _ => CommandHistoryStatus.Pending
            };
        }

        private static string GetSnapshotDisplayLabel(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return "Version (Manual)";
            }

            return reason.Trim().ToLowerInvariant() switch
            {
                "pre-ai" => "Version (Pre-AI)",
                "autosave" => "Version (Autosave)",
                "autosnap" => "Version (Autosave)",
                "auto" => "Version (Autosave)",
                _ => "Version (Manual)"
            };
        }

        private static string GetVersionReasonLabel(string reason)
        {
            return GetSnapshotDisplayLabel(reason);
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

            return BuildVersionDisplayLabel(version.CreatedAt, version.Reason, version.WordCount);
        }

        private static string FormatVersionLabel(PageVersionListItemDto version)
        {
            return BuildVersionDisplayLabel(version.CreatedAt, version.Reason, version.WordCount);
        }

        private static string BuildVersionDisplayLabel(DateTimeOffset createdAt, string reason, int wordCount)
        {
            // Keep dropdown labels free from replacement/private-use glyphs.
            string label = $"{createdAt.ToLocalTime():g} • {GetVersionReasonLabel(reason)} • {wordCount} words";
            return QualityFixClientHelpers.SanitizeUiLabel(label, $"{createdAt.ToLocalTime():g} - Snapshot - {wordCount} words");
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
            return TranslationLanguages.GetDisplayLabel(languageCode);
        }

        private async Task OnAiUndoRequested()
        {
            if (!CanUseFeature(FeatureKey.AiUndoRedo))
            {
                NavigateToUpgradeForFeature(FeatureKey.AiUndoRedo);
                return;
            }

            if (_aiUndoRedoInFlight || _pageEditor is null || _activeSection is null || !_hasAiUndoHistory)
            {
                return;
            }

            _aiUndoRedoInFlight = true;
            try
            {
                await FlushActiveEditorAsync("ai-undo");

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
                await _pageEditor.SchedulePageBreakRefreshAsync();
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
            if (!CanUseFeature(FeatureKey.AiUndoRedo))
            {
                NavigateToUpgradeForFeature(FeatureKey.AiUndoRedo);
                return;
            }

            if (_aiUndoRedoInFlight || _pageEditor is null || _activeSection is null || !_hasAiRedoHistory)
            {
                return;
            }

            _aiUndoRedoInFlight = true;
            try
            {
                await FlushActiveEditorAsync("ai-redo");

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
                await _pageEditor.SchedulePageBreakRefreshAsync();
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

        private static string TrimLeadingEchoFromGeneratedParagraph(string generatedParagraph, string contextText)
        {
            string candidate = NormalizeSingleParagraph(generatedParagraph);
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(contextText))
            {
                return candidate;
            }

            string context = NormalizeSingleParagraph(contextText);
            const int minOverlap = 80;
            int maxOverlap = Math.Min(context.Length, candidate.Length);
            for (int overlap = maxOverlap; overlap >= minOverlap; overlap--)
            {
                if (!context.EndsWith(candidate.Substring(0, overlap), StringComparison.Ordinal))
                {
                    continue;
                }

                string trimmed = candidate.Substring(overlap).TrimStart();
                return string.IsNullOrWhiteSpace(trimmed) ? candidate : trimmed;
            }

            return candidate;
        }

        private static bool IsAppendOnlyCustomTransform(AiActionOption action)
        {
            if (!string.Equals(action.ActionKey, "custom_transform", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (action.Parameters.TryGetValue("recommendedToolId", out object? recommendedToolId)
                && string.Equals(recommendedToolId?.ToString(), "novel.continue_scene", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(action.Label, "Continue Scene", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedImageMimeType(string? mimeType)
        {
            return string.Equals(mimeType, "image/png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mimeType, "image/gif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mimeType, "image/webp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryNormalizeImageUrl(string input, out string? normalized)
        {
            normalized = null;
            string value = input.Trim();
            if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = value;
                return true;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            normalized = uri.ToString();
            return true;
        }

        private sealed record AiActionOption(
            string ActionKey,
            string Label,
            string Instruction,
            bool RequiresSelection,
            Dictionary<string, object?> Parameters,
            string? Description = null,
            bool IncludeInLists = true,
            bool IsRecommended = false,
            string? RecommendationBadge = null);

        private sealed record WritingToolPromptTemplate(
            string SystemTemplate,
            string UserTemplate);

        private sealed record WritingToolDefinition(
            string Id,
            string DisplayName,
            string Description,
            WritingToolPromptTemplate PromptTemplate,
            string Category,
            bool IsIntentRecommended);

        private sealed record AiHistoryEntry(
            Guid Id,
            string ActionKey,
            string Label,
            string? Summary,
            string? BeforeText,
            string? AfterText,
            DateTimeOffset Timestamp,
            bool IsApplied = false,
            CommandHistoryStatus Status = CommandHistoryStatus.Pending,
            DateTimeOffset? LastAppliedAt = null,
            int AppliedCount = 0);

        private enum CommandHistoryStatus
        {
            Pending = 0,
            Succeeded = 1,
            Applied = 2,
            Failed = 3
        }

        private sealed record CombinedHistoryItem(
            HistoryItemType Type,
            DateTimeOffset Timestamp,
            PageVersionListItemDto? Version,
            AiHistoryEntry? Command);

        private sealed record PendingAiProposal(
            Guid ProposalId,
            string ActionKey,
            string ActionLabel,
            string? OriginalText,
            string? ProposedText,
            string? ChangesSummary,
            string? ErrorMessage,
            DateTimeOffset CreatedUtc,
            PendingAiProposalContext? Context = null);

        private sealed record PendingAiProposalContext(
            bool RequiresSelection,
            Guid SectionId,
            Guid? PageId,
            AiSelectionSnapshot? SelectionSnapshot,
            string? Scope = null,
            bool AppendAtEnd = false,
            string? ContextText = null);

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
            string SelectionText,
            AiSelectionSnapshot? SelectionSnapshot = null);

        private sealed record AiSelectionSnapshot(
            string SelectionText,
            TextRange PlainRange,
            int DocFrom,
            int DocTo,
            string SelectionHash);

        private sealed record ImageUploadResponse(
            Guid ImageId,
            string Url,
            string ContentType,
            int SizeBytes,
            string DataUri);

        private sealed record RefreshBibleRequest(bool FullRebuild, Guid? ActiveSectionId);

        private sealed record BibleSnapshotDto(
            string BibleType,
            int SchemaVersion,
            string ContentJson,
            DateTimeOffset? LastRefreshUtc,
            int ChangedSectionsSinceLastRefresh,
            int ChangedSections,
            int NewSections,
            int DeletedSections,
            int NewEntries,
            int UpdatedEntries,
            int Flags);

        private sealed record SectionImportStatsDto(
            int Paragraphs,
            int Headings,
            int Lists,
            int Characters);

        private sealed record SectionImportResponseDto(
            string Html,
            SectionImportStatsDto Stats,
            List<string> Warnings,
            string Format,
            Guid TargetSectionId);

        private sealed record ExportPrintPayload(string Html);

        private sealed record FeedbackSubmitRequest(
            string Type,
            string Subject,
            string Description,
            bool IncludeDiagnostics,
            FeedbackDiagnosticsPayload? Diagnostics);

        private sealed record FeedbackDiagnosticsPayload(
            string? Url,
            string? Version,
            string? UserAgent);

        private sealed record TextRange(int Start, int Length);

        private sealed record ContinuityAnchor(int PlainTextStart, int PlainTextLength);

        private sealed record ContinuityEvidence(string? SectionId, string Quote);

        private sealed record ContinuityIssue(
            string Severity,
            string Type,
            string Message,
            ContinuityEvidence Evidence,
            string SuggestedFix,
            ContinuityAnchor Anchor);

        private sealed record ContinuityReport(string SchemaVersion, IReadOnlyList<ContinuityIssue> Issues);

        private sealed record ContinuityApplyRange(
            int PlainFrom,
            int PlainTo,
            int DocFrom,
            int DocTo,
            string Before,
            string Prefix,
            string Suffix,
            bool StartsSentence,
            bool EndsSentence,
            string Source);

        private sealed record RepeatedWordApplyRange(
            int PlainFrom,
            int PlainTo,
            int DocFrom,
            int DocTo,
            string Before,
            string Prefix,
            string Suffix,
            bool StartsSentence,
            bool EndsSentence);

        private sealed record ContextPanelStateStorage(string Category, string Tab);

        private sealed record PromptPresetDto(
            Guid Id,
            Guid? ProjectId,
            string Name,
            string? Category,
            string Kind,
            string? BuiltinActionId,
            string? TemplateText,
            Dictionary<string, object?> Parameters,
            DateTimeOffset CreatedUtc,
            DateTimeOffset UpdatedUtc);

        private sealed record UpsertPromptPresetRequest(
            Guid? ProjectId,
            string Name,
            string? Category,
            string Kind,
            string? BuiltinActionId,
            string? TemplateText,
            Dictionary<string, object?>? Parameters);

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

        private sealed record OnboardingWalkthroughTip(
            int ServerStep,
            string Title,
            string Description,
            string? TargetSelector,
            bool ShowAiAction);

        private enum CoachTipScope
        {
            WritingTools,
            Story,
            GenericWriting
        }

        private sealed record CoachTipCandidate(
            CoachTipScope Scope,
            CoachPrimaryAction PrimaryAction,
            string PrimaryActionLabel,
            string Why,
            int Priority);

        private sealed record QualityProposalPreview(
            string Before,
            string After,
            string Prefix,
            string Suffix);

        private sealed record ContinuityProposalPreview(
            string Before,
            string After,
            string Prefix,
            string Suffix,
            int PlainFrom,
            int Length,
            bool IsDeletion,
            string DeletionText);

        private sealed record DocumentPreviewSectionItem(
            string KindLabel,
            string Title,
            string ContentHtml);

        private enum ContextTab
        {
            Notes,
            Scene,
            Navigator,
            Ai,
            Continuity,
            PromptLibrary,
            Annotations,
            Quality,
            History
        }

        private enum PanelCategory
        {
            Coach,
            Story,
            Navigator,
            NotesTasks,
            History,
            Advanced
        }

        private enum HistoryItemType
        {
            Snapshot,
            Command
        }

        private enum HistoryFilter
        {
            All,
            Snapshots,
            Commands
        }
    }
}







