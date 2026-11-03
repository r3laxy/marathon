using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>How the group pre-order turns people away, as business statements. Which status code and error body
/// each one is on the wire is decided here and nowhere else.</summary>
[Binding]
public sealed class GroupPreOrderOutcomeStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Then("it works")]
    public async Task ThenItWorks()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Succeeded).IsTrue().Because($"status {outcome.Status}, body: {outcome.Body}");
    }

    [Then("they are turned away because they are not signed in")]
    public Task ThenNotSignedIn() => Expect(401);

    [Then("they are turned away because only the owner of the reservation may do that")]
    public async Task ThenOwnerOnly()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(403).Because($"response body was: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("Only the reservation owner", StringComparison.Ordinal);
    }

    [Then("they are told it could not be found")]
    public Task ThenNotFound() => Expect(404);

    [Then("the request is rejected")]
    public Task ThenRejected() => Expect(400);

    [Then("the request is rejected saying {string}")]
    public async Task ThenRejectedSaying(string message)
    {
        var outcome = await Expect(400);
        await Assert.That(outcome.ErrorMessage).Contains(message, StringComparison.Ordinal);
    }

    [Then("the request is refused as a conflict saying {string}")]
    public async Task ThenConflictSaying(string message)
    {
        var outcome = await Expect(409);
        await Assert.That(outcome.ErrorMessage).Contains(message, StringComparison.Ordinal);
    }

    [Then("the guest is told that their session is not valid")]
    public async Task ThenInvalidGuestSession()
    {
        var outcome = await Expect(401);
        await Assert.That(outcome.ErrorMessage).Contains("Invalid guest session token", StringComparison.Ordinal);
    }

    private async Task<Outcome> Expect(int status)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(status).Because($"response body was: {outcome.Body}");
        return outcome;
    }
}
