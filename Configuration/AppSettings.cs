namespace ADOUserExporter.Configuration;

public sealed class AppSettings
{
    public AzureDevOpsSettings AzureDevOps { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
}

public sealed class AzureDevOpsSettings
{
    /// <summary>
    /// Azure DevOps Services (cloud) organization name (the part in
    /// <c>https://dev.azure.com/{organization}</c>). Leave empty when targeting
    /// an on-premises Azure DevOps Server via <see cref="CollectionUrl"/>.
    /// </summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps Server (on-premises) collection URL, e.g.
    /// <c>https://tfs.contoso.com/DefaultCollection</c> or
    /// <c>https://tfs.contoso.com/tfs/DefaultCollection</c>. When set, this takes
    /// precedence over <see cref="Organization"/> and the tool runs against the
    /// on-premises server instead of the cloud.
    /// </summary>
    public string CollectionUrl { get; set; } = string.Empty;

    public string Project { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// REST API version used for the Graph endpoints. Azure DevOps Server 2022
    /// supports <c>7.1-preview.1</c>; override for older server versions
    /// (e.g. <c>6.0-preview.1</c>).
    /// </summary>
    public string GraphApiVersion { get; set; } = "7.1-preview.1";

    /// <summary>
    /// REST API version used for the Core (projects) endpoint. Azure DevOps
    /// Server 2022 supports <c>7.1</c>; override for older server versions.
    /// </summary>
    public string ProjectsApiVersion { get; set; } = "7.1";

    /// <summary>
    /// True when targeting an on-premises Azure DevOps Server (a collection URL
    /// has been configured).
    /// </summary>
    public bool UseServer => !string.IsNullOrWhiteSpace(CollectionUrl);
}

public sealed class OutputSettings
{
    public string FilePath { get; set; } = "ado-users.csv";
}
