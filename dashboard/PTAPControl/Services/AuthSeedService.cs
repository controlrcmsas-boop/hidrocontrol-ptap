using Microsoft.AspNetCore.Identity;
using PTAPControl.Data;

namespace PTAPControl.Services;

/// <summary>
/// Servicio de inicialización de datos: crea roles predeterminados y el primer administrador
/// si la base de datos está vacía. Se ejecuta al arrancar la aplicación.
/// </summary>
public class AuthSeedService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration config,
    ILogger<AuthSeedService> logger)
{
    // Roles del sistema
    public static readonly string[] Roles = ["Administrador", "Operador", "Demo"];

    public async Task SeedAsync()
    {
        // 1. Crear roles si no existen
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Rol '{Role}' creado.", role);
            }
        }

        // 2. Crear administrador inicial si no hay ninguno
        var adminEmail = config["Auth:AdminEmail"] ?? "admin@hidrocontrol.local";
        var adminPassword = config["Auth:AdminPassword"] ?? "Admin@2026!";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Administrador HIDROCONTROL",
                EmailConfirmed = true,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Administrador");
                logger.LogInformation("Usuario administrador inicial creado: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Error creando admin: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
