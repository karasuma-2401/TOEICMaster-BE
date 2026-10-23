using System.Text;

namespace TOEICMaster.Api.Configuration;

public static class DotEnvConfiguration
{
    private static readonly IReadOnlyDictionary<string, string> KeyMappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DATABASE_CONNECTION_STRING"] = "ConnectionStrings:Postgres",
            ["JWT_SIGNING_KEY"] = "Jwt:SigningKey",
            ["GOOGLE_CLIENT_ID"] = "Google:ClientId",
            ["EMAIL_VERIFICATION_HASH_KEY"] = "EmailVerification:HashKey",
            ["SMTP_HOST"] = "Smtp:Host",
            ["SMTP_PORT"] = "Smtp:Port",
            ["SMTP_SECURITY"] = "Smtp:Security",
            ["SMTP_FROM_ADDRESS"] = "Smtp:FromAddress",
            ["SMTP_FROM_NAME"] = "Smtp:FromName",
            ["SMTP_USERNAME"] = "Smtp:Username",
            ["SMTP_PASSWORD"] = "Smtp:Password",
            ["MINIO_ROOT_USER"] = "Minio:AccessKey",
            ["MINIO_ROOT_PASSWORD"] = "Minio:SecretKey",
            ["MEDIATR_LICENSE_KEY"] = "MediatR:LicenseKey",
            ["AUTOMAPPER_LICENSE_KEY"] = "AutoMapper:LicenseKey",
            ["GEMINI_ENABLED"] = "Gemini:Enabled",
            ["GEMINI_API_KEY"] = "Gemini:ApiKey",
            ["GEMINI_MODEL"] = "Gemini:Model",
            ["GEMINI_REQUEST_TIMEOUT_SECONDS"] = "Gemini:RequestTimeoutSeconds",
            ["GEMINI_RUBRIC_VERSION"] = "Gemini:RubricVersion"
        };

    public static string? FindFile(params string[] searchRoots)
    {
        foreach (var searchRoot in searchRoots.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var directory = new DirectoryInfo(Path.GetFullPath(searchRoot));
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, ".env");
                if (File.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }
        }
        return null;
    }

    public static IConfigurationBuilder AddToeicMasterDotEnvFile(this IConfigurationBuilder configuration,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(path, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;
            var key = line[..separator].Trim().TrimStart('\uFEFF');
            var value = Unquote(line[(separator + 1)..].Trim());
            if (key.Equals("REDIS_PASSWORD", StringComparison.OrdinalIgnoreCase))
            {
                values["ConnectionStrings:Redis"] =
                    $"localhost:6379,password={value},abortConnect=false,connectTimeout=3000,syncTimeout=3000";
                continue;
            }
            var configurationKey = KeyMappings.TryGetValue(key, out var mapped)
                ? mapped
                : key.Replace("__", ":", StringComparison.Ordinal);
            values[configurationKey] = value;
        }
        return configuration.AddInMemoryCollection(values);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') ||
                                  (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];
        return value;
    }
}
