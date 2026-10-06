using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }
}
