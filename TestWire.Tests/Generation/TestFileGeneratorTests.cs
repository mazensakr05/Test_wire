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
            ProducesResponses: new List<ProducesResponseDetail>(),
            ReturnTypeProperties: new List<PropertyDetail> { new PropertyDetail("Id", "int", "System.Int32") }
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

        // Extract just the CreateThenGet method body so we don't accidentally
        // match unrelated GetById_Returns200 test content elsewhere in the file
        var methodStart = result.IndexOf("public async Task CreateThenGet_ReturnsCreatedResource()");
        var createThenGetBody = result.Substring(methodStart);

        createThenGetBody.Should().Contain("created.Id");
        createThenGetBody.Should().NotContain("GetAsync(\"api/products/1\")");
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
            ProducesResponses: new List<ProducesResponseDetail>(),
            ReturnTypeProperties: new List<PropertyDetail> { new PropertyDetail("Id", "int", "System.Int32") }
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

    [Fact]
    public void Generate_UsesAuthClient_WhenOnlyGetByIdEndpointRequiresAuthorize()
    {
        // Arrange — POST is public, GET-by-id requires [Authorize]
        var postEndpoint = new EndpointInfo(
            MethodName: "Create", HttpVerb: "HttpPost", Route: "",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true,
            HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>
            {
            new ParameterDetail("dto", "CreateProductDto", "SampleApi.DTOs.CreateProductDto",
                true, false, false, false, new())
            },
            ProducesResponses: new List<ProducesResponseDetail>(),
            ReturnTypeProperties: new List<PropertyDetail> { new PropertyDetail("Id", "int", "System.Int32") }
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById", HttpVerb: "HttpGet", Route: "{id}",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true,
            HasAuthorize: true, HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
            new ParameterDetail("id", "int", "System.Int32", false, true, false, false, new())
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController", Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { postEndpoint, getByIdEndpoint },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        // Act
        var result = TestFileGenerator.Generate(controller, context);

        var methodStart = result.IndexOf("public async Task CreateThenGet_ReturnsCreatedResource()");
        var body = result.Substring(methodStart);

        // Assert — must use _authClient since at least one endpoint requires auth
        body.Should().Contain("_authClient.PostAsJsonAsync");
        body.Should().Contain("_authClient.GetAsync");
    }

    [Fact]
    public void Generate_SkipsCreateThenGetTest_WhenBothReturnTypesAreEmpty()
    {
        var postEndpoint = new EndpointInfo(
            MethodName: "Create", HttpVerb: "HttpPost", Route: "",
            ReturnType: "", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: true, IsAsync: true,
            HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>
            {
            new ParameterDetail("dto", "CreateProductDto", "SampleApi.DTOs.CreateProductDto",
                true, false, false, false, new())
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById", HttpVerb: "HttpGet", Route: "{id}",
            ReturnType: "", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: true, IsAsync: true,
            HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail>
            {
            new ParameterDetail("id", "int", "System.Int32", false, true, false, false, new())
            },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo(
            ClassName: "ProductsController", Namespace: "SampleApi.Controllers",
            BaseRoute: "api/[controller]",
            Endpoints: new List<EndpointInfo> { postEndpoint, getByIdEndpoint },
            Dependencies: new List<ConstructorDependency>()
        );

        var context = BuildContext();

        var result = TestFileGenerator.Generate(controller, context);

        result.Should().NotContain("ReadFromJsonAsync<>()");
    }
   

    // ⚠ KNOWN LIMITATION — tracked separately, not part of #41 scope
    // When a controller has multiple valid POST/GET-by-id pairs (e.g. two resources
    // in one controller), TestFileGenerator only detects the FIRST matching pair.
    // See backlog issue: "support multiple POST/GET-by-id pairs".
    [Fact(Skip = "Known limitation — multi-pair matching not yet supported. Tracked in separate backlog issue.")]
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

    [Fact(Skip = "Known limitation — ID property name detection not yet implemented.")]
    public void Generate_CreateThenGetTest_UsesCorrectIdPropertyName_EvenWhenNotLiterallyNamedId()
    {
        // Arrange — POST returns ProductDto, but its actual "id" property is named "ProductId", not "Id"
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
            ProducesResponses: new List<ProducesResponseDetail>(),
            ReturnTypeProperties: new List<PropertyDetail>
            {
                new PropertyDetail("ProductId", "int", "System.Int32")
            }
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById",
            HttpVerb: "HttpGet",
            Route: "{productId}",
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
                Name: "productId", Type: "int", FullyQualifiedType: "System.Int32",
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

        // Assert — this SHOULD pass once the generator correctly detects the real ID
        // property name (e.g. "ProductId") instead of hardcoding "created.Id"
        result.Should().Contain("created.ProductId");
        result.Should().NotContain("created.Id)");
    }

    [Theory]
    [InlineData("Sku")]
    [InlineData("Guid")]
    [InlineData("Key")]
    public void Generate_CreateThenGetTest_UsesConfiguredIdentifierNames(string propertyName)
    {
        var postEndpoint = new EndpointInfo(
            MethodName: "Create", HttpVerb: "HttpPost", Route: "",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true,
            HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>(),
            ProducesResponses: new List<ProducesResponseDetail>(),
            ReturnTypeProperties: new List<PropertyDetail> { new PropertyDetail(propertyName, "string", "System.String") }
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById", HttpVerb: "HttpGet", Route: "{id}",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true,
            HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail> { new ParameterDetail("id", "string", "System.String", false, true, false, false, new()) },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo("ProductsController", "SampleApi.Controllers", "api/[controller]", new List<EndpointInfo> { postEndpoint, getByIdEndpoint }, new());
        var result = TestFileGenerator.Generate(controller, BuildContext());

        result.Should().Contain($"created.{propertyName}");
    }

    [Fact]
    public void Generate_DoesNotIncludeCreateThenGetTest_WhenNoIdPropertyExists()
    {
        var postEndpoint = new EndpointInfo(
            MethodName: "Create", HttpVerb: "HttpPost", Route: "",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true,
            HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 201,
            Parameters: new List<ParameterDetail>(),
            ProducesResponses: new List<ProducesResponseDetail>(),
            ReturnTypeProperties: new List<PropertyDetail> { new PropertyDetail("SomeOtherProperty", "int", "System.Int32") }
        );

        var getByIdEndpoint = new EndpointInfo(
            MethodName: "GetById", HttpVerb: "HttpGet", Route: "{id}",
            ReturnType: "ProductDto", ReturnTypeKind: ReturnTypeKind.ActionResultOfT,
            HasAmbiguousReturnType: false, IsAsync: true,
            HasAuthorize: false, HasAllowAnonymous: false,
            ExpectedStatusCode: 200,
            Parameters: new List<ParameterDetail> { new ParameterDetail("id", "int", "System.Int32", false, true, false, false, new()) },
            ProducesResponses: new List<ProducesResponseDetail>()
        );

        var controller = new ControllerInfo("ProductsController", "SampleApi.Controllers", "api/[controller]", new List<EndpointInfo> { postEndpoint, getByIdEndpoint }, new());
        var result = TestFileGenerator.Generate(controller, BuildContext());

        result.Should().NotContain("CreateThenGet_ReturnsCreatedResource");
    }
}