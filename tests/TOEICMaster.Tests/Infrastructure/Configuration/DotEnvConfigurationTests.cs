using Microsoft.Extensions.Configuration;
using TOEICMaster.Api.Configuration;

namespace TOEICMaster.Tests.Infrastructure.Configuration;

public sealed class DotEnvConfigurationTests
{
    [Fact]
    public void Loader_maps_project_variables_and_preserves_connection_string_characters()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toeicmaster-{Guid.NewGuid():N}.env");
        try
        {
            File.WriteAllText(path, """
                # local configuration
                DATABASE_CONNECTION_STRING="Host=direct.neon.tech;Password=p=a$s;SSL Mode=VerifyFull"
                JWT_SIGNING_KEY='local-signing-key'
                REDIS_PASSWORD=local-redis-password
                SMTP_PORT=587
                ADMIN_SEED_EMAIL=admin@toeicmaster.test
                Custom__Value=custom
                """);

            var configuration = new ConfigurationBuilder().AddToeicMasterDotEnvFile(path).Build();

            Assert.Equal("Host=direct.neon.tech;Password=p=a$s;SSL Mode=VerifyFull",
                configuration.GetConnectionString("Postgres"));
            Assert.Equal("local-signing-key", configuration["Jwt:SigningKey"]);
            Assert.Contains("password=local-redis-password", configuration.GetConnectionString("Redis"));
            Assert.Equal("587", configuration["Smtp:Port"]);
            Assert.Equal("admin@toeicmaster.test", configuration["AdminBootstrap:Email"]);
            Assert.Equal("custom", configuration["Custom:Value"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Later_configuration_sources_can_override_dotenv_values()
    {
        var path = Path.Combine(Path.GetTempPath(), $"toeicmaster-{Guid.NewGuid():N}.env");
        try
        {
            File.WriteAllText(path, "JWT_SIGNING_KEY=from-dotenv");
            var configuration = new ConfigurationBuilder()
                .AddToeicMasterDotEnvFile(path)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = "from-deployment-environment"
                })
                .Build();

            Assert.Equal("from-deployment-environment", configuration["Jwt:SigningKey"]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
