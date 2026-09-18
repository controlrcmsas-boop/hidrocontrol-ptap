using Microsoft.AspNetCore.HttpOverrides;
using PTAPControl.Components;
using PTAPControl.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuración de Reverse Proxy para despliegue en línea (Nginx / Cloudflare)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configuración de CORS dinámico para permitir peticiones desde el dominio público
var allowedOrigins = builder.Configuration.GetSection("HostSettings:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5088", "https://hidrocontrol.potenzia.com"];

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Cliente HTTP para Node-RED con URL configurable por ambiente
builder.Services.AddHttpClient<NodeRedApiClient>(client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("NODERED_BASE_URL")
                  ?? builder.Configuration["NodeRed:BaseUrl"]
                  ?? "http://localhost:1880/";

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Servicios de Base de Conocimiento y Supervisor IA
builder.Services.AddSingleton<PtapKnowledgeService>();

// Servicio Scoped para persistir el historial de conversación del chat durante la sesión
builder.Services.AddScoped<AiChatStateService>();

builder.Services.AddHttpClient<GeminiAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
});

var app = builder.Build();

// Aplicar Forwarded Headers antes de cualquier redirección HTTPS o validación de host
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Encabezados de seguridad y permisividad de embebido (IFrame / Mímicos) si está habilitado
app.Use(async (context, next) =>
{
    var allowIframe = app.Configuration.GetValue<bool>("HostSettings:AllowIframeEmbedding", true);
    if (allowIframe)
    {
        context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self' https://*.potenzia.com http://localhost:*");
    }
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseCors("PtapCorsPolicy");
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
