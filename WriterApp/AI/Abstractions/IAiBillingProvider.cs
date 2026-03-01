namespace WriterApp.AI.Abstractions
{
    public interface IAiBillingProvider
    {
        bool RequiresEntitlement { get; }
        bool IsBillable { get; }
    }
}
