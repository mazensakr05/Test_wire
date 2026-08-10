using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using SampleApi;
using SampleApi.DTOs;
using SampleApi.Models;

namespace SampleApi.Tests;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _authClient;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _authClient = factory.CreateClient();
        _authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "testwire");
    }

    [Fact]
    public async Task GetAll_Returns200_WithSystem_Collections_Generic_IEnumerable_SampleApi_Models_Product()
    {
        var response = await _client.GetAsync("api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<System.Collections.Generic.IEnumerable<SampleApi.Models.Product>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetById_Returns200_WithSampleApi_Models_Product()
    {
        var response = await _client.GetAsync("api/products/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SampleApi.Models.Product>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetById_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync("api/products/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDetails_Returns200_WithSampleApi_DTOs_ProductDetailsDto()
    {
        var response = await _client.GetAsync("api/products/1/details");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SampleApi.DTOs.ProductDetailsDto>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetDetails_Returns404_WhenNotFound()
    {
        var response = await _client.GetAsync("api/products/99999/details");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Returns201_WithSampleApi_Models_Product()
    {
        var request = new SampleApi.DTOs.CreateProductDto
        {
            Name = "test",
            Price = 1.0m,
            IsActive = true,
        };

        var response = await _authClient.PostAsJsonAsync("api/products", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SampleApi.Models.Product>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Create_Returns401_WhenUnauthenticated()
    {
        var response = await _client.PostAsJsonAsync("api/products", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Returns400_WhenRequestIsInvalid()
    {
        var response = await _authClient.PostAsJsonAsync("api/products", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Returns200_WithSampleApi_Models_Product()
    {
        var request = new SampleApi.DTOs.UpdateProductDto
        {
            Name = "test",
            Price = 1.0m,
        };

        var response = await _authClient.PutAsJsonAsync("api/products/1", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SampleApi.Models.Product>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Update_Returns401_WhenUnauthenticated()
    {
        var response = await _client.PutAsJsonAsync("api/products/1", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_Returns400_WhenRequestIsInvalid()
    {
        var response = await _authClient.PutAsJsonAsync("api/products/1", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Returns404_WhenNotFound()
    {
        var request = new SampleApi.DTOs.UpdateProductDto
        {
            Name = "test",
            Price = 1.0m,
        };

        var response = await _authClient.PutAsJsonAsync("api/products/99999", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204()
    {
        var response = await _authClient.DeleteAsync("api/products/1");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns401_WhenUnauthenticated()
    {
        var response = await _client.DeleteAsync("api/products/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenNotFound()
    {
        var response = await _authClient.DeleteAsync("api/products/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateThenGet_ReturnsCreatedResource()
    {
        var request = new SampleApi.DTOs.CreateProductDto
        {
            Name = "test",
            Price = 1.0m,
            IsActive = true,
        };

        var createResponse = await _authClient.PostAsJsonAsync("api/products", request);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SampleApi.Models.Product>();

        var getResponse = await _authClient.GetAsync($"api/products/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var retrieved = await getResponse.Content.ReadFromJsonAsync<SampleApi.Models.Product>();

        Assert.NotNull(retrieved);
    }

}
