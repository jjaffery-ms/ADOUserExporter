namespace ADOUserExporter.Configuration;

public sealed class AppSettings
{
    public AzureDevOpsSettings AzureDevOps { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
}

public sealed class AzureDevOpsSettings
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;
}

public sealed class OutputSettings
{
    public string FilePath { get; set; } = "ado-users.csv";
}
