using System.Text.Json;
using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>`/waiter-pre-order` — the dishes of one reservation's pre-order, as the waiter sees them. The page is normally
/// reached from a reservation card, which hands it the reservation through the browser session; <see cref="OpenAsync"/>
/// hands over the same data directly, so the scenario does not depend on the waiter dashboard's date filter.</summary>
public sealed class WaiterPreOrderPage(IPage page) : BasePage(page)
{
    public ILocator Dishes => Page.Locator(".pre-order-dish");

    public ILocator Dish(string name) => Dishes.Filter(new() { HasText = name });

    public ILocator Quantity(string name) => Dish(name).Locator(".pre-order-quantity");

    public async Task OpenAsync(string reservationId, string customerName, string tableName)
    {
        var data = JsonSerializer.Serialize(new { reservationId, createdByName = customerName, createdByRole = "Customer", tableName });
        await Page.EvaluateAsync("data => sessionStorage.setItem('waiterPreOrderData', data)", data);
        await NavigateToAsync("/waiter-pre-order");
    }
}
