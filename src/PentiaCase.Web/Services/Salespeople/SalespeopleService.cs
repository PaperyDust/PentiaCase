using PentiaCase.Web.Models.Caching;
using PentiaCase.Web.Models.ViewModels;

namespace PentiaCase.Web.Services.Salespeople;

public sealed class SalespeopleService
{
    public SalespeopleIndexViewModel BuildIndex(WingineersDataSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var orderCounts = snapshot.Orders.GroupBy(order => order.SalesPersonId)
            .ToDictionary(group => group.Key, group => group.Count());

        return new SalespeopleIndexViewModel
        {
            Salespeople = snapshot.Salespeople
                .OrderBy(person => person.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(person => person.Id)
                .Select(person => new SalespersonListItemViewModel
                {
                    Salesperson = person,
                    OrderCount = orderCounts.GetValueOrDefault(person.Id)
                })
                .ToList()
        };
    }

    public SalespersonDetailsViewModel? BuildDetails(WingineersDataSnapshot snapshot, int id)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var salesperson = snapshot.Salespeople.FirstOrDefault(person => person.Id == id);
        if (salesperson is null)
        {
            return null;
        }

        return new SalespersonDetailsViewModel
        {
            Salesperson = salesperson,
            Orders = snapshot.Orders.Where(order => order.SalesPersonId == id)
                .OrderByDescending(order => order.OrderDate)
                .ThenBy(order => order.Id)
                .ToList()
        };
    }
}
