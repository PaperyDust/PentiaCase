using System.Text.Json;
using PentiaCase.Web.Models.Api;
using PentiaCase.Web.Serialization;

namespace PentiaCase.Web.Services.Api;

public sealed class WingineersApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new WingineersDateOnlyConverter(),
            new WingineersDateTimeConverter()
        }
    };

    public async Task<IReadOnlyList<SalesPerson>> GetSalesPersonsAsync(CancellationToken cancellationToken = default)
    {
        return await GetListAsync<SalesPerson>("api/v1/SalesPersons", person => person.Id, ValidateSalesPerson, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderLine>> GetOrderLinesAsync(CancellationToken cancellationToken = default)
    {
        return await GetListAsync<OrderLine>("api/v1/Orderlines", order => order.Id, ValidateOrderLine, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, Func<T, int> getId,
        Action<T, string> validate, CancellationToken cancellationToken) where T : class
    {
        List<T> records;
        try
        {
            records = await httpClient.GetFromJsonAsync<List<T>>(path, SerializerOptions, cancellationToken)
                ?? throw new JsonException($"The Wingineers API returned null instead of a list for {path}.");
        }
        catch (JsonException exception)
        {
            throw new JsonException($"Invalid Wingineers API response for {path} at {exception.Path ?? "$"}: {exception.Message}", exception);
        }

        var identifiers = new HashSet<int>();
        for (var index = 0; index < records.Count; index++)
        {
            var location = $"{path}[{index}]";
            var record = records[index];
            if (record is null)
            {
                throw new JsonException($"Invalid Wingineers API data at {location}: record must not be null.");
            }

            var id = getId(record);
            Require(id > 0, $"{location}.Id", "must be positive");
            Require(identifiers.Add(id), $"{location}.Id", "must be unique within the response");
            validate(record, location);
        }

        return records;
    }

    private static void ValidateSalesPerson(SalesPerson person, string location)
    {
        Require(!string.IsNullOrWhiteSpace(person.Name), $"{location}.Name", "must not be blank");
        Require(person.HireDate != default, $"{location}.HireDate", "must not be the default date");
        Require(!string.IsNullOrWhiteSpace(person.Address), $"{location}.Address", "must not be blank");
        Require(!string.IsNullOrWhiteSpace(person.City), $"{location}.City", "must not be blank");
        Require(!string.IsNullOrWhiteSpace(person.ZipCode), $"{location}.ZipCode", "must not be blank");
    }

    private static void ValidateOrderLine(OrderLine order, string location)
    {
        Require(!string.IsNullOrWhiteSpace(order.OrderName), $"{location}.OrderName", "must not be blank");
        Require(order.OrderDate.Date != DateTime.MinValue.Date, $"{location}.OrderDate", "must not be the default date");
        Require(order.SalesPersonId > 0, $"{location}.SalesPersonId", "must be positive");
    }

    private static void Require(bool condition, string location, string requirement)
    {
        if (!condition)
        {
            throw new JsonException($"Invalid Wingineers API data at {location}: {requirement}.");
        }
    }
}
