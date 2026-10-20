using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PentiaCase.Web.Controllers;
using PentiaCase.Web.Models.ViewModels;
using PentiaCase.Web.Services.Api;
using PentiaCase.Web.Services.Caching;
using PentiaCase.Web.Services.Salespeople;
using Xunit;

namespace PentiaCase.Web.Tests;

public sealed class SalespeopleControllerTests
{
    private const string SalespeopleJson = """
        [
          { "id": 2, "name": "Zara", "hireDate": "31-08-2015", "address": "Street 2", "city": "Copenhagen", "zipCode": "0123" },
          { "id": 1, "name": "Alice", "hireDate": "01-02-2020", "address": "Street 1", "city": "Hellerup", "zipCode": "2900" },
          { "id": 3, "name": "Ben", "hireDate": "29-02-2024", "address": "Street 3", "city": "Copenhagen", "zipCode": "2100" }
        ]
        """;

    private const string OrdersJson = """
        [
          { "id": 1, "orderName": "Older order", "orderPrice": 5727, "orderDate": "09-05-2021 16:10", "salesPersonId": 2 },
          { "id": 2, "orderName": "Another person's order", "orderPrice": 100, "orderDate": "10-06-2022 10:00", "salesPersonId": 1 },
          { "id": 3, "orderName": "Newer order", "orderPrice": 9213, "orderDate": "01-01-2023 09:00", "salesPersonId": 2 },
          { "id": 4, "orderName": "Orphaned order", "orderPrice": 5, "orderDate": "01-01-2024 09:00", "salesPersonId": 99 }
        ]
        """;

    [Fact]
    public async Task Index_Should_ListAllPeopleAlphabeticallyWithTheirOrderCounts()
    {
        using var httpClient = CreateHttpClient(SalespeopleJson, OrdersJson);
        var controller = CreateController(httpClient);

        var result = await controller.Index(CancellationToken.None);

        var model = Assert.IsType<SalespeopleIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Collection(model.Salespeople,
            person =>
            {
                Assert.Equal("Alice", person.Salesperson.Name);
                Assert.Equal(1, person.OrderCount);
            },
            person =>
            {
                Assert.Equal("Ben", person.Salesperson.Name);
                Assert.Equal(0, person.OrderCount);
            },
            person =>
            {
                Assert.Equal("Zara", person.Salesperson.Name);
                Assert.Equal(2, person.OrderCount);
            });
    }

    [Fact]
    public async Task Index_Should_ReturnAnEmptyList_When_NoSalespeopleExist()
    {
        using var httpClient = CreateHttpClient("[]", OrdersJson);
        var controller = CreateController(httpClient);

        var result = await controller.Index(CancellationToken.None);

        var model = Assert.IsType<SalespeopleIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Salespeople);
    }

    [Fact]
    public async Task Index_Should_ShowZeroCounts_When_NoOrdersExist()
    {
        using var httpClient = CreateHttpClient(SalespeopleJson, "[]");
        var controller = CreateController(httpClient);

        var result = await controller.Index(CancellationToken.None);

        var model = Assert.IsType<SalespeopleIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(3, model.Salespeople.Count);
        Assert.All(model.Salespeople, person => Assert.Equal(0, person.OrderCount));
    }

    [Fact]
    public async Task Details_Should_ReturnPersonInformationAndOnlyTheirOrdersNewestFirst()
    {
        using var httpClient = CreateHttpClient(SalespeopleJson, OrdersJson);
        var controller = CreateController(httpClient);

        var result = await controller.Details(2, CancellationToken.None);

        var model = Assert.IsType<SalespersonDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(2, model.Salesperson.Id);
        Assert.Equal("Zara", model.Salesperson.Name);
        Assert.Equal(new DateOnly(2015, 8, 31), model.Salesperson.HireDate);
        Assert.Equal("Street 2", model.Salesperson.Address);
        Assert.Equal("Copenhagen", model.Salesperson.City);
        Assert.Equal("0123", model.Salesperson.ZipCode);
        Assert.Equal(new[] { 3, 1 }, model.Orders.Select(order => order.Id));
        Assert.Equal(5727, model.Orders[1].OrderPrice);
        Assert.Equal(new DateTime(2021, 5, 9, 16, 10, 0), model.Orders[1].OrderDate);
    }

    [Fact]
    public async Task Details_Should_ReturnAnEmptyOrderList_When_PersonHasNoOrders()
    {
        using var httpClient = CreateHttpClient(SalespeopleJson, OrdersJson);
        var controller = CreateController(httpClient);

        var result = await controller.Details(3, CancellationToken.None);

        var model = Assert.IsType<SalespersonDetailsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(3, model.Salesperson.Id);
        Assert.Empty(model.Orders);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(99)]
    public async Task Details_Should_ReturnNotFound_When_PersonDoesNotExist(int id)
    {
        using var httpClient = CreateHttpClient(SalespeopleJson, OrdersJson);
        var controller = CreateController(httpClient);

        var result = await controller.Details(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    public static IEnumerable<object[]> ApiFailureCases()
    {
        foreach (var details in new[] { false, true })
        {
            foreach (var failOrders in new[] { false, true })
            {
                foreach (var failure in new[] { "unauthorized", "server-error", "malformed", "null", "timeout", "invalid-date" })
                {
                    yield return new object[] { details, failOrders, failure };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ApiFailureCases))]
    public async Task Should_ShowUnavailableWith503_When_AnApiResponseFails(bool details, bool failOrders, string failure)
    {
        using var httpClient = CreateHttpClient((request, _) =>
        {
            var isOrders = request.RequestUri!.AbsolutePath.EndsWith("/Orderlines", StringComparison.Ordinal);
            if (isOrders != failOrders)
            {
                return JsonResponse(isOrders ? OrdersJson : SalespeopleJson);
            }

            return failure switch
            {
                "unauthorized" => new HttpResponseMessage(HttpStatusCode.Unauthorized),
                "server-error" => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                "malformed" => JsonResponse("not-json"),
                "null" => JsonResponse("null"),
                "timeout" => throw new TaskCanceledException("Simulated API timeout."),
                "invalid-date" => JsonResponse(isOrders
                    ? OrdersJson.Replace("09-05-2021 16:10", "invalid", StringComparison.Ordinal)
                    : SalespeopleJson.Replace("31-08-2015", "invalid", StringComparison.Ordinal)),
                _ => throw new InvalidOperationException("Unknown test failure.")
            };
        });
        var controller = CreateController(httpClient);

        var result = details
            ? await controller.Details(2, CancellationToken.None)
            : await controller.Index(CancellationToken.None);

        Assert.Equal("Unavailable", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_PropagateCancellation_When_RequestIsAborted(bool details)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var httpClient = CreateHttpClient((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return JsonResponse("[]");
        });
        var controller = CreateController(httpClient);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (details)
            {
                await controller.Details(2, cancellation.Token);
            }
            else
            {
                await controller.Index(cancellation.Token);
            }
        });
    }

    private static SalespeopleController CreateController(HttpClient httpClient)
    {
        var cache = new WingineersDataCache(() => new WingineersApiClient(httpClient), TimeProvider.System);
        return new SalespeopleController(cache, new SalespeopleService(), NullLogger<SalespeopleController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static HttpClient CreateHttpClient(string salespeople, string orders)
    {
        return CreateHttpClient((request, _) => JsonResponse(request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/SalesPersons" => salespeople,
            "/api/v1/Orderlines" => orders,
            _ => throw new InvalidOperationException("Unexpected API path.")
        }));
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
    {
        return new HttpClient(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://wingineers.test/")
        };
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request, cancellationToken));
        }
    }
}
