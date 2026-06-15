using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using SampleApi;

namespace SampleApi.Tests;

public class CategoriesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _authClient;

    public CategoriesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _authClient = factory.CreateClient();
        _authClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "testwire");
    }

    [Fact]
    public async Task Get_Returns200_WithSystem_Collections_Generic_IEnumerable_SampleApi_DTOs_CategoryDto()
    {
        var response = await _client.GetAsync("api/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<System.Collections.Generic.IEnumerable<SampleApi.DTOs.CategoryDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Get_Returns200_WithSampleApi_DTOs_CategoryDto()
    {
        var response = await _client.GetAsync("api/categories/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SampleApi.DTOs.CategoryDto>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Post_Returns201_WithSampleApi_DTOs_CategoryDto()
    {
        var request = new SampleApi.DTOs.CreateCategoryDto
        {
            Name = "test",
            Description = null,
        };

        var response = await _client.PostAsJsonAsync("api/categories", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SampleApi.DTOs.CategoryDto>();
        Assert.NotNull(result);
    }

}
