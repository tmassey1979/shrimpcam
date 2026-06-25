using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Tests.Persistence;

public sealed class PersistenceContractsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Persistence_contracts_are_public()
    {
        typeof(AuditRecord).IsPublic.Should().BeTrue();
        typeof(CaptureRecord).IsPublic.Should().BeTrue();
        typeof(IApplicationDataInitializer).IsPublic.Should().BeTrue();
        typeof(IAuditRecordRepository).IsPublic.Should().BeTrue();
        typeof(ICaptureRecordRepository).IsPublic.Should().BeTrue();
        typeof(ISettingsRepository).IsPublic.Should().BeTrue();
        typeof(ISessionRepository).IsPublic.Should().BeTrue();
        typeof(IUserRepository).IsPublic.Should().BeTrue();
        typeof(IUserRoleRepository).IsPublic.Should().BeTrue();
        typeof(PersistedSetting).IsPublic.Should().BeTrue();
        typeof(SessionRecord).IsPublic.Should().BeTrue();
        typeof(UserRecord).IsPublic.Should().BeTrue();
        typeof(UserRoleRecord).IsPublic.Should().BeTrue();
    }
}
