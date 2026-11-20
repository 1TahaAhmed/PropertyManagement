namespace PropertyManagement.Application.Abstractions
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
