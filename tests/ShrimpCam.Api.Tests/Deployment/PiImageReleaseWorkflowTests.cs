namespace ShrimpCam.Api.Tests.Deployment;

public sealed class PiImageReleaseWorkflowTests
{
    [Fact]
    [Trait("Category", "Api")]
    public void Release_version_source_is_shared_by_backend_web_ci_and_pi_artifacts()
    {
        var version = ReadRepositoryFile("VERSION").Trim();
        var directoryProps = ReadRepositoryFile("Directory.Build.props");
        var packageJson = ReadRepositoryFile("src", "ShrimpCam.Web", "package.json");
        var packageLock = ReadRepositoryFile("src", "ShrimpCam.Web", "package-lock.json");
        var ciWorkflow = ReadRepositoryFile(".github", "workflows", "ci.yml");
        var piWorkflow = ReadRepositoryFile(".github", "workflows", "build-pi-image.yml");

        version.Should().Be("0.0.1-alpha");
        directoryProps.Should().Contain("VERSION");
        directoryProps.Should().Contain("<Version Condition=\"'$(Version)' == ''\">$(ShrimpCamProductVersion)</Version>");
        packageJson.Should().Contain($"\"version\": \"{version}\"");
        packageLock.Should().Contain($"\"version\": \"{version}\"");
        ciWorkflow.Should().Contain("./scripts/validate-version-consistency.ps1");
        piWorkflow.Should().Contain("base_version=\"$(tr -d '[:space:]' < VERSION)\"");
        piWorkflow.Should().Contain("-SourceRevision $env:SOURCE_SHA");
        piWorkflow.Should().Contain("shrimpcam-pi-${RELEASE_VERSION}.img.xz");
        piWorkflow.Should().Contain("tag_name: ${{ env.TAG_NAME }}");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Pi_image_workflow_publishes_release_only_after_successful_ci()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "build-pi-image.yml");

        workflow.Should().Contain("workflow_run:");
        workflow.Should().Contain("workflows:");
        workflow.Should().Contain("- ci");
        workflow.Should().Contain("github.event.workflow_run.conclusion == 'success'");
        workflow.Should().NotContain("push:\n    branches:");
        workflow.Should().Contain("if: github.event_name == 'workflow_run'");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Pi_image_workflow_limits_base_image_url_to_official_lite_arm64_source()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "build-pi-image.yml");

        workflow.Should().Contain("Validate Raspberry Pi OS image source");
        workflow.Should().Contain("https://downloads.raspberrypi.com/raspios_lite_arm64_latest");
        workflow.Should().Contain("Unsupported Raspberry Pi OS image URL");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Firstboot_service_runs_before_network_online_to_apply_headless_wifi()
    {
        var service = ReadRepositoryFile("deploy", "raspberry-pi", "shrimpcam-firstboot.service");

        service.Should().Contain("DefaultDependencies=no");
        service.Should().Contain("Before=network-pre.target network-online.target");
        service.Should().Contain("Wants=network-pre.target");
        service.Should().NotContain("After=network-online.target");
        service.Should().NotContain("Wants=network-online.target");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Firstboot_script_reads_wifi_settings_from_boot_partition()
    {
        var script = ReadRepositoryFile("deploy", "raspberry-pi", "firstboot-provision.sh");

        script.Should().Contain("/boot/firmware/shrimpcam-device.env");
        script.Should().Contain("/boot/shrimpcam-device.env");
        script.Should().Contain("SHRIMPCAM_WIFI_SSID");
        script.Should().Contain("SHRIMPCAM_WIFI_PSK");
        script.Should().Contain("NetworkManager");
    }

    private static string ReadRepositoryFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(new[] { ResolveRepositoryRoot() }.Concat(pathParts).ToArray()));

    private static string ResolveRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "ShrimpCam.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
