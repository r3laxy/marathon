using System.Globalization;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;

namespace Restaurant_Aqa_Framework.StepDefinitions;

/// <summary>The group pre-order from the owner's, the guests' and the bystanders' point of view. Dishes are referred
/// to by their number on the scenario's menu ("dish 1"), people by name; ids, tokens and prices never appear in a
/// feature file. Everything a step needs from an earlier step is in the actor's memory.</summary>
[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    private const string MissingId = "00000000-0000-0000-0000-000000000000";

    // ---- the scene ----

    [Given("a restaurant with {int} dishes on the menu")]
    public Task GivenARestaurantWithDishes(int dishes) => Actor.AttemptsToAsync(new SetUpTheRestaurant(dishes));

    [Given("a customer named {word} who has booked a table for {int} guests")]
    public async Task GivenACustomerWhoHasBooked(string name, int guests) =>
        await Actor.AttemptsToAsync(new RegisterCustomer(name), new AddTable(seats: guests), new BookTableForGroup(name, guests));

    [Given("another customer named {word} who has booked a table for {int} guests")]
    public async Task GivenAnotherCustomerWithAGroup(string name, int guests) =>
        await Actor.AttemptsToAsync(new RegisterCustomer(name), new BookTableForGroup(name, guests, groupName: name));

    [Given("the guest {word} has joined the group pre-order of {word}")]
    public Task GivenGuestHasJoinedAnotherGroup(string guest, string owner) =>
        Actor.AttemptsToAsync(new JoinTheGroupPreOrder(guest, groupName: owner));

    [Given("another customer named {word} who has booked a table for {int} guests and has not opened the group pre-order")]
    public async Task GivenAnotherCustomerWhoHasNotOpened(string name, int guests) =>
        await Actor.AttemptsToAsync(
            new RegisterCustomer(name), new BookTableForGroup(name, guests, groupName: name, openGroupPreOrder: false));

    [Given("another customer named {word} is signed in")]
    public Task GivenAnotherCustomer(string name) => Actor.AttemptsToAsync(new RegisterCustomer(name));

    [Given("{word} has placed their own pre-order of dish {int}")]
    public Task GivenOwnerHasOrdered(string owner, int dish) => Actor.AttemptsToAsync(new PlaceTheOwnersOrder(owner, dish));

    [Given("the guest {word} has joined the group pre-order")]
    public Task GivenGuestHasJoined(string guest) => Actor.AttemptsToAsync(new JoinTheGroupPreOrder(guest));

    [Given("the guest {word} has put {int} portion(s) of dish {int} in their cart")]
    public Task GivenGuestHasPutDish(string guest, int portions, int dish) =>
        Actor.AttemptsToAsync(new PutDishInTheCart(guest, dish, portions, mustSucceed: true));

    [Given("the guest {word} has submitted their cart")]
    public Task GivenGuestHasSubmitted(string guest) => Actor.AttemptsToAsync(new SubmitTheCart(guest, mustSucceed: true));

    [Given("{word} has removed the guest {word} from the group pre-order")]
    public async Task GivenOwnerHasRemoved(string owner, string guest) =>
        await Actor.AttemptsToAsync(new RemoveParticipant(
            Recall<CustomerSession>(owner).IdToken, Recall<GuestSession>(guest).SessionId, mustSucceed: true));

    [Given("{word} has submitted the group pre-order")]
    public async Task GivenOwnerHasSubmittedTheGroup(string owner) =>
        await Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(Recall<CustomerSession>(owner).IdToken, mustSucceed: true));

    // ---- guests ----

    [When("the guest {word} joins the group pre-order")]
    public Task WhenGuestJoins(string guest) => Actor.AttemptsToAsync(new JoinTheGroupPreOrder(guest, mustSucceed: false));

    [When("the guest {word} puts {int} portion(s) of dish {int} in their cart")]
    public Task WhenGuestPutsDish(string guest, int portions, int dish) =>
        Actor.AttemptsToAsync(new PutDishInTheCart(guest, dish, portions));

    [When("the guest {word} puts {int} portion(s) of dish {int} in the cart of {word}")]
    public Task WhenGuestPutsDishInSomeoneElsesCart(string guest, int portions, int dish, string other) =>
        Actor.AttemptsToAsync(new PutDishInTheCart(guest, dish, portions, intoCartOf: other));

    [When("the guest {word} submits their cart")]
    public Task WhenGuestSubmitsCart(string guest) => Actor.AttemptsToAsync(new SubmitTheCart(guest));

    // ---- looking at the group pre-order ----

    [When("{word} views the group pre-order")]
    public Task WhenCustomerViews(string customer) =>
        Actor.AttemptsToAsync(new ViewTheGroupPreOrder(Recall<CustomerSession>(customer).IdToken));

    [When("{word} views the group pre-order of {word}")]
    public Task WhenCustomerViewsAnotherGroup(string customer, string owner) =>
        Actor.AttemptsToAsync(new ViewTheGroupPreOrder(
            Recall<CustomerSession>(customer).IdToken, Recall<GroupOrder>(owner).GroupReservationId));

    [When("the waiter views the group pre-order")]
    public async Task WhenWaiterViews() =>
        await Actor.AttemptsToAsync(new ViewTheGroupPreOrder((await Actor.AsksForAsync(new TheWaiterSession())).IdToken));

    [When("the guest {word} views the group pre-order")]
    public Task WhenGuestViews(string guest) =>
        Actor.AttemptsToAsync(new ViewTheGroupPreOrder(Recall<GuestSession>(guest).Token));

    [When("someone who is not signed in views the group pre-order")]
    public Task WhenAnonymousViews() => Actor.AttemptsToAsync(new ViewTheGroupPreOrder(null));

    [When("{word} views a group pre-order that does not exist")]
    public Task WhenCustomerViewsMissing(string customer) =>
        Actor.AttemptsToAsync(new ViewTheGroupPreOrder(Recall<CustomerSession>(customer).IdToken, MissingId));

    [When("anyone checks how many people have joined")]
    public Task WhenAnyoneChecksStatus() => Actor.AttemptsToAsync(new CheckTheGroupStatus());

    // ---- submitting the group pre-order ----

    [When("{word} submits the group pre-order")]
    public Task WhenOwnerSubmits(string customer) =>
        Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(Recall<CustomerSession>(customer).IdToken));

    [When("the waiter submits the group pre-order")]
    public async Task WhenWaiterSubmits() =>
        await Actor.AttemptsToAsync(new SubmitTheGroupPreOrder((await Actor.AsksForAsync(new TheWaiterSession())).IdToken));

    [When("someone who is not signed in submits the group pre-order")]
    public Task WhenAnonymousSubmits() => Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(null));

    [When("{word} submits the group pre-order without saying which reservation it is for")]
    public Task WhenOwnerSubmitsWithoutReservation(string customer) =>
        Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(Recall<CustomerSession>(customer).IdToken, useOwn: false));

    [When("{word} submits the group pre-order for a reservation that does not exist")]
    public Task WhenOwnerSubmitsForMissingReservation(string customer) =>
        Actor.AttemptsToAsync(new SubmitTheGroupPreOrder(Recall<CustomerSession>(customer).IdToken, useOwn: false, reservationId: MissingId));

    [When("{word} looks at their order")]
    public Task WhenOwnerLooksAtOrder(string owner) => Actor.AttemptsToAsync(new ViewTheOrder(owner));

    // ---- removing participants ----

    [When("{word} removes the guest {word} from the group pre-order")]
    public Task WhenOwnerRemoves(string owner, string guest) =>
        Actor.AttemptsToAsync(new RemoveParticipant(Recall<CustomerSession>(owner).IdToken, Recall<GuestSession>(guest).SessionId));

    [When("{word} removes themselves from the group pre-order")]
    public Task WhenOwnerRemovesSelf(string owner) =>
        Actor.AttemptsToAsync(new RemoveParticipant(Recall<CustomerSession>(owner).IdToken, Recall<GroupOrder>().OwnerSessionId));

    [When("{word} removes a guest who is not in the group pre-order")]
    public Task WhenOwnerRemovesMissingGuest(string owner) =>
        Actor.AttemptsToAsync(new RemoveParticipant(Recall<CustomerSession>(owner).IdToken, MissingId));

    [When("{word} removes the guest {word} from a group pre-order that does not exist")]
    public Task WhenOwnerRemovesFromMissingGroup(string owner, string guest) =>
        Actor.AttemptsToAsync(new RemoveParticipant(Recall<CustomerSession>(owner).IdToken, Recall<GuestSession>(guest).SessionId, MissingId));

    [When("{word} tries to remove {word}, a guest of another group, from the group pre-order")]
    public Task WhenOwnerRemovesGuestOfAnotherGroup(string owner, string guest) =>
        Actor.AttemptsToAsync(new RemoveParticipant(Recall<CustomerSession>(owner).IdToken, Recall<GuestSession>(guest).SessionId));

    [When("{word} tries to remove the guest {word} from the group pre-order")]
    public Task WhenOtherCustomerRemoves(string customer, string guest) =>
        Actor.AttemptsToAsync(new RemoveParticipant(Recall<CustomerSession>(customer).IdToken, Recall<GuestSession>(guest).SessionId));

    [When("the waiter tries to remove the guest {word} from the group pre-order")]
    public async Task WhenWaiterRemoves(string guest) =>
        await Actor.AttemptsToAsync(new RemoveParticipant(
            (await Actor.AsksForAsync(new TheWaiterSession())).IdToken, Recall<GuestSession>(guest).SessionId));

    [When("someone who is not signed in tries to remove the guest {word} from the group pre-order")]
    public Task WhenAnonymousRemoves(string guest) =>
        Actor.AttemptsToAsync(new RemoveParticipant(null, Recall<GuestSession>(guest).SessionId));

    // ---- what the owner sees ----

    [Then("the group pre-order has {int} participant(s)")]
    public async Task ThenParticipantCount(int count)
    {
        var group = await Group();

        // The group is read from an eventually consistent store: right after a removal it can still list the old crowd.
        var ownerToken = Recall<CustomerSession>(Recall<GroupOrder>().OwnerName).IdToken;
        for (var attempt = 1; group.Participants.Count != count && attempt <= 8; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await Actor.AttemptsToAsync(new ViewTheGroupPreOrder(ownerToken));
            group = await Group();
        }

        await Assert.That(group.Participants.Count).IsEqualTo(count);
    }

    [Then("the owner is listed first")]
    public async Task ThenOwnerFirst()
    {
        var group = await Group();
        await Assert.That(group.Participants[0].Type).IsEqualTo("Owner");
    }

    [Then("the guest {word} is listed")]
    public async Task ThenGuestListed(string guest) =>
        await Assert.That((await GroupEventually(g => g.Participants.Any(p => p.Name == guest && p.Type == "Guest")))
            .Participants.Any(p => p.Name == guest && p.Type == "Guest")).IsTrue();

    [Then("the guest {word} is not listed")]
    public async Task ThenGuestNotListed(string guest) =>
        await Assert.That((await GroupEventually(g => g.Participants.All(p => p.Name != guest)))
            .Participants.Any(p => p.Name == guest)).IsFalse();

    [Then("the group total is zero")]
    public async Task ThenGroupTotalZero() => await Assert.That((await Group()).Total).IsEqualTo(0m);

    [Then("the group total is the price of:")]
    public async Task ThenGroupTotalIs(DataTable portions) =>
        await Assert.That((await Group()).Total).IsEqualTo(PriceOf(portions));

    [Then("the group total is the sum of what each participant owes")]
    public async Task ThenGroupTotalIsSum()
    {
        var group = await Group();
        await Assert.That(group.Total).IsEqualTo(group.Participants.Sum(p => p.Total));
    }

    [Then("the cart of {word} is empty")]
    public async Task ThenCartEmpty(string who)
    {
        var participant = await Participant(who);
        await Assert.That(participant.Items.Count).IsEqualTo(0);
        await Assert.That(participant.Total).IsEqualTo(0m);
    }

    [Then("the cart of {word} holds:")]
    public async Task ThenCartHolds(string who, DataTable portions)
    {
        var participant = await Participant(who);
        await Assert.That(Portions(participant.Items.Select(i => (i.Dish.Id, i.Quantity)))).IsEquivalentTo(Portions(portions));
        await Assert.That(participant.Total).IsEqualTo(PriceOf(portions));
    }

    [Then("{word} is shown as submitted")]
    public async Task ThenSubmitted(string who) => await Assert.That((await Participant(who)).IsSubmitted).IsTrue();

    [Then("{word} is shown as not submitted yet")]
    public async Task ThenNotSubmitted(string who) => await Assert.That((await Participant(who)).IsSubmitted).IsFalse();

    [Then("the group status shows {int} of {int} participants")]
    public async Task ThenStatusCounts(int current, int max)
    {
        var status = await Actor.AsksForAsync(new TheResponseAs<GroupStatusResponse>());
        await Assert.That(status.CurrentParticipants).IsEqualTo(current);
        await Assert.That(status.MaxCapacity).IsEqualTo(max);
    }

    [Then("the group status is {word}")]
    public async Task ThenStatusIs(string expected) =>
        await Assert.That((await Actor.AsksForAsync(new TheResponseAs<GroupStatusResponse>())).Status).IsEqualTo(expected);

    // ---- what a submission produced ----

    [Then("the submitted dishes are:")]
    public async Task ThenSubmittedDishes(DataTable portions)
    {
        var submitted = await Actor.AsksForAsync(new TheResponseAs<List<SubmittedDishResponse>>());
        await Assert.That(Portions(submitted.Select(d => (d.DishId, d.Quantity)))).IsEquivalentTo(Portions(portions));
    }

    [Then("the order is marked as sent to the restaurant")]
    public async Task ThenOrderSent()
    {
        var status = (await Actor.AsksForAsync(new TheResponseJson())).EnumerateObject()
            .First(p => string.Equals(p.Name, "status", StringComparison.OrdinalIgnoreCase)).Value;

        // The status is an enum without a string converter on the wire: a number (3) or its name.
        var sent = status.ValueKind == System.Text.Json.JsonValueKind.Number
            ? status.GetInt32() == (int)OrderStatus.SubmittedByCustomer
            : status.GetString() == nameof(OrderStatus.SubmittedByCustomer);
        await Assert.That(sent).IsTrue().Because($"the order status was {status}");
    }

    [When("the waiter looks at the reservation")]
    public async Task WhenWaiterLooksAtReservation() =>
        await Actor.AttemptsToAsync(new ViewTheReservationAsWaiter());

    [Then("the waiter sees these dishes in the pre-order:")]
    public async Task ThenWaiterSees(DataTable portions)
    {
        var body = (await Actor.AsksForAsync(new TheOutcome())).Body;
        foreach (var (dish, _) in Rows(portions))
            await Assert.That(body.Contains(dish.Id, StringComparison.Ordinal)).IsTrue().Because($"dish {dish.Name} is missing from: {body}");
    }

    [Then("the waiter does not see dish {int} in the pre-order")]
    public async Task ThenWaiterDoesNotSee(int dish)
    {
        var body = (await Actor.AsksForAsync(new TheOutcome())).Body;
        await Assert.That(body.Contains(Actor.RecallAll<DishResponse>()[dish - 1].Id, StringComparison.Ordinal)).IsFalse();
    }

    [Then("the order holds:")]
    public async Task ThenOrderHolds(DataTable portions)
    {
        var order = await Actor.AsksForAsync(new TheResponseAs<GetOrderResponse>());
        await Assert.That(Portions(order.OrderItems.Select(i => (i.Dish.Id, i.Quantity)))).IsEquivalentTo(Portions(portions));
    }

    // ---- helpers ----

    private async Task<GroupSnapshotResponse> Group() => await Actor.AsksForAsync(new TheResponseAs<GroupSnapshotResponse>());

    /// <summary>The group as it is once <paramref name="expected"/> holds. The snapshot is read from an eventually consistent
    /// store, so right after a change it may still show the old crowd: ask again (as the owner) for a few seconds.</summary>
    private async Task<GroupSnapshotResponse> GroupEventually(Func<GroupSnapshotResponse, bool> expected)
    {
        var group = await Group();
        var ownerToken = Recall<CustomerSession>(Recall<GroupOrder>().OwnerName).IdToken;
        for (var attempt = 1; !expected(group) && attempt <= 8; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await Actor.AttemptsToAsync(new ViewTheGroupPreOrder(ownerToken));
            group = await Group();
        }

        return group;
    }

    /// <summary>A person by the name they were given in the scenario: the guest of that name, else the owner.</summary>
    private async Task<ParticipantResponse> Participant(string who)
    {
        var group = await Group();
        var found = Actor.TryRecall<GroupOrder>(out var order) && order.OwnerName == who
            ? group.Participants.FirstOrDefault(p => p.Type == "Owner")
            : group.Participants.FirstOrDefault(p => p.Name == who);

        return found ?? throw new InvalidOperationException($"{who} is not in the group pre-order: {string.Join(", ", group.Participants.Select(p => p.Name))}");
    }

    /// <summary>Portions per dish id, as a sorted list of "dishId:portions" so two lists compare regardless of order.</summary>
    private static List<string> Portions(IEnumerable<(string DishId, int Quantity)> items) =>
        [.. items.GroupBy(i => i.DishId).Select(g => $"{g.Key}:{g.Sum(i => i.Quantity)}").Order(StringComparer.Ordinal)];

    private List<string> Portions(DataTable table) => Portions(Rows(table).Select(r => (r.Dish.Id, r.Portions)));

    private decimal PriceOf(DataTable table) => Rows(table).Sum(r => r.Dish.Price * r.Portions);

    private IEnumerable<(DishResponse Dish, int Portions)> Rows(DataTable table)
    {
        var dishes = Actor.RecallAll<DishResponse>();
        return table.Rows.Select(row => (
            dishes[int.Parse(row["dish"], CultureInfo.InvariantCulture) - 1],
            int.Parse(row["portions"], CultureInfo.InvariantCulture)));
    }
}
