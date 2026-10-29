namespace PropertyManagement.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IDbTransaction : IAsyncDisposable, IDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
