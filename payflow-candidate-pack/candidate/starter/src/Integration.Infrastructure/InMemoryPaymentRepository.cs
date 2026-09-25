using System.Collections.Concurrent;
using Integration.Application;
using Integration.Domain;

namespace Integration.Infrastructure;

/// <summary>
/// Good enough for the exercise — a real database is explicitly out of scope.
/// If your design needs different lookups, change the interface.
/// </summary>
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<string, Payment> _byOrderId = new();

    public Task<Payment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default) =>
        Task.FromResult(_byOrderId.TryGetValue(orderId, out var payment) ? payment : null);

    public Task<Payment?> FindByPayFlowIdAsync(string payFlowPaymentId, CancellationToken ct = default) =>
        Task.FromResult(_byOrderId.Values
            .FirstOrDefault(p => p.PayFlowPaymentId == payFlowPaymentId));

    public Task SaveAsync(Payment payment, CancellationToken ct = default)
    {
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        _byOrderId[payment.OrderId] = payment;
        return Task.CompletedTask;
    }
}
