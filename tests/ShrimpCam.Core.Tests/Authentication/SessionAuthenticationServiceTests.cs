using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class SessionAuthenticationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Authenticate_async_returns_user_identity_with_assigned_roles()
    {
        var clock = Substitute.For<IClock>();
        var sessionRepository = Substitute.For<ISessionRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var userRoleRepository = Substitute.For<IUserRoleRepository>();
        var issuedAt = new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var token = "shrimp-session-token";
        var hashedToken = SessionTokenHasher.ComputeHash(token);

        clock.UtcNow.Returns(issuedAt);
        sessionRepository.GetByTokenHashAsync(hashedToken, CancellationToken.None)
            .Returns(new SessionRecord(sessionId, userId, hashedToken, issuedAt.AddMinutes(-5), issuedAt.AddHours(4), null));
        userRepository.GetByIdAsync(userId, CancellationToken.None)
            .Returns(new UserRecord(userId, "shrimp-admin", "hashed-password", true, issuedAt.AddDays(-1)));
        userRoleRepository.ListByUserIdAsync(userId, CancellationToken.None)
            .Returns(
                [
                    new UserRoleRecord(userId, "Administrator", issuedAt.AddDays(-1)),
                    new UserRoleRecord(userId, "Viewer", issuedAt.AddDays(-1)),
                ]);

        var service = new SessionAuthenticationService(clock, sessionRepository, userRepository, userRoleRepository);

        var result = await service.AuthenticateAsync(token, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        result.Identity.Should().NotBeNull();
        result.Identity!.UserName.Should().Be("shrimp-admin");
        result.Identity.Roles.Should().BeEquivalentTo(["Administrator", "Viewer"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Authenticate_async_rejects_expired_sessions()
    {
        var clock = Substitute.For<IClock>();
        var sessionRepository = Substitute.For<ISessionRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var userRoleRepository = Substitute.For<IUserRoleRepository>();
        var now = new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var token = "expired-session-token";
        var hashedToken = SessionTokenHasher.ComputeHash(token);

        clock.UtcNow.Returns(now);
        sessionRepository.GetByTokenHashAsync(hashedToken, CancellationToken.None)
            .Returns(new SessionRecord(Guid.NewGuid(), userId, hashedToken, now.AddHours(-8), now.AddMinutes(-1), null));

        var service = new SessionAuthenticationService(clock, sessionRepository, userRepository, userRoleRepository);

        var result = await service.AuthenticateAsync(token, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(SessionAuthenticationFailureReasons.InvalidSession);
        await userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default).ConfigureAwait(true);
        await userRoleRepository.DidNotReceiveWithAnyArgs().ListByUserIdAsync(default, default).ConfigureAwait(true);
    }
}
