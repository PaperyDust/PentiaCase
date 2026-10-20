using PentiaCase.Web.Models.Caching;
using PentiaCase.Web.Models.ViewModels;

namespace PentiaCase.Web.Services.Reports;

public sealed class MonthlyOrdersService
{
    public MonthlyOrdersViewModel BuildReport(WingineersDataSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var orders = snapshot.Orders;
        if (orders.Count == 0)
        {
            return new MonthlyOrdersViewModel
            {
                Months = Array.Empty<MonthlyOrderCountViewModel>()
            };
        }

        var orderCounts = orders
            .GroupBy(order => new DateOnly(order.OrderDate.Year, order.OrderDate.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());
        var firstMonth = orderCounts.Keys.Min();
        var lastMonth = orderCounts.Keys.Max();
        var monthCount = (lastMonth.Year - firstMonth.Year) * 12 + lastMonth.Month - firstMonth.Month + 1;

        return new MonthlyOrdersViewModel
        {
            Months = Enumerable.Range(0, monthCount)
                .Select(offset => firstMonth.AddMonths(offset))
                .Select(month => new MonthlyOrderCountViewModel
                {
                    Month = month,
                    OrderCount = orderCounts.GetValueOrDefault(month)
                })
                .ToList()
        };
    }
}
