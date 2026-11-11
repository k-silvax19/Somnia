using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Somnia.Infraestrutura.Compartilhado.Orm.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRelacoesSeguir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelacoesSeguir",
                columns: table => new
                {
                    SeguidorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeguidoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelacoesSeguir", x => new { x.SeguidorId, x.SeguidoId });
                    table.CheckConstraint("CK_RelacoesSeguir_UsuariosDistintos", "[SeguidorId] <> [SeguidoId]");
                    table.ForeignKey(
                        name: "FK_RelacoesSeguir_AspNetUsers_SeguidoId",
                        column: x => x.SeguidoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RelacoesSeguir_AspNetUsers_SeguidorId",
                        column: x => x.SeguidorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RelacoesSeguir_SeguidoId_CreatedAt_SeguidorId",
                table: "RelacoesSeguir",
                columns: new[] { "SeguidoId", "CreatedAt", "SeguidorId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_RelacoesSeguir_SeguidorId_CreatedAt_SeguidoId",
                table: "RelacoesSeguir",
                columns: new[] { "SeguidorId", "CreatedAt", "SeguidoId" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RelacoesSeguir");
        }
    }
}
