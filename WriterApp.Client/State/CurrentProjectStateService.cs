using System;
using System.Collections.Generic;

namespace WriterApp.Client.State
{
    public sealed class CurrentProjectStateService
    {
        private readonly Dictionary<Guid, string> _titlesByProjectId = new();
        private Guid? _projectId;
        private string? _projectTitle;

        public Guid? ProjectId => _projectId;
        public string? ProjectTitle => _projectTitle;

        public event Action? Changed;

        public void SetCurrent(Guid projectId, string? projectTitle = null)
        {
            if (projectId == Guid.Empty)
            {
                Clear();
                return;
            }

            string? normalized = NormalizeTitle(projectTitle);
            if (normalized is not null)
            {
                _titlesByProjectId[projectId] = normalized;
            }

            if (normalized is null && _titlesByProjectId.TryGetValue(projectId, out string? cached))
            {
                normalized = cached;
            }

            if (_projectId == projectId && string.Equals(_projectTitle, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _projectId = projectId;
            _projectTitle = normalized;
            Changed?.Invoke();
        }

        public void CacheProject(Guid projectId, string? projectTitle)
        {
            if (projectId == Guid.Empty)
            {
                return;
            }

            string? normalized = NormalizeTitle(projectTitle);
            if (normalized is null)
            {
                return;
            }

            bool changed = !_titlesByProjectId.TryGetValue(projectId, out string? current)
                || !string.Equals(current, normalized, StringComparison.Ordinal);
            _titlesByProjectId[projectId] = normalized;

            if (!changed || _projectId != projectId || string.Equals(_projectTitle, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _projectTitle = normalized;
            Changed?.Invoke();
        }

        public bool TryGetCachedTitle(Guid projectId, out string title)
        {
            return _titlesByProjectId.TryGetValue(projectId, out title!);
        }

        public void Clear()
        {
            if (_projectId is null && _projectTitle is null)
            {
                return;
            }

            _projectId = null;
            _projectTitle = null;
            Changed?.Invoke();
        }

        private static string? NormalizeTitle(string? title)
        {
            return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }
    }
}
