using Api_Tlapaleria.DTOs;

namespace Api_Tlapaleria.Services
{
    public interface IWorkTaskService
    {
        Task<List<WorkTaskDto>> GetMyTasksAsync(int userId, string? status = null);
        Task<PagedResponse<WorkTaskDto>> GetAllTasksAsync(
            int pageNumber,
            int pageSize,
            int? assignedToUserId = null,
            string? status = null,
            string? priority = null,
            string? search = null);
        Task<List<WorkWorkerItemDto>> GetAssignableWorkersAsync();
        Task<WorkTaskDto> CreateTaskAsync(CreateWorkTaskDto dto, int assignedByUserId);
        Task<WorkTaskDto> UpdateTaskAsync(int id, UpdateWorkTaskDto dto);
        Task<WorkTaskDto> UpdateTaskStatusAsync(int id, UpdateWorkTaskStatusDto dto, int userId, bool canManageAll);
        Task<bool> DeleteTaskAsync(int id);
    }
}
