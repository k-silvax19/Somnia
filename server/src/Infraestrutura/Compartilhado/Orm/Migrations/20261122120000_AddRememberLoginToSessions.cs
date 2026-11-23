using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Somnia.Infraestrutura.Compartilhado.Orm.Migrations;

public sealed partial class AddRememberLoginToSessions : Migration
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
