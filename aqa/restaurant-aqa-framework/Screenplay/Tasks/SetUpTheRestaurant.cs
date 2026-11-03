using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Gives the scenario a restaurant with a menu: the standing test location and table when they are configured
/// (the only ones with a waiter), otherwise a location of its own. Either way it adds its own dishes, which are removed
/// again afterwards.</summary>
public sealed class SetUpTheRestaurant(int dishes) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        if (TestTable.IsConfigured)
        {
            var locationId = TestTable.LocationId!;
            actor.Remember(new LocationResponse(locationId, "", "", 0, "", null, 0));
            actor.Remember(new TableResponse(Settings.Current.TestTableId!, locationId, 0, 0));
        }
        else
        {
            await new SetUpLocation().PerformAsAsync(actor);
        }

        for (var i = 0; i < dishes; i++)
            await new AddDish().PerformAsAsync(actor);
    }
}
