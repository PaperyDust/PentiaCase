namespace PentiaCase.Web.Models.Api;

public sealed class SalesPerson
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required DateOnly HireDate { get; init; }

    public required string Address { get; init; }

    public required string City { get; init; }

    public required string ZipCode { get; init; }
}
