using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TGHarker.Identity.Silo.Migrations
{
    /// <inheritdoc />
    public partial class AddedApplicationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationRoleAssignmentStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    RoleId = table.Column<string>(type: "text", nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRoleAssignmentStates", x => x.GrainId);
                });

            migrationBuilder.CreateTable(
                name: "UserApplicationRolesStates",
                columns: table => new
                {
                    GrainId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApplicationRolesStates", x => x.GrainId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_ApplicationId",
                table: "ApplicationRoleAssignmentStates",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_ClientId",
                table: "ApplicationRoleAssignmentStates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_IsActive",
                table: "ApplicationRoleAssignmentStates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_OrganizationId",
                table: "ApplicationRoleAssignmentStates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_RoleId",
                table: "ApplicationRoleAssignmentStates",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_Scope",
                table: "ApplicationRoleAssignmentStates",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_TenantId",
                table: "ApplicationRoleAssignmentStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoleAssignmentState_UserId",
                table: "ApplicationRoleAssignmentStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserApplicationRolesState_ClientId",
                table: "UserApplicationRolesStates",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_UserApplicationRolesState_TenantId",
                table: "UserApplicationRolesStates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserApplicationRolesState_UserId",
                table: "UserApplicationRolesStates",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationRoleAssignmentStates");

            migrationBuilder.DropTable(
                name: "UserApplicationRolesStates");
        }
    }
}
