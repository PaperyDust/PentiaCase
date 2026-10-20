using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PentiaCase.Web.Controllers;
using PentiaCase.Web.Models.ViewModels;
using PentiaCase.Web.Services.Api;
using PentiaCase.Web.Services.Caching;
using PentiaCase.Web.Services.Salespeople;
using Xunit;

namespace PentiaCase.Web.Tests;

public sealed class DataCacheAndRefreshTests
{
    [Fact]
    public async Task Should_ReuseBothDatasetsWithoutAutomaticExpiry()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        var clock = new TestClock();
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), clock);
        var original = await cache.GetAsync();
        handler.Version = 2;
        clock.UtcNow = clock.UtcNow.AddDays(30);

        var cached = await cache.GetAsync();

        Assert.Same(original, cached);
        Assert.Equal(1, handler.SalespeopleCalls);
        Assert.Equal(1, handler.OrderCalls);
    }

    [Fact]
    public async Task Should_ShareOneInitialLoadAcrossConcurrentReaders()
    {
        using var handler = new DatasetHandler { ResponsesAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        var readers = Enumerable.Range(0, 5).Select(_ => cache.GetAsync()).ToArray();

        handler.ResponsesAllowed.SetResult();
        var snapshots = await Task.WhenAll(readers);

        Assert.All(snapshots, snapshot => Assert.Same(snapshots[0], snapshot));
        Assert.Equal(1, handler.SalespeopleCalls);
        Assert.Equal(1, handler.OrderCalls);
    }

    [Fact]
    public async Task Should_ReplaceBothDatasetsAndFetchTimeAfterSuccessfulRefresh()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        var clock = new TestClock();
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), clock);
        var original = await cache.GetAsync();
        handler.Version = 2;
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var replacement = await cache.RefreshAsync();

        Assert.NotSame(original, replacement);
        Assert.Equal(2, Assert.Single(replacement.Salespeople).Id);
        Assert.Equal(2, Assert.Single(replacement.Orders).Id);
        Assert.Equal(clock.UtcNow, replacement.FetchedAtUtc);
        Assert.NotEqual(original.FetchedAtUtc, replacement.FetchedAtUtc);
        Assert.Same(replacement, await cache.GetAsync());
    }

    [Fact]
    public async Task Should_PreserveBothDatasetsAndTimestampWhenRefreshFails()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        var original = await cache.GetAsync();
        handler.Version = 2;
        handler.FailOrders = true;

        await Assert.ThrowsAsync<HttpRequestException>(() => cache.RefreshAsync());

        Assert.Same(original, cache.Current);
        Assert.Same(original, await cache.GetAsync());
        Assert.Equal(1, Assert.Single(cache.Current!.Salespeople).Id);
        Assert.Equal(2, handler.SalespeopleCalls);
        Assert.Equal(2, handler.OrderCalls);
    }

    [Fact]
    public async Task Should_AllowRetryAfterAnInitialFailure()
    {
        using var handler = new DatasetHandler { FailOrders = true };
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetAsync());
        Assert.Null(cache.Current);
        handler.FailOrders = false;

        var snapshot = await cache.GetAsync();

        Assert.Single(snapshot.Salespeople);
        Assert.Single(snapshot.Orders);
    }

    [Fact]
    public async Task Should_KeepServingThePreviousSnapshotWhileRefreshing()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        var original = await cache.GetAsync();
        handler.ResponsesAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = cache.RefreshAsync();

        var reader = cache.GetAsync();
        var completedWithoutWaiting = reader.IsCompletedSuccessfully;
        handler.ResponsesAllowed.SetResult();
        await refresh;

        Assert.True(completedWithoutWaiting);
        Assert.Same(original, await reader);
    }

    [Fact]
    public async Task Should_PreserveTheSnapshotWhenRefreshIsCancelled()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        var original = await cache.GetAsync();
        handler.ResponsesAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var refresh = cache.RefreshAsync(cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);

        Assert.Same(original, cache.Current);
    }

    [Fact]
    public async Task Should_CacheValidEmptyDatasets()
    {
        using var handler = new DatasetHandler { Empty = true };
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());

        var snapshot = await cache.GetAsync();

        Assert.Empty(snapshot.Salespeople);
        Assert.Empty(snapshot.Orders);
        Assert.Same(snapshot, await cache.GetAsync());
        Assert.Equal(1, handler.OrderCalls);
    }

    [Fact]
    public async Task Homepage_Should_LoadBothDatasetsBeforeRendering()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        var controller = CreateHomeController(cache);

        var result = await controller.Index(CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.NotNull(cache.Current);
        Assert.Equal(cache.Current.FetchedAtUtc, controller.ViewData["DataFetchedAtUtc"]);
        Assert.Equal(1, handler.SalespeopleCalls);
        Assert.Equal(1, handler.OrderCalls);
    }

    [Theory]
    [InlineData("/Reports/MonthlyOrders?year=2022", "/Reports/MonthlyOrders?year=2022")]
    [InlineData("https://example.test/", "/")]
    [InlineData("//example.test/", "/")]
    public async Task Refresh_Should_ReturnToTheCurrentPageButRejectExternalRedirects(string returnUrl, string expected)
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        var controller = CreateHomeController(cache);

        var result = await controller.Refresh(returnUrl, CancellationToken.None);

        Assert.Equal(expected, Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.NotNull(cache.Current);
        Assert.NotNull(controller.TempData["DataRefreshSuccess"]);
    }

    [Fact]
    public async Task Refresh_Should_ShowTheReplacementDataWhenTheCurrentPageReloads()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        await cache.GetAsync();
        handler.Version = 2;
        var controller = CreateHomeController(cache);

        var redirect = await controller.Refresh("/Salespeople", CancellationToken.None);
        var page = new SalespeopleController(cache, new SalespeopleService(), NullLogger<SalespeopleController>.Instance);
        var result = await page.Index(CancellationToken.None);

        Assert.Equal("/Salespeople", Assert.IsType<LocalRedirectResult>(redirect).Url);
        var model = Assert.IsType<SalespeopleIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Person 2", Assert.Single(model.Salespeople).Salesperson.Name);
        Assert.Equal(cache.Current!.FetchedAtUtc, page.ViewData["DataFetchedAtUtc"]);
        Assert.Equal(2, handler.SalespeopleCalls);
        Assert.Equal(2, handler.OrderCalls);
    }

    [Fact]
    public async Task Refresh_Should_WarnAndReturnToTheCurrentPageWhenFetchingFails()
    {
        using var handler = new DatasetHandler();
        using var client = CreateClient(handler);
        using var cache = new WingineersDataCache(() => new WingineersApiClient(client), new TestClock());
        var original = await cache.GetAsync();
        handler.FailOrders = true;
        var controller = CreateHomeController(cache);

        var result = await controller.Refresh("/Salespeople/Details/1", CancellationToken.None);

        Assert.Equal("/Salespeople/Details/1", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Same(original, cache.Current);
        Assert.Contains("Previously fetched", Assert.IsType<string>(controller.TempData["DataRefreshError"]));
    }

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://wingineers.test/") };
    }

    private static HomeController CreateHomeController(WingineersDataCache cache)
    {
        var controller = new HomeController(cache, NullLogger<HomeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor()
            }
        };
        controller.Url = new UrlHelper(controller.ControllerContext);
        controller.TempData = new TempDataDictionary(controller.HttpContext, new EmptyTempDataProvider());
        return controller;
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class DatasetHandler : HttpMessageHandler
    {
        public int Version { get; set; } = 1;
        public bool FailOrders { get; set; }
        public bool Empty { get; set; }
        public TaskCompletionSource? ResponsesAllowed { get; set; }
        public int SalespeopleCalls;
        public int OrderCalls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isOrders = request.RequestUri!.AbsolutePath.EndsWith("/Orderlines", StringComparison.Ordinal);
            if (isOrders)
            {
                Interlocked.Increment(ref OrderCalls);
            }
            else
            {
                Interlocked.Increment(ref SalespeopleCalls);
            }

            if (ResponsesAllowed is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            if (isOrders && FailOrders)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            var json = Empty ? "[]" : isOrders
                ? $$"""[{ "id": {{Version}}, "orderName": "Order {{Version}}", "orderPrice": 10, "orderDate": "2025-01-01 12:00", "salesPersonId": {{Version}} }]"""
                : $$"""[{ "id": {{Version}}, "name": "Person {{Version}}", "hireDate": "01-01-2020", "address": "Street 1", "city": "City", "zipCode": "0123" }]""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
