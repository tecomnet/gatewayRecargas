using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using TecomNet.Domain.Service.Services;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.Sql;
using TecomNet.Infrastructure.WebServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar Entity Framework con retry policy para errores transitorios y timeout
builder.Services.AddDbContext<TecomNetDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions =>
        {
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
            sqlServerOptions.CommandTimeout(30); // Timeout de 30 segundos para comandos SQL
        }));

builder.Services.AddHttpClient<TecomNet.Infrastructure.WebServices.AltanApiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IAltanApiService, TecomNet.Domain.Service.Services.AltanApiService>();

// Servicios de reportes y SFTP
builder.Services.AddScoped<TecomNet.DomainService.Core.Services.ISftpClientService, SftpClientService>();
builder.Services.AddScoped<TecomNet.DomainService.Core.Services.IReporteRecargasService, ReporteRecargasService>();

// Configurar Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Hangfire Dashboard (solo en desarrollo)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new GateWayRecargas.HangfireAuthorizationFilter() }
    });
}

app.UseHttpsRedirection();

// Hangfire Dashboard para producción (opcional, con autenticación)
// app.UseHangfireDashboard("/hangfire", new DashboardOptions { ... });

// Programar job diario
RecurringJob.AddOrUpdate<GateWayRecargas.BackgroundJobs.GenerarReporteDiarioJob>(
    "generar-reporte-diario",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily(1, 0), // Ejecutar diariamente a la 1:00 AM (hora UTC)
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

app.UseAuthorization();

app.MapControllers();

app.Run();
