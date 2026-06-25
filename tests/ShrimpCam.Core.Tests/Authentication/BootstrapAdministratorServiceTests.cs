using NSubstitute;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Persistence;

namespace ShrimpCam.Core.Tests.Authentication;

public sealed class BootstrapAdministratorServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Bootstrap_creates_first_administrator_when_none_exists()
    {
        var clock = Substitute.For<IClock>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var passwordPolicy = Substitute.For<IPasswordPolicy>();
        var userRepository = Substitute.For<IUserRepository>();
        var userRoleRepository = Substitute.For<IUserRoleRepository>();
        var now = new DateTimeOffset(2026, 06, 25, 03, 00, 00, TimeSpan.Zero);

        clock.UtcNow.Returns(now);
        userRoleRepository.AnyInRoleAsync("Administrator", Arg.Any<CancellationToken>()).Returns(false);
        passwordPolicy.IsSatisfiedBy("StrongShrimp123").Returns(true);
        passwordHasher.HashPassword("StrongShrimp123").Returns("hashed-password");

        var service = new BootstrapAdministratorService(clock, passwordHasher, passwordPolicy, userRepository, userRoleRepository);

        var result = await service.BootstrapAsync(
                new BootstrapAdministratorRequest("shrimp-admin", "StrongShrimp123"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Succeeded.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.UserName.Should().Be("shrimp-admin");
        result.User.RoleName.Should().Be("Administrator");

        await userRepository.Received(1).CreateAsync(
                Arg.Is<UserRecord>(record =>
                    record.UserName == "shrimp-admin"
                    && record.PasswordHash == "hashed-password"
                    && record.IsEnabled
                    && record.CreatedAtUtc == now),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
        await userRoleRepository.Received(1).AssignAsync(
                Arg.Is<UserRoleRecord>(record =>
                    record.UserId == result.User.UserId
                    && record.RoleName == "Administrator"
                    && record.AssignedAtUtc == now),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Bootstrap_rejects_requests_once_an_administrator_already_exists()
    {
        var service = new BootstrapAdministratorService(
            Substitute.For<IClock>(),
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IPasswordPolicy>(),
            Substitute.For<IUserRepository>(),
            CreateRoleRepository(anyInRole: true));

        var result = await service.BootstrapAsync(
                new BootstrapAdministratorRequest("shrimp-admin", "StrongShrimp123"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Should().Be(BootstrapAdministratorResult.Failure(BootstrapAdministratorFailureReasons.AlreadyConfigured));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Bootstrap_rejects_weak_passwords()
    {
        var passwordPolicy = Substitute.For<IPasswordPolicy>();
        passwordPolicy.IsSatisfiedBy("weak").Returns(false);

        var service = new BootstrapAdministratorService(
            Substitute.For<IClock>(),
            Substitute.For<IPasswordHasher>(),
            passwordPolicy,
            Substitute.For<IUserRepository>(),
            CreateRoleRepository(anyInRole: false));

        var result = await service.BootstrapAsync(
                new BootstrapAdministratorRequest("shrimp-admin", "weak"),
                CancellationToken.None)
            .ConfigureAwait(true);

        result.Should().Be(BootstrapAdministratorResult.Failure(BootstrapAdministratorFailureReasons.WeakPassword));
    }

    private static IUserRoleRepository CreateRoleRepository(bool anyInRole)
    {
        var repository = Substitute.For<IUserRoleRepository>();
        repository.AnyInRoleAsync("Administrator", Arg.Any<CancellationToken>()).Returns(anyInRole);
        return repository;
    }
}
