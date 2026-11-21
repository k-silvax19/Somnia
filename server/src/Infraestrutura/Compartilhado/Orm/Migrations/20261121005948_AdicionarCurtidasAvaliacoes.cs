using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Somnia.Infraestrutura.Compartilhado.Orm.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCurtidasAvaliacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CurtidasAvaliacoes",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvaliacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurtidasAvaliacoes", x => new { x.UsuarioId, x.AvaliacaoId });
                    table.ForeignKey(
                        name: "FK_CurtidasAvaliacoes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CurtidasAvaliacoes_Avaliacoes_AvaliacaoId",
                        column: x => x.AvaliacaoId,
                        principalTable: "Avaliacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurtidasAvaliacoes_AvaliacaoId_CreatedAt_UsuarioId",
                table: "CurtidasAvaliacoes",
                columns: new[] { "AvaliacaoId", "CreatedAt", "UsuarioId" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurtidasAvaliacoes");
        }
    }
}
