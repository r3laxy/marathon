using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Registers a brand-new customer through the service, signs them in and remembers them under their name.</summary>
public sealed class RegisterCustomer(string name) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var details = SampleDataFactory.RegisterRequest() with { FirstName = $"{SampleDataFactory.Prefix}{name}" };
        var auth = new AuthApi(actor.UsingTheApi());

        var registration = await auth.RegisterAsync(details);
        if (!registration.Ok)
            throw new InvalidOperationException($"Registering {name} failed with {registration.Status}: {await registration.TextAsync()}");

        var signIn = await auth.SignInAsync(new SignInRequest(details.Email, details.Password));
        if (!signIn.Ok)
            throw new InvalidOperationException($"Signing {name} in failed with {signIn.Status}: {await signIn.TextAsync()}");

        actor.Remember(name, new CustomerSession(name, details, (await signIn.ReadAsAsync<SignInResponse>()).IdToken));
    }
}
