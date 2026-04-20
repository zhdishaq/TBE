using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.PricingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "OutboxState");

            migrationBuilder.DropColumn(
                name: "Delivered",
                table: "OutboxMessage");

            migrationBuilder.RenameColumn(
                name: "Created",
                table: "InboxState",
                newName: "Received");

            migrationBuilder.AddColumn<string>(
                name: "BusName",
                table: "OutboxState",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockId",
                table: "OutboxState",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "OutboxState",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InboxConsumerId",
                table: "OutboxMessage",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpirationTime",
                table: "OutboxMessage",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "Headers",
                table: "OutboxMessage",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatorId",
                table: "OutboxMessage",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "OutboxMessage",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "OutboxMessage",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAddress",
                table: "OutboxMessage",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConsumerId",
                table: "InboxState",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Consumed",
                table: "InboxState",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastSequenceNumber",
                table: "InboxState",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockId",
                table: "InboxState",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InboxState",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarkupRuleAuditLog",
                schema: "pricing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkupRuleAuditLog", x => x.Id);
                    table.CheckConstraint("CK_MarkupRuleAuditLog_Action", "[Action] IN ('Created','Updated','Deactivated','Deleted')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_BusName_Created",
                table: "OutboxState",
                columns: new[] { "BusName", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

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
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_MarkupRuleAuditLog_Actor_ChangedAt",
                schema: "pricing",
                table: "MarkupRuleAuditLog",
                columns: new[] { "Actor", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MarkupRuleAuditLog_AgencyId_ChangedAt",
                schema: "pricing",
                table: "MarkupRuleAuditLog",
                columns: new[] { "AgencyId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_MarkupRuleAuditLog_RuleId_ChangedAt",
                schema: "pricing",
                table: "MarkupRuleAuditLog",
                columns: new[] { "RuleId", "ChangedAt" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId" },
                principalTable: "InboxState",
                principalColumns: new[] { "MessageId", "ConsumerId" });

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxMessage_OutboxState_OutboxId",
                table: "OutboxMessage",
                column: "OutboxId",
                principalTable: "OutboxState",
                principalColumn: "OutboxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                table: "OutboxMessage");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboxMessage_OutboxState_OutboxId",
                table: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "MarkupRuleAuditLog",
                schema: "pricing");

            migrationBuilder.DropIndex(
                name: "IX_OutboxState_BusName_Created",
                table: "OutboxState");

            migrationBuilder.DropIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "OutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                table: "OutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState");

            migrationBuilder.DropColumn(
                name: "BusName",
                table: "OutboxState");

            migrationBuilder.DropColumn(
                name: "LockId",
                table: "OutboxState");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OutboxState");

            migrationBuilder.DropColumn(
                name: "Headers",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "InitiatorId",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "SourceAddress",
                table: "OutboxMessage");

            migrationBuilder.DropColumn(
                name: "LastSequenceNumber",
                table: "InboxState");

            migrationBuilder.DropColumn(
                name: "LockId",
                table: "InboxState");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InboxState");

            migrationBuilder.RenameColumn(
                name: "Received",
                table: "InboxState",
                newName: "Created");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "OutboxState",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "InboxConsumerId",
                table: "OutboxMessage",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpirationTime",
                table: "OutboxMessage",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Delivered",
                table: "OutboxMessage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConsumerId",
                table: "InboxState",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Consumed",
                table: "InboxState",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
