using Api_Tlapaleria.Data;
using Api_Tlapaleria.Services; // Necesario para AuthService
using Microsoft.AspNetCore.Authentication.JwtBearer; // Necesario para JWT
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens; // Necesario para validar el token
using System.Security.Claims; // Necesario para el escudo de peticiones 
using System.Text;
using System.Text.Json.Serialization; 
using System.Threading.RateLimiting; // Necesario para el escudo de peticiones 
using Microsoft.AspNetCore.RateLimiting;  // Necesario para el escudo de peticiones 


internal class Program
{
    private static void Main(string[] args)
    {
        // --- PANTALLA DE CARGA ---
        Console.WriteLine(@"
    ___    ____  ____    __    ____   ____ 
   / _ \  |  _ \(_  _)  |  )  (  __) /    \
  / ___ \ |  __/ _)(_   | (_/\ | _) |  ()  |
 /_/   \_\|_)   (____)  \____/(____) \____/  
");
        Console.WriteLine("ejecutando...");
        Console.WriteLine("versión 1.3\n");
        // -------------------------

        var builder = WebApplication.CreateBuilder(args);

        // 1. BASE DE DATOS (Esto ya lo tenías)
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<TlapaleriaContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        });

        //Evita bucles de lectura en json(NO BORRAR/DONT DELETE)
        builder.Services.AddControllers().AddJsonOptions(x =>
        {
            // Ignora los ciclos infinitos en toda la API
            x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

        //CORS 
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("PoliticaFrontend", policy =>
            {
                policy.SetIsOriginAllowed(origin => true) // <-- ¡LA MAGIA! Acepta literalmente cualquier IP o URL
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials(); // Mantiene la seguridad de tus cookies intacta
            });
        });

        // 2. INYECCIÓN DE DEPENDENCIAS 
        // Aquí registramos los servicios
        builder.Services.AddScoped<AuthService>();

        //Servicio de Usuarios
        builder.Services.AddScoped<IUserService, UserService>();

        //Servicio de suppliers
        builder.Services.AddScoped<ISupplierService, SupplierService>();

        //Servicio de Productos
        builder.Services.AddScoped<IProductService, ProductService>();

        // Servicio de Pedidos
        builder.Services.AddScoped<IPendingOrderService, PendingOrderService>();

        //Servicio de Kardex de Productos 
        builder.Services.AddScoped<IInventoryService, InventoryService>();

        //Servicio de venta(Sales service)
        builder.Services.AddScoped<ISaleService, SaleService>();

        //Servicio de Reembolsos 
        builder.Services.AddScoped<IReturnService, ReturnService>();

        // PermissionService
        builder.Services.AddScoped<PermissionService>();


        //CONFIGURACIÓN DE JWT Y COOKIES
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

                    // 3.RESPUESTA DE ERROR (Para que salga success: false en JSON)
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

        // --- ESCUDO: RATE LIMITER (BLINDAJE CONTRA ABUSOS Y DDoS) ---
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Qué hacer cuando detectamos el abuso (La alerta)
            options.OnRejected = async (context, token) =>
            {
                var httpContext = context.HttpContext;

                // Extraemos al culpable (Su ID de usuario del token o su IP si es anónimo)
                var ipInfractor = httpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconocida";
                var usuarioId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? httpContext.User.FindFirst("id")?.Value
                                ?? "Usuario Anónimo";

                // Imprimimos la alerta roja en la consola / logs
                var logger = httpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("⚠️ ALERTA DE SEGURIDAD: El usuario '{UserId}' desde la IP [{IP}] excedió el límite de peticiones (Posible DoS).", usuarioId, ipInfractor);

                // Respuesta JSON estandarizada con la estructura de tu API
                httpContext.Response.ContentType = "application/json";
                var jsonResponse = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Has excedido el límite de solicitudes permitidas. Por seguridad, espera un momento antes de reintentar.",
                    data = (object)null
                });

                await httpContext.Response.WriteAsync(jsonResponse, cancellationToken: token);
            };

            // Regla matemática: Límite por Usuario Logueado o por IP
            options.AddPolicy("ProteccionAbuso", httpContext =>
            {
                // Obtenemos una clave única para identificar quién hace la petición
                var partitionKey = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                   ?? httpContext.User.FindFirst("id")?.Value
                                   ?? httpContext.Connection.RemoteIpAddress?.ToString()
                                   ?? "cliente_general";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 60, // Límite: 60 peticiones...
                        Window = TimeSpan.FromMinutes(1) // ... por cada 1 minuto.
                    });
            });
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

        // --- POLITICAS DE CORS ---
        app.UseCors("PoliticaFrontend");

        // --- ACTIVAMOS EL ESCUDO LIMITADOR DE PETICIONES ---
        app.UseRateLimiter();

        // 4. ACTIVAR LA SEGURIDAD
        // El orden importa: Primero Authenticate (¿Quién eres?) luego Authorize (¿Tienes permiso?)
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();


        // --- INTERCEPTOR DE ARRANQUE EXITOSO ---
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.Clear(); // Limpiamos todo el ruido de los logs de Entity Framework

            Console.ForegroundColor = ConsoleColor.Cyan; // Un poco de color para la terminal
            Console.WriteLine(@"
    ___    ____  ____    __    ____   ____ 
   / _ \  |  _ \(_  _)  |  )  (  __) /    \
  / ___ \ |  __/ _)(_   | (_/\ | _) |  ()  |
 /_/   \_\|_)   (____)  \____/(____) \____/ 
    ");
            Console.ResetColor();

            Console.WriteLine("Running...");
            Console.WriteLine("version 1.3\n");

            // --- LEEMOS Y MOSTRAMOS LOS PUERTOS ACTIVOS ---
            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var url in app.Urls)
            {
                Console.WriteLine($"[+] Escuchando en: {url}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\ns4lm0.exe\n");
            Console.ResetColor();
        });

        // Arrancamos la API
        app.Run();
    }
}