namespace TestWire.cli.Generation;

public enum TestFramework { XUnit, NUnit }

/// <summary>
/// Immutable options bag threaded through all generation steps.
///
/// Adding a new generation option here is sufficient — no cascading positional
/// parameter additions across every method signature needed.
/// </summary>
public sealed record GenerationContext(
    string ProjectNamespace,
    string TargetFramework,
    TestFramework Framework    = TestFramework.XUnit,
    bool OverwriteExisting     = false
);
