using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed record ScreenItem(string Name, int Quantity);

public sealed record ScreenParticipant(string Title, string Status, bool HasNoDishes, decimal Subtotal, IReadOnlyList<ScreenItem> Items);

public sealed record ScreenGroup(
    IReadOnlyList<ScreenParticipant> Participants,
    decimal GrandTotal,
    bool Sent,
    bool NobodyHasChosenAnything,
    IReadOnlyList<ScreenItem> Consolidated);

/// <summary>What the group pre-order page shows once it has loaded: everyone listed, their dishes and subtotals, the grand
/// total and the consolidated order. Waits for the page to settle, since an answer is a plain value.</summary>
public sealed class TheGroupPreOrderOnScreen : IQuestion<ScreenGroup>
{
    public async Task<ScreenGroup> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.GrandTotal.WaitForAsync();
        await page.Participants.First.WaitForAsync();

        var participants = new List<ScreenParticipant>();
        for (var i = 0; i < await page.Participants.CountAsync(); i++)
        {
            var card = page.Participant(i);
            var items = new List<ScreenItem>();
            foreach (var item in await card.Items.AllAsync())
                items.Add(await ReadItemAsync(ParticipantCardComponent.ItemName(item), ParticipantCardComponent.ItemQuantity(item)));

            participants.Add(new ScreenParticipant(
                Title: (await card.Title.TextContentAsync())?.Trim() ?? "",
                Status: (await card.StatusChip.TextContentAsync())?.Trim() ?? "",
                HasNoDishes: await card.EmptyNote.CountAsync() > 0,
                Subtotal: Money(await card.Subtotal.TextContentAsync()),
                Items: items));
        }

        var consolidated = new List<ScreenItem>();
        foreach (var row in await page.ConsolidatedItems.AllAsync())
        {
            var text = (await row.Locator("span").First.TextContentAsync())?.Trim() ?? "";
            var split = text.LastIndexOf('×');
            consolidated.Add(new ScreenItem(text[..split].Trim(), int.Parse(text[(split + 1)..].Trim(), CultureInfo.InvariantCulture)));
        }

        return new ScreenGroup(
            participants,
            Money(await page.GrandTotal.TextContentAsync()),
            await page.SentButton.CountAsync() > 0,
            await page.EmptyState.CountAsync() > 0,
            consolidated);
    }

    private static async Task<ScreenItem> ReadItemAsync(ILocator name, ILocator quantity)
    {
        var times = (await quantity.TextContentAsync())?.Replace("×", "", StringComparison.Ordinal).Trim() ?? "0";
        return new ScreenItem((await name.TextContentAsync())?.Trim() ?? "", int.Parse(times, CultureInfo.InvariantCulture));
    }

    /// <summary>"$1,234.50" → 1234.50.</summary>
    internal static decimal Money(string? text) =>
        decimal.Parse((text ?? "0").Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim(), CultureInfo.InvariantCulture);
}
