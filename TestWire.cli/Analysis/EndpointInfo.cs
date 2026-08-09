namespace TestWire.cli.Analysis;

public enum ReturnTypeKind
{
    Unknown,
    ActionResultOfT,
    IActionResultWithInferredT,
    PlainType

}

public sealed record EndpointInfo(
    string MethodName,
    string HttpVerb,
    string Route,
    string ReturnType,
    ReturnTypeKind ReturnTypeKind,
    bool HasAmbiguousReturnType,
    bool IsAsync,
    bool HasAuthorize,
    bool HasAllowAnonymous,
    int ExpectedStatusCode,
    List<ParameterDetail> Parameters,
    List<ProducesResponseDetail> ProducesResponses,
    List<PropertyDetail>? ReturnTypeProperties = null
);

public sealed record ParameterDetail(
    string Name,
    string Type,
    string FullyQualifiedType,
    bool IsFromBody,
    bool IsFromRoute,
    bool IsFromQuery,
    bool IsFromHeader,
    List<PropertyDetail> DtoProperties
);

public sealed record ProducesResponseDetail(
    int StatusCode,
    string? TypeName
);