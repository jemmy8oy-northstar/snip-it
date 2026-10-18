using System.Text.Json;
using Balenthiran.Snipit.Abstractions.DataModels;
using Balenthiran.Snipit.DataModels.Models;
using Balenthiran.Snipit.WebApi;

namespace Balenthiran.Snipit.Tests.Api;

/// <summary>
/// Pins the wire format the committed <c>openapi.json</c> promises. The document is generated
/// from these same options at build time, so a change here that isn't reflected in the schema
/// (or vice versa) is exactly the drift that let the frontend talk to a fixture for a month.
/// </summary>
public class ApiJsonContractTests
{
    private static JsonSerializerOptions WebApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        SnipitJsonOptions.Configure(options);
        return options;
    }

    [Fact]
    public void JobStatus_SerialisesAsItsName_NotItsOrdinal()
    {
        var json = JsonSerializer.Serialize(NewJob(JobStatus.Completed), WebApiOptions());

        Assert.Contains("\"status\":\"Completed\"", json);
        Assert.DoesNotContain("\"status\":2", json);
    }

    [Fact]
    public void JobStatus_RoundTripsFromItsName()
    {
        var json = JsonSerializer.Serialize(NewJob(JobStatus.Failed), WebApiOptions());

        var parsed = JsonSerializer.Deserialize<TranscriptionJob>(json, WebApiOptions());

        Assert.Equal(JobStatus.Failed, parsed!.Status);
    }

    /// <summary>
    /// <c>Error</c> is required-but-nullable, so the schema says the key is always there. If the
    /// serialiser ever started omitting nulls, every response would violate its own document and
    /// the generated client's non-optional `error: string | null` would be a lie.
    /// </summary>
    [Fact]
    public void NullError_IsStillWrittenAsAnExplicitNull()
    {
        var json = JsonSerializer.Serialize(NewJob(JobStatus.Pending), WebApiOptions());

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("error", out var error));
        Assert.Equal(JsonValueKind.Null, error.ValueKind);
    }

    [Fact]
    public void Numbers_AreWrittenAsJsonNumbers()
    {
        var json = JsonSerializer.Serialize(
            new TranscriptWord { Text = "hello", Start = 1.5, End = 2, Kept = true },
            WebApiOptions());

        Assert.Contains("\"start\":1.5", json);
        Assert.DoesNotContain("\"start\":\"1.5\"", json);
    }

    /// <summary>
    /// The reason the generated client no longer carries `number | string` unions: the API does
    /// not accept a number sent as a string, so the document does not have to advertise it.
    /// </summary>
    [Fact]
    public void NumberSentAsAString_IsRejected()
    {
        const string json = """{"text":"hello","start":"1.5","end":2,"kept":true}""";

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<TranscriptWord>(json, WebApiOptions()));
    }

    /// <summary>`required` is enforced by the deserialiser, not just documented — which is what
    /// makes it stronger than a `[Required]` annotation nothing validates.</summary>
    [Fact]
    public void MissingRequiredProperty_IsRejected()
    {
        const string missingKept = """{"text":"hello","start":1.5,"end":2}""";

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<TranscriptWord>(missingKept, WebApiOptions()));
    }

    private static TranscriptionJob NewJob(JobStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        Error = null,
        CreatedAt = DateTime.UtcNow,
    };
}
