using Api_Tlapaleria.Data;
using Api_Tlapaleria.Services; // Necesario para AuthService
using Microsoft.AspNetCore.Authentication.JwtBearer; // Necesario para JWT
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens; // Necesario para validar el token
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. BASE DE DATOS (Esto ya lo tenías)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TlapaleriaContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// 2. INYECCIÓN DE DEPENDENCIAS (NUEVO)
// Aquí registramos tu servicio de Login para poder usarlo en el Controller
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<Api_Tlapaleria.Services.IUserService, Api_Tlapaleria.Services.UserService>();

// Y asegúrate de haber registrado el PermissionService también:
builder.Services.AddScoped<Api_Tlapaleria.Services.PermissionService>();

// 3. CONFIGURACIÓN DE JWT Y COOKIES (NUEVO - ¡IMPORTANTE!)
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

        options.Events = new JwtBearerEvents
        {
            // 1. Leer Cookie (Igual que antes)
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(token)) context.Token = token;
                return Task.CompletedTask;
            },

            // 2. VALIDACIONES DE SEGURIDAD (Activo + Rol)
            OnTokenValidated = async context =>
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<TlapaleriaContext>();
                var userIdClaim = context.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                var userRoleClaim = context.Principal.FindFirst(System.Security.Claims.ClaimTypes.Role); // <--- Leemos el rol del token

                if (userIdClaim == null || userRoleClaim == null)
                {
                    context.Fail("Token corrupto");
                    return;
                }

                var userId = int.Parse(userIdClaim.Value);
                var tokenRole = userRoleClaim.Value;

                // Buscamos al usuario: Su estado y su rol actual
                var user = await dbContext.Users
                    .AsNoTracking()
                    .Include(u => u.Rol) // Importante cargar la relación
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, NombreRol = u.Rol.Nombre }) // Seleccionamos el nombre explícitamente
                    .FirstOrDefaultAsync();

                // Validación A: ¿Existe y está activo?
                if (user == null || !user.IsActive)
                {
                    context.Fail("Tu cuenta ha sido desactivada.");
                    return;
                }

                // Validación B: ¿El rol coincide?
                // Comparamos el rol del token contra el nombre que viene de la BD
                if (user.NombreRol != tokenRole)
                {
                    context.Fail("Roles inconsistentes, vuelve a iniciar sesión");
                    return;
                }
            },

            // 3. PERSONALIZAR RESPUESTA DE ERROR (Para que salga success: false en JSON)
            OnChallenge = context =>
            {
                // Esto evita el comportamiento por defecto (que solo manda un 401 vacío)
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                // Usamos nuestra clase estándar para responder el error
                var mensajeError = context.AuthenticateFailure?.Message ?? "No estás autorizado";

                // Creamos el JSON manualmente porque estamos a bajo nivel en el middleware
                var jsonResponse = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    message = mensajeError,
                    data = (object)null
                });

                return context.Response.WriteAsync(jsonResponse);
            }
        };
    });
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 4. ACTIVAR LA SEGURIDAD (NUEVO)
// El orden importa: Primero Authenticate (¿Quién eres?) luego Authorize (¿Tienes permiso?)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();