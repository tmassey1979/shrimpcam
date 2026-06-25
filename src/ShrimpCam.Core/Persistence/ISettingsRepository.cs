namespace ShrimpCam.Core.Persistence;

public interface ISettingsRepository
{
    Task UpsertAsync(PersistedSetting setting, CancellationToken cancellationToken);

    Task<PersistedSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken);

    Task<IReadOnlyList<PersistedSetting>> ListAsync(CancellationToken cancellationToken);
}
