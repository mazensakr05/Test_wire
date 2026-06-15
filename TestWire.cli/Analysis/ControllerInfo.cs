namespace TestWire.cli.Analysis;

public sealed record ControllerInfo(
    string ClassName,
    string Namespace,
    string BaseRoute,
    List<EndpointInfo> Endpoints,
    List<ConstructorDependency> Dependencies
)
{
    // E.g. "MyApp.Controllers" -> "MyApp"
    public string ProjectNamespace => Namespace.Replace(".Controllers", "");
}