using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TBE.BookingService.Application;

namespace TBE.BookingService.API.Controllers;

/// <summary>
/// Plan 06-02 Task 1 (BO-02) — POST /api/backoffice/bookings/manual.
/// ops-cs (+ ops-admin via policy widening) hits this endpoint to
/// insert a staff-entered booking without running the saga.
///
/// <para>
/// Lives in BookingService.API (not BackofficeService) because
/// <see cref="ManualBookingCommand"/> owns the <c>Saga.BookingSagaState</c>
/// row via <c>BookingDbContext</c> — a cross-service write from
/// BackofficeDbContext would bypass the BookingService's domain
/// invariants. The BookingService.API <c>Program.cs</c> registers a
/// second <c>"Backoffice"</c> JWT scheme + four ops policies (duplicated
/// from BackofficeService.API) so the Backoffice bearer token is
/// validated here against the tbe-backoffice realm.
/// </para>
///
/// <para>
/// RFC-7807 problem+json type URIs (PATTERNS.md Pattern G):
///   <c>/errors/duplicate-supplier-reference</c> (409),
///   <c>/errors/manual-booking-invalid-amount</c> (400),
///   <c>/errors/manual-booking-invalid-itinerary</c> (400),
///   <c>/errors/missing-preferred-username</c> (401).
/// </para>
/// </summary>
[ApiController]
[Route("api/backoffice/bookings/manual")]
[Authorize(Policy = "BackofficeCsPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class ManualBookingsController : ControllerBase
{
    private readonly ManualBookingCommand _command;
    private readonly ILogger<ManualBookingsController> _logger;

    public ManualBookingsController(
        ManualBookingCommand command,
        ILogger<ManualBookingsController> logger)
    {
        _command = command;
        _logger = logger;
    }

    /// <summary>
    /// Body DTO — per Pitfall 28 it literally does not expose Channel
    /// or Status properties, so a malformed JSON trying to set them is
    /// silently dropped (System.Text.Json unknown-key policy).
    /// </summary>
    public sealed record CreateManualBookingRequest(
        [property: Required] string BookingReference,
        [property: Required] string Pnr,
        [property: Required] string ProductType,
        decimal BaseFareAmount,
        decimal SurchargeAmount,
        decimal TaxAmount,
        [property: Required] string Currency,
        Guid? CustomerId,
        [property: Required] string CustomerName,
        [property: Required] string CustomerEmail,
        string? CustomerPhone,
        Guid? AgencyId,
        [property: Required] string ItineraryJson,
        string? SupplierReference,
        string? Notes);

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateManualBookingRequest request,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return new ObjectResult(new ProblemDetails
            {
                Type = "/errors/missing-preferred-username",
                Title = "Authenticated user is missing preferred_username claim.",
                Status = StatusCodes.Status401Unauthorized,
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
        }

        var input = new ManualBookingInput(
            request.BookingReference,
            request.Pnr,
            request.ProductType,
            request.BaseFareAmount,
            request.SurchargeAmount,
            request.TaxAmount,
            request.Currency,
            request.CustomerId,
            request.CustomerName,
            request.CustomerEmail,
            request.CustomerPhone,
            request.AgencyId,
            request.ItineraryJson,
            request.SupplierReference,
            request.Notes);

        try
        {
            var bookingId = await _command.CreateAsync(input, actor, ct);
            return Created(
                $"/api/backoffice/bookings/{bookingId}",
                new { BookingId = bookingId });
        }
        catch (ManualBookingValidationException ex)
        {
            return new ObjectResult(new ProblemDetails
            {
                Type = $"/errors/manual-booking-{ex.Kind}",
                Title = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            })
            {
                StatusCode = StatusCodes.Status400BadRequest,
            };
        }
        catch (DuplicateSupplierReferenceException ex)
        {
            _logger.LogInformation(
                "manual-booking-duplicate-supplier-reference {SupplierReference} {ExistingBookingId} {Actor}",
                ex.SupplierReference, ex.ExistingBookingId, actor);
            return new ObjectResult(new ProblemDetails
            {
                Type = "/errors/duplicate-supplier-reference",
                Title = ex.Message,
                Status = StatusCodes.Status409Conflict,
                Extensions =
                {
                    ["existingBookingId"] = ex.ExistingBookingId,
                    ["supplierReference"] = ex.SupplierReference,
                },
            })
            {
                StatusCode = StatusCodes.Status409Conflict,
            };
        }
    }
}
