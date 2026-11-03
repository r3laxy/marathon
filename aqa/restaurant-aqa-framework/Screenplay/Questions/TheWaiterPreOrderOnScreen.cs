using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>The dishes the waiter's pre-order page lists, with the quantity each shows.</summary>
public sealed class TheWaiterPreOrderOnScreen : IQuestion<IReadOnlyList<ScreenItem>>
{
    public async Task<IReadOnlyList<ScreenItem>> AnsweredByAsync(Actor actor)
    {
        var page = new WaiterPreOrderPage(actor.LookingAtThePage());
        await page.Dishes.First.WaitForAsync();

        var items = new List<ScreenItem>();
        foreach (var dish in await page.Dishes.AllAsync())
        {
            var text = (await dish.TextContentAsync()) ?? "";
            var quantity = (await dish.Locator(".pre-order-quantity").TextContentAsync())?.Replace("Quantity:", "", StringComparison.Ordinal).Trim() ?? "0";
            items.Add(new ScreenItem(text, int.Parse(quantity, CultureInfo.InvariantCulture)));
        }

        return items;
    }
}
