namespace PentiaCase.Web.Models.Api;

public sealed class OrderLine
{
    public required int Id { get; init; }

    public required string OrderName { get; init; }

    public required int OrderPrice { get; init; }

    public required DateTime OrderDate { get; init; }

    public required int SalesPersonId { get; init; }
}
