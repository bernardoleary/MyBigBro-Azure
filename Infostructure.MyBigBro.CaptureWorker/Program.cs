using System;
using Infostructure.MyBigBro.BusinessLogic.GeoSpatial;
using Infostructure.MyBigBro.BusinessLogic.WebCam;
using Infostructure.MyBigBro.CaptureWorker;
using Infostructure.MyBigBro.DataModel.DataAccess;
using Infostructure.MyBigBro.ImageStorageServiceAgent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

var connectionString = builder.Configuration.GetConnectionString("MyBigBroPostgres")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__MyBigBroPostgres")
    ?? "Host=localhost;Port=5433;Database=mybigbro;Username=mybigbro;Password=mybigbro_dev_pwd";

builder.Services.AddDbContext<MyBigBroContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
           .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IMyBigBroContext>(sp => sp.GetRequiredService<MyBigBroContext>());
builder.Services.AddScoped<IMyBigBroRepository>(sp =>
    new MyBigBroRepository(sp.GetRequiredService<IMyBigBroContext>()));
builder.Services.AddScoped<ILocation>(sp =>
    new Location(sp.GetRequiredService<IMyBigBroRepository>()));
builder.Services.AddScoped<IGeometry, Geometry>();
builder.Services.AddScoped<IStorageServiceAgent, ConfigurableStorageServiceAgent>();
builder.Services.AddScoped<IWebCamControl>(sp =>
    new WebCamControl(
        sp.GetRequiredService<IStorageServiceAgent>(),
        sp.GetRequiredService<IMyBigBroRepository>(),
        sp.GetRequiredService<IGeometry>()));

builder.Services.AddHostedService<CaptureJobWorker>();

var host = builder.Build();
host.Run();
