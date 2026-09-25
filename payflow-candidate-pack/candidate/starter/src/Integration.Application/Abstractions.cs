using Integration.Domain;

namespace Integration.Application;

/// <summary>Everything our application needs from PayFlow. Implement in Infrastructure.</summary>
public interface IPayFlowGateway
{
    Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentCommand command, CancellationToken ct = default);

    Task<PaymentSnapshot> GetPaymentAsync(string payFlowPaymentId, CancellationToken ct = default);

    Task CaptureAsync(string payFlowPaymentId, long amount, CancellationToken ct = default);

    Task RefundAsync(string payFlowPaymentId, long amount, string? reason = null, CancellationToken ct = default);
}

public interface IPaymentRepository
{
    Task<Payment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default);

    Task<Payment?> FindByPayFlowIdAsync(string payFlowPaymentId, CancellationToken ct = default);

    Task SaveAsync(Payment payment, CancellationToken ct = default);
}

public sealed record CreatePaymentCommand(
    string OrderId,
    long Amount,
    string Currency,
    CardDetails Card,
    string ReturnUrl);

public sealed record CardDetails(
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string Cvv,
    string? HolderName = null);

public sealed record CreatePaymentResult(
    string PayFlowPaymentId,
    string Status,
    string? RedirectUrl,
    string? DeclineReason);

public sealed record PaymentSnapshot(
    string PayFlowPaymentId,
    string Status,
    long Amount,
    long CapturedAmount,
    long RefundedAmount,
    string? DeclineReason);
