using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Common.Results
{
    public sealed record Error
    (
        string Code,
        string Message,
        string? PropertyName = null

    );
}
