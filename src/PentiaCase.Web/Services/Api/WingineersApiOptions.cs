using Microsoft.Extensions.Options;

namespace PentiaCase.Web.Services.Api;

public sealed class WingineersApiOptions
{
    public const string SectionName = "WingineersApi";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}

public sealed class WingineersApiOptionsValidator : IValidateOptions<WingineersApiOptions>
{
    public ValidateOptionsResult Validate(string? name, WingineersApiOptions options)
    {
        var failures = new List<string>();
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            failures.Add("WingineersApi:BaseUrl must be an absolute HTTPS URL without credentials, a query, or a fragment.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey.Any(character => character < '!' || character > '~'))
        {
            failures.Add("WingineersApi:ApiKey must contain only visible ASCII characters without whitespace. Configure it using User Secrets or deployment configuration.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
