namespace WriterApp.Application.Commands
{
    public interface IAiRangeEditCommand : IAiEditCommand
    {
        TextRange Range { get; }
    }
}
