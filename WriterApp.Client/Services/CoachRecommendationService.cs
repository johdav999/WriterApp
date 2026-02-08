using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WriterApp.Client.Services
{
    public sealed class CoachRecommendationService
    {
        private static readonly Regex PlaceholderTitlePattern = new(
            "^(untitled|new\\s+(part|chapter|scene)|part\\s*\\d+|chapter\\s*\\d+|scene\\s*\\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public CoachCardRecommendation BuildProjectRecommendation(ProjectCoachInput input)
        {
            List<string> observations = new();
            Dictionary<CoachPrimaryAction, int> actionScores = new();

            if (input.SceneCount == 0)
            {
                observations.Add("Your manuscript has no scenes yet.");
                AddScore(actionScores, CoachPrimaryAction.AddFirstScene, 120);
            }

            if (input.PlaceholderTitleCount > 0)
            {
                observations.Add($"{input.PlaceholderTitleCount} node title(s) are still placeholders.");
                AddScore(actionScores, CoachPrimaryAction.RenamePlaceholderNode, 95);
            }

            if (input.UnlinkedSceneCount > 0)
            {
                observations.Add($"{input.UnlinkedSceneCount} scene(s) are not linked to section text yet.");
                AddScore(actionScores, CoachPrimaryAction.OpenNextScene, 80);
            }

            if (input.SceneCount > 0 && input.DraftedSceneCount == 0)
            {
                observations.Add("Scenes are planned, but drafting has not started.");
                AddScore(actionScores, CoachPrimaryAction.OpenNextScene, 90);
            }

            if (input.GoalsEnabled && !input.IsProgressTab)
            {
                AddScore(actionScores, CoachPrimaryAction.ReviewProgress, 55);
            }

            if (observations.Count == 0)
            {
                observations.Add("Structure is in good shape and ready for drafting.");
                observations.Add("You can continue from your next scene.");
                AddScore(actionScores, CoachPrimaryAction.OpenNextScene, 100);
            }

            CoachPrimaryAction primary = ResolvePrimaryAction(actionScores, CoachPrimaryAction.OpenNextScene);
            string why = primary switch
            {
                CoachPrimaryAction.AddFirstScene => "A first scene turns outline planning into draft momentum.",
                CoachPrimaryAction.RenamePlaceholderNode => "Clear titles improve navigation and scene intent.",
                CoachPrimaryAction.ReviewProgress => "Progress review helps focus the next writing block.",
                _ => "Drafting the next scene is usually the highest-impact move."
            };

            return new CoachCardRecommendation(
                "Project coach",
                observations.Take(3).ToList(),
                GetActionLabel(primary),
                why,
                primary,
                input.PlaceholderNodeId,
                input.NextSceneNodeId);
        }

        public CoachCardRecommendation BuildSceneRecommendation(SceneCoachInput input)
        {
            List<string> observations = new();
            Dictionary<CoachPrimaryAction, int> actionScores = new();

            if (input.MissingSceneCardFields >= 3)
            {
                observations.Add("Scene card context is still sparse.");
                AddScore(actionScores, CoachPrimaryAction.SuggestSceneCardFromText, 115);
            }

            if (input.HasQualityIssues)
            {
                observations.Add("Quality checks flagged issues worth a quick pass.");
                AddScore(actionScores, CoachPrimaryAction.RunQualityCheck, 100);
            }
            else if (input.WordCount >= 160)
            {
                observations.Add("This scene is long enough for a quality pass.");
                AddScore(actionScores, CoachPrimaryAction.RunQualityCheck, 72);
            }

            if (input.HasContinuityIssues)
            {
                observations.Add("Continuity report found potential conflicts.");
                AddScore(actionScores, CoachPrimaryAction.RunContinuityCheck, 104);
            }
            else if (!input.HasContinuityReport && input.WordCount >= 180)
            {
                observations.Add("No continuity report yet for this scene.");
                AddScore(actionScores, CoachPrimaryAction.RunContinuityCheck, 70);
            }

            if (!input.HasOutlineNodes)
            {
                observations.Add("Outline nodes are missing for this document.");
                AddScore(actionScores, CoachPrimaryAction.OpenOutline, 76);
            }

            if (input.HasSelection)
            {
                observations.Add("You have active selection context for targeted edits.");
            }

            if (observations.Count == 0)
            {
                observations.Add("Scene is in a stable state.");
                observations.Add("Use the coach to sharpen the next revision pass.");
                AddScore(actionScores, CoachPrimaryAction.SuggestSceneCardFromText, 96);
            }

            CoachPrimaryAction primary = ResolvePrimaryAction(actionScores, CoachPrimaryAction.SuggestSceneCardFromText);
            string why = primary switch
            {
                CoachPrimaryAction.RunQualityCheck => "A quality pass catches readability and style friction early.",
                CoachPrimaryAction.RunContinuityCheck => "Continuity checks protect timeline and character consistency.",
                CoachPrimaryAction.OpenOutline => "Outline context helps ensure this scene supports the bigger arc.",
                _ => "A stronger scene card gives clearer intent before line edits."
            };

            return new CoachCardRecommendation(
                "Scene coach",
                observations.Take(3).ToList(),
                GetActionLabel(primary),
                why,
                primary,
                null,
                null);
        }

        public bool IsPlaceholderTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            return PlaceholderTitlePattern.IsMatch(title.Trim());
        }

        private static void AddScore(Dictionary<CoachPrimaryAction, int> scores, CoachPrimaryAction action, int points)
        {
            if (!scores.TryAdd(action, points))
            {
                scores[action] += points;
            }
        }

        private static CoachPrimaryAction ResolvePrimaryAction(
            Dictionary<CoachPrimaryAction, int> scores,
            CoachPrimaryAction fallback)
        {
            if (scores.Count == 0)
            {
                return fallback;
            }

            return scores
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .First()
                .Key;
        }

        private static string GetActionLabel(CoachPrimaryAction action)
        {
            return action switch
            {
                CoachPrimaryAction.AddFirstScene => "Create first scene",
                CoachPrimaryAction.RenamePlaceholderNode => "Rename placeholder",
                CoachPrimaryAction.ReviewProgress => "Review progress",
                CoachPrimaryAction.OpenNextScene => "Continue in next scene",
                CoachPrimaryAction.SuggestSceneCardFromText => "Suggest scene card from text",
                CoachPrimaryAction.RunQualityCheck => "Run quality check",
                CoachPrimaryAction.RunContinuityCheck => "Run continuity check",
                CoachPrimaryAction.OpenOutline => "Open outline",
                _ => "Continue"
            };
        }
    }

    public sealed record ProjectCoachInput(
        int PartCount,
        int ChapterCount,
        int SceneCount,
        int DraftedSceneCount,
        int PlaceholderTitleCount,
        int UnlinkedSceneCount,
        bool GoalsEnabled,
        bool IsProgressTab,
        Guid? PlaceholderNodeId,
        Guid? NextSceneNodeId);

    public sealed record SceneCoachInput(
        bool HasSelection,
        int MissingSceneCardFields,
        bool HasQualityIssues,
        bool HasContinuityReport,
        bool HasContinuityIssues,
        bool HasOutlineNodes,
        int WordCount);

    public sealed record CoachCardRecommendation(
        string ContextTitle,
        IReadOnlyList<string> Observations,
        string PrimaryActionLabel,
        string Why,
        CoachPrimaryAction PrimaryAction,
        Guid? TargetNodeId,
        Guid? NextSceneNodeId);

    public enum CoachPrimaryAction
    {
        None = 0,
        AddFirstScene = 1,
        RenamePlaceholderNode = 2,
        ReviewProgress = 3,
        OpenNextScene = 4,
        SuggestSceneCardFromText = 5,
        RunQualityCheck = 6,
        RunContinuityCheck = 7,
        OpenOutline = 8
    }
}
