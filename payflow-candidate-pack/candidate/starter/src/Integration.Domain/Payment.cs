namespace Integration.Domain;

/// <summary>
/// Our own record of a payment we have asked PayFlow to process.
/// This is a starting point, not a prescription — reshape it if your design calls for it.
/// </summary>
public sealed class Payment
{
    public required string OrderId { get; init; }

    /// <summary>PayFlow's identifier. Null until we have heard back from them.</summary>
    public string? PayFlowPaymentId { get; set; }

    public required long Amount { get; init; }
    public required string Currency { get; init; }

    public PaymentState State { get; set; } = PaymentState.New;

    public long CapturedAmount { get; set; }
    public long RefundedAmount { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum PaymentState
{
    New,
    AwaitingAuthentication,
    Authorized,
    Captured,
    Refunded,
    Failed
}
