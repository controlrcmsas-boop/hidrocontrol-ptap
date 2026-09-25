using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PTAPControl.Components;
using PTAPControl.Data;
using PTAPControl.Services;
// ─── Carga automática de archivo .env ─────────────────────────────────────────
var envFiles = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env")
};
foreach (var file in envFiles)
{
    if (File.Exists(file))
    {
        foreach (var line in File.ReadAllLines(file))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(parts[0].Trim())))
            {
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            }
        }
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);


// ─── Reverse Proxy (Nginx / Cloudflare) ──────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ─── CORS dinámico ────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("HostSettings:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5088", "https://hidrocontrol.potenzia.app"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("PtapCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─── Base de datos de autenticación (SQLite) ──────────────────────────────────
var dbPath = builder.Configuration["Auth:DbPath"] ?? "hidrocontrol-auth.db";
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
{
    Directory.CreateDirectory(dbDir);
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ─── ASP.NET Core Identity ────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Contraseña
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Cuenta
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;

    // Bloqueo por intentos fallidos
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configurar cookie de autenticación
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ─── Autorización ─────────────────────────────────────────────────────────────
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ─── Blazor ───────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── Cliente HTTP para Node-RED ───────────────────────────────────────────────
builder.Services.AddHttpClient<NodeRedApiClient>(client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("NODERED_BASE_URL")
                  ?? builder.Configuration["NodeRed:BaseUrl"]
                  ?? "http://localhost:1880/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// ─── Servicios del sistema PTAP ───────────────────────────────────────────────
builder.Services.AddSingleton<PtapSimulationEngine>();
builder.Services.AddSingleton<PtapKnowledgeService>();
builder.Services.AddScoped<AiChatStateService>();
builder.Services.AddTransient<AuthSeedService>();

builder.Services.AddHttpClient<GeminiAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
});

// ─── Cliente HTTP para TypeSafe AI (Jev System One) ──────────────────────────
builder.Services.AddHttpClient<TypeSafeAiClientService>(client =>
{
    var apiKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")
                 ?? builder.Configuration["Ai:TypeSafeApiKey"]
                 ?? string.Empty;
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }
});

var app = builder.Build();

// ─── Migraciones y seed automático al arrancar ────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var seeder = scope.ServiceProvider.GetRequiredService<AuthSeedService>();
    await seeder.SeedAsync();
}

// ─── Pipeline HTTP ────────────────────────────────────────────────────────────
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Encabezados de seguridad (iframe / SCADA mímico)
app.Use(async (context, next) =>
{
    var allowIframe = app.Configuration.GetValue<bool>("HostSettings:AllowIframeEmbedding", true);
    if (allowIframe)
    {
        context.Response.Headers.Append("Content-Security-Policy",
            "frame-ancestors 'self' https://*.potenzia.com http://localhost:*");
    }
    await next();
});

// Reverse proxy (Cloudflare) maneja la terminación SSL
// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseCors("PtapCorsPolicy");

// Auth middleware (ORDEN IMPORTA: antes de Antiforgery y Blazor)
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// ─── Endpoint de autenticación HTTP ──────────────────────────────────────────
// Blazor Server no puede escribir cookies desde el circuito WebSocket.
// El formulario de login hace POST aquí (HTTP puro), donde sí hay contexto HTTP.
app.MapPost("/auth/login", async (
    HttpContext ctx,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILogger<Program> logger) =>
{
    var form = ctx.Request.Form;
    var email     = form["email"].ToString().Trim();
    var password  = form["password"].ToString();
    var remember  = form["rememberMe"].ToString() == "true";
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith("/"))
        returnUrl = "/";

    var errorBase = $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        return Results.Redirect(errorBase + "&error=empty");

    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
        return Results.Redirect(errorBase + "&error=invalid");

    if (!user.EmailConfirmed)
        return Results.Redirect(errorBase + "&error=unconfirmed");

    if (user.ApprovalStatus == ApprovalStatus.Pending)
        return Results.Redirect("/pending-approval");

    if (user.ApprovalStatus == ApprovalStatus.Rejected)
        return Results.Redirect(errorBase + "&error=rejected");

    var result = await signInManager.PasswordSignInAsync(user, password, remember, lockoutOnFailure: true);

    if (result.Succeeded)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        logger.LogInformation("Usuario {Email} inició sesión.", user.Email);

        var isSimulador = await userManager.IsInRoleAsync(user, "Simulador");
        var isAdmin = await userManager.IsInRoleAsync(user, "Administrador");

        if (isSimulador && (returnUrl == "/" || returnUrl == "/login"))
        {
            return Results.Redirect("/simulador");
        }

        if (isAdmin && (returnUrl == "/" || returnUrl == "/login"))
        {
            return Results.Redirect("/admin/users");
        }

        return Results.Redirect(returnUrl);
    }
    else if (result.IsLockedOut)
        return Results.Redirect(errorBase + "&error=locked");
    else
        return Results.Redirect(errorBase + "&error=invalid");
})
.AllowAnonymous()
.DisableAntiforgery();

// Endpoint de logout HTTP
app.MapGet("/auth/logout", async (SignInManager<ApplicationUser> sm) =>
{
    await sm.SignOutAsync();
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ─── Endpoints de diagnóstico (solo en Development) ──────────────────────────
if (app.Environment.IsDevelopment())
{
    // GET /dev/users — lista todos los usuarios de la BD
    app.MapGet("/dev/users", async (UserManager<ApplicationUser> um) =>
    {
        var users = um.Users.Select(u => new {
            u.Email, u.EmailConfirmed, u.ApprovalStatus, u.FullName,
            Roles = (IList<string>?)null
        }).ToList();
        return Results.Ok(users);
    });

    // GET /dev/reset-admin — resetea la contraseña del admin a Admin@2026!
    app.MapGet("/dev/reset-admin", async (UserManager<ApplicationUser> um, IConfiguration cfg) =>
    {
        var email = cfg["Auth:AdminEmail"] ?? "admin@hidrocontrol.local";
        var newPwd = cfg["Auth:AdminPassword"] ?? "Admin@2026!";
        var user = await um.FindByEmailAsync(email);
        if (user is null) return Results.NotFound($"No se encontró el usuario {email}");

        // Asegura que esté aprobado y confirmado
        user.EmailConfirmed = true;
        user.ApprovalStatus = ApprovalStatus.Approved;
        await um.UpdateAsync(user);

        // Reset de contraseña
        var token = await um.GeneratePasswordResetTokenAsync(user);
        var result = await um.ResetPasswordAsync(user, token, newPwd);

        if (result.Succeeded)
            return Results.Ok($"✅ Contraseña del admin '{email}' reseteada a '{newPwd}'. Roles: Administrador");
        else
            return Results.Problem(string.Join(", ", result.Errors.Select(e => e.Description)));
    });
}

app.Run();
