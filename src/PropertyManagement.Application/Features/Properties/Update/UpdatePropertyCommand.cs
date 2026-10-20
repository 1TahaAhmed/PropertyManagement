using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Features.Properties.Update
{
    public sealed record UpdatePropertyCommand(
       Guid PropertyId,
    string Name,
    string Address,
    PropertyType Type,
    string Description = "")
    : IRequest<Result<PropertyResponse>>;
}
