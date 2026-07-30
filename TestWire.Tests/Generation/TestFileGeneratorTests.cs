using FluentAssertions;
using TestWire.cli.Analysis;
using TestWire.cli.Generation;
using Xunit;

namespace TestWire.Tests.Generation;

public class TestFileGeneratorTests
{
    private static ControllerInfo BuildController(bool includeGetById)
    {
        var postEndpoint = new EndpointInfo(
            MethodName: "Create",
            HttpVerb: "HttpPost",
            Route: "",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "dto",
                    Type: "CreateProductDto",
                    FullyQualifiedType: "SampleApi.DTOs.CreateProductDto",
                    IsFromBody: true,
                    IsFromRoute: false,
                    IsFromQuery: false,
                    IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var endpoints = new List<EndpointInfo> { postEndpoint };

        if (includeGetById)
        {
            var getByIdEndpoint = new EndpointInfo(
                MethodName: "GetById",
                HttpVerb: "HttpGet",
                Route: "{id}",
                ReturnType: "ProductDto",
                ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
                HasAmbiguousReturnType: false,
                IsAsync: true,
                HasAuthorize: false,
                HasAllowAnonymous: false,
                ExpectedStatusCode: 200,
                Parameters: new List<ParameterDetail>
                {
                    new ParameterDetail(
                        Name: "id",
                        Type: "int",
                        FullyQualifiedType: "System.Int32",
                        IsFromBody: false,
                        IsFromRoute: true,
                        IsFromQuery: false,
                        IsFromHeader: false,
                        DtoProperties: new List<PropertyDetail>()
                    )
                },
                ProducesResponses: new List<ProducesResponseDetail>()
            );

            endpoints.Add(getByIdEndpoint);
        }

        return new ControllerInfo(
            ClassName: "ProductsController",
            Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: endpoints,
            Dependencies: new List<ConstructorDependency>()
        );
    }

    private static GenerationContext BuildContext()
    {
        return new GenerationContext(
            ProjectNamespace: "SampleApi",
            TargetFramework: "net8.0",
            Framework: TestFramework.XUnit
        );
    }

    [Fact]
    public void Generate_IncludesCreateThenGetTest_WhenPostAndGetByIdExist()
    {
        // Arrange
        var controller = BuildController(includeGetById: true);
        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert
        result.Should().Contain("CreateThenGet_ReturnsCreatedResource");
        result.Should().Contain("PostAsJsonAsync");
        result.Should().Contain("GetAsync");
    }

    [Fact]
    public void Generate_DoesNotIncludeCreateThenGetTest_WhenGetByIdIsMissing()
    {
        // Arrange
        var controller = BuildController(includeGetById: false);
        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert
        result.Should().NotContain("CreateThenGet_ReturnsCreatedResource");
    }

    [Fact]
    public void Generate_DoesNotIncludeCreateThenGetTest_WhenPostIsMissing()
    {
        // Arrange — only GET-by-id, no POST
        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById",
            HttpVerb: "HttpGet",
            Route: "{id}",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "id", Type: "int", FullyQualifiedType: "System.Int32",
                    IsFromBody: false, IsFromRoute: true, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController",
            Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { getByIdEndpoint },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert
        result.Should().NotContain("CreateThenGet_ReturnsCreatedResource");
    }

    [Fact]
    public void Generate_DoesNotIncludeCreateThenGetTest_WhenReturnTypesMismatch()
    {
        // Arrange — POST returns ProductDto, GET-by-id returns a different type
        var postEndpoint = new EndpointInfo(
            MethodName: "Create",
            HttpVerb: "HttpPost",
            Route: "",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "dto", Type: "CreateProductDto", FullyQualifiedType: "SampleApi.DTOs.CreateProductDto",
                    IsFromBody: true, IsFromRoute: false, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById",
            HttpVerb: "HttpGet",
            Route: "{id}",
            ReturnType: "CategoryDto", // mismatched on purpose
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "id", Type: "int", FullyQualifiedType: "System.Int32",
                    IsFromBody: false, IsFromRoute: true, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController",
            Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { postEndpoint, getByIdEndpoint },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert — mismatched types must NOT be paired
        result.Should().NotContain("CreateThenGet_ReturnsCreatedResource");
    }

    [Fact]
    public void Generate_UsesAuthClient_WhenPostEndpointRequiresAuthorize()
    {
        // Arrange — [Authorize] on POST, should use _authClient in generated test
        var postEndpoint = new EndpointInfo(
            MethodName: "Create",
            HttpVerb: "HttpPost",
            Route: "",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: true,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "dto", Type: "CreateProductDto", FullyQualifiedType: "SampleApi.DTOs.CreateProductDto",
                    IsFromBody: true, IsFromRoute: false, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById",
            HttpVerb: "HttpGet",
            Route: "{id}",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: true,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "id", Type: "int", FullyQualifiedType: "System.Int32",
                    IsFromBody: false, IsFromRoute: true, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController",
            Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { postEndpoint, getByIdEndpoint },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert — combined test should use _authClient, not _client
        result.Should().Contain("CreateThenGet_ReturnsCreatedResource");
        result.Should().Contain("_authClient.PostAsJsonAsync");
        result.Should().Contain("_authClient.GetAsync");
    }

    [Fact]
    public void Generate_DoesNotIncludeCreateThenGetTest_WhenGetHasMultipleRouteParameters()
    {
        // Arrange — GET has TWO route params, e.g. api/products/{id}/reviews/{reviewId}
        // This should NOT be treated as a plain "get by id" endpoint
        var postEndpoint = new EndpointInfo(
            MethodName: "Create",
            HttpVerb: "HttpPost",
            Route: "",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "dto", Type: "CreateProductDto", FullyQualifiedType: "SampleApi.DTOs.CreateProductDto",
                    IsFromBody: true, IsFromRoute: false, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var getReviewEndpoint = new EndpointInfo(
            MethodName: "GetReview",
            HttpVerb: "HttpGet",
            Route: "{id}/reviews/{reviewId}",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "id", Type: "int", FullyQualifiedType: "System.Int32",
                    IsFromBody: false, IsFromRoute: true, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                ),
                new ParameterDetail(
                    Name: "reviewId", Type: "int", FullyQualifiedType: "System.Int32",
                    IsFromBody: false, IsFromRoute: true, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController",
            Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { postEndpoint, getReviewEndpoint },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert — two route params disqualifies this as a "get by id" match
        result.Should().NotContain("CreateThenGet_ReturnsCreatedResource");
    }
    // ⚠ KNOWN BUG — tracked separately, not part of #41 scope
    // ProjectAnalyzer.HasAttribute(param, "FromRoute") only detects explicit [FromRoute].
    // ASP.NET Core also implicitly binds route params by name-matching the route template,
    // even with no attribute. IsFromRoute is currently false in that case, so our pairing
    // logic (and other features relying on IsFromRoute) will miss valid "get by id" endpoints.
    [Fact]
    public void Generate_ShouldIncludeCreateThenGetTest_EvenWhenFromRouteAttributeIsMissing()
    {
        // Arrange — GET has route "{id}" and param named "id", but NO [FromRoute] attribute
        // This is valid ASP.NET Core implicit binding — IsFromRoute should be true, but today it's false
        var postEndpoint = new EndpointInfo(
            MethodName: "Create",
            HttpVerb: "HttpPost",
            Route: "",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "dto", Type: "CreateProductDto", FullyQualifiedType: "SampleApi.DTOs.CreateProductDto",
                    IsFromBody: true, IsFromRoute: false, IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById",
            HttpVerb: "HttpGet",
            Route: "{id}",
            ReturnType: "ProductDto",
            ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false,
            IsAsync: true,
            HasAuthorize: false,
            HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail(
                    Name: "id", Type: "int", FullyQualifiedType: "System.Int32",
                    IsFromBody: false,
                    IsFromRoute: false, // <-- bug: should be true, ASP.NET binds this implicitly
                    IsFromQuery: false, IsFromHeader: false,
                    DtoProperties: new List<PropertyDetail>()
                )
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController",
            Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { postEndpoint, getByIdEndpoint },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert — this SHOULD pass once the analyzer bug is fixed
        result.Should().Contain("CreateThenGet_ReturnsCreatedResource");
    }

    // ⚠ KNOWN LIMITATION — tracked separately, not part of #41 scope
    // When a controller has multiple valid POST/GET-by-id pairs (e.g. two resources
    // in one controller), TestFileGenerator only detects the FIRST matching pair.
    // See backlog issue: "support multiple POST/GET-by-id pairs".
    [Fact]
    public void Generate_ShouldIncludeCreateThenGetTest_ForEachDistinctResourcePair()
    {
        // Arrange — controller has TWO distinct resource pairs: Product and Category
        var createProduct = new EndpointInfo(
            MethodName: "CreateProduct", HttpVerb: "HttpPost", Route: "",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true, HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>(), ProducesResponses: new List<ProducesResponseDetail>()
        );

        var getProduct = new EndpointInfo(
            MethodName: "GetProductById", HttpVerb: "HttpGet", Route: "{id}",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true, HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail("id", "int", "System.Int32", false, true, false, false, new())
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var createCategory = new EndpointInfo(
            MethodName: "CreateCategory", HttpVerb: "HttpPost", Route: "category",
            ReturnType: "CategoryDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true, HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>(), ProducesResponses: new List<ProducesResponseDetail>()
        );

        var getCategory = new EndpointInfo(
            MethodName: "GetCategoryById", HttpVerb: "HttpGet", Route: "category/{id}",
            ReturnType: "CategoryDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true, HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
                new ParameterDetail("id", "int", "System.Int32", false, true, false, false, new())
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController",
            Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { createProduct, getProduct, createCategory, getCategory },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        // Assert — this SHOULD pass once multi-pair matching is implemented
        result.Should().Contain("CreateThenGet_Product_ReturnsCreatedResource");
        result.Should().Contain("CreateThenGet_Category_ReturnsCreatedResource");
    }
}