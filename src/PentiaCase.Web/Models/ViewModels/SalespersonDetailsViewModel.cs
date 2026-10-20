using PentiaCase.Web.Models.Api;

namespace PentiaCase.Web.Models.ViewModels;

public sealed class SalespersonDetailsViewModel
{
    public required SalesPerson Salesperson { get; init; }

    public required IReadOnlyList<OrderLine> Orders { get; init; }
}
