namespace ShrimpCam.Core.Persistence;

public sealed record UserRoleRecord(
    Guid UserId,
    string RoleName,
    DateTimeOffset AssignedAtUtc);
