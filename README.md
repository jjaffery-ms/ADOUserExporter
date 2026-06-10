# ADO User Exporter

A small .NET console application that connects to an Azure DevOps organization and exports its users — along with the groups each user belongs to — into a CSV file.

It uses the [Azure DevOps Graph REST API](https://learn.microsoft.com/rest/api/azure/devops/graph) to:

- Enumerate all **groups** in the organization (with pagination).
- Enumerate all **users** in the organization (with pagination).
- Resolve each user's **group memberships**.
- Write the results to a CSV file with the columns `DisplayName`, `Email`, `PrincipalName`, and `Groups`.

## Output

The generated CSV looks like this:

| DisplayName | Email | PrincipalName | Groups |
| ----------- | ----- | ------------- | ------ |
| Jane Doe | jane@contoso.com | jane@contoso.com | Project Administrators; Contributors |

Values containing commas, quotes, or newlines are automatically quoted and escaped, and the file is written as UTF-8 (with BOM).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An Azure DevOps organization you have access to
- A **Personal Access Token (PAT)** with permission to read users and groups (the **Member Entitlement Management** / **Graph (Read)** scopes)

## Getting Started

### 1. Clone the repository

```pwsh
git clone <your-repo-url>
cd ADOUserExporter
```

### 2. Configure your organization

Edit [appsettings.json](appsettings.json) and set your Azure DevOps organization name:

```json
{
  "AzureDevOps": {
    "Organization": "your-organization",
    "Project": "Demos"
  },
  "Output": {
    "FilePath": "ado-users-export.csv"
  }
}
```

- `Organization` — the name of your Azure DevOps organization (the part in `https://dev.azure.com/<organization>`).
- `Project` — included for context; the export operates at the organization level.
- `Output:FilePath` — the path of the CSV file to write. Relative paths are resolved against the working directory.

### 3. Provide your Personal Access Token

The PAT is a secret, so it is read from an environment variable rather than the config file (to keep it out of source control). Set the `ADO_PAT_FOR_READING_USERS` environment variable before running.

**PowerShell**

```pwsh
$env:ADO_PAT_FOR_READING_USERS = "<your-pat>"
```

**Command Prompt**

```cmd
set ADO_PAT_FOR_READING_USERS=<your-pat>
```

**Bash**

```bash
export ADO_PAT_FOR_READING_USERS="<your-pat>"
```

### 4. Run the application

```pwsh
dotnet run
```

You'll see progress as the tool fetches groups, fetches users, and resolves memberships:

```
Connecting to organization 'your-organization'...
Fetching groups...
  Found 42 groups.
Fetching users...
  Found 137 users.
Resolving group memberships...
  Processed 25/137 users...
  ...
Export complete. Wrote 137 users to 'ado-users-export.csv'.
```

## Exit Codes

| Code | Meaning |
| ---- | ------- |
| `0` | Success |
| `1` | Missing or invalid configuration (organization not set, or PAT environment variable not set) |
| `2` | Error communicating with Azure DevOps |

## Project Structure

```
ADOUserExporter/
├── Program.cs                  # Entry point: orchestrates the export and writes the CSV
├── appsettings.json            # Organization, project, and output configuration
├── Configuration/
│   └── AppSettings.cs          # Strongly-typed configuration model
├── Models/
│   └── GraphModels.cs          # DTOs for the Azure DevOps Graph API responses
└── Services/
    └── AzureDevOpsClient.cs    # Thin client over the Graph REST API
```

## How It Works

1. Configuration is loaded from `appsettings.json` and bound to `AppSettings`.
2. The PAT is read from the `ADO_PAT_FOR_READING_USERS` environment variable and used for Basic authentication against `https://vssps.dev.azure.com/`.
3. All groups and users are retrieved, following the `X-MS-ContinuationToken` header for pagination.
4. For each user with an email address, the tool requests the groups the user is a direct member of (`direction=up`) and maps the returned descriptors back to friendly group names.
5. The combined rows are written to the configured CSV file.

## Security Notes

- Never commit your Personal Access Token. It is intentionally read from an environment variable.
- Grant the PAT only the minimum scopes required to read users and groups.
- Treat the generated CSV as sensitive, since it contains user identity information.
