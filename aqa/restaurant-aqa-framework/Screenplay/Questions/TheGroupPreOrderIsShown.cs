using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>Whether the group pre-order page shows the group (its heading) rather than an error. Waits a few seconds for
/// the heading, because a refusal also takes a moment to arrive.</summary>
public sealed class TheGroupPreOrderIsShown : IQuestion<bool>
{
    public async Task<bool> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        try
        {
            await page.Heading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
