using PropertyManagement.Application.Abstractions;

namespace PropertyManagement.Infrastructure.Common.Time
{
    public class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
