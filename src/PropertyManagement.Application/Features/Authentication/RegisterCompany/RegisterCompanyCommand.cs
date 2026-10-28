using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.RegisterCompany;

public sealed record RegisterCompanyCommand(
    string CompanyName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Address
) : IRequest<Result<RegisterCompanyResponse>>;