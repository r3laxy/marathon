using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Clicks "Remove Guest" on a guest's card and confirms in the dialog; waits until the guest is gone.</summary>
public sealed class RemoveTheGuestOnScreen(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.ParticipantNamed(guestName).RemoveAsync();
        await page.ConfirmRemoveButton.ClickAsync();
        await page.Participants.Filter(new() { HasText = guestName }).WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Detached });
    }
}
