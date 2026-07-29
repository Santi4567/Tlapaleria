using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Models; // Asegúrate de importar el namespace donde está ApiResponse
using System.Net;
using System.Text.Json;

namespace Api_Tlapaleria.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Deja que la petición siga hacia los controladores
                await _next(context);
            }
            catch (Exception ex)
            {
                // Si explota en CUALQUIER parte, se atrapa aquí
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            // Por defecto es un Error 400 (BadRequest)
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            // Personalización de códigos HTTP basados en el tipo de error
            if (exception is UnauthorizedAccessException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
            else if (exception.Message.Contains("no fue encontrado") || exception.Message.Contains("no existe"))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            // Se arma la respuesta con tu ApiResponse
            var response = ApiResponse<object>.Error(exception.Message);

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return context.Response.WriteAsync(jsonResponse);
        }
    }
}