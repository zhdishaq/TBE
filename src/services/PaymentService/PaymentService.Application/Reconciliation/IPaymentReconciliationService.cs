namespace TBE.PaymentService.Application.Reconciliation;

/// <summary>
/// Plan 06-02 Task 3 (BO-06) — nightly reconciliation scan contract.
/// Implementation lives in <c>PaymentService.Infrastructure</c> (requires
/// <c>PaymentDbContext</c>). The <c>ReconciliationJob</c> BackgroundService
/// invokes <see cref="ScanAsync"/> on a Cronos schedule.
/// </summary>
public interface IPaymentReconciliationService
{
    Task ScanAsync(CancellationToken ct);
}
