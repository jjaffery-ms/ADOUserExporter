using System.Text.Json.Serialization;

namespace ADOUserExporter.Models;

public sealed class GraphUser
{
    [JsonPropertyName("subjectKind")]
    public string? SubjectKind { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("mailAddress")]
    public string? MailAddress { get; set; }

    [JsonPropertyName("principalName")]
    public string? PrincipalName { get; set; }

    [JsonPropertyName("descriptor")]
    public string? Descriptor { get; set; }
}

public sealed class GraphGroup
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("principalName")]
    public string? PrincipalName { get; set; }

    [JsonPropertyName("descriptor")]
    public string? Descriptor { get; set; }
}

public sealed class GraphMembership
{
    [JsonPropertyName("containerDescriptor")]
    public string? ContainerDescriptor { get; set; }

    [JsonPropertyName("memberDescriptor")]
    public string? MemberDescriptor { get; set; }
}

public sealed class GraphListResponse<T>
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
}
