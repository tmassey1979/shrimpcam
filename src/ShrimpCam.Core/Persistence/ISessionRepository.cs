namespace ShrimpCam.Core.Persistence;

public interface ISessionRepository
{
    Task CreateAsync(SessionRecord session, CancellationToken cancellationToken);

    Task<SessionRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SessionRecord?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
}
