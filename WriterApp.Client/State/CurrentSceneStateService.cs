using System;

namespace WriterApp.Client.State
{
    public sealed class CurrentSceneStateService
    {
        private Guid? _projectId;
        private Guid? _sceneNodeId;

        public Guid? ProjectId => _projectId;
        public Guid? SceneNodeId => _sceneNodeId;

        public event Action? Changed;

        public void SetCurrent(Guid projectId, Guid sceneNodeId)
        {
            if (projectId == Guid.Empty || sceneNodeId == Guid.Empty)
            {
                Clear();
                return;
            }

            SetState(projectId, sceneNodeId);
        }

        public void Clear()
        {
            SetState(null, null);
        }

        private void SetState(Guid? projectId, Guid? sceneNodeId)
        {
            if (_projectId == projectId && _sceneNodeId == sceneNodeId)
            {
                return;
            }

            _projectId = projectId;
            _sceneNodeId = sceneNodeId;
            Changed?.Invoke();
        }
    }
}
