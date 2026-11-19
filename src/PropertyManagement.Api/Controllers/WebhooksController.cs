using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Application.Features.Payments.Webhooks;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[AllowAnonymous]
[Route("api/v1/webhooks/payments")]
public sealed class WebhooksController(ISender sender) : ApiControllerBase
{
    [HttpPost("{provider}")]
    [ProducesResponseType(typeof(WebhookProcessingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HandlePaymentWebhook(
        [FromRoute] PaymentProviderType provider,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }

        if (provider == PaymentProviderType.Paymob &&
            !headers.ContainsKey("hmac") &&
            Request.Query.TryGetValue("hmac", out var queryHmac) &&
            !string.IsNullOrWhiteSpace(queryHmac))
        {
            headers["hmac"] = queryHmac.ToString();
        }

        var result = await sender.Send(
            new ProcessPaymentWebhookCommand(provider, rawBody, headers),
            cancellationToken);

        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }
}
