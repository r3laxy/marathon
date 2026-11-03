using dotenv.net.Utilities;

namespace Restaurant_Aqa_Framework.Configuration;

/// <summary>The standing table the group pre-order scenarios book. A reservation is assigned to a waiter of the table's
/// location, and a waiter cannot be created through the API, so a table that has a waiter has to exist already:
/// <c>TESTTABLE_ID</c> (read by <see cref="Settings"/>) and the id of its location, <c>TESTTABLE_LOCATION_ID</c>.</summary>
public static class TestTable
{
    public static bool IsConfigured => Settings.Current.TestTableId is { Length: > 0 } && LocationId is { Length: > 0 };

    public static string? LocationId =>
        EnvReader.TryGetStringValue("TESTTABLE_LOCATION_ID", out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
