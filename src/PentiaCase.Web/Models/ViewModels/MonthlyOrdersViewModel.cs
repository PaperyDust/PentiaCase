namespace PentiaCase.Web.Models.ViewModels;

public sealed class MonthlyOrdersViewModel
{
    public required IReadOnlyList<MonthlyOrderCountViewModel> Months { get; init; }

    public int TotalOrders => Months.Sum(month => month.OrderCount);
}
