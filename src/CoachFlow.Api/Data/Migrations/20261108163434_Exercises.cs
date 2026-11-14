using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CoachFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Exercises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sport = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    DefaultDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    MinParticipants = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxParticipants = table.Column<int>(type: "INTEGER", nullable: true),
                    Level = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exercises_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Focuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sport = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Focuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Focuses_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseFocuses",
                columns: table => new
                {
                    ExerciseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FocusesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseFocuses", x => new { x.ExerciseId, x.FocusesId });
                    table.ForeignKey(
                        name: "FK_ExerciseFocuses_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExerciseFocuses_Focuses_FocusesId",
                        column: x => x.FocusesId,
                        principalTable: "Focuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Focuses",
                columns: new[] { "Id", "CreatedAt", "CreatedById", "Name", "NormalizedName", "Sport" },
                values: new object[,]
                {
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000001"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Aufwärmen", "aufwärmen", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000002"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Pritschen", "pritschen", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000003"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Baggern", "baggern", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000004"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Aufschlag", "aufschlag", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000005"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Annahme", "annahme", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000006"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Zuspiel", "zuspiel", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000007"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Angriff", "angriff", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000008"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Block", "block", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000009"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Feldabwehr", "feldabwehr", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000010"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Spielverständnis", "spielverständnis", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000011"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Koordination", "koordination", "volleyball" },
                    { new Guid("0a6f1c2e-0001-4000-8000-000000000012"), new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), null, "Athletik", "athletik", "volleyball" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseFocuses_FocusesId",
                table: "ExerciseFocuses",
                column: "FocusesId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_Sport",
                table: "Exercises",
                column: "Sport");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_UpdatedById",
                table: "Exercises",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Focuses_CreatedById",
                table: "Focuses",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Focuses_Sport_NormalizedName",
                table: "Focuses",
                columns: new[] { "Sport", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseFocuses");

            migrationBuilder.DropTable(
                name: "Exercises");

            migrationBuilder.DropTable(
                name: "Focuses");
        }
    }
}
