using Gastos.Application.Interfaces;
using Gastos.Application.Services;
using Gastos.Domain.Interfaces;
using Gastos.Infrastructure.Data;
using Gastos.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IDespesaRepository, DespesaRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IForgotPasswordRepository, ForgotPasswordRepository>();
builder.Services.AddScoped<IRecuperacaoSenhaRepository, RecuperacaoSenhaRepository>();

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

var issuer = builder.Configuration["Jwt:Issuer"] ??
             throw new ArgumentNullException("A chave 'Jwt:Issuer' n�o foi encontrada nas configura��es.");
var audience = builder.Configuration["Jwt:Audience"] ??
               throw new ArgumentNullException("A chave 'Jwt:Audience' n�o foi encontrada nas configura��es.");
var accessSecret = builder.Configuration["Jwt:AccessSecret"] ??
                   throw new ArgumentNullException("A chave 'Jwt:AccessSecret' n�o foi encontrada nas configura��es.");

var accessKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessSecret));

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
            IssuerSigningKey = accessKey
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Token inv�lido: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"Token v�lido para: {context.Principal.Identity.Name}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddCors(p => p.AddPolicy("corsapp", policy =>
{
    policy
        .WithOrigins("https://gastosservice-ovgk.onrender.com")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
}));

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Gastos API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira o token JWT no campo abaixo.\nExemplo: Bearer <seu_token>"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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

builder.Services.AddControllers();
builder.Services.AddAuthorization();

var app = builder.Build();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");

if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("corsapp");
app.UseRouting();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => "OK");

if (app.Environment.IsProduction())
{
    var timer = new System.Threading.Timer(async _ =>
    {
        try
        {
            using var client = new HttpClient();
            var url = "https://gastosservice-ovgk.onrender.com/health";
            var response = await client.GetAsync(url);

            Console.WriteLine($"[KeepAlive] {DateTime.Now}: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KeepAlive ERRO] {ex.Message}");
        }
    }, null, TimeSpan.Zero, TimeSpan.FromMinutes(14));
}

app.Run();
