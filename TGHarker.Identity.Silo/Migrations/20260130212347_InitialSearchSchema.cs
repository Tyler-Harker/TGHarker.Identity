using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TGHarker.Identity.Silo.Migrations
{
    /// <inheritdoc />
    public partial class InitialSearchSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    ClientName = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "InvitationStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationInvitationStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    OrganizationId = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationInvitationStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMembershipStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    OrganizationId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMembershipStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    Identifier = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "TenantMembershipStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMembershipStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "TenantStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    Identifier = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "UserStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStates", x => x.GrainId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientState_ClientName",
                table: "ClientStates",
                column: "ClientName");

            migrationBuilder.CreateIndex(
                name: "IX_ClientState_IsActive",
                table: "ClientStates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ClientState_TenantId",
                table: "ClientStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationState_Email",
                table: "InvitationStates",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationState_Status",
                table: "InvitationStates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationState_TenantId",
                table: "InvitationStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationState_Token",
                table: "InvitationStates",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationState_Email",
                table: "OrganizationInvitationStates",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationState_OrganizationId",
                table: "OrganizationInvitationStates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationState_Status",
                table: "OrganizationInvitationStates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationState_TenantId",
                table: "OrganizationInvitationStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationInvitationState_Token",
                table: "OrganizationInvitationStates",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembershipState_IsActive",
                table: "OrganizationMembershipStates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembershipState_OrganizationId",
                table: "OrganizationMembershipStates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembershipState_TenantId",
                table: "OrganizationMembershipStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembershipState_UserId",
                table: "OrganizationMembershipStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationState_Identifier",
                table: "OrganizationStates",
                column: "Identifier");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationState_IsActive",
                table: "OrganizationStates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationState_Name",
                table: "OrganizationStates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationState_TenantId",
                table: "OrganizationStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembershipState_IsActive",
                table: "TenantMembershipStates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembershipState_TenantId",
                table: "TenantMembershipStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembershipState_UserId",
                table: "TenantMembershipStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantState_Identifier",
                table: "TenantStates",
                column: "Identifier");

            migrationBuilder.CreateIndex(
                name: "IX_TenantState_IsActive",
                table: "TenantStates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TenantState_Name",
                table: "TenantStates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_UserState_Email",
                table: "UserStates",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_UserState_IsActive",
                table: "UserStates",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientStates");

            migrationBuilder.DropTable(
                name: "InvitationStates");

            migrationBuilder.DropTable(
                name: "OrganizationInvitationStates");

            migrationBuilder.DropTable(
                name: "OrganizationMembershipStates");

            migrationBuilder.DropTable(
                name: "OrganizationStates");

            migrationBuilder.DropTable(
                name: "TenantMembershipStates");

            migrationBuilder.DropTable(
                name: "TenantStates");

            migrationBuilder.DropTable(
                name: "UserStates");
        }
    }
}
