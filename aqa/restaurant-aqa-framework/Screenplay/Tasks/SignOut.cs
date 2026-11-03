namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Ends whatever session the browser holds (cookies and the app's local and session storage), so someone else
/// can sign in on the same browser.</summary>
public sealed class SignOut : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        await page.EvaluateAsync("() => { localStorage.clear(); sessionStorage.clear(); }");
        await page.Context.ClearCookiesAsync();
    }
}
