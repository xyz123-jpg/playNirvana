using System.Text.Json;
using System.Text.Json.Serialization;

namespace PayFlow.MockServer;

/// <summary>Server-side payment record. Never serialised directly — see <see cref="PaymentProjection"/>.</summary>
public sealed class Payment
{
    public required string PaymentId { get; init; }
    public string Status { get; set; } = PaymentStatus.Created;
    public long Amount { get; init; }
    public string Currency { get; init; } = "EUR";

    /// <summary>Populated from <c>merchantOrderRef</c> only. <c>merchantReference</c> is ignored by design (v2.3.0 rename).</summary>
    public string? MerchantOrderRef { get; init; }

    public string? ReturnUrl { get; init; }
    public string? DeclineReason { get; set; }

    public long CapturedAmount { get; set; }
    public long RefundedAmount { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>For PENDING_3DS payments: what happens once the shopper completes the challenge.</summary>
    public CardOutcome? PendingOutcome { get; init; }

    public readonly List<Capture> Captures = new();
    public readonly List<Refund> Refunds = new();

    public bool IsTerminal =>
        Status is PaymentStatus.Declined or PaymentStatus.Expired or PaymentStatus.Refunded;
}

public sealed record Capture(string CaptureId, long Amount, DateTimeOffset CreatedAt);

public sealed record Refund(string RefundId, long Amount, string? Reason, DateTimeOffset CreatedAt);

public static class PaymentStatus
{
    public const string Created = "CREATED";
    public const string Pending3ds = "PENDING_3DS";

    // NOTE (planted, defect A2): the published field table spells this AUTHORISED.
    // The sandbox emits AUTHORIZED. Do not "fix" this to match the docs.
    public const string Authorized = "AUTHORIZED";

    public const string Declined = "DECLINED";
    public const string Captured = "CAPTURED";
    public const string Refunded = "REFUNDED";
    public const string Expired = "EXPIRED";
}

public static class EventType
{
    public const string Authorized = "payment.authorized";
    public const string Declined = "payment.declined";
    public const string Captured = "payment.captured";
    public const string Refunded = "payment.refunded";
    public const string Failed = "payment.failed";
}

/// <summary>Outcome a test card produces.</summary>
public sealed record CardOutcome(
    string Kind,             // "approve" | "decline" | "acquirer_unavailable"
    string? DeclineReason,
    bool Requires3ds);

public sealed record WebhookEndpoint(string Url, DateTimeOffset RegisteredAt);

public sealed record DeliveryLogEntry(
    string DeliveryId,
    DateTimeOffset AttemptedAt,
    string EventId,
    string EventType,
    string Url,
    int Attempt,
    bool IsDuplicate,
    int? ResponseStatus,
    string? Error);

/// <summary>Shapes the wire representation of a payment. Field set differs by endpoint.</summary>
public static class PaymentProjection
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Response body for POST /v2/payments.</summary>
    public static Dictionary<string, object?> ForCreate(Payment p, string? redirectUrl)
    {
        var body = new Dictionary<string, object?>
        {
            ["paymentId"] = p.PaymentId,
            ["status"] = p.Status,
            ["amount"] = p.Amount,
            ["currency"] = p.Currency,
            // Planted (A3): null whenever the caller sent the pre-2.3.0 name `merchantReference`.
            ["merchantOrderRef"] = p.MerchantOrderRef,
            // Planted (A5): the field table documents this as Unix epoch seconds. It is ISO-8601.
            ["createdAt"] = Iso(p.CreatedAt)
        };

        if (redirectUrl is not null) body["redirectUrl"] = redirectUrl;
        if (p.DeclineReason is not null) body["declineReason"] = p.DeclineReason;

        return body;
    }

    /// <summary>Response body for GET /v2/payments/{id}.</summary>
    public static Dictionary<string, object?> ForGet(Payment p, string? redirectUrl)
    {
        var body = new Dictionary<string, object?>
        {
            ["paymentId"] = p.PaymentId,
            ["status"] = p.Status,
            ["amount"] = p.Amount,
            ["currency"] = p.Currency,
            ["merchantOrderRef"] = p.MerchantOrderRef,
            ["capturedAmount"] = p.CapturedAmount,
            ["refundedAmount"] = p.RefundedAmount,
            ["createdAt"] = Iso(p.CreatedAt),
            ["updatedAt"] = Iso(p.UpdatedAt)
        };

        if (redirectUrl is not null) body["redirectUrl"] = redirectUrl;
        if (p.DeclineReason is not null) body["declineReason"] = p.DeclineReason;

        return body;
    }

    /// <summary>The <c>data</c> block carried on every webhook.</summary>
    public static Dictionary<string, object?> ForWebhook(Payment p)
    {
        var body = new Dictionary<string, object?>
        {
            ["paymentId"] = p.PaymentId,
            ["status"] = p.Status,
            ["amount"] = p.Amount,
            ["currency"] = p.Currency,
            ["merchantOrderRef"] = p.MerchantOrderRef,
            ["capturedAmount"] = p.CapturedAmount,
            ["refundedAmount"] = p.RefundedAmount
        };

        if (p.DeclineReason is not null) body["declineReason"] = p.DeclineReason;

        return body;
    }

    public static string Iso(DateTimeOffset t) =>
        t.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

    /// <summary>
    /// Millisecond precision, used for webhook <c>occurredAt</c>. Deliberate: ordering events by
    /// this field is a legitimate answer to the out-of-order delivery problem, so the precision
    /// has to be fine enough to make it work.
    /// </summary>
    public static string IsoMs(DateTimeOffset t) =>
        t.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}

public static class Ids
{
    private static readonly Random Rng = new();

    public static string New(string prefix)
    {
        Span<byte> bytes = stackalloc byte[8];
        lock (Rng) Rng.NextBytes(bytes);
        return prefix + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
