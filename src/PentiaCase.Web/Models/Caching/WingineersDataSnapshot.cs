using PentiaCase.Web.Models.Api;

namespace PentiaCase.Web.Models.Caching;

public sealed record WingineersDataSnapshot(
    IReadOnlyList<SalesPerson> Salespeople,
    IReadOnlyList<OrderLine> Orders,
    DateTimeOffset FetchedAtUtc);
