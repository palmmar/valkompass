using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valkompass.Application.Dtos;

/// <summary>
/// Serialiserar en enum som camelCase-sträng – <c>preElection</c>, inte <c>PreElection</c> –
/// så att den ser ut som allt annat i API:t.
/// </summary>
/// <remarks>
/// <see cref="JsonConverterAttribute"/> kan bara peka ut en typ, inte skicka med en namnpolicy,
/// så policyn bakas in här. Utan den blir enum-fält den enda delen av svaret som kommer ut i
/// PascalCase, och en klient som jämför mot fel sträng får inget fel – bara fel läge.
/// </remarks>
public sealed class CamelCaseJsonStringEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public CamelCaseJsonStringEnumConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
