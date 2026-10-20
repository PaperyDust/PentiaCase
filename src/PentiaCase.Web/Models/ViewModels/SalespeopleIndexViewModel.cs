namespace PentiaCase.Web.Models.ViewModels;

public sealed class SalespeopleIndexViewModel
{
    public required IReadOnlyList<SalespersonListItemViewModel> Salespeople { get; init; }
}
