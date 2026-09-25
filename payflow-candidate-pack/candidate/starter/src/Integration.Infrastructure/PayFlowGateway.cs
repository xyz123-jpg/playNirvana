using Integration.Application;

namespace Integration.Infrastructure;

/// <summary>Binds the <c>PayFlow</c> section of configuration.</summary>
public sealed class PayFlowOptions
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
}

/// <summary>
/// HTTP client for PayFlow. The shell is here so you do not have to spend the timebox on
/// plumbing — the calls themselves are yours to write.
/// </summary>
public sealed class PayFlowGateway : IPayFlowGateway
{
    private readonly HttpClient _http;

    public PayFlowGateway(HttpClient http) => _http = http;

    public Task<CreatePaymentResult> CreatePaymentAsync(
        CreatePaymentCommand command, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<PaymentSnapshot> GetPaymentAsync(
        string payFlowPaymentId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task CaptureAsync(
        string payFlowPaymentId, long amount, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task RefundAsync(
        string payFlowPaymentId, long amount, string? reason = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
