using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PentiaCase.Web.Services.Caching;
using PentiaCase.Web.Services.Salespeople;

namespace PentiaCase.Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SalespeopleController(
    WingineersDataCache dataCache,
    SalespeopleService salespeopleService,
    ILogger<SalespeopleController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await dataCache.GetAsync(cancellationToken);
            ViewData["DataFetchedAtUtc"] = snapshot.FetchedAtUtc;
            return View(salespeopleService.BuildIndex(snapshot));
        }
        catch (Exception exception) when (IsApiFailure(exception, cancellationToken))
        {
            return ApiUnavailable(exception);
        }
    }

    [HttpGet("Salespeople/Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            var snapshot = await dataCache.GetAsync(cancellationToken);
            ViewData["DataFetchedAtUtc"] = snapshot.FetchedAtUtc;
            var model = salespeopleService.BuildDetails(snapshot, id);
            if (model is null)
            {
                return NotFound();
            }

            return View(model);
        }
        catch (Exception exception) when (IsApiFailure(exception, cancellationToken))
        {
            return ApiUnavailable(exception);
        }
    }

    private ViewResult ApiUnavailable(Exception exception)
    {
        logger.LogError(exception, "Unable to load salespeople data from the Wingineers API.");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("Unavailable");
    }

    private static bool IsApiFailure(Exception exception, CancellationToken cancellationToken)
    {
        return exception is HttpRequestException or JsonException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
    }
}
