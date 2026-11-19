namespace PropertyManagement.Infrastructure.Payments;

public sealed class PaymentProviderOptions
{
    public const string SectionName = "Payments";

    public int WebhookToleranceSeconds { get; set; } = 300;
    public SimulatedProviderOptions Simulated { get; set; } = new();
    public StripeProviderOptions Stripe { get; set; } = new();
    public PaymobProviderOptions Paymob { get; set; } = new();
}

public sealed class SimulatedProviderOptions
{
    public string WebhookSecret { get; set; } = "sim_whsec_local_dev_secret_change_in_prod";
    public decimal FeePercent { get; set; } = 0.029m;
    public decimal FixedFee { get; set; } = 3.00m;
}

public sealed class StripeProviderOptions
{
    public string BaseUrl { get; set; } = "https://api.stripe.com";
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public sealed class PaymobProviderOptions
{
    public string BaseUrl { get; set; } = "https://accept.paymob.com/api";
    public string ApiKey { get; set; } = string.Empty;
    public string HmacSecret { get; set; } = string.Empty;
    public string IntegrationId { get; set; } = string.Empty;
    public string IframeId { get; set; } = string.Empty;
}
