using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// === 1. НАСТРОЙКИ ЗАЩИТЫ (JWT) ===
var jwtKey = "SUPER_SECRET_KEY_12345_MUST_BE_VERY_LONG_STRING"; 

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
        ValidateIssuer = false, // В учебных целях отключаем проверку издателя
        ValidateAudience = false // В учебных целях отключаем проверку получателя
    };
});

// === 2. ПОДКЛЮЧЕНИЕ СЕРВИСОВ ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Разрешаем запросы с Фронтенда (CORS)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// === 3. ЗАПУСК ПРИЛОЖЕНИЯ ===
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// ВАЖНО: Сначала Authentication (Кто ты?), потом Authorization (Можно ли тебе?)
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

app.Run();