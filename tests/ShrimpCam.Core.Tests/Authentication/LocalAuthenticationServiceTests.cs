using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class LocalAuthenticationServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Authenticate_succeeds_for_active_user_with_valid_password()
    {
        var clock = Substitute.For<IClock>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var sessionRepository = Substitute.For<ISessionRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var now = new DateTimeOffset(2026, 06, 25, 01, 00, 00, TimeSpan.Zero);
        var user = new UserRecord(Guid.NewGuid(), "shrimp-admin", "stored-hash", true, now.AddDays(-1));

        clock.UtcNow.Returns(now);
        userRepository.GetByUserNameAsync("shrimp-admin", Arg.Any<CancellationToken>()).Returns(user);
        passwordHasher.VerifyPassword("shrimp-password", "stored-hash").Returns(true);

        var service = new LocalAuthenticationService(clock, passwordHasher, sessionRepository, userRepository);

        var result = await service.AuthenticateAsync(
                new AuthenticationRequest("shrimp-admin", "shrimp-password"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        result.Session.Should().NotBeNull();
        result.Session!.UserId.Should().Be(user.Id);
        result.Session.UserName.Should().Be("shrimp-admin");
        result.Session.ExpiresAtUtc.Should().Be(now.AddHours(8));
        result.Session.Token.Should().NotBeNullOrWhiteSpace();
        await sessionRepository.Received(1).CreateAsync(
                Arg.Is<SessionRecord>(record =>
                    record.UserId == user.Id
                    && record.CreatedAtUtc == now
                    && record.ExpiresAtUtc == now.AddHours(8)
                    && !string.IsNullOrWhiteSpace(record.TokenHash)),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Authenticate_rejects_invalid_credentials_without_creating_a_session()
    {
        var clock = Substitute.For<IClock>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var sessionRepository = Substitute.For<ISessionRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var now = new DateTimeOffset(2026, 06, 25, 01, 00, 00, TimeSpan.Zero);
        var user = new UserRecord(Guid.NewGuid(), "shrimp-admin", "stored-hash", true, now.AddDays(-1));

        userRepository.GetByUserNameAsync("shrimp-admin", Arg.Any<CancellationToken>()).Returns(user);
        passwordHasher.VerifyPassword("wrong-password", "stored-hash").Returns(false);

        var service = new LocalAuthenticationService(clock, passwordHasher, sessionRepository, userRepository);

        var result = await service.AuthenticateAsync(
                new AuthenticationRequest("shrimp-admin", "wrong-password"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Should().Be(AuthenticationResult.Failure(AuthenticationFailureReasons.InvalidCredentials));
        await sessionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Authenticate_rejects_disabled_accounts_without_verifying_the_password()
    {
        var clock = Substitute.For<IClock>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var sessionRepository = Substitute.For<ISessionRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var user = new UserRecord(Guid.NewGuid(), "shrimp-viewer", "stored-hash", false, DateTimeOffset.UtcNow.AddDays(-1));

        userRepository.GetByUserNameAsync("shrimp-viewer", Arg.Any<CancellationToken>()).Returns(user);

        var service = new LocalAuthenticationService(clock, passwordHasher, sessionRepository, userRepository);

        var result = await service.AuthenticateAsync(
                new AuthenticationRequest("shrimp-viewer", "shrimp-password"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Should().Be(AuthenticationResult.Failure(AuthenticationFailureReasons.InvalidCredentials));
        passwordHasher.DidNotReceiveWithAnyArgs().VerifyPassword(default!, default!);
        await sessionRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Authenticate_rejects_blank_credentials()
    {
        var service = new LocalAuthenticationService(
            Substitute.For<IClock>(),
            Substitute.For<IPasswordHasher>(),
            Substitute.For<ISessionRepository>(),
            Substitute.For<IUserRepository>());

        var result = await service.AuthenticateAsync(
                new AuthenticationRequest(" ", string.Empty),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Should().Be(AuthenticationResult.Failure(AuthenticationFailureReasons.InvalidCredentials));
    }

}
