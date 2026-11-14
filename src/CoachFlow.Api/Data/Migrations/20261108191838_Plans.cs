using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoachFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Plans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sport = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ParticipantCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Spread = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingPlans_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlanFocuses",
                columns: table => new
                {
                    FocusesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingPlanId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFocuses", x => new { x.FocusesId, x.TrainingPlanId });
                    table.ForeignKey(
                        name: "FK_PlanFocuses_Focuses_FocusesId",
                        column: x => x.FocusesId,
                        principalTable: "Focuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanFocuses_TrainingPlans_TrainingPlanId",
                        column: x => x.TrainingPlanId,
                        principalTable: "TrainingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanPosition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    PlannedDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanPosition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanPosition_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlanPosition_TrainingPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "TrainingPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanFocuses_TrainingPlanId",
                table: "PlanFocuses",
                column: "TrainingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanPosition_ExerciseId",
                table: "PlanPosition",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanPosition_PlanId",
                table: "PlanPosition",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlans_Sport",
                table: "TrainingPlans",
                column: "Sport");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlans_UpdatedById",
                table: "TrainingPlans",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanFocuses");

            migrationBuilder.DropTable(
                name: "PlanPosition");

            migrationBuilder.DropTable(
                name: "TrainingPlans");
        }
    }
}
