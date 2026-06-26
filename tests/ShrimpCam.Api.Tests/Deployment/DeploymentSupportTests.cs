using System.Xml.Linq;

namespace ShrimpCam.Api.Tests.Deployment;

public sealed class DeploymentSupportTests
{
    [Fact]
    [Trait("Category", "Api")]
    public void Api_host_references_service_manager_lifetime_packages()
    {
        var project = XDocument.Load(Path.Combine(ResolveRepositoryRoot(), "src", "ShrimpCam.Api", "ShrimpCam.Api.csproj"));
        var packageReferences = project
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .ToArray();

        packageReferences.Should().Contain("Microsoft.Extensions.Hosting.Systemd");
        packageReferences.Should().Contain("Microsoft.Extensions.Hosting.WindowsServices");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Program_configures_systemd_and_windows_service_lifetimes()
    {
        var program = File.ReadAllText(Path.Combine(ResolveRepositoryRoot(), "src", "ShrimpCam.Api", "Program.cs"));

        program.Should().Contain("builder.Host.UseSystemd();");
        program.Should().Contain("builder.Host.UseWindowsService");
        program.Should().Contain("ServiceName = \"ShrimpCam\"");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Systemd_unit_starts_automatically_with_restart_policy_and_data_paths()
    {
        var service = File.ReadAllText(Path.Combine(ResolveRepositoryRoot(), "deploy", "raspberry-pi", "shrimpcam-api.service"));

        service.Should().Contain("WantedBy=multi-user.target");
        service.Should().Contain("WorkingDirectory=/opt/shrimpcam/api");
        service.Should().Contain("Restart=always");
        service.Should().Contain("RestartSec=5");
        service.Should().Contain("StateDirectory=shrimpcam");
        service.Should().Contain("LogsDirectory=shrimpcam");
        service.Should().Contain("ShrimpCam__Storage__DatabasePath=/var/lib/shrimpcam/data/shrimpcam.db");
        service.Should().Contain("ShrimpCam__Storage__ImageRootPath=/var/lib/shrimpcam/images");
        service.Should().Contain("ShrimpCam__Storage__TimelapseRootPath=/var/lib/shrimpcam/timelapse");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Windows_service_script_sets_automatic_start_restart_policy_and_data_paths()
    {
        var script = File.ReadAllText(Path.Combine(ResolveRepositoryRoot(), "deploy", "windows", "install-shrimpcam-service.ps1"));

        script.Should().Contain("New-Service");
        script.Should().Contain("-Name $ServiceName");
        script.Should().Contain("-StartupType Automatic");
        script.Should().Contain("sc.exe failure $ServiceName");
        script.Should().Contain("restart/5000/restart/5000/restart/30000");
        script.Should().Contain("ShrimpCam__Storage__DatabasePath");
        script.Should().Contain("ShrimpCam__Storage__ImageRootPath");
        script.Should().Contain("ShrimpCam__Storage__TimelapseRootPath");
        script.Should().Contain("Start-Service -Name $ServiceName");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Publish_script_validates_versions_and_stamps_source_revision()
    {
        var script = File.ReadAllText(Path.Combine(ResolveRepositoryRoot(), "scripts", "publish-shrimpcam.ps1"));

        script.Should().Contain("validate-version-consistency.ps1");
        script.Should().Contain("git -C $repoRoot rev-parse --short HEAD");
        script.Should().Contain("/p:ShrimpCamSourceRevision=$SourceRevision");
    }

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
