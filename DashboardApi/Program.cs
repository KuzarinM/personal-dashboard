using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.Hubs; 
using DashboardApi.Services;
using Jering.Javascript.NodeJS;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CalendarAggregatorService>();
builder.Services.AddSingleton<TelegramService>();
builder.Services.AddScoped<EmailService>();

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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All
});

// --- 6. INIT DB ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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