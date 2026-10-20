using PentiaCase.Web.Models.Api;

namespace PentiaCase.Web.Models.ViewModels;

public sealed class SalespersonListItemViewModel
{
    public required SalesPerson Salesperson { get; init; }

    public int OrderCount { get; init; }
}
