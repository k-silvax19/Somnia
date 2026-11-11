using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Somnia.Infraestrutura.Compartilhado.Orm.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAvaliacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultReviewVisibility",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DefaultScoreVisibility",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "Avaliacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Score = table.Column<int>(type: "int", nullable: true),
                    Review = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avaliacoes", x => x.Id);
                    table.CheckConstraint("CK_Avaliacoes_Conteudo", "[Score] IS NOT NULL OR LEN(LTRIM(RTRIM([Review]))) > 0 AND [Review] IS NOT NULL");
                    table.CheckConstraint("CK_Avaliacoes_Provider", "[Provider] IN ('spotify', 'simulated')");
                    table.CheckConstraint("CK_Avaliacoes_Score", "[Score] IS NULL OR [Score] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_Avaliacoes_Type", "[Type] IN ('album', 'track')");
                    table.ForeignKey(
                        name: "FK_Avaliacoes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Avaliacoes_Provider_Type_ExternalId_CreatedAt_Id",
                table: "Avaliacoes",
                columns: new[] { "Provider", "Type", "ExternalId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Avaliacoes_UsuarioId_CreatedAt_Id",
                table: "Avaliacoes",
                columns: new[] { "UsuarioId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Avaliacoes_UsuarioId_Provider_Type_ExternalId",
                table: "Avaliacoes",
                columns: new[] { "UsuarioId", "Provider", "Type", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Avaliacoes");

            migrationBuilder.AddColumn<int>(
                name: "DefaultReviewVisibility",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultScoreVisibility",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
