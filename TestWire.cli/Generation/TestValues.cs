namespace TestWire.cli.Generation;

/// <summary>
/// Single source of truth for all test value generation.
///
/// Two distinct output contracts exist and must never be mixed:
///   - AsExpression  → C# source code  (quoted strings, Guid.NewGuid(), type-safe literals)
///   - AsRouteSegment → URL path literals (bare values, fixed GUID, no quotes)
///
/// Having a single class here is the explicit fix for the bug where two copies of
/// GetTestValueForType existed in MethodBodyBuilder and RouteBuilder with different
/// output contracts, making them easy to accidentally swap.
/// </summary>
public static class TestValues
{
    /// <summary>
    /// Returns a C# expression for use inside generated source code.
    /// Examples: <c>1</c>, <c>"test"</c>, <c>Guid.NewGuid()</c>, <c>true</c>
    /// </summary>
    public static string AsExpression(string type) => type.ToLowerInvariant() switch
    {
        "int" or "int32" or "int64" or "long"   => "1",
        "guid"                                   => "Guid.NewGuid()",
        "string"                                 => "\"test\"",
        "bool" or "boolean"                      => "true",
        "datetime" or "datetimeoffset"           => "DateTime.UtcNow",
        "dateonly"                               => "DateOnly.FromDateTime(DateTime.UtcNow)",
        "decimal"                                => "1.0m",
        "double" or "float"                      => "1.0",
        _                                        => "null"
    };

    /// <summary>
    /// Returns a literal value for embedding in a URL path segment or query string.
    /// Examples: <c>1</c>, <c>00000000-0000-0000-0000-000000000001</c>, <c>test</c>
    /// </summary>
    public static string AsRouteSegment(string type) => type.ToLowerInvariant() switch
    {
        "int" or "int32" or "int64" or "long"   => "1",
        "guid"                                   => "00000000-0000-0000-0000-000000000001",
        "string"                                 => "test",
        "bool" or "boolean"                      => "true",
        "datetime" or "datetimeoffset"           => "2024-01-01T00:00:00Z",
        "dateonly"                               => "2024-01-01",
        "decimal" or "double" or "float"         => "1",
        _                                        => "1"
    };

    /// <summary>
    /// Returns a "not found" route segment — a value that should not match any seeded data.
    /// GUIDs are randomised so they are guaranteed non-existent; integers use an implausibly
    /// large value; strings use a clearly fake sentinel.
    /// </summary>
    public static string AsNotFoundSegment(string type) => type.ToLowerInvariant() switch
    {
        "guid"   => Guid.NewGuid().ToString(),
        "string" => "nonexistent-xyz-404",
        _        => "99999"
    };

    /// <summary>Maps an HTTP status integer to its <c>HttpStatusCode</c> enum member expression.</summary>
    public static string ToStatusCodeExpression(int statusCode) => statusCode switch
    {
        200 => "HttpStatusCode.OK",
        201 => "HttpStatusCode.Created",
        202 => "HttpStatusCode.Accepted",
        204 => "HttpStatusCode.NoContent",
        400 => "HttpStatusCode.BadRequest",
        401 => "HttpStatusCode.Unauthorized",
        404 => "HttpStatusCode.NotFound",
        409 => "HttpStatusCode.Conflict",
        _   => $"(HttpStatusCode){statusCode}"
    };

    /// <summary>
    /// Returns true when the status code implies a typed JSON response body.
    /// 204 No Content and all 4xx/5xx codes must NOT attempt to deserialise the response body.
    /// </summary>
    public static bool HasResponseBody(int statusCode) =>
        statusCode is >= 200 and < 300 and not 204;
}
