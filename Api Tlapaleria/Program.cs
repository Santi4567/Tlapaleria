using Api_Tlapaleria.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- INICIO DE LA CONFIGURACIÓN DE BASE DE DATOS ---

// 1. Leemos la cadena del appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. Inyectamos el contexto usando el driver de Pomelo MySql
builder.Services.AddDbContext<TlapaleriaContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// --- FIN DE LA CONFIGURACIÓN ---

builder.Services.AddControllers();
// ... resto del código ...