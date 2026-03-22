using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;

namespace WriterApp.Client.State
{
    public sealed class ProjectStructureCacheService
    {
        private readonly ILogger<ProjectStructureCacheService> _logger;
        private List<ProjectDto>? _projects;
        private readonly Dictionary<Guid, ProjectTreeDto> _treesByProjectId = new();

        public ProjectStructureCacheService(ILogger<ProjectStructureCacheService> logger)
        {
            _logger = logger;
        }

        public bool TryGetProjects(out IReadOnlyList<ProjectDto> projects)
        {
            if (_projects is null)
            {
                _logger.LogDebug("ProjectStructureCache miss Scope=Projects");
                projects = Array.Empty<ProjectDto>();
                return false;
            }

            _logger.LogDebug("ProjectStructureCache hit Scope=Projects Count={Count}", _projects.Count);
            projects = _projects.ToArray();
            return true;
        }

        public void SetProjects(IEnumerable<ProjectDto> projects)
        {
            _projects = (projects ?? Array.Empty<ProjectDto>()).ToList();
            _logger.LogDebug("ProjectStructureCache stored Scope=Projects Count={Count}", _projects.Count);
        }

        public bool TryGetProject(Guid projectId, out ProjectDto project)
        {
            if (projectId != Guid.Empty && _treesByProjectId.TryGetValue(projectId, out ProjectTreeDto? cachedTree))
            {
                project = cachedTree.Project;
                _logger.LogDebug("ProjectStructureCache hit Scope=Project ProjectId={ProjectId} Source=Tree", projectId);
                return true;
            }

            if (_projects is not null)
            {
                ProjectDto? cachedProject = _projects.FirstOrDefault(item => item.Id == projectId);
                if (cachedProject is not null)
                {
                    project = cachedProject;
                    _logger.LogDebug("ProjectStructureCache hit Scope=Project ProjectId={ProjectId} Source=Projects", projectId);
                    return true;
                }
            }

            _logger.LogDebug("ProjectStructureCache miss Scope=Project ProjectId={ProjectId}", projectId);
            project = default!;
            return false;
        }

        public void SetProject(ProjectDto project)
        {
            if (project.Id == Guid.Empty)
            {
                return;
            }

            if (_projects is null)
            {
                _projects = new List<ProjectDto> { project };
                _logger.LogDebug("ProjectStructureCache stored Scope=Project ProjectId={ProjectId} Source=Direct", project.Id);
                return;
            }

            int index = _projects.FindIndex(item => item.Id == project.Id);
            if (index >= 0)
            {
                _projects[index] = project;
            }
            else
            {
                _projects.Add(project);
            }

            _logger.LogDebug("ProjectStructureCache stored Scope=Project ProjectId={ProjectId} Source=Direct", project.Id);
        }

        public bool TryGetProjectTree(Guid projectId, out ProjectTreeDto tree)
        {
            if (_treesByProjectId.TryGetValue(projectId, out ProjectTreeDto? cached))
            {
                _logger.LogDebug("ProjectStructureCache hit Scope=Tree ProjectId={ProjectId} NodeCount={NodeCount}", projectId, cached.Nodes.Count);
                tree = Clone(cached);
                return true;
            }

            _logger.LogDebug("ProjectStructureCache miss Scope=Tree ProjectId={ProjectId}", projectId);
            tree = default!;
            return false;
        }

        public void SetProjectTree(ProjectTreeDto tree)
        {
            if (tree.Project.Id == Guid.Empty)
            {
                return;
            }

            ProjectTreeDto clone = Clone(tree);
            _treesByProjectId[tree.Project.Id] = clone;
            _logger.LogDebug(
                "ProjectStructureCache stored Scope=Tree ProjectId={ProjectId} NodeCount={NodeCount}",
                tree.Project.Id,
                tree.Nodes.Count);

            if (_projects is null)
            {
                return;
            }

            int index = _projects.FindIndex(project => project.Id == tree.Project.Id);
            if (index >= 0)
            {
                _projects[index] = tree.Project;
            }
            else
            {
                _projects.Add(tree.Project);
            }
        }

        public void InvalidateProjectTree(Guid projectId, string reason = "unspecified")
        {
            if (projectId == Guid.Empty)
            {
                return;
            }

            bool removed = _treesByProjectId.Remove(projectId);
            _logger.LogDebug(
                "ProjectStructureCache invalidated Scope=Tree ProjectId={ProjectId} Reason={Reason} Removed={Removed}",
                projectId,
                reason,
                removed);
        }

        public void InvalidateProjects(string reason = "unspecified")
        {
            _projects = null;
            _logger.LogDebug("ProjectStructureCache invalidated Scope=Projects Reason={Reason}", reason);
        }

        public void InvalidateProject(Guid projectId, string reason = "unspecified")
        {
            InvalidateProjectTree(projectId, reason);
            InvalidateProjects(reason);
        }

        public void Clear()
        {
            _projects = null;
            _treesByProjectId.Clear();
            _logger.LogDebug("ProjectStructureCache invalidated Scope=All Reason=clear");
        }

        private static ProjectTreeDto Clone(ProjectTreeDto tree)
        {
            return new ProjectTreeDto(tree.Project, tree.Nodes.ToArray());
        }
    }
}
