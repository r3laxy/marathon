using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Opens the scenario's group pre-order page (the visitor has to be signed in already).</summary>
public sealed class OpenTheGroupPreOrder : ITask
{
    public Task PerformAsAsync(Actor actor) =>
        new GroupOrderPage(actor.LookingAtThePage()).NavigateAsync(actor.Recall<GroupOrder>().ReservationId);
}
