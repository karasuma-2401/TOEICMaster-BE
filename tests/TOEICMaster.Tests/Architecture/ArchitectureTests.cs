using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TOEICMaster.Api.Controllers;
using TOEICMaster.Domain.Entities;
using TOEICMaster.Tests.Api.Common;

namespace TOEICMaster.Tests.Architecture;

public sealed class ArchitectureTests
{
    private static DirectoryInfo FindRepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TOEICMaster.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        return root;
    }

    [Theory]
    [InlineData("TOEICMaster.Domain")]
    [InlineData("TOEICMaster.Application")]
    public void Core_does_not_depend_on_infrastructure_or_http_frameworks(string assemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies();
        string[] forbidden =
        [
            "TOEICMaster.Infrastructure", "TOEICMaster.Api", "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore", "Npgsql", "StackExchange.Redis", "Minio", "MailKit", "MimeKit"
        ];
        Assert.DoesNotContain(references, reference =>
            forbidden.Any(prefix => reference.Name!.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void Layers_have_only_allowed_project_references()
    {
        var root = FindRepoRoot();

        var rules = new Dictionary<string, string[]>
        {
            ["Domain"] = [],
            ["Application"] = ["Domain"],
            ["Infrastructure"] = ["Application"],
            ["Api"] = ["Application", "Infrastructure"]
        };
        foreach (var (layer, allowed) in rules)
        {
            var project = XDocument.Load(Path.Combine(root.FullName, "src", $"TOEICMaster.{layer}",
                $"TOEICMaster.{layer}.csproj"));
            var references = project.Descendants("ProjectReference")
                .Select(element => ((string)element.Attribute("Include")!).Replace('\\', '/').Split('/').Last())
                .ToArray();
            Assert.Equal(allowed.Select(name => $"TOEICMaster.{name}.csproj").Order(), references.Order());
            if (layer == "Domain")
                Assert.Empty(project.Descendants("PackageReference"));
        }
    }

    [Fact]
    public void Controllers_route_through_ISender_without_direct_persistence_or_mapper_dependencies()
    {
        var apiAssembly = typeof(AdminUsersController).Assembly;
        var controllers = apiAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(controllers);

        // TODO(HIGH-12): Migrate KnowledgeController reads from IKnowledgeReader to MediatR queries in Wave 3.
        HashSet<(string Controller, string Dependency)> allowed =
        [
            ("KnowledgeController", "IKnowledgeReader")
        ];

        var violations = new List<string>();
        foreach (var controller in controllers)
        {
            foreach (var ctor in controller.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    var paramType = parameter.ParameterType;
                    var isForbidden = paramType.Name == "ToeicDbContext" ||
                                      paramType.Name == "IMapper" ||
                                      paramType.Name.EndsWith("Repository", StringComparison.Ordinal) ||
                                      paramType.Name.EndsWith("Reader", StringComparison.Ordinal);

                    if (isForbidden && !allowed.Contains((controller.Name, paramType.Name)))
                    {
                        violations.Add($"{controller.Name} injects {paramType.FullName}");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Controllers_use_ClaimsPrincipalExtensions_instead_of_raw_claim_extraction()
    {
        var root = FindRepoRoot();
        var controllersDir = Path.Combine(root.FullName, "src", "TOEICMaster.Api", "Controllers");
        var files = Directory.GetFiles(controllersDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        string[] forbiddenPatterns =
        [
            "FindFirstValue(\"sub\")",
            "FindFirstValue(JwtRegisteredClaimNames.Sub)",
            "FindFirstValue(ClaimTypes.NameIdentifier)"
        ];

        var violations = new List<string>();
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var pattern in forbiddenPatterns)
            {
                if (content.Contains(pattern, StringComparison.Ordinal))
                    violations.Add($"{Path.GetFileName(file)} contains {pattern}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void MediatR_commands_have_validators_unless_explicitly_allowlisted()
    {
        var appAssembly = typeof(TOEICMaster.Application.DependencyInjection).Assembly;

        var commandTypes = appAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } &&
                           type.Name.EndsWith("Command", StringComparison.Ordinal) &&
                           (typeof(IRequest).IsAssignableFrom(type) ||
                            type.GetInterfaces().Any(iface =>
                                iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IRequest<>))))
            .ToArray();

        Assert.NotEmpty(commandTypes);

        var validatedTypes = appAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(iface => iface.GetGenericArguments()[0])
            .ToHashSet();

        // Explicit allowlist of parameterless/ID-only action commands and legacy Wave 3 exceptions.
        // Zero new AdminUsers commands are permitted here.
        HashSet<string> allowlist =
        [
            // Parameterless / ID-only / file-upload action commands
            "CompleteMediaUploadIntentCommand",
            "DeleteFlashcardDeckCommand",
            "DeleteProfileAvatarCommand",
            "ForkExamRevisionCommand",
            "NextSpeakingPhaseCommand",
            "PublishExamRevisionCommand",
            "ResetFlashcardDeckProgressCommand",
            "StartExamAttemptCommand",
            "StartReadingCommand",
            "SubmitExamAttemptCommand",
            "AbandonExamAttemptCommand",
            "ToggleQuestionFlagCommand",
            "UploadExamImageCommand",
            "UploadKnowledgeImageCommand",
            "UploadProfileAvatarCommand",
            "UploadSpeakingImageCommand",

            // TODO(Wave-3): Add FluentValidation validators for remaining legacy payload commands
            "ArchiveSpeakingExerciseCommand",
            "CloneSpeakingTestCommand",
            "CreateSpeakingExerciseCommand",
            "CreateSpeakingTestCommand",
            "PublishSpeakingTestCommand",
            "SaveExamAttemptAnswersCommand",
            "SubmitLrPracticeCommand",
            "SubmitSpeakingAttemptCommand",
            "UpdateSpeakingExerciseCommand",
            "UpdateSpeakingTestCommand"
        ];

        var missing = commandTypes
            .Where(type => !validatedTypes.Contains(type) && !allowlist.Contains(type.Name))
            .Select(type => type.FullName!)
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Application_and_infrastructure_use_TimeProvider_instead_of_DateTime_UtcNow_or_Now()
    {
        var root = FindRepoRoot();
        string[] targetDirs =
        [
            Path.Combine(root.FullName, "src", "TOEICMaster.Application"),
            Path.Combine(root.FullName, "src", "TOEICMaster.Infrastructure")
        ];

        var pattern = new Regex(@"\bDateTime(?:Offset)?\.(?:UtcNow|Now)\b", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var dir in targetDirs)
        {
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root.FullName, file).Replace('\\', '/');
                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (pattern.IsMatch(lines[index]))
                        violations.Add($"{relative}:{index + 1}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Global_json_options_use_PascalCase_string_enums_and_no_incompatible_dto_converters()
    {
        using var factory = new OpenApiSnapshotFactory();
        var mvcOptions = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value
            .JsonSerializerOptions;
        var httpOptions = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

        Assert.Contains(mvcOptions.Converters, converter => converter is JsonStringEnumConverter);
        Assert.Contains(httpOptions.Converters, converter => converter is JsonStringEnumConverter);
        Assert.Equal("\"Draft\"", JsonSerializer.Serialize(ExamRevisionStatus.Draft, mvcOptions));
        Assert.Equal("\"InProgress\"", JsonSerializer.Serialize(ExamAttemptStatus.InProgress, mvcOptions));
        Assert.Equal("\"Draft\"", JsonSerializer.Serialize(ExamRevisionStatus.Draft, httpOptions));
        Assert.Equal("\"InProgress\"", JsonSerializer.Serialize(ExamAttemptStatus.InProgress, httpOptions));

        // Verify no DTO enum property on Api or Application assemblies overrides enum serialization with an integer converter.
        Assembly[] dtoAssemblies =
        [
            typeof(AdminUsersController).Assembly,
            typeof(TOEICMaster.Application.DependencyInjection).Assembly
        ];
        var badConverters = new List<string>();
        foreach (var assembly in dtoAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    if (!underlying.IsEnum)
                        continue;

                    var converterAttr = property.GetCustomAttribute<JsonConverterAttribute>();
                    if (converterAttr?.ConverterType is { } converterType &&
                        !typeof(JsonStringEnumConverter).IsAssignableFrom(converterType) &&
                        !(converterType.IsGenericType &&
                          converterType.GetGenericTypeDefinition() == typeof(JsonStringEnumConverter<>)))
                    {
                        badConverters.Add($"{type.FullName}.{property.Name}: {converterType.Name}");
                    }
                }
            }
        }

        Assert.Empty(badConverters);
    }
}
