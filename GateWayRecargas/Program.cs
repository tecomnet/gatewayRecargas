using Microsoft.EntityFrameworkCore;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.Sql;

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
