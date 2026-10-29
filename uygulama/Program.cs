using System.Text.Json;
using YemekhaneApi.Data;
using YemekhaneApi.Repositories;
using YemekhaneApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddSingleton<RedisService>();
builder.Services.AddScoped<YemekhaneRepository>();

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new
{
    durum = "healthy",
    servis = "uygulama-web-api",
    zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
}));

app.MapControllers();

app.Run();

