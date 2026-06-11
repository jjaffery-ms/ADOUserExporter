using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ADOUserExporter.Configuration;
using ADOUserExporter.Models;

namespace ADOUserExporter.Services;

/// <summary>
/// Thin client over the Azure DevOps Graph and Core REST APIs used to enumerate
/// users, groups, projects, and memberships. Works against both Azure DevOps
/// Services (cloud) and Azure DevOps Server (on-premises, e.g. 2022).
/// </summary>
public sealed class AzureDevOpsClient : IDisposable
{
    private const string ContinuationTokenHeader = "X-MS-ContinuationToken";

    private readonly HttpClient _http;
    private readonly string _graphBaseUrl;
    private readonly string _coreBaseUrl;
    private readonly string _graphApiVersion;
    private readonly string _projectsApiVersion;

    public AzureDevOpsClient(AzureDevOpsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _graphApiVersion = settings.GraphApiVersion;
        _projectsApiVersion = settings.ProjectsApiVersion;

        if (settings.UseServer)
        {
            // Azure DevOps Server (on-premises): Graph and Core both live under
            // the collection URL. There is no separate vssps.* host.
            var collection = settings.CollectionUrl.TrimEnd('/');
            _graphBaseUrl = collection;
            _coreBaseUrl = collection;
        }
        else
        {
            // Azure DevOps Services (cloud): Graph is hosted on vssps.dev.azure.com
            // while the Core (projects) API is on dev.azure.com.
            var organization = Uri.EscapeDataString(settings.Organization);
            _graphBaseUrl = $"https://vssps.dev.azure.com/{organization}";
            _coreBaseUrl = $"https://dev.azure.com/{organization}";
        }

        var authToken = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($":{settings.PersonalAccessToken}"));

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", authToken);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Returns all users in the organization/collection, following pagination.</summary>
    public Task<List<GraphUser>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        GetPagedFromUriAsync<GraphUser>(
            $"{_graphBaseUrl}/_apis/graph/users", _graphApiVersion, cancellationToken);

    /// <summary>Returns all groups in the organization/collection, following pagination.</summary>
    public Task<List<GraphGroup>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        GetPagedFromUriAsync<GraphGroup>(
            $"{_graphBaseUrl}/_apis/graph/groups", _graphApiVersion, cancellationToken);

    /// <summary>
    /// Returns all team projects in the organization/collection, following
    /// pagination. This uses the Core REST API rather than the Graph API.
    /// </summary>
    public Task<List<AzureDevOpsProject>> GetProjectsAsync(CancellationToken cancellationToken = default) =>
        GetPagedFromUriAsync<AzureDevOpsProject>(
            $"{_coreBaseUrl}/_apis/projects", _projectsApiVersion, cancellationToken);

    /// <summary>
    /// Returns the group descriptors that the given subject (user) is a direct
    /// member of (direction=up).
    /// </summary>
    public async Task<List<string>> GetMembershipContainersAsync(
        string subjectDescriptor,
        CancellationToken cancellationToken = default)
    {
        var requestUri =
            $"{_graphBaseUrl}/_apis/graph/Memberships/{Uri.EscapeDataString(subjectDescriptor)}" +
            $"?direction=up&api-version={_graphApiVersion}";

        using var response = await _http.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GraphListResponse<GraphMembership>>(
            stream, cancellationToken: cancellationToken);

        return payload?.Value
            .Where(m => !string.IsNullOrEmpty(m.ContainerDescriptor))
            .Select(m => m.ContainerDescriptor!)
            .ToList() ?? new List<string>();
    }

    private async Task<List<T>> GetPagedFromUriAsync<T>(
        string requestUriBase,
        string apiVersion,
        CancellationToken cancellationToken)
    {
        var results = new List<T>();
        string? continuationToken = null;

        do
        {
            var requestUri = $"{requestUriBase}?api-version={apiVersion}";
            if (!string.IsNullOrEmpty(continuationToken))
            {
                requestUri += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";
            }

            using var response = await _http.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<GraphListResponse<T>>(
                stream, cancellationToken: cancellationToken);

            if (payload?.Value is { Count: > 0 })
            {
                results.AddRange(payload.Value);
            }

            continuationToken = response.Headers.TryGetValues(ContinuationTokenHeader, out var values)
                ? values.FirstOrDefault()
                : null;
        }
        while (!string.IsNullOrEmpty(continuationToken));

        return results;
    }

    public void Dispose() => _http.Dispose();
}
