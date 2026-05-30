using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.Hubs; 
using DashboardApi.Services;
using Jering.Javascript.NodeJS;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using System.Text;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

builder.Host.UseNLog();

// --- 1. CONFIG & DB ---
var dbPath = builder.Configuration["ConnectionStrings:DefaultConnection"] ?? "Data Source=dashboard.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(dbPath));

// --- 2. SERVICES ---
builder.Services.AddHttpClient();
builder.Services.AddNodeJS();
builder.Services.AddSignalR();
builder.Services.AddHostedService<ReminderBackgroundService>();
builder.Services.AddHostedService<MonitoringBackgroundService>();
builder.Services.AddHostedService<SmartPollerService>();

builder.Services.AddScoped<MorningReportService>();
builder.Services.AddScoped<EdgeTtsService>();
builder.Services.AddScoped<GeminiPodcastService>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CalendarAggregatorService>();
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddScoped<EmailService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // ВАЖНО: Укажите здесь IP-адреса ваших прокси (Nginx, балансировщики), 
    // чтобы никто извне не мог подделать IP. 
    // Если прокси нет, можно разрешить всё (но это менее безопасно):
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// --- 3. JWT AUTHENTICATION ---
var key = builder.Configuration["Jwt:Key"] ?? "vash_ochen_dlinniy_secret_key_dlya_podpisi_tokena_at_least_32_chars";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "DashboardBackend";
var audience = builder.Configuration["Jwt:Audience"] ?? "DashboardFrontend";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
        // Разрешаем SignalR передавать токен через QueryString (это нужно для WebSockets)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// --- 4. CONTROLLERS ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- 5. SWAGGER ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Dashboard API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

app.UseForwardedHeaders(); // ОБЯЗАТЕЛЬНО вызвать здесь


// --- 6. INIT DB & SAFE MIGRATIONS ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var connString = builder.Configuration["ConnectionStrings:DefaultConnection"] ?? "Data Source=dashboard.db";
    var dbFilePath = connString.Replace("Data Source=", "").Trim();
    var backupFilePath = dbFilePath + ".bak";

    try
    {
        // 1. Делаем бекап, если база уже существует
        if (System.IO.File.Exists(dbFilePath))
        {
            System.IO.File.Copy(dbFilePath, backupFilePath, overwrite: true);
            logger.Info("DB Backup created successfully.");
        }

        // 2. Накатываем миграции (ВМЕСТО EnsureCreated!)
        var pendingMigrations = db.Database.GetPendingMigrations();
        if (pendingMigrations.Any())
        {
            logger.Info($"Applying {pendingMigrations.Count()} pending migrations...");
            db.Database.Migrate(); // Применяет миграции
            logger.Info("Migrations applied successfully.");
        }

        // Удаляем бекап при успехе (по желанию можно оставить)
        if (System.IO.File.Exists(backupFilePath))
            System.IO.File.Delete(backupFilePath);
    }
    catch (Exception ex)
    {
        logger.Fatal(ex, "FATAL ERROR during migrations! Rolling back database...");

        // 3. Восстанавливаем базу из бекапа при падении
        if (System.IO.File.Exists(backupFilePath))
        {
            System.IO.File.Copy(backupFilePath, dbFilePath, overwrite: true);
            logger.Fatal("Database restored from backup.");
        }

        // Роняем приложение, так как состояние БД непредсказуемо
        Environment.Exit(1);
    }

    // Инициализация дефолтного админа
    if (!db.Users.Any())
    {
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = auth.HashPassword("123"),
            IsAdmin = true
        });
        db.SaveChanges();
    }
}

// --- 7. PIPELINE ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(x => x
    .WithOrigins("http://localhost:5173") // ВАЖНО: Для SignalR нужно явно указать Origin фронта (или AllowAnyOrigin, но с Credentials аккуратнее)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials()); // <--- ВАЖНО для SignalR

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Маппим Хаб
app.MapHub<NotificationHub>("/api/hub/notifications"); // <--- NEW

app.MapFallbackToFile("index.html");
app.Run();