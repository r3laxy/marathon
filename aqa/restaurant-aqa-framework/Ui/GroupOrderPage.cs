using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>`/reservations/:id/group-order` — the owner's view of the group pre-order.</summary>
public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Heading => Page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Exact = true });
    public ILocator StatusBadge => Page.Locator(".title-row .badge");
    public ILocator ErrorState => Page.Locator("section.state--error");
    public ILocator EmptyState => Page.Locator(".empty-state");

    private ILocator ParticipantRows => Page.Locator("ul.participants > li");

    public ILocator GrandTotal => Page.Locator(".grand-total-value");
    public ILocator ConsolidatedItems => Page.Locator("section.consolidated li");
    public ILocator SubmitButton => Page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true });
    public ILocator SentButton => Page.GetByRole(AriaRole.Button, new() { Name = "Pre-Order Sent", Exact = true });

    // The two confirmation dialogs are the page's only `app-modal`s while open.
    public ILocator ConfirmSubmitButton => Page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true });
    public ILocator ConfirmRemoveButton => Page.GetByRole(AriaRole.Button, new() { Name = "Remove Guest", Exact = true });

    public Task NavigateAsync(string reservationId) => NavigateToAsync($"/reservations/{reservationId}/group-order");

    public ILocator Participants => ParticipantRows;

    public ParticipantCardComponent Participant(int index) => new(ParticipantRows.Nth(index));

    public ParticipantCardComponent ParticipantNamed(string name) => new(ParticipantRows.Filter(new() { HasText = name }));
}
