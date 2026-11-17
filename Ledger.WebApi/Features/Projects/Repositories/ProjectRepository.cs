using Ledger.WebApi.Extensions.Data;
using Ledger.WebApi.Features.Projects.Models.Entities;

namespace Ledger.WebApi.Features.Projects.Repositories;

/// <summary>
/// The buy-and-sell projects of a user (dbo.Project).
/// </summary>
internal interface IProjectRepository
{
    /// <summary>Lists the projects, oldest first.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The projects.</returns>
    Task<List<ProjectRow>> ListAsync(Guid userId);

    /// <summary>
    /// Adds a project (no id) or changes one. Adding a name the user already has returns that project as it is.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="projectId">The project to change, or null for a new one.</param>
    /// <param name="name">The name.</param>
    /// <param name="targetSale">The price hoped for.</param>
    /// <param name="worksBudget">What the works should cost.</param>
    /// <param name="agreedPrice">The price agreed with the seller (null: none).</param>
    /// <returns>The project's id, or null when the project to change isn't this user's.</returns>
    Task<Guid?> SaveAsync(Guid userId, Guid? projectId, string name, decimal? targetSale, decimal? worksBudget, decimal? agreedPrice);

    /// <summary>Deletes a project; its entries stay, without a project.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="projectId">The project.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid userId, Guid projectId);
}

/// <summary>
/// The projects in SQL Server (stored procedures in Ledger.Database/StoredProcedures/Projects).
/// </summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class ProjectRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IProjectRepository
{
    /// <inheritdoc />
    public Task<List<ProjectRow>> ListAsync(Guid userId) => QueryAsync<ProjectRow>("[dbo].[ProjectList]", new { UserUniqueID = userId });

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? projectId, string name, decimal? targetSale, decimal? worksBudget, decimal? agreedPrice) =>
        ExecuteScalarAsync<Guid?>("[dbo].[ProjectSave]", new
        {
            UserUniqueID = userId,
            ProjectUniqueID = projectId,
            Name = name,
            TargetSale = targetSale,
            WorksBudget = worksBudget,
            AgreedPrice = agreedPrice,
        });

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, Guid projectId) =>
        ExecuteAsync("[dbo].[ProjectDelete]", new { UserUniqueID = userId, ProjectUniqueID = projectId });
}

/// <summary>
/// The projects in memory (demo mode).
/// </summary>
/// <param name="store">The memory tables.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed class MemoryProjectRepository(MemoryStore store, TimeProvider timeProvider) : IProjectRepository
{
    /// <inheritdoc />
    public Task<List<ProjectRow>> ListAsync(Guid userId)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.For(userId).Projects.Select(Copy).ToList());
        }
    }

    /// <inheritdoc />
    public Task<Guid?> SaveAsync(Guid userId, Guid? projectId, string name, decimal? targetSale, decimal? worksBudget, decimal? agreedPrice)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            if (projectId is null)
            {
                if (ledger.Projects.FirstOrDefault(project => project.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } same)
                {
                    return Task.FromResult<Guid?>(same.ProjectUniqueID);
                }

                var row = new ProjectRow
                {
                    ProjectUniqueID = Guid.NewGuid(),
                    Name = name,
                    TargetSale = targetSale,
                    WorksBudget = worksBudget,
                    AgreedPrice = agreedPrice,
                    CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                };
                ledger.Projects.Add(row);
                return Task.FromResult<Guid?>(row.ProjectUniqueID);
            }

            var existing = ledger.Projects.FirstOrDefault(project => project.ProjectUniqueID == projectId);
            if (existing is null)
            {
                return Task.FromResult<Guid?>(null);
            }

            existing.Name = name;
            existing.TargetSale = targetSale;
            existing.WorksBudget = worksBudget;
            existing.AgreedPrice = agreedPrice;
            return Task.FromResult<Guid?>(existing.ProjectUniqueID);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid userId, Guid projectId)
    {
        lock (store.Gate)
        {
            var ledger = store.For(userId);
            if (ledger.Projects.RemoveAll(project => project.ProjectUniqueID == projectId) > 0)
            {
                foreach (var entry in ledger.Entries.Where(entry => entry.ProjectUniqueID == projectId))
                {
                    entry.ProjectUniqueID = null;
                }
            }
        }

        return Task.CompletedTask;
    }

    private static ProjectRow Copy(ProjectRow project) => new()
    {
        ProjectUniqueID = project.ProjectUniqueID,
        Name = project.Name,
        TargetSale = project.TargetSale,
        WorksBudget = project.WorksBudget,
        AgreedPrice = project.AgreedPrice,
        CreatedAt = project.CreatedAt,
    };
}
