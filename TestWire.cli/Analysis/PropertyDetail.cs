namespace TestWire.cli.Analysis;

public sealed record PropertyDetail(
    string Name,
    string Type,
    string FullyQualifiedType,
    IReadOnlyList<ValidationAttributeInfo> ValidationAttributes
);


public sealed record ValidationAttributeInfo(
    string Name,
    IReadOnlyList<string> Arguments
);