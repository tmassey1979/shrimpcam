using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShrimpCam.Core.Authentication;
using ShrimpCam.Core.Configuration;
using ShrimpCam.Core.Persistence;

#nullable enable
#pragma warning disable CA2007

namespace ShrimpCam.Api.Tests.Captures;

public sealed class CaptureBrowsingEndpointTests
{
    [Fact]
    [Trait("Category", "Api")]
    public async Task Viewer_can_browse_captures_by_date_range_with_paging_metadata()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            var earlyCapture = CreateCapture("2026-06-23T10:00:00Z", "early");
            var firstExpected = CreateCapture("2026-06-24T12:00:00Z", "noon");
            var secondExpected = CreateCapture("2026-06-24T08:00:00Z", "morning");
            var tooLate = CreateCapture("2026-06-25T08:00:00Z", "late");
            await SeedCapturesAsync(rootPath, [earlyCapture, firstExpected, secondExpected, tooLate]).ConfigureAwait(true);

            await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var fromUtc = Uri.EscapeDataString("2026-06-24T00:00:00Z");
            var toUtc = Uri.EscapeDataString("2026-06-24T23:59:59Z");
            var response = await client.GetAsync($"/captures?fromUtc={fromUtc}&toUtc={toUtc}&page=1&pageSize=1").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<CaptureListResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Items.Should().ContainSingle();
            payload.Items[0].Id.Should().Be(firstExpected.Id);
            payload.Items[0].ImageUrl.Should().Be($"/captures/{firstExpected.Id}/image");
            payload.Paging.PageNumber.Should().Be(1);
            payload.Paging.PageSize.Should().Be(1);
            payload.Paging.TotalItems.Should().Be(2);
            payload.Paging.TotalPages.Should().Be(2);
            payload.Paging.HasPreviousPage.Should().BeFalse();
            payload.Paging.HasNextPage.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Viewer_can_retrieve_capture_detail_by_identifier()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            var capture = CreateCapture("2026-06-24T12:00:00Z", "detail");
            await SeedCapturesAsync(rootPath, [capture]).ConfigureAwait(true);

