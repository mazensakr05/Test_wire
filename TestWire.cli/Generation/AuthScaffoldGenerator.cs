namespace TestWire.cli.Generation;

public static class AuthScaffoldGenerator
{
    public static string GenerateTestAuthHandler(string projectNamespace)
    {
        return $$"""
        using System.Security.Claims;
        using System.Text.Encodings.Web;
        using Microsoft.AspNetCore.Authentication;
        using Microsoft.Extensions.Logging;
        using Microsoft.Extensions.Options;

        namespace {{projectNamespace}}.Tests;

        public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthHandler(
                IOptionsMonitor<AuthenticationSchemeOptions> options,
                ILoggerFactory logger,
                UrlEncoder encoder)
                : base(options, logger, encoder)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (!Request.Headers.ContainsKey("Authorization"))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "testwire-user"),
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "Admin"),
                };

                var identity  = new ClaimsIdentity(claims, "Test");
                var principal = new ClaimsPrincipal(identity);
                var ticket    = new AuthenticationTicket(principal, "Test");

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }
        """;
    }

    public static string GenerateCustomWebApplicationFactory(string projectNamespace)
    {
        return $$"""
        using Microsoft.AspNetCore.Authentication;
        using Microsoft.AspNetCore.Hosting;
        using Microsoft.AspNetCore.Mvc.Testing;
        using Microsoft.AspNetCore.TestHost;
        using Microsoft.Extensions.DependencyInjection;
        using {{projectNamespace}};

        namespace {{projectNamespace}}.Tests;

        public class CustomWebApplicationFactory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = "Test";
                            options.DefaultChallengeScheme = "Test";
                        })
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                });
            }
        }
        """;
    }
}