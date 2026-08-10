using System.Text.Json;
using System.Text.Json.Serialization;

namespace Balenthiran.Snipit.WebApi;

/// <summary>
/// The API's JSON contract, in one place so tests exercise the same configuration the app runs
/// rather than a re-declared copy of it.
/// </summary>
public static class SnipitJsonOptions
{
    /// <summary>
    /// Applies the two deliberate departures from the minimal-API JSON defaults.
    ///
    /// <para><b>Enums as names.</b> The default is the ordinal, which types <c>JobStatus</c> as a
    /// bare number in a generated client and forces consumers to hard-code that Completed == 2 —
    /// so reordering the C# enum silently breaks clients that still compile.</para>
    ///
    /// <para><b>Strict numbers.</b> <see cref="JsonSerializerDefaults.Web"/> enables
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/>, and .NET's schema exporter
    /// faithfully documents that as <c>["number","string"]</c> — so every double and int in the
    /// document generates a <c>number | string</c> union a caller has to narrow. Nothing sends
    /// numbers as strings, so the leniency bought nothing and cost the whole numeric surface.</para>
    /// </summary>
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.NumberHandling = JsonNumberHandling.Strict;
    }
}
