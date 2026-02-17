using System;

namespace WriterApp.Application.Documents
{
    public static class SceneRouteBuilder
    {
        public static string BuildRelativeSceneEditorPath(Guid projectId, Guid sceneNodeId)
        {
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("Project id is required.", nameof(projectId));
            }

            if (sceneNodeId == Guid.Empty)
            {
                throw new ArgumentException("Scene id is required.", nameof(sceneNodeId));
            }

            return $"projects/{projectId}/scenes/{sceneNodeId}";
        }
    }
}