            await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"/captures/{capture.Id}").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<CaptureResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Id.Should().Be(capture.Id);
            payload.RelativeImagePath.Should().Be(capture.RelativeImagePath);
            payload.RelativeMetadataPath.Should().Be(capture.RelativeMetadataPath);
            payload.FileName.Should().Be(capture.FileName);
            payload.SourceType.Should().Be(capture.SourceType);
            payload.MetadataUrl.Should().Be($"/captures/{capture.Id}/metadata");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Viewer_can_retrieve_capture_image_and_metadata_files()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            var capture = CreateCapture("2026-06-24T12:00:00Z", "file-access");
            await SeedCapturesAsync(rootPath, [capture]).ConfigureAwait(true);
            WriteCaptureFiles(rootPath, capture, [0xFF, 0xD8, 0xFF, 0xD9], """{"source":"test"}""");

            await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var imageResponse = await client.GetAsync($"/captures/{capture.Id}/image").ConfigureAwait(true);
            var metadataResponse = await client.GetAsync($"/captures/{capture.Id}/metadata").ConfigureAwait(true);

            imageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            imageResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
            (await imageResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(true)).Should().Equal([0xFF, 0xD8, 0xFF, 0xD9]);
            metadataResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            metadataResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
            (await metadataResponse.Content.ReadAsStringAsync().ConfigureAwait(true)).Should().Be("""{"source":"test"}""");
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Missing_capture_file_returns_not_found()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);
            var capture = CreateCapture("2026-06-24T12:00:00Z", "missing-file");
            await SeedCapturesAsync(rootPath, [capture]).ConfigureAwait(true);

            await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"/captures/{capture.Id}/image").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Invalid_capture_filters_are_rejected()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var token = await SeedUserAndLoginAsync(rootPath, "shrimp-viewer", "ViewerPass123", "Viewer").ConfigureAwait(true);

            await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var fromUtc = Uri.EscapeDataString("2026-06-25T00:00:00Z");
            var toUtc = Uri.EscapeDataString("2026-06-24T00:00:00Z");
            var response = await client.GetAsync($"/captures?fromUtc={fromUtc}&toUtc={toUtc}&page=0&pageSize=101").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetailsResponse>().ConfigureAwait(true);

            payload.Should().NotBeNull();
            payload!.Errors.Keys.Should().Contain(["fromUtc", "toUtc", "page", "pageSize"]);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Unauthenticated_callers_cannot_browse_captures()
    {
        var rootPath = CreateTempRoot();

        try
        {
            await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/captures").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task Unauthenticated_callers_cannot_retrieve_capture_files()
    {
        var rootPath = CreateTempRoot();

        try
        {
            var capture = CreateCapture("2026-06-24T12:00:00Z", "protected-file");
            await SeedCapturesAsync(rootPath, [capture]).ConfigureAwait(true);

            await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
            using var client = factory.CreateClient();

            var response = await client.GetAsync($"/captures/{capture.Id}/image").ConfigureAwait(true);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            DeleteDirectory(rootPath);
        }
    }

    private static CaptureRecord CreateCapture(string capturedAtUtc, string name)
    {
        var instant = DateTimeOffset.Parse(capturedAtUtc, System.Globalization.CultureInfo.InvariantCulture);

        return new CaptureRecord(
            Guid.NewGuid(),
            $"{instant:yyyy/MM/dd}/{name}.jpg",
            $"{instant:yyyy/MM/dd}/{name}.json",
            $"{name}.jpg",
            "Scheduled",
            instant);
    }

    private static async Task SeedCapturesAsync(string rootPath, IReadOnlyList<CaptureRecord> captures)
    {
        using var provider = BuildProvider(rootPath);
        var initializer = provider.GetRequiredService<IApplicationDataInitializer>();
        var captureRepository = provider.GetRequiredService<ICaptureRecordRepository>();

        await initializer.InitializeAsync(provider.GetRequiredService<IOptions<ShrimpCamOptions>>().Value.Storage, CancellationToken.None).ConfigureAwait(true);

        foreach (var capture in captures)
        {
            await captureRepository.CreateAsync(capture, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private static void WriteCaptureFiles(string rootPath, CaptureRecord capture, byte[] imageBytes, string metadata)
    {
        var imagePath = Path.Combine(rootPath, "images", capture.RelativeImagePath.Replace('/', Path.DirectorySeparatorChar));
        var metadataPath = Path.Combine(rootPath, "images", capture.RelativeMetadataPath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);
        File.WriteAllBytes(imagePath, imageBytes);
        File.WriteAllText(metadataPath, metadata);
    }

    private static async Task<string> SeedUserAndLoginAsync(string rootPath, string userName, string password, string roleName)
    {
        var createdAtUtc = new DateTimeOffset(2026, 06, 25, 04, 00, 00, TimeSpan.Zero);
        using var provider = BuildProvider(rootPath);
        var initializer = provider.GetRequiredService<IApplicationDataInitializer>();
        var passwordHasher = provider.GetRequiredService<IPasswordHasher>();
        var userRepository = provider.GetRequiredService<IUserRepository>();
        var roleRepository = provider.GetRequiredService<IUserRoleRepository>();
        var userId = Guid.NewGuid();

        await initializer.InitializeAsync(provider.GetRequiredService<IOptions<ShrimpCamOptions>>().Value.Storage, CancellationToken.None).ConfigureAwait(true);
        await userRepository.CreateAsync(
                new UserRecord(userId, userName, passwordHasher.HashPassword(password), true, createdAtUtc),
                CancellationToken.None)
            .ConfigureAwait(true);
        await roleRepository.AssignAsync(new UserRoleRecord(userId, roleName, createdAtUtc), CancellationToken.None).ConfigureAwait(true);

        await using var factory = new CaptureBrowsingWebApplicationFactory(rootPath);
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(userName, password)).ConfigureAwait(true);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginSuccessResponse>().ConfigureAwait(true);
        payload.Should().NotBeNull();
        return payload!.Token;
    }

    private static ServiceProvider BuildProvider(string rootPath)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<ShrimpCamOptions>>(Options.Create(CreateOptions(rootPath)));
        ShrimpCam.Infrastructure.DependencyInjection.AddInfrastructure(services);
        return services.BuildServiceProvider();
    }

    private static ShrimpCamOptions CreateOptions(string rootPath) =>
        new()
        {
            Camera = new CameraOptions
            {
                Platform = "Windows",
                Source = "Logitech C920",
            },
            Storage = new StorageOptions
            {
                DatabasePath = Path.Combine(rootPath, "shrimpcam.db"),
                ImageRootPath = Path.Combine(rootPath, "images"),
                TimelapseRootPath = Path.Combine(rootPath, "timelapse"),
                RetentionDays = 30,
            },
        };

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string rootPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!Directory.Exists(rootPath))
            {
                return;
            }

            try
            {
                Directory.Delete(rootPath, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    private sealed record LoginRequest(string UserName, string Password);

    private sealed record LoginSuccessResponse(
        string Status,
        Guid SessionId,
        Guid UserId,
        string UserName,
        string Token,
        DateTimeOffset ExpiresAtUtc);

    private sealed record CaptureListResponse(CaptureResponse[] Items, CapturePagingResponse Paging);

    private sealed record CapturePagingResponse(
        int PageNumber,
        int PageSize,
        int TotalItems,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);

    private sealed record CaptureResponse(
        Guid Id,
        string RelativeImagePath,
        string RelativeMetadataPath,
        string FileName,
        string SourceType,
        DateTimeOffset CapturedAtUtc,
        string ImageUrl,
        string MetadataUrl);

    private sealed record ValidationProblemDetailsResponse(string Type, string Title, int Status, Dictionary<string, string[]> Errors);

    private sealed class CaptureBrowsingWebApplicationFactory(string rootPath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(
                (_, configBuilder) => configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ShrimpCam:Camera:Platform"] = "Windows",
                        ["ShrimpCam:Camera:Source"] = "Logitech C920",
                        ["ShrimpCam:Storage:DatabasePath"] = Path.Combine(rootPath, "shrimpcam.db"),
                        ["ShrimpCam:Storage:ImageRootPath"] = Path.Combine(rootPath, "images"),
                        ["ShrimpCam:Storage:TimelapseRootPath"] = Path.Combine(rootPath, "timelapse"),
                    }));
            builder.ConfigureServices(
                services => services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
        }
    }
}

#pragma warning restore CA2007
