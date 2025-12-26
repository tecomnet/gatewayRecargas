using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TecomNet.Domain.Service.Services;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.Sql;
using TecomNet.Infrastructure.WebServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "GateWay Recargas API", 
        Version = "v1.0" 
    });
    
    // Usar el atributo [Tags] si está disponible, sino usar nombre del controlador
    c.TagActionsBy(api =>
    {
        // Buscar el atributo [Tags] en los metadatos
        var controllerType = api.ActionDescriptor.RouteValues["controller"];
        
        // Si el controlador tiene atributo [Tags], usarlo
        // Por ahora, usaremos el nombre del controlador y el DocumentFilter ordenará
        return new[] { controllerType ?? "Default" };
    });
    
    // Configurar orden de tags explícitamente
    c.DocumentFilter<GateWayRecargas.Filters.SwaggerTagOrderDocumentFilter>();
    
    // Configurar JWT en Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Ingresa tu token JWT obtenido del endpoint de login. Solo pega el token sin la palabra 'Bearer'.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

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

// Servicio de autenticación
builder.Services.AddScoped<IAuthService, TecomNet.Domain.Service.Services.AuthService>();

// Configurar JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey no configurado");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();



    app.UseSwagger();
    app.UseSwaggerUI();


app.UseHttpsRedirection();

// IMPORTANTE: UseAuthentication debe ir ANTES de UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
