namespace WriterApp.AI.Abstractions
{
    public interface IAiRouter
    {
        AiProviderSelection Route(AiRequest request);
    }
}
