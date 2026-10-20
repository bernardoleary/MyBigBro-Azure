using System;
using System.IO;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.Services;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.ImageStorageServiceAgent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Configure StorageOptions from "Storage" configuration section or environment variables
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

// Configure PostgreSQL + PostGIS DbContext (Scoped)
var connectionString = builder.Configuration.GetConnectionString("MyBigBroPostgres")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__MyBigBroPostgres")
    ?? "Host=localhost;Port=5433;Database=mybigbro;Username=mybigbro;Password=mybigbro_dev_pwd";

builder.Services.AddDbContext<MyBigBroContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
           .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IMyBigBroContext>(sp => sp.GetRequiredService<MyBigBroContext>());
builder.Services.AddScoped<IMyBigBroRepository>(sp =>
    new MyBigBroRepository(sp.GetRequiredService<IMyBigBroContext>()));

// Register BusinessLogic and Storage services
builder.Services.AddScoped<ILocation>(sp =>
    new Location(sp.GetRequiredService<IMyBigBroRepository>()));
builder.Services.AddScoped<IGeometry, Geometry>();
builder.Services.AddScoped<IStorageServiceAgent, ConfigurableStorageServiceAgent>();
builder.Services.AddScoped<IWebCamControl>(sp =>
    new WebCamControl(
        sp.GetRequiredService<IStorageServiceAgent>(),
        sp.GetRequiredService<IMyBigBroRepository>(),
        sp.GetRequiredService<IGeometry>()));
builder.Services.AddScoped<IGeoMarkerService>(sp =>
    new GeoMarkerService(
        sp.GetRequiredService<IWebCamControl>(),
        sp.GetRequiredService<ILocation>(),
        sp.GetRequiredService<IMyBigBroRepository>()));
builder.Services.AddScoped<IAccountService>(sp =>
    new AccountService(sp.GetRequiredService<IMyBigBroRepository>()));
builder.Services.AddScoped<IFormsAuthenticationService, FormsAuthenticationService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Preserve PascalCase JSON serialization for AngularJS frontend and Android app compatibility
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

var app = builder.Build();

app.UseCors();

// Serve static frontend files if hosted directly from Infostructure.MyBigBro.Web4
var contentRoot = app.Environment.ContentRootPath;
if (File.Exists(Path.Combine(contentRoot, "index.html")))
{
    var fileProvider = new PhysicalFileProvider(contentRoot);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        DefaultFileNames = new[] { "index.html" }
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider
    });
}

app.MapGet("/healthz", async (MyBigBroContext db) =>
{
    bool canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "Healthy", database = "PostgreSQL" })
        : Results.Problem("Database connection failed", statusCode: 503);
});

app.MapControllers();

app.Run();
