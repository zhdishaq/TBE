using TBE.BookingService.Application.Consumers.CompensationConsumers;
using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="ISagaDeadLetterStore"/> used by
/// <c>SagaDeadLetterSink</c> to persist unrecoverable-failure ledger rows.
/// Registered in DI by BookingService.API Program.cs.
/// </summary>
public class SagaDeadLetterStore : ISagaDeadLetterStore
{
    private readonly BookingDbContext _db;

    public SagaDeadLetterStore(BookingDbContext db) => _db = db;

    public async Task AddAsync(SagaDeadLetter entry, CancellationToken ct)
    {
        await _db.SagaDeadLetters.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
    }
}
