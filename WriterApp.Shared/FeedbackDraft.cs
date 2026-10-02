namespace WriterApp.Application.Feedback;

public sealed record FeedbackDraft(string Type, string Subject, string Description)
{
    public void Validate()
    {
        if (Type is not ("bug" or "enhancement") || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 120
            || string.IsNullOrWhiteSpace(Description) || Description.Length > 8000)
            throw new InvalidOperationException("Choose a feedback type, a title up to 120 characters and a description up to 8,000 characters.");
    }
    public bool IncludeDiagnostics => false;
    public object? Diagnostics => null;
}
