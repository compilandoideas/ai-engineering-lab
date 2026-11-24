using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HitlSendReply.Web.Mailbox.Migrations
{
    /// <inheritdoc />
    public partial class CreateInboundEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbound_emails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    To = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TicketId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_emails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_emails_ReceivedAt",
                table: "inbound_emails",
                column: "ReceivedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbound_emails");
        }
    }
}
