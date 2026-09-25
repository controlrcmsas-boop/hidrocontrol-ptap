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
    public static readonly string[] Roles = ["Administrador", "Gerente", "Operador", "Simulador", "Demo"];

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

        // 3. Crear los 4 usuarios demo predeterminados
        await EnsureUserAsync("admin@hidrocontrol.potenzia.app", "Admin2026!", "Administrador de Sistema", "Administrador");
        await EnsureUserAsync("gerente@hidrocontrol.potenzia.app", "Gerente2026!", "Gerente General / Auditor", "Gerente");
        await EnsureUserAsync("operador@hidrocontrol.potenzia.app", "Operador2026!", "Ingeniero Técnico / Operador PTAP", "Operador");
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
            // Asegurar que tenga el rol asignado exclusivamente y esté aprobado
            var currentRoles = await userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(role) || currentRoles.Count > 1)
            {
                if (currentRoles.Any())
                {
                    await userManager.RemoveFromRolesAsync(user, currentRoles);
                }
                await userManager.AddToRoleAsync(user, role);
                logger.LogInformation("Roles actualizados para {Email}: {Role}", email, role);
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
