using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Booking");

            migrationBuilder.EnsureSchema(
                name: "Saga");

            migrationBuilder.CreateTable(
                name: "Basket",
                schema: "Booking",
                columns: table => new
                {
                    BasketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FlightBookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HotelBookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CarBookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FlightSubtotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    HotelSubtotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChargedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FlightCaptured = table.Column<bool>(type: "bit", nullable: false),
                    HotelCaptured = table.Column<bool>(type: "bit", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Basket", x => x.BasketId);
                });

            migrationBuilder.CreateTable(
                name: "BasketEventLog",
                schema: "Booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BasketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HandledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketEventLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingSagaState",
                schema: "Saga",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", rowVersion: true, nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ChannelKind = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WalletReservationTxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OfferToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GdsPnr = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TicketNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgencyNetFare = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AgencyMarkupAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AgencyGrossAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AgencyCommissionAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AgencyMarkupOverride = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    CustomerPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BaseFareAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SurchargeAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TicketingDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeoutTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSuccessfulStep = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Warn24HSent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Warn2HSent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CancelledByStaff = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CancellationReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancellationRequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CancellationApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CancellationApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupplierReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ItineraryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingSagaState", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "CarBooking",
                schema: "Booking",
                columns: table => new
                {
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DropoffLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PickupAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DropoffAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DriverAge = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarBooking", x => x.BookingId);
                });

            migrationBuilder.CreateTable(
                name: "HotelBookingSagaState",
                schema: "Booking",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", rowVersion: true, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SupplierRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rooms = table.Column<int>(type: "int", nullable: false),
                    Adults = table.Column<int>(type: "int", nullable: false),
                    Children = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FailureCause = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InitiatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelBookingSagaState", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true),
                    BusName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "SagaDeadLetter",
                schema: "Saga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSuccessfulStep = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FailedStep = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExceptionDetail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaDeadLetter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Basket_FlightBookingId",
                schema: "Booking",
                table: "Basket",
                column: "FlightBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Basket_HotelBookingId",
                schema: "Booking",
                table: "Basket",
                column: "HotelBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Basket_Status",
                schema: "Booking",
                table: "Basket",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Basket_UserId",
                schema: "Booking",
                table: "Basket",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketEventLog_BasketId_EventId",
                schema: "Booking",
                table: "BasketEventLog",
                columns: new[] { "BasketId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_AgencyId",
                schema: "Saga",
                table: "BookingSagaState",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_Channel",
                schema: "Saga",
                table: "BookingSagaState",
                column: "ChannelKind");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_CustomerEmail",
                schema: "Saga",
                table: "BookingSagaState",
                column: "CustomerEmail",
                filter: "[CustomerEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_CustomerId",
                schema: "Saga",
                table: "BookingSagaState",
                column: "CustomerId",
                filter: "[CustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_GdsPnr",
                schema: "Saga",
                table: "BookingSagaState",
                column: "GdsPnr",
                filter: "[GdsPnr] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_SupplierReference_InitiatedAt",
                schema: "Saga",
                table: "BookingSagaState",
                columns: new[] { "SupplierReference", "InitiatedAtUtc" },
                filter: "[SupplierReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_TicketingDeadlineUtc",
                schema: "Saga",
                table: "BookingSagaState",
                column: "TicketingDeadlineUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CarBooking_Status",
                schema: "Booking",
                table: "CarBooking",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CarBooking_UserId",
                schema: "Booking",
                table: "CarBooking",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookingSagaState_Status",
                schema: "Booking",
                table: "HotelBookingSagaState",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookingSagaState_SupplierRef",
                schema: "Booking",
                table: "HotelBookingSagaState",
                column: "SupplierRef");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookingSagaState_UserId",
                schema: "Booking",
                table: "HotelBookingSagaState",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true,
                filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true,
                filter: "[OutboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_BusName_Created",
                table: "OutboxState",
                columns: new[] { "BusName", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_SagaDeadLetter_CorrelationId",
                schema: "Saga",
                table: "SagaDeadLetter",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SagaDeadLetter_Resolved_CreatedAtUtc",
                schema: "Saga",
                table: "SagaDeadLetter",
                columns: new[] { "Resolved", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Basket",
                schema: "Booking");

            migrationBuilder.DropTable(
                name: "BasketEventLog",
                schema: "Booking");

            migrationBuilder.DropTable(
                name: "BookingSagaState",
                schema: "Saga");

            migrationBuilder.DropTable(
                name: "CarBooking",
                schema: "Booking");

            migrationBuilder.DropTable(
                name: "HotelBookingSagaState",
                schema: "Booking");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "SagaDeadLetter",
                schema: "Saga");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");
        }
    }
}
