using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TOEICMaster.Tests.Api.Common;

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Database=test;Username=test;Password=test;Timeout=1",
                ["ConnectionStrings:Redis"] = "127.0.0.1:1,abortConnect=false,connectTimeout=100,syncTimeout=100",
                ["Minio:Endpoint"] = "127.0.0.1:1",
                ["Minio:AccessKey"] = "test-access",
                ["Minio:SecretKey"] = "test-secret",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Smtp:Host"] = "127.0.0.1",
                ["Smtp:Port"] = "1",
                ["Smtp:FromAddress"] = "noreply@toeicmaster.test",
                ["Smtp:Security"] = "None",
                ["Jwt:Issuer"] = "TOEICMaster.Tests",
                ["Jwt:Audience"] = "TOEICMaster.Tests.Client",
                ["Jwt:SigningKey"] = "test-only-signing-key-with-32-characters",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "1",
                ["Jwt:RememberMeRefreshTokenDays"] = "30",
                ["Google:ClientId"] = "test-client.apps.googleusercontent.com",
                ["EmailVerification:HashKey"] = "test-email-verification-key-32-characters"
            }));
    }
}
