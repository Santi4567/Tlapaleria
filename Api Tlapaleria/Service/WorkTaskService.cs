using Api_Tlapaleria.Data;
using Api_Tlapaleria.DTOs;
using Api_Tlapaleria.Enums;
using Api_Tlapaleria.Hubs;
using Api_Tlapaleria.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api_Tlapaleria.Services
{
    public class WorkTaskService : IWorkTaskService
    {
        private readonly TlapaleriaContext _context;
        private readonly IHubContext<InventoryHub> _hubContext;

        public WorkTaskService(TlapaleriaContext context, IHubContext<InventoryHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private static WorkTaskDto MapToDto(WorkTask t)
        {
            return new WorkTaskDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToUserName = t.AssignedToUser?.Name ?? $"Usuario #{t.AssignedToUserId}",
                AssignedByUserId = t.AssignedByUserId,
                AssignedByUserName = t.AssignedByUser?.Name ?? $"Usuario #{t.AssignedByUserId}",
                DueDate = t.DueDate,
                WorkerNotes = t.WorkerNotes,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            };
        }

        private async Task NotifyTasksUpdatedAsync(int assignedToUserId, string action, int taskId)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("WorkTasksUpdated", new
                {
                    taskId,
                    assignedToUserId,
                    action
                });
            }
            catch
            {
                // Si no hay clientes conectados al hub, no interrumpir la operación
            }
        }

        public async Task<List<WorkTaskDto>> GetMyTasksAsync(int userId, string? status = null)
        {
            var query = _context.WorkTasks
                .AsNoTracking()
                .Include(t => t.AssignedToUser)
                .Include(t => t.AssignedByUser)
                .Where(t => t.IsActive && t.AssignedToUserId == userId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
            }

            var list = await query
                .OrderBy(t => t.Status == WorkTaskStatus.Completada.ToString() || t.Status == WorkTaskStatus.Cancelada.ToString() ? 1 : 0)
                .ThenByDescending(t => t.Priority == WorkTaskPriority.Urgente.ToString() ? 2 : t.Priority == WorkTaskPriority.Normal.ToString() ? 1 : 0)
                .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();

            return list.Select(MapToDto).ToList();
        }

        public async Task<PagedResponse<WorkTaskDto>> GetAllTasksAsync(
            int pageNumber,
            int pageSize,
            int? assignedToUserId = null,
            string? status = null,
            string? priority = null,
            string? search = null)
        {
            var query = _context.WorkTasks
                .AsNoTracking()
                .Include(t => t.AssignedToUser)
                .Include(t => t.AssignedByUser)
                .Where(t => t.IsActive)
                .AsQueryable();

            if (assignedToUserId.HasValue && assignedToUserId.Value > 0)
            {
                query = query.Where(t => t.AssignedToUserId == assignedToUserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(priority))
            {
                query = query.Where(t => t.Priority == priority);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(t =>
                    t.Title.Contains(term) ||
                    (t.Description != null && t.Description.Contains(term)) ||
                    (t.AssignedToUser != null && t.AssignedToUser.Name.Contains(term)));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var tasks = await query
                .OrderBy(t => t.Status == WorkTaskStatus.Completada.ToString() || t.Status == WorkTaskStatus.Cancelada.ToString() ? 1 : 0)
                .ThenByDescending(t => t.Priority == WorkTaskPriority.Urgente.ToString() ? 2 : t.Priority == WorkTaskPriority.Normal.ToString() ? 1 : 0)
                .ThenByDescending(t => t.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResponse<WorkTaskDto>
            {
                Data = tasks.Select(MapToDto).ToList(),
                TotalItems = totalItems,
                TotalPages = Math.Max(1, totalPages),
                CurrentPage = pageNumber
            };
        }

        public async Task<List<WorkWorkerItemDto>> GetAssignableWorkersAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Include(u => u.Rol)
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .Select(u => new WorkWorkerItemDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Username = u.Username,
                    Rol = u.Rol != null ? u.Rol.Nombre : string.Empty
                })
                .ToListAsync();
        }

        public async Task<WorkTaskDto> CreateTaskAsync(CreateWorkTaskDto dto, int assignedByUserId)
        {
            bool workerExists = await _context.Users.AnyAsync(u => u.Id == dto.AssignedToUserId && u.IsActive);
            if (!workerExists)
                throw new Exception("El trabajador seleccionado no existe o está inactivo.");

            var nuevaTarea = new WorkTask
            {
                Title = dto.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                Priority = dto.Priority,
                Status = WorkTaskStatus.Pendiente.ToString(),
                AssignedToUserId = dto.AssignedToUserId,
                AssignedByUserId = assignedByUserId,
                DueDate = dto.DueDate,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.WorkTasks.Add(nuevaTarea);
            await _context.SaveChangesAsync();

            var created = await _context.WorkTasks
                .AsNoTracking()
                .Include(t => t.AssignedToUser)
                .Include(t => t.AssignedByUser)
                .FirstAsync(t => t.Id == nuevaTarea.Id);

            await NotifyTasksUpdatedAsync(created.AssignedToUserId, "created", created.Id);

            return MapToDto(created);
        }

        public async Task<WorkTaskDto> UpdateTaskAsync(int id, UpdateWorkTaskDto dto)
        {
            var tarea = await _context.WorkTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (tarea == null)
                throw new Exception("La tarea no existe o fue eliminada.");

            bool workerExists = await _context.Users.AnyAsync(u => u.Id == dto.AssignedToUserId && u.IsActive);
            if (!workerExists)
                throw new Exception("El trabajador seleccionado no existe o está inactivo.");

            tarea.Title = dto.Title.Trim();
            tarea.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            tarea.Priority = dto.Priority;
            tarea.AssignedToUserId = dto.AssignedToUserId;
            tarea.DueDate = dto.DueDate;

            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                tarea.Status = dto.Status;
                if (dto.Status == WorkTaskStatus.EnProgreso.ToString() && !tarea.StartedAt.HasValue)
                {
                    tarea.StartedAt = DateTime.Now;
                }
                else if (dto.Status == WorkTaskStatus.Completada.ToString() && !tarea.CompletedAt.HasValue)
                {
                    tarea.CompletedAt = DateTime.Now;
                }
            }

            if (dto.WorkerNotes != null)
            {
                tarea.WorkerNotes = string.IsNullOrWhiteSpace(dto.WorkerNotes) ? null : dto.WorkerNotes.Trim();
            }

            tarea.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var updated = await _context.WorkTasks
                .AsNoTracking()
                .Include(t => t.AssignedToUser)
                .Include(t => t.AssignedByUser)
                .FirstAsync(t => t.Id == tarea.Id);

            await NotifyTasksUpdatedAsync(updated.AssignedToUserId, "updated", updated.Id);

            return MapToDto(updated);
        }

        public async Task<WorkTaskDto> UpdateTaskStatusAsync(int id, UpdateWorkTaskStatusDto dto, int userId, bool canManageAll)
        {
            var tarea = await _context.WorkTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (tarea == null)
                throw new Exception("La tarea no existe o ya fue retirada.");

            if (tarea.AssignedToUserId != userId && !canManageAll)
                throw new Exception("Solo puedes actualizar el estado de las tareas que tienes asignadas.");

            if (tarea.Status == WorkTaskStatus.Cancelada.ToString() && !canManageAll)
                throw new Exception("Esta tarea fue cancelada por el encargado y no puede modificarse.");

            tarea.Status = dto.Status;

            if (dto.Status == WorkTaskStatus.EnProgreso.ToString() && !tarea.StartedAt.HasValue)
            {
                tarea.StartedAt = DateTime.Now;
            }
            else if (dto.Status == WorkTaskStatus.Completada.ToString())
            {
                if (!tarea.StartedAt.HasValue) tarea.StartedAt = DateTime.Now;
                tarea.CompletedAt = DateTime.Now;
            }

            if (dto.WorkerNotes != null)
            {
                tarea.WorkerNotes = string.IsNullOrWhiteSpace(dto.WorkerNotes) ? null : dto.WorkerNotes.Trim();
            }

            tarea.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var updated = await _context.WorkTasks
                .AsNoTracking()
                .Include(t => t.AssignedToUser)
                .Include(t => t.AssignedByUser)
                .FirstAsync(t => t.Id == tarea.Id);

            await NotifyTasksUpdatedAsync(updated.AssignedToUserId, "status", updated.Id);

            return MapToDto(updated);
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            var tarea = await _context.WorkTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (tarea == null)
                throw new Exception("La tarea no existe o ya fue retirada.");

            tarea.IsActive = false;
            tarea.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await NotifyTasksUpdatedAsync(tarea.AssignedToUserId, "deleted", tarea.Id);

            return true;
        }
    }
}
