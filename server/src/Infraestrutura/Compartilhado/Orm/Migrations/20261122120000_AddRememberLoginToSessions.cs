using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Somnia.Infraestrutura.Compartilhado.Orm;

#nullable disable

namespace Somnia.Infraestrutura.Compartilhado.Orm.Migrations;

[DbContext(typeof(SomniaDbContext))]
[Migration("20261122120000_AddRememberLoginToSessions")]
public sealed class AddRememberLoginToSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "LembrarLogin",
            table: "Sessoes",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LembrarLogin",
            table: "Sessoes");
    }
}
