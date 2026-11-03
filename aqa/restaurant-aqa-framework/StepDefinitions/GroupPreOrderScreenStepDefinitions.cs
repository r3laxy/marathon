using System.Globalization;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>The group pre-order as the storefront shows it, to the owner and to the waiter. Only `@ui` scenarios use
/// these sentences. What is on the screen is read by one question and compared here with the dishes the scenario put
/// on the menu (named by number, as in the API steps).</summary>
[Binding]
public sealed class GroupPreOrderScreenStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    // The page rounds every price to cents, so sums over several dishes may differ from the exact total by a cent or two.
    private const decimal CentsTolerance = 0.03m;

    [When("{word} signs in and opens the group pre-order")]
    public Task WhenOwnerOpensTheGroupPreOrder(string customer)
    {
        var details = Recall<CustomerSession>(customer).Details;
        return Actor.AttemptsToAsync(new SignInThroughTheLoginForm(details.Email, details.Password), new OpenTheGroupPreOrder());
    }

    [When("{word} removes the guest {word} on the screen")]
    public Task WhenOwnerRemovesOnScreen(string owner, string guest)
    {
        _ = owner;
        return Actor.AttemptsToAsync(new RemoveTheGuestOnScreen(guest));
    }

    [When("{word} submits the group pre-order on the screen")]
    public Task WhenOwnerSubmitsOnScreen(string owner)
    {
        _ = owner;
        return Actor.AttemptsToAsync(new SubmitTheGroupPreOrderOnScreen());
    }

    [When("{word} signs out")]
    public Task WhenSignsOut(string customer)
    {
        _ = customer;
        return Actor.AttemptsToAsync(new SignOut());
    }

    [When("the waiter signs in and opens the pre-order of {word}")]
    public Task WhenWaiterOpensThePreOrder(string customer)
    {
        var (username, password) = Settings.Current.RequireWaiter();
        return Actor.AttemptsToAsync(new SignInThroughTheLoginForm(username, password), new OpenThePreOrderAsWaiter(customer));
    }

    // ---- the owner's screen ----

    [Then("the screen lists {int} participant(s)")]
    public async Task ThenScreenLists(int count) =>
        await Assert.That((await Screen()).Participants.Count).IsEqualTo(count);

    [Then("the screen lists the guest {word}")]
    public async Task ThenScreenListsGuest(string guest) =>
        await Assert.That((await Screen()).Participants.Any(p => p.Title.Contains(guest, StringComparison.Ordinal))).IsTrue();

    [Then("the screen does not list the guest {word}")]
    public async Task ThenScreenDoesNotListGuest(string guest) =>
        await Assert.That((await Screen()).Participants.Any(p => p.Title.Contains(guest, StringComparison.Ordinal))).IsFalse();

    [Then("the screen shows {word} as {string}")]
    public async Task ThenScreenShowsStatus(string guest, string status) =>
        await Assert.That((await Participant(guest)).Status).IsEqualTo(status);

    [Then("the screen shows no dishes for {word}")]
    public async Task ThenScreenShowsNoDishes(string guest)
    {
        var participant = await Participant(guest);
        await Assert.That(participant.HasNoDishes).IsTrue();
        await Assert.That(participant.Subtotal).IsEqualTo(0m);
    }

    [Then("the screen shows no dishes for the owner")]
    public async Task ThenScreenShowsNoDishesForOwner()
    {
        var owner = (await Screen()).Participants[0];
        await Assert.That(owner.HasNoDishes).IsTrue();
    }

    [Then("the screen shows for {word}:")]
    public async Task ThenScreenShowsFor(string guest, DataTable portions)
    {
        var participant = await Participant(guest);
        await Assert.That(Items(participant.Items)).IsEquivalentTo(Items(portions));
        await AssertMoney(participant.Subtotal, PriceOf(portions));
    }

    [Then("the screen shows the grand total of:")]
    public async Task ThenScreenShowsGrandTotal(DataTable portions) =>
        await AssertMoney((await Screen()).GrandTotal, PriceOf(portions));

    [Then("the screen shows that nobody has added dishes yet")]
    public async Task ThenNobodyHasChosen()
    {
        var screen = await Screen();
        await Assert.That(screen.NobodyHasChosenAnything).IsTrue();
        await Assert.That(screen.GrandTotal).IsEqualTo(0m);
    }

    [Then("the screen shows the pre-order as sent")]
    public async Task ThenScreenShowsSent() => await Assert.That((await Screen()).Sent).IsTrue();

    [Then("the consolidated pre-order lists:")]
    public async Task ThenConsolidatedLists(DataTable portions) =>
        await Assert.That(Items((await Screen()).Consolidated)).IsEquivalentTo(Items(portions));

    [Then("the group pre-order is shown")]
    public async Task ThenGroupShown() => await Assert.That(await Actor.AsksForAsync(new TheGroupPreOrderIsShown())).IsTrue();

    [Then("the group pre-order is not shown")]
    public async Task ThenGroupNotShown() => await Assert.That(await Actor.AsksForAsync(new TheGroupPreOrderIsShown())).IsFalse();

    // ---- the waiter's screen ----

    [Then("the waiter sees on the pre-order page:")]
    public async Task ThenWaiterSeesOnPage(DataTable portions)
    {
        var shown = await Actor.AsksForAsync(new TheWaiterPreOrderOnScreen());
        foreach (var (dish, quantity) in Rows(portions))
        {
            var line = shown.FirstOrDefault(i => i.Name.Contains(dish.Name, StringComparison.Ordinal));
            await Assert.That(line).IsNotNull().Because($"dish {dish.Name} is not on the waiter's page");
            await Assert.That(line!.Quantity).IsEqualTo(quantity);
        }
    }

    [Then("the waiter does not see dish {int} on the pre-order page")]
    public async Task ThenWaiterDoesNotSeeOnPage(int dish)
    {
        var name = Actor.RecallAll<DishResponse>()[dish - 1].Name;
        var shown = await Actor.AsksForAsync(new TheWaiterPreOrderOnScreen());
        await Assert.That(shown.Any(i => i.Name.Contains(name, StringComparison.Ordinal))).IsFalse();
    }

    // ---- helpers ----

    private async Task<ScreenGroup> Screen() => await Actor.AsksForAsync(new TheGroupPreOrderOnScreen());

    private async Task<ScreenParticipant> Participant(string name)
    {
        var screen = await Screen();
        return screen.Participants.FirstOrDefault(p => p.Title.Contains(name, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{name} is not on the screen: {string.Join(", ", screen.Participants.Select(p => p.Title))}");
    }

    private static async Task AssertMoney(decimal actual, decimal expected) =>
        await Assert.That(Math.Abs(actual - expected)).IsLessThanOrEqualTo(CentsTolerance)
            .Because($"the screen shows {actual:0.00}, the prices add up to {expected:0.00}");

    private static List<string> Items(IEnumerable<ScreenItem> items) =>
        [.. items.GroupBy(i => i.Name).Select(g => $"{g.Key}:{g.Sum(i => i.Quantity)}").Order(StringComparer.Ordinal)];

    private List<string> Items(DataTable table) => Items(Rows(table).Select(r => new ScreenItem(r.Dish.Name, r.Portions)));

    private decimal PriceOf(DataTable table) => Rows(table).Sum(r => r.Dish.Price * r.Portions);

    private IEnumerable<(DishResponse Dish, int Portions)> Rows(DataTable table)
    {
        var dishes = Actor.RecallAll<DishResponse>();
        return table.Rows.Select(row => (
            dishes[int.Parse(row["dish"], CultureInfo.InvariantCulture) - 1],
            int.Parse(row["portions"], CultureInfo.InvariantCulture)));
    }
}
