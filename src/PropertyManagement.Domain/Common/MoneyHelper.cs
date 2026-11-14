namespace PropertyManagement.Domain.Common;

public static class CurrencyCode
{
    public const string Default = "USD";

    public static string Normalize(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency code is required.", nameof(currency));
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || !normalized.All(c => c is >= 'A' and <= 'Z'))
        {
            throw new ArgumentException($"Currency code '{currency}' must be a valid 3-letter ISO code (e.g. USD, EGP).", nameof(currency));
        }

        return normalized;
    }
}

public static class MoneyRounding
{
    public static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
