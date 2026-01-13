// Program.cs
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tracking.Web.Services;
using Tracking.Web.Models;
using Tracking.Web.Endpoints;

// ========== Configurar Serilog ANTES de criar o builder ==========
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("🚀 Iniciando TrackingWeb...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ========== Substituir logging padrão pelo Serilog ==========
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "TrackingWeb")
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}    {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "Logs/tracking-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}"));

    // ========== Configurações ==========
    builder.Services.Configure<TrackingOptions>(builder.Configuration.GetSection("Tracking"));

    // ========== Memory Cache ==========
    builder.Services.AddMemoryCache();

    // ========== HttpClients ==========
    builder.Services.AddHttpClient("internalApi", client =>
    {
        var apiBase = builder.Configuration["Tracking:ApiBaseUrl"];
        if (!string.IsNullOrEmpty(apiBase))
            client.BaseAddress = new Uri(apiBase);
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    builder.Services.AddHttpClient("turnstile", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(8);
    });

    // ========== Serviços ==========
    builder.Services.AddScoped<ITrackingService, TrackingService>();
    builder.Services.AddSingleton<IRateLimitService, MemoryRateLimitService>();
    builder.Services.AddSingleton<IAlertService, AlertService>();

    builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
    {
        client.BaseAddress = new Uri("http://localhost");
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    // ========== Blazor ==========
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();

    var app = builder.Build();

    // ========== Middleware de Logging ==========
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondido em {Elapsed:0.0000}ms com {StatusCode}";
        options.GetLevel = (httpContext, elapsed, ex) => ex != null
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode > 499
                ? LogEventLevel.Error
                : LogEventLevel.Information;

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // ========== Middleware ==========
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    // ========== ENDPOINTS - ORDEM CRÍTICA ==========
    app.UseEndpoints(endpoints =>
    {
        Log.Information("🔧 Registrando endpoints...");

        // 1️⃣ ENDPOINT DA API (PRIMEIRO!)
        endpoints.MapPost("/api/track", async (
            TrackRequest req,
            HttpContext http,
            ITrackingService trackingService,
            IRateLimitService rateLimitService,
            IOptions<TrackingOptions> opts,
            ILogger<Program> logger) =>
        {
            var cfg = opts.Value;

            logger.LogInformation("🟢 ENDPOINT /api/track ACIONADO! Code: {Code}", req.Code);

            if (string.IsNullOrWhiteSpace(req.Code))
            {
                logger.LogWarning("📛 Requisição sem código do IP {IP}",
                    http.Connection.RemoteIpAddress);
                return Results.BadRequest(new { error = "Código obrigatório" });
            }

            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!await rateLimitService.TryIncrementAsync($"ip:{ip}", cfg.RateLimit.PerIpLimit, cfg.RateLimit.PerIpWindowSeconds))
            {
                logger.LogWarning("🚫 Rate limit IP {IP}", ip);
                return Results.StatusCode(429);
            }

            if (!await rateLimitService.TryIncrementAsync($"code:{req.Code}", cfg.RateLimit.PerCodeLimit, cfg.RateLimit.PerCodeWindowSeconds))
            {
                logger.LogWarning("🚫 Rate limit código {Code}", req.Code);
                return Results.StatusCode(429);
            }

            if (cfg.RequireTurnstile)
            {
                if (string.IsNullOrEmpty(req.Token))
                {
                    logger.LogWarning("🤖 CAPTCHA ausente");
                    return Results.BadRequest(new { error = "Verificação obrigatória" });
                }

                var captchaOk = await trackingService.VerifyTurnstileAsync(req.Token, ip);
                if (!captchaOk)
                {
                    logger.LogWarning("❌ CAPTCHA falhou");
                    return Results.BadRequest(new { error = "Verificação falhou" });
                }
            }

            try
            {
                logger.LogInformation("🔵 Consultando TrackingService para: {Code}", req.Code);

                var apiResponse = await trackingService.GetTrackingAsync(req.Code);

                if (apiResponse == null)
                {
                    logger.LogWarning("⚠️ TrackingService retornou NULL");
                    return Results.NotFound(new { error = "Não encontrado" });
                }

                logger.LogInformation("🔵 TrackingService OK - Message: {Message}", apiResponse.Message);

                if (apiResponse.Message != "OK")
                {
                    logger.LogInformation("ℹ️ Mensagem: {Message}", apiResponse.Message);

                    if (apiResponse.Message.Contains("não localizado", StringComparison.OrdinalIgnoreCase))
                    {
                        return Results.NotFound(new
                        {
                            error = "CPF ou e-mail não localizado",
                            message = apiResponse.Message
                        });
                    }

                    return Results.Ok(new
                    {
                        message = apiResponse.Message,
                        found = false
                    });
                }

                if (apiResponse.ShippingEvents == null ||
                    !apiResponse.ShippingEvents.Any(e => e.DtShipping.HasValue && !string.IsNullOrEmpty(e.DsCode)))
                {
                    logger.LogWarning("⚠️ Sem eventos válidos");
                    return Results.NotFound(new
                    {
                        error = "Sem eventos",
                        orderInfo = apiResponse.Info
                    });
                }

                var response = new FrontendTrackingResponse
                {
                    OrderNumber = apiResponse.Info?.Number,
                    OrderDate = apiResponse.Info?.Date,
                    PredictionDate = apiResponse.Info?.Prediction,
                    IdErp = apiResponse.Info?.IdErp,
                    Message = apiResponse.Message,
                    Events = apiResponse.ShippingEvents
                        .Where(e => e.DtShipping.HasValue && !string.IsNullOrEmpty(e.DsCode))
                        .OrderByDescending(e => e.DtShipping!.Value)
                        .Select(e => new FrontendEvent
                        {
                            Date = e.DtShipping!.Value.ToString("dd/MM/yyyy HH:mm"),
                            Status = e.DsCode,
                            Description = e.Message,
                            Complement = e.Complement,
                            InternalCode = e.InternalCode
                        })
                        .ToList()
                };

                logger.LogInformation("✅ Sucesso: {Code} - {Count} eventos", req.Code, response.Events.Count);

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "💥 Erro ao processar {Code}", req.Code);
                return Results.Problem(detail: $"Erro: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("TrackOrder")
        .WithTags("Rastreamento");

        Log.Information("   ✅ POST /api/track");

        // 2️⃣ Diagnóstico (DEV)
        if (app.Environment.IsDevelopment())
        {
            endpoints.MapRateLimitEndpoints(); // 👈 CORRIGIDO: usa 'endpoints' ao invés de 'app'
            Log.Information("   ✅ Rate Limit Endpoints");
        }

        // 3️⃣ Blazor Hub
        endpoints.MapBlazorHub();
        Log.Information("   ✅ Blazor Hub");

        // 4️⃣ Fallback (ÚLTIMO!)
        endpoints.MapFallbackToPage("/_Host");
        Log.Information("   ✅ Fallback /_Host");
    });

    Log.Information("✅ TrackingWeb iniciado com sucesso!");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 Aplicação falhou ao iniciar");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;