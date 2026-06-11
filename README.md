# ADO User Exporter

A small .NET console application that connects to an Azure DevOps organization (cloud) or an on-premises **Azure DevOps Server** (e.g. 2022) and exports its users — along with the groups each user belongs to — into a CSV file.

It uses the [Azure DevOps Graph REST API](https://learn.microsoft.com/rest/api/azure/devops/graph) to:

- Enumerate all **groups** in the organization (with pagination).
- Enumerate all **users** in the organization (with pagination).
- Enumerate all **projects** in the organization (with pagination).
- Resolve each user's **group memberships**, splitting them into organization-level groups and the groups the user belongs to within each project.
- Write the results to a CSV file with the columns `DisplayName`, `Email`, `PrincipalName`, `OrganizationGroups`, `Projects`, and `ProjectGroups`.

## Output

The generated CSV looks like this:

| DisplayName | Email | PrincipalName | OrganizationGroups | Projects | ProjectGroups |
| ----------- | ----- | ------------- | ------------------ | -------- | ------------- |
| Jane Doe | jane@contoso.com | jane@contoso.com | Project Collection Valid Users | Web; Mobile | Web: Contributors, Readers; Mobile: Build Administrators |

- `OrganizationGroups` — organization (collection) level groups the user is a direct member of.
- `Projects` — the distinct projects the user is associated with (derived from project-scoped group membership).
- `ProjectGroups` — for each project, the groups the user belongs to within that project, formatted as `ProjectName: Group1, Group2`.

Values containing commas, quotes, or newlines are automatically quoted and escaped, and the file is written as UTF-8 (with BOM).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An Azure DevOps organization (cloud) or an on-premises Azure DevOps Server 2019+ (the Graph REST API is available on Server 2019 and later, including 2022) that you have access to
- A **Personal Access Token (PAT)** scoped to **Graph (Read)** and **Project and Team (Read)**. The Graph scope is required to enumerate users, groups, and memberships; the Project and Team scope is required to enumerate projects and map project-scoped groups to project names.

## Getting Started

### 1. Clone the repository

```pwsh
git clone <your-repo-url>
cd ADOUserExporter
```

### 2. Configure your organization or server

Edit [appsettings.json](appsettings.json).

**Azure DevOps Services (cloud)** — set your organization name:

```json
{
  "AzureDevOps": {
    "Organization": "your-organization"
  },
  "Output": {
    "FilePath": "ado-users-export.csv"
  }
}
```

**Azure DevOps Server (on-premises, e.g. 2022)** — set the full collection URL instead. When `CollectionUrl` is set it takes precedence over `Organization`:

```json
{
  "AzureDevOps": {
    "CollectionUrl": "https://tfs.contoso.com/DefaultCollection"
  },
  "Output": {
    "FilePath": "ado-users-export.csv"
  }
}
```

- `Organization` — (cloud) the name of your Azure DevOps organization (the part in `https://dev.azure.com/<organization>`).
- `CollectionUrl` — (on-premises) the full collection URL, e.g. `https://tfs.contoso.com/DefaultCollection` or `https://tfs.contoso.com/tfs/DefaultCollection`. Unlike the cloud, Azure DevOps Server has no separate `vssps.*` host — both the Graph and Core APIs are served from the collection URL.
- `GraphApiVersion` / `ProjectsApiVersion` — optional REST API version overrides. The defaults (`7.1-preview.1` and `7.1`) work for Azure DevOps Services and Azure DevOps Server 2022. For older server versions, set lower values (e.g. `6.0-preview.1` / `6.0`).
- `Output:FilePath` — the path of the CSV file to write. Relative paths are resolved against the working directory.

### 3. Provide your Personal Access Token

The PAT must be scoped to **Graph (Read)** and **Project and Team (Read)** so the tool can read users, groups, memberships, and projects. When creating the token in Azure DevOps, select those scopes (you may need to choose "Show all scopes" to see them).

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
Connecting to 'your-organization'...
Fetching groups...
  Found 42 groups.
Fetching projects...
  Found 8 projects.
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
| `1` | Missing or invalid configuration (neither organization nor collection URL set, or PAT environment variable not set) |
| `2` | Error communicating with Azure DevOps |

## Project Structure

```
ADOUserExporter/
├── Program.cs                  # Entry point: orchestrates the export and writes the CSV
├── appsettings.json            # Organization, and output configuration
├── Configuration/
│   └── AppSettings.cs          # Strongly-typed configuration model
├── Models/
│   └── GraphModels.cs          # DTOs for the Azure DevOps Graph API responses
└── Services/
    └── AzureDevOpsClient.cs    # Thin client over the Graph REST API
```

## How It Works

1. Configuration is loaded from `appsettings.json` and bound to `AppSettings`.
2. The PAT is read from the `ADO_PAT_FOR_READING_USERS` environment variable and used for Basic authentication. For cloud, requests go to `https://vssps.dev.azure.com/` (Graph API) and `https://dev.azure.com/` (Core/projects API). For Azure DevOps Server, both APIs are served from the configured collection URL.
3. All groups, projects, and users are retrieved, following the `X-MS-ContinuationToken` header for pagination.
4. For each user with an email address, the tool requests the groups the user is a direct member of (`direction=up`) and maps the returned descriptors back to friendly group names. Each group's container domain is used to determine whether it is an organization-level group or a project-scoped group (`vstfs:///Classification/TeamProject/{projectId}`), and project ids are mapped to project names.
5. The combined rows are written to the configured CSV file.

## Security Notes

- Never commit your Personal Access Token. It is intentionally read from an environment variable.
- Grant the PAT only the minimum scopes required to read users and groups.
- Treat the generated CSV as sensitive, since it contains user identity information.
