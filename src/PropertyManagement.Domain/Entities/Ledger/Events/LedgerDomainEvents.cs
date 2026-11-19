using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities.Ledger.Events;

public sealed record JournalEntryPostedEvent(
    Guid JournalEntryId,
    Guid OrganizationId,
    string EntryNumber,
    string SourceType,
    Guid SourceId,
    decimal TotalAmount,
    string Currency) : IDomainEvent;
