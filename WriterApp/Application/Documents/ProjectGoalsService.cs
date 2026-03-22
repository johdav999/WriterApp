using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed class ProjectGoalsService : IProjectGoalsService
    {
        private static readonly Regex WordRegex = new(@"\b[\p{L}\p{N}']+\b", RegexOptions.Compiled);
        private readonly AppDbContext _dbContext;
        private readonly IProjectWordCountService _projectWordCountService;
        private readonly IConfiguration _configuration;

        public ProjectGoalsService(
            AppDbContext dbContext,
            IProjectWordCountService projectWordCountService,
            IConfiguration configuration)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _projectWordCountService = projectWordCountService ?? throw new ArgumentNullException(nameof(projectWordCountService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<ProjectGoalDto?> UpsertGoalAsync(
            string ownerUserId,
            Guid projectId,
            ProjectGoalUpdateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return null;
            }

            ProjectRecord? project = await GetOwnedProjectAsync(ownerUserId, projectId, tracked: true, ct);
            if (project is null)
            {
                return null;
            }

            ProjectGoalRecord? goal = await _dbContext.ProjectGoals
                .FirstOrDefaultAsync(item => item.ProjectId == projectId, ct);

            if (goal is null)
            {
                goal = new ProjectGoalRecord
                {
                    ProjectId = projectId
                };
                _dbContext.ProjectGoals.Add(goal);
            }

            goal.DailyTargetWords = Math.Max(0, request.DailyTargetWords);
            goal.WeeklyTargetWords = Math.Max(0, request.WeeklyTargetWords);
            goal.Timezone = NormalizeTimezone(request.Timezone);
            goal.UpdatedUtc = DateTimeOffset.UtcNow;
            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return ToGoalDto(goal);
        }

        public async Task<ProjectProgressDashboardDto?> GetDashboardAsync(
            string ownerUserId,
            Guid projectId,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return null;
            }

            ProjectRecord? project = await GetOwnedProjectAsync(ownerUserId, projectId, tracked: false, ct);
            if (project is null)
            {
                return null;
            }

            await _projectWordCountService.EnsureProjectCurrentAsync(projectId, ct);
            int totalWords = await GetProjectTotalWordsAsync(projectId, ct);
            bool milestonesChanged = await UpdateMilestoneCompletionAsync(projectId, totalWords, ct);
            if (milestonesChanged)
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            ProjectGoalRecord? goalRecord = await _dbContext.ProjectGoals
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ProjectId == projectId, ct);
            ProjectGoalDto goal = goalRecord is null
                ? new ProjectGoalDto(projectId, 0, 0, "UTC", DateTimeOffset.UtcNow)
                : ToGoalDto(goalRecord);

            TimeZoneInfo timeZone = ResolveTimeZone(goal.Timezone);
            DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
            DateOnly weekStart = today.AddDays(-6);
            string weekStartText = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string todayText = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            List<ProjectProgressDailyRecord> weekRows = (await _dbContext.ProjectProgressDaily
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId)
                .ToListAsync(ct))
                .Where(item => string.CompareOrdinal(item.Date, weekStartText) >= 0 && string.CompareOrdinal(item.Date, todayText) <= 0)
                .ToList();

            Dictionary<string, int> dayLookup = weekRows.ToDictionary(item => item.Date, item => item.WordsDelta);
            int todayWords = dayLookup.TryGetValue(todayText, out int todayValue) ? todayValue : 0;
            int thisWeekWords = weekRows.Sum(item => item.WordsDelta);
            int streak = await ComputeStreakAsync(projectId, today, goal.DailyTargetWords, ct);

            List<ProjectMilestoneRecord> milestoneRecords = await _dbContext.ProjectMilestones
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId)
                .ToListAsync(ct);
            milestoneRecords = milestoneRecords
                .OrderBy(item => item.Status)
                .ThenBy(item => item.UpdatedUtc)
                .ToList();
            List<ProjectMilestoneDto> milestones = milestoneRecords.Select(ToMilestoneDto).ToList();

            WritingSessionRecord? activeSession = await _dbContext.WritingSessions
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId && item.EndedUtc == null)
                .OrderByDescending(item => item.StartedUtc)
                .FirstOrDefaultAsync(ct);
            WritingSessionDto? active = activeSession is null ? null : ToSessionDto(activeSession, totalWords);

            List<WritingSessionRecord> recentRecords = await _dbContext.WritingSessions
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId)
                .OrderByDescending(item => item.StartedUtc)
                .Take(20)
                .ToListAsync(ct);
            List<WritingSessionDto> recent = recentRecords.Select(item => ToSessionDto(item, totalWords)).ToList();

            return new ProjectProgressDashboardDto(
                projectId,
                goal,
                todayWords,
                thisWeekWords,
                streak,
                totalWords,
                milestones,
                active,
                recent);
        }

        public async Task<ProjectMilestoneDto?> CreateMilestoneAsync(
            string ownerUserId,
            Guid projectId,
            ProjectMilestoneCreateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return null;
            }

            ProjectRecord? project = await GetOwnedProjectAsync(ownerUserId, projectId, tracked: true, ct);
            if (project is null)
            {
                return null;
            }

            string title = string.IsNullOrWhiteSpace(request.Title) ? "Milestone" : request.Title.Trim();
            DateTime now = DateTime.UtcNow;
            ProjectMilestoneRecord milestone = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = title,
                TargetWords = request.TargetWords is null ? null : Math.Max(0, request.TargetWords.Value),
                TargetNodeId = request.TargetNodeId,
                Status = ProjectMilestoneStatus.Pending,
                UpdatedUtc = now
            };

            _dbContext.ProjectMilestones.Add(milestone);
            project.UpdatedUtc = now;
            await _dbContext.SaveChangesAsync(ct);

            int totalWords = await GetProjectTotalWordsAsync(projectId, ct);
            bool changed = await UpdateMilestoneCompletionAsync(projectId, totalWords, ct);
            if (changed)
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            ProjectMilestoneRecord refreshed = await _dbContext.ProjectMilestones
                .AsNoTracking()
                .FirstAsync(item => item.Id == milestone.Id, ct);
            return ToMilestoneDto(refreshed);
        }

        public async Task<ProjectMilestoneDto?> UpdateMilestoneAsync(
            string ownerUserId,
            Guid projectId,
            Guid milestoneId,
            ProjectMilestoneUpdateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return null;
            }

            ProjectRecord? project = await GetOwnedProjectAsync(ownerUserId, projectId, tracked: true, ct);
            if (project is null)
            {
                return null;
            }

            ProjectMilestoneRecord? milestone = await _dbContext.ProjectMilestones
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == milestoneId, ct);
            if (milestone is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                milestone.Title = request.Title.Trim();
            }

            milestone.TargetWords = request.TargetWords is null ? null : Math.Max(0, request.TargetWords.Value);
            milestone.TargetNodeId = request.TargetNodeId;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                ProjectMilestoneStatus next = ParseMilestoneStatus(request.Status);
                milestone.Status = next;
                milestone.CompletedUtc = next == ProjectMilestoneStatus.Completed ? DateTimeOffset.UtcNow : null;
            }

            milestone.UpdatedUtc = DateTimeOffset.UtcNow;
            project.UpdatedUtc = milestone.UpdatedUtc;
            await _dbContext.SaveChangesAsync(ct);

            int totalWords = await GetProjectTotalWordsAsync(projectId, ct);
            bool changed = await UpdateMilestoneCompletionAsync(projectId, totalWords, ct);
            if (changed)
            {
                await _dbContext.SaveChangesAsync(ct);
            }

            ProjectMilestoneRecord refreshed = await _dbContext.ProjectMilestones
                .AsNoTracking()
                .FirstAsync(item => item.Id == milestone.Id, ct);
            return ToMilestoneDto(refreshed);
        }

        public async Task<bool> DeleteMilestoneAsync(
            string ownerUserId,
            Guid projectId,
            Guid milestoneId,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return false;
            }

            ProjectRecord? project = await GetOwnedProjectAsync(ownerUserId, projectId, tracked: true, ct);
            if (project is null)
            {
                return false;
            }

            ProjectMilestoneRecord? milestone = await _dbContext.ProjectMilestones
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == milestoneId, ct);
            if (milestone is null)
            {
                return false;
            }

            _dbContext.ProjectMilestones.Remove(milestone);
            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }

        public async Task<WritingSessionDto?> StartSessionAsync(
            string ownerUserId,
            Guid projectId,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return null;
            }

            ProjectRecord? project = await GetOwnedProjectAsync(ownerUserId, projectId, tracked: true, ct);
            if (project is null)
            {
                return null;
            }

            WritingSessionRecord? active = await _dbContext.WritingSessions
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.EndedUtc == null, ct);
            if (active is not null)
            {
                int currentTotal = await GetProjectTotalWordsAsync(projectId, ct);
                return ToSessionDto(active, currentTotal);
            }

            await _projectWordCountService.RefreshProjectAsync(projectId, ct);
            int totalWords = await GetProjectTotalWordsAsync(projectId, ct);
            DateTime now = DateTime.UtcNow;

            WritingSessionRecord session = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                StartedUtc = now,
                EndedUtc = null,
                DurationSeconds = 0,
                WordsDelta = 0,
                StartWordCount = totalWords
            };

            _dbContext.WritingSessions.Add(session);
            project.UpdatedUtc = new DateTimeOffset(now, TimeSpan.Zero);
            await _dbContext.SaveChangesAsync(ct);

            return ToSessionDto(session, totalWords);
        }

        public async Task<WritingSessionDto?> StopSessionAsync(
            string ownerUserId,
            Guid projectId,
            Guid sessionId,
            string? notes,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return null;
            }

            bool ownsProject = await _dbContext.Projects
                .AnyAsync(project => project.Id == projectId && project.OwnerUserId == ownerUserId, ct);
            if (!ownsProject)
            {
                return null;
            }

            WritingSessionRecord? session = await _dbContext.WritingSessions
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == sessionId, ct);
            if (session is null)
            {
                return null;
            }

            if (session.EndedUtc.HasValue)
            {
                int currentTotal = await GetProjectTotalWordsAsync(projectId, ct);
                return ToSessionDto(session, currentTotal);
            }

            await _projectWordCountService.RefreshProjectAsync(projectId, ct);
            int totalWords = await GetProjectTotalWordsAsync(projectId, ct);
            DateTime ended = DateTime.UtcNow;
            session.EndedUtc = ended;
            session.DurationSeconds = Math.Max(0, (int)(ended - session.StartedUtc).TotalSeconds);
            session.WordsDelta = totalWords - session.StartWordCount;
            session.Notes = Normalize(notes);

            ProjectRecord? project = await _dbContext.Projects.FirstOrDefaultAsync(item => item.Id == projectId, ct);
            if (project is not null)
            {
                project.UpdatedUtc = new DateTimeOffset(ended, TimeSpan.Zero);
            }

            await _dbContext.SaveChangesAsync(ct);
            return ToSessionDto(session, totalWords);
        }

        public async Task TrackPageDeltaAsync(
            PageRecord? beforePage,
            PageRecord? afterPage,
            string eventKey,
            CancellationToken ct)
        {
            if (!IsEnabled() || string.IsNullOrWhiteSpace(eventKey))
            {
                return;
            }

            Guid? sectionId = afterPage?.SectionId ?? beforePage?.SectionId;
            if (!sectionId.HasValue)
            {
                return;
            }

            int beforeWords = CountWords(beforePage?.Content);
            int afterWords = CountWords(afterPage?.Content);
            int delta = afterWords - beforeWords;
            if (delta == 0)
            {
                return;
            }

            List<Guid> projectIds = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.LinkedSectionId == sectionId.Value)
                .Select(node => node.ProjectId)
                .Distinct()
                .ToListAsync(ct);
            if (projectIds.Count == 0)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Dictionary<Guid, string> timezoneByProject = await _dbContext.ProjectGoals
                .AsNoTracking()
                .Where(goal => projectIds.Contains(goal.ProjectId))
                .ToDictionaryAsync(item => item.ProjectId, item => item.Timezone, ct);

            foreach (Guid projectId in projectIds)
            {
                bool alreadyApplied = await _dbContext.ProjectProgressEvents
                    .AnyAsync(item => item.ProjectId == projectId && item.EventKey == eventKey, ct);
                if (alreadyApplied)
                {
                    continue;
                }

                string timezone = timezoneByProject.TryGetValue(projectId, out string? tz) ? tz : "UTC";
                DateOnly localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, ResolveTimeZone(timezone)).DateTime);
                string day = localDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                _dbContext.ProjectProgressEvents.Add(new ProjectProgressEventRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    EventKey = eventKey,
                    Date = day,
                    WordsDelta = delta,
                    CreatedUtc = now
                });

                ProjectProgressDailyRecord? daily = await _dbContext.ProjectProgressDaily
                    .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Date == day, ct);
                if (daily is null)
                {
                    daily = new ProjectProgressDailyRecord
                    {
                        ProjectId = projectId,
                        Date = day,
                        WordsDelta = delta,
                        UpdatedUtc = now
                    };
                    _dbContext.ProjectProgressDaily.Add(daily);
                }
                else
                {
                    daily.WordsDelta += delta;
                    daily.UpdatedUtc = now;
                }

                int totalWords = await GetProjectTotalWordsAsync(projectId, ct);
                await UpdateMilestoneCompletionAsync(projectId, totalWords, ct);
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        private async Task<int> ComputeStreakAsync(Guid projectId, DateOnly today, int dailyTargetWords, CancellationToken ct)
        {
            if (dailyTargetWords <= 0)
            {
                return 0;
            }

            string maxDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            List<ProjectProgressDailyRecord> rows = await _dbContext.ProjectProgressDaily
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId)
                .ToListAsync(ct);
            Dictionary<string, int> lookup = rows
                .Where(item => string.CompareOrdinal(item.Date, maxDate) <= 0)
                .ToDictionary(item => item.Date, item => item.WordsDelta);

            int streak = 0;
            DateOnly cursor = today;
            while (true)
            {
                string key = cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!lookup.TryGetValue(key, out int words) || words < dailyTargetWords)
                {
                    break;
                }

                streak++;
                cursor = cursor.AddDays(-1);
            }

            return streak;
        }

        private async Task<bool> UpdateMilestoneCompletionAsync(Guid projectId, int totalWords, CancellationToken ct)
        {
            List<ProjectMilestoneRecord> milestones = await _dbContext.ProjectMilestones
                .Where(item => item.ProjectId == projectId)
                .ToListAsync(ct);

            Guid[] targetNodeIds = milestones
                .Where(item => item.TargetNodeId.HasValue)
                .Select(item => item.TargetNodeId!.Value)
                .Distinct()
                .ToArray();

            Dictionary<Guid, int> nodeWords = targetNodeIds.Length == 0
                ? new Dictionary<Guid, int>()
                : await _dbContext.ProjectNodes
                    .AsNoTracking()
                    .Where(item => item.ProjectId == projectId && targetNodeIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, item => item.WordCountCache, ct);

            bool changed = false;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (ProjectMilestoneRecord milestone in milestones)
            {
                bool shouldComplete = false;
                if (milestone.TargetWords.HasValue && totalWords >= milestone.TargetWords.Value)
                {
                    shouldComplete = true;
                }

                if (!shouldComplete && milestone.TargetNodeId.HasValue && nodeWords.TryGetValue(milestone.TargetNodeId.Value, out int nodeCount))
                {
                    shouldComplete = nodeCount > 0;
                }

                if (shouldComplete && milestone.Status != ProjectMilestoneStatus.Completed)
                {
                    milestone.Status = ProjectMilestoneStatus.Completed;
                    milestone.CompletedUtc = now;
                    milestone.UpdatedUtc = now;
                    changed = true;
                }
            }

            return changed;
        }

        private async Task<ProjectRecord?> GetOwnedProjectAsync(
            string ownerUserId,
            Guid projectId,
            bool tracked,
            CancellationToken ct)
        {
            IQueryable<ProjectRecord> query = tracked
                ? _dbContext.Projects
                : _dbContext.Projects.AsNoTracking();
            return await query.FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == ownerUserId, ct);
        }

        private async Task<int> GetProjectTotalWordsAsync(Guid projectId, CancellationToken ct)
        {
            return await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.ProjectId == projectId && node.ParentId == null)
                .SumAsync(node => node.WordCountCache, ct);
        }

        private static ProjectMilestoneStatus ParseMilestoneStatus(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "completed" => ProjectMilestoneStatus.Completed,
                _ => ProjectMilestoneStatus.Pending
            };
        }

        private static ProjectGoalDto ToGoalDto(ProjectGoalRecord record)
        {
            return new ProjectGoalDto(
                record.ProjectId,
                record.DailyTargetWords,
                record.WeeklyTargetWords,
                record.Timezone,
                record.UpdatedUtc);
        }

        private static ProjectMilestoneDto ToMilestoneDto(ProjectMilestoneRecord record)
        {
            return new ProjectMilestoneDto(
                record.Id,
                record.ProjectId,
                record.Title,
                record.TargetWords,
                record.TargetNodeId,
                record.Status == ProjectMilestoneStatus.Completed ? "completed" : "pending",
                record.CompletedUtc,
                record.UpdatedUtc);
        }

        private static WritingSessionDto ToSessionDto(WritingSessionRecord record, int currentTotalWords)
        {
            int wordsDelta = record.EndedUtc.HasValue ? record.WordsDelta : currentTotalWords - record.StartWordCount;
            int duration = record.EndedUtc.HasValue
                ? record.DurationSeconds
                : Math.Max(0, (int)(DateTime.UtcNow - record.StartedUtc).TotalSeconds);
            DateTimeOffset startedUtc = new(record.StartedUtc, TimeSpan.Zero);
            DateTimeOffset? endedUtc = record.EndedUtc.HasValue
                ? new DateTimeOffset(record.EndedUtc.Value, TimeSpan.Zero)
                : null;

            return new WritingSessionDto(
                record.Id,
                record.ProjectId,
                startedUtc,
                endedUtc,
                duration,
                wordsDelta,
                record.Notes,
                !record.EndedUtc.HasValue);
        }

        private static int CountWords(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return 0;
            }

            string plain = PlainTextMapper.ToPlainText(html);
            return WordRegex.Matches(plain).Count;
        }

        private static TimeZoneInfo ResolveTimeZone(string? timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
            {
                return TimeZoneInfo.Utc;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }

        private static string NormalizeTimezone(string? timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
            {
                return "UTC";
            }

            TimeZoneInfo resolved = ResolveTimeZone(timezone.Trim());
            return resolved.Id;
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private bool IsEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:GoalsEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:GoalsEnabled")
                ?? false;
        }
    }
}
