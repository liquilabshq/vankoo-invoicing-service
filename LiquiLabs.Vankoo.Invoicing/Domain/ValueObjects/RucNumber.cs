using LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;

public sealed record RucNumber
{
    public string Value { get; init; }

    private RucNumber(string value)
    {
        if (!IsValid(value))
            throw new InvalidRucException($"RUC '{value}' is not valid");

        Value = value;
    }

    public static RucNumber Of(string value) => new(value);

    private static bool IsValid(string? ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc)) return false;
        if (ruc.Length != 11) return false;
        if (!ruc.All(char.IsDigit)) return false;

        var prefix = ruc[..2];
        return prefix is "10" or "15" or "20";
    }

    public bool IsNaturalPerson() => Value.StartsWith("10");
    public bool IsLegalEntity() => Value.StartsWith("20");

    public override string ToString() => Value;
}