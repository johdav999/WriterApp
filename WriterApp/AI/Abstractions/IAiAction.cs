namespace WriterApp.AI.Abstractions
{
    public interface IAiAction
    {
        string ActionId { get; }
        string DisplayName { get; }
        AiModality[] Modalities { get; }
        bool RequiresSelection { get; }
        AiRequest BuildRequest(AiActionInput input);
    }
}
