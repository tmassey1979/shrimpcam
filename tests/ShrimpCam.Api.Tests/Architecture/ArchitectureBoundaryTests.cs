using System.Xml.Linq;
using ShrimpCam.Core.Abstractions;
using ShrimpCam.Infrastructure;

namespace ShrimpCam.Api.Tests.Architecture;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();
    private static readonly string CoreProjectPath = Path.Combine(RepositoryRoot, "src", "ShrimpCam.Core", "ShrimpCam.Core.csproj");
    private static readonly string InfrastructureProjectPath = Path.Combine(RepositoryRoot, "src", "ShrimpCam.Infrastructure", "ShrimpCam.Infrastructure.csproj");
    private static readonly string ApiProjectPath = Path.Combine(RepositoryRoot, "src", "ShrimpCam.Api", "ShrimpCam.Api.csproj");

    [Fact]
    [Trait("Category", "Api")]
    public void Core_project_allows_only_approved_references()
    {
        var project = LoadProject(CoreProjectPath);

        ProjectReferences(project)
            .Should()
            .BeEmpty("ShrimpCam.Core must not reference infrastructure or host projects directly.");

        FrameworkReferences(project)
            .Should()
            .NotContain(
                reference => reference.Equals("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase),
                "ShrimpCam.Core must stay free of ASP.NET Core framework dependencies.");

        PackageReferences(project)
            .Should()
            .NotContain(
                reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase),
                "ShrimpCam.Core must stay free of ASP.NET Core package dependencies.");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Core_assembly_does_not_reference_host_or_infrastructure_assemblies()
    {
        var references = typeof(IClock).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        references
            .Should()
            .NotContain(
                reference => reference.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase),
                "ShrimpCam.Core must stay isolated from ASP.NET Core assemblies.");

        references
            .Should()
            .NotContain(
                reference => reference.Equals("ShrimpCam.Infrastructure", StringComparison.OrdinalIgnoreCase),
                "ShrimpCam.Core must not reverse-reference infrastructure.");

        references
            .Should()
            .NotContain(
                reference => reference.Equals("ShrimpCam.Api", StringComparison.OrdinalIgnoreCase),
                "ShrimpCam.Core must not reference the API host.");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Infrastructure_depends_on_core_without_creating_a_reverse_dependency()
    {
        var infrastructureReferences = typeof(DependencyInjection).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        infrastructureReferences
            .Should()
            .Contain(
                reference => reference.Equals("ShrimpCam.Core", StringComparison.OrdinalIgnoreCase),
                "ShrimpCam.Infrastructure is expected to implement services defined by ShrimpCam.Core.");

        var coreProject = LoadProject(CoreProjectPath);

        ProjectReferences(coreProject)
            .Should()
            .NotContain(
                reference => reference.Equals("ShrimpCam.Infrastructure.csproj", StringComparison.OrdinalIgnoreCase),
                "ShrimpCam.Core must not create a reverse dependency on infrastructure.");
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Api_project_references_only_the_approved_application_layers()
    {
        var project = LoadProject(ApiProjectPath);
        var projectReferences = ProjectReferences(project);

        projectReferences
            .Should()
            .BeEquivalentTo(
                [
                    "ShrimpCam.Core.csproj",
                    "ShrimpCam.Infrastructure.csproj"
                ],
                "the API host should compose only the Core and Infrastructure layers.");

        var apiAssemblyReferences = typeof(Program).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        apiAssemblyReferences
            .Should()
            .Contain(reference => reference.Equals("ShrimpCam.Core", StringComparison.OrdinalIgnoreCase));

        apiAssemblyReferences
            .Should()
            .Contain(reference => reference.Equals("ShrimpCam.Infrastructure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Api")]
    public void Infrastructure_project_references_only_core_as_an_internal_project_dependency()
    {
        var project = LoadProject(InfrastructureProjectPath);

        ProjectReferences(project)
            .Should()
            .BeEquivalentTo(
                ["ShrimpCam.Core.csproj"],
                "ShrimpCam.Infrastructure should depend only on ShrimpCam.Core inside the solution.");
    }

    private static XDocument LoadProject(string projectPath)
    {
        File.Exists(projectPath).Should().BeTrue($"expected project file '{projectPath}' to exist for architecture validation.");
        return XDocument.Load(projectPath);
    }

    private static string[] ProjectReferences(XDocument project) =>
        project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => ProjectReferenceFileName(element.Attribute("Include")?.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

    private static string? ProjectReferenceFileName(string? include)
    {
        if (string.IsNullOrWhiteSpace(include))
        {
            return null;
        }

        var normalized = include
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFileName(normalized);
    }

    private static string[] FrameworkReferences(XDocument project) =>
        project.Descendants()
            .Where(element => element.Name.LocalName == "FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

    private static string[] PackageReferences(XDocument project) =>
        project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ShrimpCam.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root for architecture validation.");
    }
}
