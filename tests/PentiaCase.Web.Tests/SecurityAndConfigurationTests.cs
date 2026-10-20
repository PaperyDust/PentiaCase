using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PentiaCase.Web.Services.Api;
using Xunit;

namespace PentiaCase.Web.Tests;

public sealed class SecurityAndConfigurationTests
{
    [Fact]
    public void Should_AcceptValidHttpsConfiguration()
    {
        var options = new WingineersApiOptions { BaseUrl = "https://wingineers.test/", ApiKey = "test-key" };

        var result = new WingineersApiOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("https://wingineers.test/", "")]
    [InlineData("https://wingineers.test/", "test-key\r\n")]
    [InlineData("http://wingineers.test/", "test-key")]
    [InlineData("not-a-url", "test-key")]
    [InlineData("https://user:password@wingineers.test/", "test-key")]
    public async Task Should_FailAtStartup_When_ConfigurationIsInvalid(string baseUrl, string key)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IValidateOptions<WingineersApiOptions>, WingineersApiOptionsValidator>();
        builder.Services.AddOptions<WingineersApiOptions>()
            .Configure(options =>
            {
                options.BaseUrl = baseUrl;
                options.ApiKey = key;
            })
            .ValidateOnStart();
        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }
}
