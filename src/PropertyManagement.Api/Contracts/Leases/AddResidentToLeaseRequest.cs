using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Leases;

public sealed record AddResidentToLeaseRequest(
    Guid ResidentId,
    LeaseResidentRole Role = LeaseResidentRole.PrimaryTenant);
