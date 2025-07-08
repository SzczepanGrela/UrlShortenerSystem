using UrlShortener.API.Extensions;
using UrlShortener.API.Middleware;
using UrlShortener.API.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/urlshortener-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add custom services
builder.Services.AddUrlShortenerServices(builder.Configuration);

// Add background services
builder.Services.AddHostedService<LinkCleanupService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add correlation ID middleware (before rate limiting)
app.UseMiddleware<CorrelationIdMiddleware>();

// Add rate limiting middleware
app.UseRateLimiting();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
