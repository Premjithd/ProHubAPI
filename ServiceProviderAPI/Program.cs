using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceProviderAPI.Data;
using ServiceProviderAPI.Services;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("🔧 Starting application...");

try
{
    Console.WriteLine("📦 Adding DbContext...");
    // Add services to the container.
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"📍 Connection string: {(string.IsNullOrEmpty(connectionString) ? "NOT FOUND" : "OK")}");
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.CommandTimeout(30); // Set 30 second timeout for database commands
        });
    });

    Console.WriteLine("🔐 Adding JWT services...");
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IVerificationService, VerificationService>();

    // Add HttpClient for external API calls (e.g., Nominatim address service)
    builder.Services.AddHttpClient();

    Console.WriteLine("🎮 Adding controllers...");
    // Add controllers with JSON serializer options to handle circular references
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    Console.WriteLine("🔑 Adding authentication...");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
            };
        });

    Console.WriteLine("📚 Adding Swagger...");
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    Console.WriteLine("🌐 Adding CORS...");
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngular", policy =>
        {
            //policy.WithOrigins("http://localhost:4200")
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    Console.WriteLine("🏗️ Building application...");
    var app = builder.Build();

    Console.WriteLine("✅ Application built successfully");

    // Use CORS
    app.UseCors("AllowAngular");

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Console.WriteLine("🚀 Starting web host...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Fatal error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
    throw;
}
