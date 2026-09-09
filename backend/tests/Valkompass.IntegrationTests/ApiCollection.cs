namespace Valkompass.IntegrationTests;

/// <summary>
/// Alla integrationstester delar en <see cref="ApiFactory"/>.
/// </summary>
/// <remarks>
/// Nödvändigt, inte bara snabbare: <see cref="ApiFactory"/> sätter anslutningssträngen som en
/// process-miljövariabel, eftersom minimal hosting läser den i Program.cs innan
/// ConfigureAppConfiguration hinner appliceras. Två fabriker parallellt hade därför skrivit
/// över varandras anslutningssträng och kört migrations mot samma databas samtidigt.
/// </remarks>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
