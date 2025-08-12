using Feedster.DAL.Models;
namespace Feedster.DAL.Repositories;

/// <summary>
/// Provides data access for <see cref="UserSettings"/>.
/// </summary>
public interface IUserRepository
{
    /// <summary>Returns the single user settings instance.</summary>
    Task<UserSettings> Get(CancellationToken cancellationToken = default);

    /// <summary>Updates the user settings.</summary>
    Task Update(UserSettings userSettings, CancellationToken cancellationToken = default);
}
