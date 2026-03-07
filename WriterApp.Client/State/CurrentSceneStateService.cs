using System;

namespace WriterApp.Client.State
{
    public sealed class CurrentSceneStateService
    {
        private Guid? _projectId;
        private Guid? _sceneNodeId;
        private string? _sceneTitle;

        public Guid? ProjectId => _projectId;
        public Guid? SceneNodeId => _sceneNodeId;
        public string? SceneTitle => _sceneTitle;

        public event Action? Changed;

        public void SetCurrent(Guid projectId, Guid sceneNodeId, string? sceneTitle = null)
        {
            if (projectId == Guid.Empty || sceneNodeId == Guid.Empty)
            {
                Clear();
                return;
            }

            bool isSameScene = _projectId == projectId && _sceneNodeId == sceneNodeId;
            string? nextTitle = string.IsNullOrWhiteSpace(sceneTitle)
                ? (isSameScene ? _sceneTitle : null)
                : sceneTitle.Trim();
            SetState(projectId, sceneNodeId, nextTitle);
        }

        public void SetTitle(string? sceneTitle)
        {
            if (!_projectId.HasValue || !_sceneNodeId.HasValue)
            {
                return;
            }

            string? nextTitle = string.IsNullOrWhiteSpace(sceneTitle) ? null : sceneTitle.Trim();
            SetState(_projectId, _sceneNodeId, nextTitle);
        }

        public void Clear()
        {
            SetState(null, null, null);
        }

        private void SetState(Guid? projectId, Guid? sceneNodeId, string? sceneTitle)
        {
            if (_projectId == projectId
                && _sceneNodeId == sceneNodeId
                && string.Equals(_sceneTitle, sceneTitle, StringComparison.Ordinal))
            {
                return;
            }

            _projectId = projectId;
            _sceneNodeId = sceneNodeId;
            _sceneTitle = sceneTitle;
            Changed?.Invoke();
        }
    }
}
