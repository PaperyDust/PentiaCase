namespace PentiaCase.Web.Models.ViewModels;

public sealed class MonthlyOrderCountViewModel
{
    public DateOnly Month { get; init; }

    public int OrderCount { get; init; }
}
