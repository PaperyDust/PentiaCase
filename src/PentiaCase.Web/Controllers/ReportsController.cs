using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PentiaCase.Web.Services.Caching;
using PentiaCase.Web.Services.Reports;

namespace PentiaCase.Web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportsController(
    WingineersDataCache dataCache,
    MonthlyOrdersService monthlyOrdersService,
    ILogger<ReportsController> logger) : Controller
{
    public async Task<IActionResult> MonthlyOrders(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await dataCache.GetAsync(cancellationToken);
            ViewData["DataFetchedAtUtc"] = snapshot.FetchedAtUtc;
            return View(monthlyOrdersService.BuildReport(snapshot));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Unable to load monthly order data from the Wingineers API.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Unavailable");
        }
    }
}
