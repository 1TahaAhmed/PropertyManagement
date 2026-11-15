using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Common;

namespace PropertyManagement.Application.Features.Authentication.RegisterCompany;

public sealed record RegisterCompanyCommand(
    string CompanyName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Address,
    string DefaultCurrency = CurrencyCode.Default
) : IRequest<Result<RegisterCompanyResponse>>;
