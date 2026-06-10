using System.Text;
using ADOUserExporter.Configuration;
using ADOUserExporter.Models;
using ADOUserExporter.Services;
using Microsoft.Extensions.Configuration;

const string PatEnvironmentVariable = "ADO_PAT_FOR_READING_USERS";

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var settings = configuration.Get<AppSettings>() ?? new AppSettings();

// The PAT is a secret and is read from an environment variable rather than the
// config file so it is never committed to source control.
settings.AzureDevOps.PersonalAccessToken =
    Environment.GetEnvironmentVariable(PatEnvironmentVariable) ?? string.Empty;

if (!ValidateSettings(settings.AzureDevOps))
{
    return 1;
}

try
{
    using var client = new AzureDevOpsClient(settings.AzureDevOps);

    Console.WriteLine($"Connecting to organization '{settings.AzureDevOps.Organization}'...");

    Console.WriteLine("Fetching groups...");
    var groups = await client.GetGroupsAsync();
    var groupsByDescriptor = groups
        .Where(g => !string.IsNullOrEmpty(g.Descriptor))
        .ToDictionary(g => g.Descriptor!, g => g);
    Console.WriteLine($"  Found {groups.Count} groups.");

    Console.WriteLine("Fetching users...");
    var users = await client.GetUsersAsync();
    Console.WriteLine($"  Found {users.Count} users.");

    var rows = new List<UserGroupRow>();

    Console.WriteLine("Resolving group memberships...");
    var processed = 0;
    foreach (var user in users)
    {
        processed++;
        if (string.IsNullOrEmpty(user.Descriptor))
        {
            continue;
        }

        if (string.IsNullOrWhiteSpace(user.MailAddress))
        {
            continue;
        }

        var containerDescriptors = await client.GetMembershipContainersAsync(user.Descriptor);

        var groupNames = containerDescriptors
            .Select(descriptor =>
                groupsByDescriptor.TryGetValue(descriptor, out var group)
                    ? group.DisplayName ?? group.PrincipalName ?? descriptor
                    : descriptor)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        rows.Add(new UserGroupRow(
            user.DisplayName ?? string.Empty,
            user.MailAddress ?? string.Empty,
            user.PrincipalName ?? string.Empty,
            string.Join("; ", groupNames)));

        if (processed % 25 == 0)
        {
            Console.WriteLine($"  Processed {processed}/{users.Count} users...");
        }
    }

    WriteCsv(settings.Output.FilePath, rows);

    Console.WriteLine($"Export complete. Wrote {rows.Count} users to '{settings.Output.FilePath}'.");
    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Error communicating with Azure DevOps: {ex.Message}");
    return 2;
}

static bool ValidateSettings(AzureDevOpsSettings ado)
{
    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(ado.Organization) || ado.Organization == "your-organization")
    {
        missing.Add("AzureDevOps:Organization in appsettings.json");
    }
    if (string.IsNullOrWhiteSpace(ado.PersonalAccessToken))
    {
        missing.Add("environment variable 'ADO_PAT_FOR_READING_USERS'");
    }

    if (missing.Count > 0)
    {
        Console.Error.WriteLine(
            "Missing required configuration: " + string.Join(", ", missing));
        return false;
    }

    return true;
}

static void WriteCsv(string filePath, IReadOnlyList<UserGroupRow> rows)
{
    var sb = new StringBuilder();
    sb.AppendLine("DisplayName,Email,PrincipalName,Groups");

    foreach (var row in rows)
    {
        sb.Append(Escape(row.DisplayName)).Append(',')
          .Append(Escape(row.Email)).Append(',')
          .Append(Escape(row.PrincipalName)).Append(',')
          .Append(Escape(row.Groups))
          .Append('\n');
    }

    File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
}

static string Escape(string value)
{
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    return value;
}

internal readonly record struct UserGroupRow(
    string DisplayName,
    string Email,
    string PrincipalName,
    string Groups);
