using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ADOUserExporter.Configuration;
using ADOUserExporter.Models;

namespace ADOUserExporter.Services;

/// <summary>
/// Thin client over the Azure DevOps Graph REST API used to enumerate users,
/// groups, and their memberships for an organization.
/// </summary>
public sealed class AzureDevOpsClient : IDisposable
{
    private const string ApiVersion = "7.1-preview.1";
    private const string ContinuationTokenHeader = "X-MS-ContinuationToken";

    private readonly HttpClient _http;
    private readonly string _organization;

    public AzureDevOpsClient(AzureDevOpsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _organization = Uri.EscapeDataString(settings.Organization);

        var authToken = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($":{settings.PersonalAccessToken}"));

        _http = new HttpClient
        {
            BaseAddress = new Uri("https://vssps.dev.azure.com/")
        };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", authToken);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Returns all users in the organization, following pagination.</summary>
    public Task<List<GraphUser>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        GetPagedAsync<GraphUser>("graph/users", cancellationToken);

    /// <summary>Returns all groups in the organization, following pagination.</summary>
    public Task<List<GraphGroup>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        GetPagedAsync<GraphGroup>("graph/groups", cancellationToken);

    /// <summary>
    /// Returns the group descriptors that the given subject (user) is a direct
    /// member of (direction=up).
    /// </summary>
    public async Task<List<string>> GetMembershipContainersAsync(
        string subjectDescriptor,
        CancellationToken cancellationToken = default)
    {
        var requestUri =
            $"{_organization}/_apis/graph/Memberships/{Uri.EscapeDataString(subjectDescriptor)}" +
            $"?direction=up&api-version={ApiVersion}";

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

    private async Task<List<T>> GetPagedAsync<T>(
        string resource,
        CancellationToken cancellationToken)
    {
        var results = new List<T>();
        string? continuationToken = null;

        do
        {
            var requestUri =
                $"{_organization}/_apis/{resource}?api-version={ApiVersion}";
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
