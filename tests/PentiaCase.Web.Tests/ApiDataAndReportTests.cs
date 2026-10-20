using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PentiaCase.Web.Controllers;
using PentiaCase.Web.Models.ViewModels;
using PentiaCase.Web.Services.Api;
using PentiaCase.Web.Services.Caching;
using PentiaCase.Web.Services.Reports;
using Xunit;

namespace PentiaCase.Web.Tests;

public sealed class ApiDataAndReportTests
{
    private const string ValidOrder = """
        { "id": 1, "orderName": "Order", "orderPrice": 0, "orderDate": "2021-12-06 02:21", "salesPersonId": 1 }
        """;

    private const string ValidSalesperson = """
        { "id": 1, "name": "Alice", "hireDate": "01-02-2020", "address": "Street 1", "city": "Hellerup", "zipCode": "0290" }
        """;

    [Theory]
    [InlineData("06-12-2021 02:21")]
    [InlineData("2021-12-06 02:21")]
    public async Task Should_AcceptBothOrderDateFormats(string date)
    {
        using var httpClient = CreateHttpClient($"[{ValidOrder.Replace("2021-12-06 02:21", date, StringComparison.Ordinal)}]");
        var client = new WingineersApiClient(httpClient);

        var orders = await client.GetOrderLinesAsync();

        Assert.Equal(new DateTime(2021, 12, 6, 2, 21, 0), Assert.Single(orders).OrderDate);
    }

    [Theory]
    [InlineData("orderDate", null)]
    [InlineData("orderPrice", null)]
    [InlineData("orderPrice", "12.50")]
    [InlineData("orderDate", "\"01-01-0001 00:00\"")]
    [InlineData("orderDate", "\"invalid\"")]
    [InlineData("orderName", "null")]
    [InlineData("orderName", "\" \"")]
    [InlineData("id", "0")]
    [InlineData("salesPersonId", "0")]
    public async Task Should_RejectInvalidOrderFields(string field, string? jsonValue)
    {
        using var httpClient = CreateHttpClient(ChangeField(ValidOrder, field, jsonValue));
        var client = new WingineersApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<JsonException>(() => client.GetOrderLinesAsync());

        Assert.Contains(field, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("hireDate", null)]
    [InlineData("hireDate", "\"01-01-0001\"")]
    [InlineData("name", "null")]
    [InlineData("city", "\" \"")]
    public async Task Should_RejectInvalidSalespersonFields(string field, string? jsonValue)
    {
        using var httpClient = CreateHttpClient(ChangeField(ValidSalesperson, field, jsonValue));
        var client = new WingineersApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<JsonException>(() => client.GetSalesPersonsAsync());

        Assert.Contains(field, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task Should_RejectNullResponsesAndRecords(string json)
    {
        using var httpClient = CreateHttpClient(json);
        var client = new WingineersApiClient(httpClient);

        await Assert.ThrowsAsync<JsonException>(() => client.GetOrderLinesAsync());
    }

    [Fact]
    public async Task Should_RejectDuplicateIdentifiers()
    {
        using var httpClient = CreateHttpClient($"[{ValidOrder},{ValidOrder}]");
        var client = new WingineersApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<JsonException>(() => client.GetOrderLinesAsync());

        Assert.Contains("unique", exception.Message);
    }

    [Fact]
    public async Task MonthlyOrders_Should_FillMissingMonthsAcrossYearsChronologically()
    {
        var json = $$"""
            [
              { "id": 2, "orderName": "Later order", "orderPrice": 10, "orderDate": "2022-02-01 12:00", "salesPersonId": 1 },
              {{ValidOrder}}
            ]
            """;
        using var httpClient = CreateHttpClient(json, includeSalesperson: true);
        var controller = CreateReportsController(httpClient);

        var result = await controller.MonthlyOrders(CancellationToken.None);

        var model = Assert.IsType<MonthlyOrdersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(new[] { new DateOnly(2021, 12, 1), new DateOnly(2022, 1, 1), new DateOnly(2022, 2, 1) },
            model.Months.Select(month => month.Month));
        Assert.Equal(new[] { 1, 0, 1 }, model.Months.Select(month => month.OrderCount));
        Assert.Equal(2, model.TotalOrders);
    }

    [Fact]
    public async Task MonthlyOrders_Should_ReturnAnEmptyReport_When_NoOrdersExist()
    {
        using var httpClient = CreateHttpClient("[]", includeSalesperson: true);
        var controller = CreateReportsController(httpClient);

        var result = await controller.MonthlyOrders(CancellationToken.None);

        var model = Assert.IsType<MonthlyOrdersViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.Months);
        Assert.Equal(0, model.TotalOrders);
    }

    [Fact]
    public async Task MonthlyOrders_Should_Return503InsteadOfMisleadingTotals_When_DataIsInvalid()
    {
        using var httpClient = CreateHttpClient(ChangeField(ValidOrder, "orderDate", null), includeSalesperson: true);
        var controller = CreateReportsController(httpClient);

        var result = await controller.MonthlyOrders(CancellationToken.None);

        Assert.Equal("Unavailable", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, controller.Response.StatusCode);
    }

    private static string ChangeField(string record, string field, string? jsonValue)
    {
        var json = JsonNode.Parse(record)!.AsObject();
        if (jsonValue is null)
        {
            json.Remove(field);
        }
        else
        {
            json[field] = JsonNode.Parse(jsonValue);
        }

        return $"[{json.ToJsonString()}]";
    }

    private static ReportsController CreateReportsController(HttpClient httpClient)
    {
        var cache = new WingineersDataCache(() => new WingineersApiClient(httpClient), TimeProvider.System);
        return new ReportsController(cache, new MonthlyOrdersService(), NullLogger<ReportsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static HttpClient CreateHttpClient(string json, bool includeSalesperson = false)
    {
        return new HttpClient(new JsonResponseHandler(json, includeSalesperson)) { BaseAddress = new Uri("https://wingineers.test/") };
    }

    private sealed class JsonResponseHandler(string json, bool includeSalesperson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(includeSalesperson && request.RequestUri!.AbsolutePath.EndsWith("/SalesPersons", StringComparison.Ordinal)
                    ? $"[{ValidSalesperson}]" : json, Encoding.UTF8, "application/json")
            });
        }
    }
}
