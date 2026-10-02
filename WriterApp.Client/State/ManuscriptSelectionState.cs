namespace WriterApp.Client.State;

public sealed class ManuscriptSelectionState
{
    public Guid? ProjectId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public void Select(Guid projectId, Guid? documentId) { ProjectId = projectId; DocumentId = documentId; }
    public Guid? ForProject(Guid projectId) => ProjectId == projectId ? DocumentId : null;
}
