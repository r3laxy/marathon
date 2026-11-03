using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Presses "Submit Pre-Order", confirms in the dialog and waits until the page says the pre-order was sent.</summary>
public sealed class SubmitTheGroupPreOrderOnScreen : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.SubmitButton.ClickAsync();
        await page.ConfirmSubmitButton.ClickAsync();
        await page.SentButton.WaitForAsync();
    }
}
