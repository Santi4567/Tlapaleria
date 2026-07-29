using System.Security.Claims;

namespace Api_Tlapaleria.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var claimId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int userId))
            {
                // Al lanzar esta excepción, el Middleware global la atrapará automáticamente
                throw new UnauthorizedAccessException("Token inválido o no contiene la identidad del usuario.");
            }

            return userId;
        }
    }
}