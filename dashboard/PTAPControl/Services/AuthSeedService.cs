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
    public static readonly string[] Roles = ["Administrador", "Operador", "Simulador", "Demo"];

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

        // 2. Crear administrador de configuración si no existe
        var adminEmail = config["Auth:AdminEmail"] ?? "admin@hidrocontrol.local";
        var adminPassword = config["Auth:AdminPassword"] ?? "Admin@2026!";
        await EnsureUserAsync(adminEmail, adminPassword, "Administrador HIDROCONTROL", "Administrador");

        // 3. Crear los 3 usuarios demo predeterminados
        await EnsureUserAsync("admin@hidrocontrol.potenzia.app", "Admin2026!", "Director Gerencial / Administrador", "Administrador");
        await EnsureUserAsync("operador@hidrocontrol.potenzia.app", "Operador2026!", "Técnico de Planta / Operador SCADA", "Operador");
        await EnsureUserAsync("simulador@hidrocontrol.potenzia.app", "Simulador2026!", "Demostración Comercial (Simulador)", "Simulador");
    }

    private async Task EnsureUserAsync(string email, string password, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
                logger.LogInformation("Usuario {Role} creado: {Email}", role, email);
            }
            else
            {
                logger.LogError("Error creando usuario {Email}: {Errors}",
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Asegurar que tenga el rol asignado y esté aprobado
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
            if (user.ApprovalStatus != ApprovalStatus.Approved || !user.EmailConfirmed)
            {
                user.ApprovalStatus = ApprovalStatus.Approved;
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }
        }
    }
}
