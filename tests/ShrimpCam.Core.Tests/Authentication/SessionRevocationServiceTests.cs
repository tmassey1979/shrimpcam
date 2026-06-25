using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class SessionRevocationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Revoke_async_marks_an_active_session_as_revoked()
    {
        var clock = Substitute.For<IClock>();
        var sessionRepository = Substitute.For<ISessionRepository>();
        var now = new DateTimeOffset(2026, 06, 25, 12, 00, 00, TimeSpan.Zero);
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var session = new SessionRecord(sessionId, userId, "token-hash", now.AddHours(-2), now.AddHours(6), null);

        clock.UtcNow.Returns(now);
        sessionRepository.GetByIdAsync(sessionId, CancellationToken.None).Returns(session);

        var service = new SessionRevocationService(clock, sessionRepository);

        var result = await service.RevokeAsync(sessionId, CancellationToken.None).ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        result.RevokedSession.Should().NotBeNull();
        result.RevokedSession!.RevokedAtUtc.Should().Be(now);
        await sessionRepository.Received(1)
            .UpdateAsync(
                Arg.Is<SessionRecord>(record =>
                    record.Id == sessionId &&
                    record.UserId == userId &&
                    record.RevokedAtUtc == now),
                CancellationToken.None)
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Revoke_async_rejects_unknown_sessions()
    {
        var clock = Substitute.For<IClock>();
        var sessionRepository = Substitute.For<ISessionRepository>();
        var sessionId = Guid.NewGuid();

        sessionRepository.GetByIdAsync(sessionId, CancellationToken.None).Returns((SessionRecord?)null);

        var service = new SessionRevocationService(clock, sessionRepository);

        var result = await service.RevokeAsync(sessionId, CancellationToken.None).ConfigureAwait(true);

        result.Should().Be(SessionRevocationResult.Failure(SessionRevocationFailureReasons.SessionNotFound));
        await sessionRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default).ConfigureAwait(true);
    }
}
