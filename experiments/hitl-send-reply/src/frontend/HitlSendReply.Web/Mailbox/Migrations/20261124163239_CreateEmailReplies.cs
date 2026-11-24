using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HitlSendReply.Web.Mailbox.Migrations
{
    /// <inheritdoc />
    public partial class CreateEmailReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_replies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CallId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    FinalText = table.Column<string>(type: "text", nullable: true),
                    Edited = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Reviewer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_replies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_replies_inbound_emails_EmailId",
                        column: x => x.EmailId,
                        principalTable: "inbound_emails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_replies_EmailId_CallId",
                table: "email_replies",
                columns: new[] { "EmailId", "CallId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_replies");
        }
    }
}
