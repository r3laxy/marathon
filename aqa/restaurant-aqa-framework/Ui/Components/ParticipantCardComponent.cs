using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>`app-participant-card` on the group pre-order page: one person, their status chip, their dishes and subtotal.</summary>
public sealed class ParticipantCardComponent(ILocator root)
{
    public ILocator Title => root.Locator(".title");
    public ILocator StatusChip => root.Locator(".chip");
    public ILocator Items => root.Locator("li.item");
    public ILocator EmptyNote => root.Locator(".empty");
    public ILocator Subtotal => root.Locator(".subtotal span").Last;
    public ILocator RemoveButton => root.Locator("button.remove-link");

    public static ILocator ItemName(ILocator item) => item.Locator(".name");
    public static ILocator ItemQuantity(ILocator item) => item.Locator(".qty");

    public Task RemoveAsync() => RemoveButton.ClickAsync();
}
