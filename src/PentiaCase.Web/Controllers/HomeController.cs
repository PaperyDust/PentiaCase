using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PentiaCase.Web.Models.ViewModels;
using PentiaCase.Web.Services.Caching;

namespace PentiaCase.Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class HomeController(WingineersDataCache dataCache, ILogger<HomeController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await dataCache.GetAsync(cancellationToken);
            ViewData["DataFetchedAtUtc"] = snapshot.FetchedAtUtc;
            return View();
        }
        catch (Exception exception) when (IsApiFailure(exception, cancellationToken))
        {
            logger.LogError(exception, "Unable to initialize the Wingineers data cache.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Unavailable");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(string? returnUrl, CancellationToken cancellationToken)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/");
        try
        {
            await dataCache.RefreshAsync(cancellationToken);
            TempData["DataRefreshSuccess"] = "Data refreshed successfully.";
        }
        catch (Exception exception) when (IsApiFailure(exception, cancellationToken))
        {
            logger.LogError(exception, "Unable to refresh the Wingineers data cache.");
            TempData["DataRefreshError"] = dataCache.Current is null
                ? "Data could not be fetched. Please try again."
                : "Data refresh failed. Previously fetched data is still being displayed.";
        }

        return LocalRedirect(destination);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static bool IsApiFailure(Exception exception, CancellationToken cancellationToken)
    {
        return exception is HttpRequestException or JsonException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
    }
}
