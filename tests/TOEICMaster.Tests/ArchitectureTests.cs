using System.Xml.Linq;

namespace TOEICMaster.Tests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("TOEICMaster.Domain")]
    [InlineData("TOEICMaster.Application")]
    public void Core_does_not_depend_on_infrastructure_or_http_frameworks(string assemblyName)
    {
        var references = System.Reflection.Assembly.Load(assemblyName).GetReferencedAssemblies();
        string[] forbidden = ["TOEICMaster.Infrastructure", "TOEICMaster.Api", "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore", "Npgsql", "StackExchange.Redis", "Minio", "MailKit", "MimeKit"];
        Assert.DoesNotContain(references, reference =>
            forbidden.Any(prefix => reference.Name!.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void Layers_have_only_allowed_project_references()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TOEICMaster.slnx")))
            root = root.Parent;
        Assert.NotNull(root);

        var rules = new Dictionary<string, string[]>
        {
            ["Domain"] = [],
            ["Application"] = ["Domain"],
            ["Infrastructure"] = ["Application"],
            ["Api"] = ["Application", "Infrastructure"]
        };
        foreach (var (layer, allowed) in rules)
        {
            var project = XDocument.Load(Path.Combine(root.FullName, "src", $"TOEICMaster.{layer}", $"TOEICMaster.{layer}.csproj"));
            var references = project.Descendants("ProjectReference")
                .Select(element => ((string)element.Attribute("Include")!).Replace('\\', '/').Split('/').Last())
                .ToArray();
            Assert.Equal(allowed.Select(name => $"TOEICMaster.{name}.csproj").Order(), references.Order());
            if (layer == "Domain")
                Assert.Empty(project.Descendants("PackageReference"));
        }
    }
}
