// Shared API types for the B2C portal.
//
// Shapes mirror the backend projections in
// `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs`
// (BookingDtoPublic) with a few frontend-only fields (`productType`,
// `departureDate`) that the dashboard needs for the Upcoming/Past
// partitioning and that later plans (04-02, 04-03) will populate on the
// backend. They are optional here so the shape stays backwards-compatible
// with the shipped `/customers/me/bookings` payload.
//
// NEVER import this file from a file that renders PII — everything here
// is already the public DTO shape (UserId + passport/document data are
// stripped server-side per COMP-01/02 + D-20).

export interface BookingDtoPublic {
  bookingId: string;
  status: number;
  bookingReference: string;
  pnr: string | null;
  ticketNumber: string | null;
  totalAmount: number;
  currency: string;
  createdAt: string;
  /**
   * Optional — backend will populate from Phase 4 plan 04-02 onwards.
   * The dashboard partitioner falls back to `createdAt` when absent.
   */
  departureDate?: string;
  /**
   * Optional — `"flight" | "hotel" | "car"`. Used by `BookingRow` to pick
   * the correct lucide icon. Falls back to the booking-reference prefix
   * when absent.
   */
  productType?: 'flight' | 'hotel' | 'car';
}

export interface CustomerBookingsPage {
  page: number;
  size: number;
  items: BookingDtoPublic[];
}
