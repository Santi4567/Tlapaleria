using System.Security.Claims;
using Api_Tlapaleria.Attributes;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Extensions;
using Api_Tlapaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Tlapaleria.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WorkTasksController : ControllerBase
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly PermissionService _permissionService;

        public WorkTasksController(IWorkTaskService workTaskService, PermissionService permissionService)
        {
            _workTaskService = workTaskService;
            _permissionService = permissionService;
        }

        // 1. OBTENER LAS TAREAS DEL TRABAJADOR LOGUEADO (Cualquier trabajador autenticado)
        [HttpGet("my")]
        public async Task<ActionResult<ApiResponse<List<WorkTaskDto>>>> GetMyTasks([FromQuery] string? status = null)
        {
            int userId = User.GetUserId();
            var tareas = await _workTaskService.GetMyTasksAsync(userId, status);
            return Ok(ApiResponse<List<WorkTaskDto>>.Exito(tareas));
        }

        // 2. ACTUALIZAR ESTADO / NOTA DE UNA TAREA (El propio trabajador o alguien con add.work)
        [HttpPut("{id}/status")]
        public async Task<ActionResult<ApiResponse<WorkTaskDto>>> UpdateTaskStatus(
            int id,
            [FromBody] UpdateWorkTaskStatusDto dto)
        {
            int userId = User.GetUserId();
            string rol = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            bool canManageAll = await _permissionService.UserHasPermissionAsync(rol, "add.work");

            var actualizada = await _workTaskService.UpdateTaskStatusAsync(id, dto, userId, canManageAll);
            return Ok(ApiResponse<WorkTaskDto>.Exito(actualizada, "Estado de la tarea actualizado."));
        }

        // 3. OBTENER TODAS LAS TAREAS DEL PERSONAL (Requiere permiso add.work)
        [HttpGet]
        [RequierePermiso("add.work")]
        public async Task<ActionResult<ApiResponse<PagedResponse<WorkTaskDto>>>> GetAllTasks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] int? assignedToUserId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? priority = null,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var resultado = await _workTaskService.GetAllTasksAsync(page, pageSize, assignedToUserId, status, priority, search);
            return Ok(ApiResponse<PagedResponse<WorkTaskDto>>.Exito(resultado));
        }

        // 4. OBTENER LISTA DE TRABAJADORES ACTIVOS PARA ASIGNAR TAREAS (Requiere permiso add.work)
        [HttpGet("workers")]
        [RequierePermiso("add.work")]
        public async Task<ActionResult<ApiResponse<List<WorkWorkerItemDto>>>> GetAssignableWorkers()
        {
            var trabajadores = await _workTaskService.GetAssignableWorkersAsync();
            return Ok(ApiResponse<List<WorkWorkerItemDto>>.Exito(trabajadores));
        }

        // 5. CREAR Y ASIGNAR UNA TAREA (Requiere permiso add.work)
        [HttpPost]
        [RequierePermiso("add.work")]
        public async Task<ActionResult<ApiResponse<WorkTaskDto>>> CreateTask([FromBody] CreateWorkTaskDto dto)
        {
            int assignedByUserId = User.GetUserId();
            var creada = await _workTaskService.CreateTaskAsync(dto, assignedByUserId);
            return Ok(ApiResponse<WorkTaskDto>.Exito(creada, "Tarea asignada exitosamente."));
        }

        // 6. MODIFICAR / REASIGNAR UNA TAREA (Requiere permiso add.work)
        [HttpPut("{id}")]
        [RequierePermiso("add.work")]
        public async Task<ActionResult<ApiResponse<WorkTaskDto>>> UpdateTask(int id, [FromBody] UpdateWorkTaskDto dto)
        {
            var actualizada = await _workTaskService.UpdateTaskAsync(id, dto);
            return Ok(ApiResponse<WorkTaskDto>.Exito(actualizada, "Tarea actualizada exitosamente."));
        }

        // 7. QUITAR UNA TAREA (Requiere permiso add.work)
        [HttpDelete("{id}")]
        [RequierePermiso("add.work")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTask(int id)
        {
            var eliminada = await _workTaskService.DeleteTaskAsync(id);
            return Ok(ApiResponse<bool>.Exito(eliminada, "Tarea retirada exitosamente."));
        }
    }
}
