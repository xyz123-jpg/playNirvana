using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace PayFlow.MockServer;

/// <summary>In-memory state for the sandbox. Nothing survives a restart, by design.</summary>
public sealed class PaymentStore
{
    private readonly ConcurrentDictionary<string, Payment> _payments = new();
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _idempotency = new();
    private readonly ConcurrentDictionary<string, WebhookEndpoint> _endpoints = new();
    private readonly ConcurrentQueue<DateTimeOffset> _requestTimes = new();

    public IReadOnlyCollection<WebhookEndpoint> Endpoints => _endpoints.Values.ToArray();

    // ---------------------------------------------------------------- payments

    public void Add(Payment payment) => _payments[payment.PaymentId] = payment;

    public Payment? Get(string paymentId) =>
        _payments.TryGetValue(paymentId, out var p) ? p : null;

    public IEnumerable<Payment> All() => _payments.Values;

    // ------------------------------------------------------------ idempotency

    /// <summary>
    /// Planted divergence D1: <c>Idempotency-Key</c> is fully enforced here, although the
    /// endpoint reference never documents it — only one line in the changelog hints at it.
    /// </summary>
    public sealed record IdempotencyRecord(string BodyHash, string PaymentId);

    public enum IdempotencyResult { Fresh, Replay, Conflict }

    public IdempotencyResult CheckIdempotency(string key, string rawBody, out string? existingPaymentId)
    {
        var hash = Sha256(rawBody);

        if (_idempotency.TryGetValue(key, out var record))
        {
            existingPaymentId = record.PaymentId;
            return record.BodyHash == hash ? IdempotencyResult.Replay : IdempotencyResult.Conflict;
        }

        existingPaymentId = null;
        return IdempotencyResult.Fresh;
    }

    public void RecordIdempotency(string key, string rawBody, string paymentId) =>
        _idempotency[key] = new IdempotencyRecord(Sha256(rawBody), paymentId);

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    // -------------------------------------------------------- webhook targets

    public void RegisterEndpoint(string url) =>
        _endpoints[url] = new WebhookEndpoint(url, DateTimeOffset.UtcNow);

    public void ClearEndpoints() => _endpoints.Clear();

    // ------------------------------------------------------------ rate limits

    /// <summary>
    /// 20 requests per rolling 10 seconds. The published error table lists 429 but states no
    /// limit anywhere, and the <c>Retry-After</c> header we send is undocumented (defect B5).
    /// </summary>
    public bool IsRateLimited()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddSeconds(-10);

        while (_requestTimes.TryPeek(out var oldest) && oldest < cutoff)
            _requestTimes.TryDequeue(out _);

        if (_requestTimes.Count >= 20)
            return true;

        _requestTimes.Enqueue(now);
        return false;
    }
}

/// <summary>
/// Expires abandoned 3-D Secure payments after 90 seconds — silently.
/// Planted defect C5: no webhook is emitted for this transition, so a purely webhook-driven
/// integration leaves the order pending forever.
/// </summary>
public sealed class ThreeDsExpirySweeper : BackgroundService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(90);

    private readonly PaymentStore _store;
    private readonly ILogger<ThreeDsExpirySweeper> _log;

    public ThreeDsExpirySweeper(PaymentStore store, ILogger<ThreeDsExpirySweeper> log)
    {
        _store = store;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var payment in _store.All())
            {
                if (payment.Status != PaymentStatus.Pending3ds) continue;
                if (now - payment.CreatedAt < Ttl) continue;

                payment.Status = PaymentStatus.Expired;
                payment.UpdatedAt = now;

                // Deliberately NO webhook here.
                _log.LogInformation(
                    "[sandbox] 3DS challenge abandoned, payment {PaymentId} expired (no webhook sent)",
                    payment.PaymentId);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
