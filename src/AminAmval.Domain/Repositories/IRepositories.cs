using AminAmval.Domain.Entities;

namespace AminAmval.Domain.Repositories;

public interface IAssetRepository
{
    Task<Asset?> GetByIdAsync(string id, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<Asset?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, string? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Asset asset, CancellationToken cancellationToken = default);
    Task UpdateAsync(Asset asset, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByPersonnelCodeAsync(string personnelCode, CancellationToken cancellationToken = default);
    Task<User?> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUsernameAsync(string username, string? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPersonnelCodeAsync(string personnelCode, string? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNationalIdAsync(string nationalId, string? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default);
}

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);
    Task DeleteAsync(Category category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Department?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Department department, CancellationToken cancellationToken = default);
    Task UpdateAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteAsync(Department department, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Assignment?> GetActiveByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Assignment>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Assignment>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken = default);
}

public interface IDispositionRequestRepository
{
    Task<DispositionRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<DispositionRequest?> GetPendingByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DispositionRequest>> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
    Task AddAsync(DispositionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(DispositionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DispositionRequest>> GetPendingAsync(CancellationToken cancellationToken = default);
}

public interface IAuthSessionRepository
{
    Task<AuthSession?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthSession>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task AddAsync(AuthSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(AuthSession session, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IAuditRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetByAssetIdAsync(string assetId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetByUserIdAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}

public interface IImageRepository
{
    Task<UploadedImage?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(UploadedImage image, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UploadedImage>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
}