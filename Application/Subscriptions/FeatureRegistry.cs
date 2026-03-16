using System;
using System.Collections.Generic;

namespace WriterApp.Application.Subscriptions
{
#if CLIENT_FEATURE_REGISTRY_INTERNAL
    internal
#else
    public
#endif
    enum FeatureKey
    {
        SignIn,
        ProtectedAppAccess,
        AccountPage,
        OnboardingIntent,
        GuidedEditorWalkthrough,
        StartPage,
        ContinueWriting,
        SmartWorkspaceRouting,
        ProjectCrud,
        WordCountAndStreak,
        DocumentCrud,
        ManageSections,
        ManagePages,
        ImportTxt,
        RichTextEditor,
        CollapsiblePanels,
        SelectionToolbar,
        Autosave,
        UpgradeFlow,
        StripeCheckout,
        UpgradePrompts,
        PlanQuotaHandling,
        FeedbackSubmission,
        ConvertDocumentToProject,
        ProjectNavigator,
        ProjectStructureEditing,
        OpenSceneInEditor,
        ProjectProgressDashboard,
        WritingGoals,
        Milestones,
        WritingSessionTracking,
        ReorderSections,
        ImportDocx,
        DocumentTranslation,
        FocusMode,
        InsertImages,
        ZoomPrintLayout,
        HeadingNumbering,
        DraftRecovery,
        SynopsisEditor,
        AiSynopsisEvaluation,
        SceneNotes,
        SceneCards,
        OutlineEditing,
        Annotations,
        AnnotationResolve,
        QualityChecks,
        Search,
        VersionHistory,
        RestoreVersion,
        RewriteSelection,
        TranslateText,
        NextParagraph,
        ExportDocument,
        ExportPreview,
        ExportFormats,
        SynopsisExport,
        BillingPortal,
        OutlineTemplates,
        AiGuidingQuestions,
        AiSynopsisSuggestions,
        StoryCanon,
        ContinuityCheck,
        CanonRefresh,
        AiActionHistory,
        AiUndoRedo,
        SceneAiSuggestions,
        StoryCoach,
        GenerateOutline,
        PromptLibrary,
        AdvancedReviseTools,
        ExportTemplates,
        ExportPresets
    }

#if CLIENT_FEATURE_REGISTRY_INTERNAL
    internal
#else
    public
#endif
    enum PlanTier
    {
        Free = 0,
        Standard = 1,
        Professional = 2
    }

#if CLIENT_FEATURE_REGISTRY_INTERNAL
    internal
#else
    public
#endif
    static class FeatureRegistry
    {
        public static readonly Dictionary<FeatureKey, PlanTier> FeatureMinimumTier = new()
        {
            [FeatureKey.SignIn] = PlanTier.Free,
            [FeatureKey.ProtectedAppAccess] = PlanTier.Free,
            [FeatureKey.AccountPage] = PlanTier.Free,
            [FeatureKey.OnboardingIntent] = PlanTier.Free,
            [FeatureKey.GuidedEditorWalkthrough] = PlanTier.Free,
            [FeatureKey.StartPage] = PlanTier.Free,
            [FeatureKey.ContinueWriting] = PlanTier.Free,
            [FeatureKey.SmartWorkspaceRouting] = PlanTier.Free,
            [FeatureKey.ProjectCrud] = PlanTier.Free,
            [FeatureKey.WordCountAndStreak] = PlanTier.Free,
            [FeatureKey.DocumentCrud] = PlanTier.Free,
            [FeatureKey.ManageSections] = PlanTier.Free,
            [FeatureKey.ManagePages] = PlanTier.Free,
            [FeatureKey.ImportTxt] = PlanTier.Free,
            [FeatureKey.RichTextEditor] = PlanTier.Free,
            [FeatureKey.CollapsiblePanels] = PlanTier.Free,
            [FeatureKey.SelectionToolbar] = PlanTier.Free,
            [FeatureKey.Autosave] = PlanTier.Free,
            [FeatureKey.UpgradeFlow] = PlanTier.Free,
            [FeatureKey.StripeCheckout] = PlanTier.Free,
            [FeatureKey.UpgradePrompts] = PlanTier.Free,
            [FeatureKey.PlanQuotaHandling] = PlanTier.Free,
            [FeatureKey.FeedbackSubmission] = PlanTier.Free,
            [FeatureKey.ConvertDocumentToProject] = PlanTier.Standard,
            [FeatureKey.ProjectNavigator] = PlanTier.Free,
            [FeatureKey.ProjectStructureEditing] = PlanTier.Free,
            [FeatureKey.OpenSceneInEditor] = PlanTier.Free,
            [FeatureKey.ProjectProgressDashboard] = PlanTier.Standard,
            [FeatureKey.WritingGoals] = PlanTier.Standard,
            [FeatureKey.Milestones] = PlanTier.Standard,
            [FeatureKey.WritingSessionTracking] = PlanTier.Standard,
            [FeatureKey.ReorderSections] = PlanTier.Standard,
            [FeatureKey.ImportDocx] = PlanTier.Standard,
            [FeatureKey.DocumentTranslation] = PlanTier.Standard,
            [FeatureKey.FocusMode] = PlanTier.Standard,
            [FeatureKey.InsertImages] = PlanTier.Standard,
            [FeatureKey.ZoomPrintLayout] = PlanTier.Standard,
            [FeatureKey.HeadingNumbering] = PlanTier.Standard,
            [FeatureKey.DraftRecovery] = PlanTier.Standard,
            [FeatureKey.SynopsisEditor] = PlanTier.Standard,
            [FeatureKey.AiSynopsisEvaluation] = PlanTier.Standard,
            [FeatureKey.SceneNotes] = PlanTier.Standard,
            [FeatureKey.SceneCards] = PlanTier.Standard,
            [FeatureKey.OutlineEditing] = PlanTier.Standard,
            [FeatureKey.Annotations] = PlanTier.Standard,
            [FeatureKey.AnnotationResolve] = PlanTier.Standard,
            [FeatureKey.QualityChecks] = PlanTier.Standard,
            [FeatureKey.Search] = PlanTier.Standard,
            [FeatureKey.VersionHistory] = PlanTier.Standard,
            [FeatureKey.RestoreVersion] = PlanTier.Standard,
            [FeatureKey.RewriteSelection] = PlanTier.Standard,
            [FeatureKey.TranslateText] = PlanTier.Standard,
            [FeatureKey.NextParagraph] = PlanTier.Standard,
            [FeatureKey.ExportDocument] = PlanTier.Standard,
            [FeatureKey.ExportPreview] = PlanTier.Standard,
            [FeatureKey.ExportFormats] = PlanTier.Standard,
            [FeatureKey.SynopsisExport] = PlanTier.Standard,
            [FeatureKey.BillingPortal] = PlanTier.Standard,
            [FeatureKey.OutlineTemplates] = PlanTier.Professional,
            [FeatureKey.AiGuidingQuestions] = PlanTier.Professional,
            [FeatureKey.AiSynopsisSuggestions] = PlanTier.Professional,
            [FeatureKey.StoryCanon] = PlanTier.Professional,
            [FeatureKey.ContinuityCheck] = PlanTier.Professional,
            [FeatureKey.CanonRefresh] = PlanTier.Professional,
            [FeatureKey.AiActionHistory] = PlanTier.Professional,
            [FeatureKey.AiUndoRedo] = PlanTier.Professional,
            [FeatureKey.SceneAiSuggestions] = PlanTier.Professional,
            [FeatureKey.StoryCoach] = PlanTier.Professional,
            [FeatureKey.GenerateOutline] = PlanTier.Professional,
            [FeatureKey.PromptLibrary] = PlanTier.Professional,
            [FeatureKey.AdvancedReviseTools] = PlanTier.Professional,
            [FeatureKey.ExportTemplates] = PlanTier.Professional,
            [FeatureKey.ExportPresets] = PlanTier.Professional
        };

        public static bool IsFeatureAllowed(FeatureKey feature, PlanTier userTier)
        {
            if (!FeatureMinimumTier.TryGetValue(feature, out PlanTier requiredTier))
            {
                throw new ArgumentOutOfRangeException(nameof(feature), feature, "Feature is not registered.");
            }

            return userTier >= requiredTier;
        }
    }
}
