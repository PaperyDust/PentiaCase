using PentiaCase.Web.Models.Api;
using PentiaCase.Web.Models.Caching;
using PentiaCase.Web.Services.Reports;
using PentiaCase.Web.Services.Salespeople;
using Xunit;

namespace PentiaCase.Web.Tests;

public sealed class FeatureServiceTests
{
    [Fact]
    public void Salespeople_Should_SortNamesWithIdTieBreaksAndIncludeZeroOrderPeople()
    {
        var snapshot = new WingineersDataSnapshot(
            new[] { Person(2, "Zara"), Person(3, "alice"), Person(1, "Alice") },
            new[] { Order(1, 2, new DateTime(2025, 1, 1)), Order(2, 2, new DateTime(2025, 1, 2)) },
            DateTimeOffset.UnixEpoch);
        var service = new SalespeopleService();

        var model = service.BuildIndex(snapshot);

        Assert.Equal(new[] { 1, 3, 2 }, model.Salespeople.Select(item => item.Salesperson.Id));
        Assert.Equal(new[] { 0, 0, 2 }, model.Salespeople.Select(item => item.OrderCount));
    }

    [Fact]
    public void SalespersonDetails_Should_FilterOrdersAndSortDateTiesById()
    {
        var newer = new DateTime(2025, 2, 1);
        var snapshot = new WingineersDataSnapshot(
            new[] { Person(1, "Alice"), Person(2, "Ben") },
            new[]
            {
                Order(2, 1, newer),
                Order(4, 2, newer),
                Order(3, 1, new DateTime(2025, 1, 1)),
                Order(1, 1, newer)
            },
            DateTimeOffset.UnixEpoch);
        var service = new SalespeopleService();

        var model = service.BuildDetails(snapshot, 1);

        Assert.NotNull(model);
        Assert.Equal(1, model.Salesperson.Id);
        Assert.Equal(new[] { 1, 2, 3 }, model.Orders.Select(order => order.Id));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(99)]
    public void SalespersonDetails_Should_ReturnNullWhenPersonDoesNotExist(int id)
    {
        var snapshot = EmptySnapshot();
        var service = new SalespeopleService();

        var model = service.BuildDetails(snapshot, id);

        Assert.Null(model);
    }

    [Fact]
    public void MonthlyReport_Should_GroupYearsChronologicallyAndFillMissingMonths()
    {
        var snapshot = new WingineersDataSnapshot(
            Array.Empty<SalesPerson>(),
            new[]
            {
                Order(1, 1, new DateTime(2022, 2, 1)),
                Order(2, 1, new DateTime(2021, 12, 1)),
                Order(3, 1, new DateTime(2021, 12, 31))
            },
            DateTimeOffset.UnixEpoch);
        var service = new MonthlyOrdersService();

        var model = service.BuildReport(snapshot);

        Assert.Equal(new[] { new DateOnly(2021, 12, 1), new DateOnly(2022, 1, 1), new DateOnly(2022, 2, 1) },
            model.Months.Select(month => month.Month));
        Assert.Equal(new[] { 2, 0, 1 }, model.Months.Select(month => month.OrderCount));
        Assert.Equal(3, model.TotalOrders);
    }

    [Fact]
    public void MonthlyReport_Should_ReturnAnEmptyReportWhenNoOrdersExist()
    {
        var service = new MonthlyOrdersService();

        var model = service.BuildReport(EmptySnapshot());

        Assert.Empty(model.Months);
        Assert.Equal(0, model.TotalOrders);
    }

    [Fact]
    public void Salespeople_Should_RejectAMissingSnapshot()
    {
        var service = new SalespeopleService();

        Assert.Throws<ArgumentNullException>(() => service.BuildIndex(null!));
    }

    [Fact]
    public void MonthlyReport_Should_RejectAMissingSnapshot()
    {
        var service = new MonthlyOrdersService();

        Assert.Throws<ArgumentNullException>(() => service.BuildReport(null!));
    }

    private static WingineersDataSnapshot EmptySnapshot()
    {
        return new WingineersDataSnapshot(Array.Empty<SalesPerson>(), Array.Empty<OrderLine>(), DateTimeOffset.UnixEpoch);
    }

    private static SalesPerson Person(int id, string name)
    {
        return new SalesPerson
        {
            Id = id,
            Name = name,
            HireDate = new DateOnly(2020, 1, 1),
            Address = "Street 1",
            City = "City",
            ZipCode = "0123"
        };
    }

    private static OrderLine Order(int id, int salespersonId, DateTime date)
    {
        return new OrderLine
        {
            Id = id,
            OrderName = "Order",
            OrderPrice = 10,
            OrderDate = date,
            SalesPersonId = salespersonId
        };
    }
}
