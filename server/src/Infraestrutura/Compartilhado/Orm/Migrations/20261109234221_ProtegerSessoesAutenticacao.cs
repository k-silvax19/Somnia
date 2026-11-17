using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Somnia.Infraestrutura.Compartilhado.Orm.Migrations
{
    /// <inheritdoc />
    public partial class ProtegerSessoesAutenticacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [RefreshTokens];");

            migrationBuilder.AddColumn<Guid>(
                name: "SessaoId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Sessoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevogadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessoes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessaoId",
                table: "RefreshTokens",
                column: "SessaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_UsuarioId",
                table: "Sessoes",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Sessoes_SessaoId",
                table: "RefreshTokens",
                column: "SessaoId",
                principalTable: "Sessoes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Sessoes_SessaoId",
                table: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Sessoes");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SessaoId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessaoId",
                table: "RefreshTokens");
        }
    }
}
