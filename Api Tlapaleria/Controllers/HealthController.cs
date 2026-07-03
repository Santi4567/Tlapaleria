using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous] // No requiere token
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetStatus()
        {
            // Regresamos un JSON anónimo súper rápido
            return Ok(new
            {
                status = "online",
                message = "API LEO funcionando correctamente.",
                version = "1.0",
                timestamp = DateTime.Now,
                system = "s4lm0.exe"
            });
        }
    }
}