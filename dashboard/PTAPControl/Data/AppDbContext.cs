using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PTAPControl.Data;

/// <summary>
/// Contexto de base de datos que incluye Identity y las tablas de HIDROCONTROL.
/// Se usa SQLite para la capa de autenticación (portátil, sin servidor externo).
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Renombrar la tabla de usuarios con prefijo "Auth" para claridad
        builder.Entity<ApplicationUser>().ToTable("AuthUsers");
    }
}
