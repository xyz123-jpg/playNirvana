using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PayFlow.MockServer;

/// <summary>
/// Delivers signed webhooks. Behaviour is deliberately at-odds with docs/03-webhooks.md, which
/// claims exactly-once, in-order delivery.
///
/// Planted behaviour:
///  - C2a  every event is delivered TWICE with the same eventId (primary, then a copy at +3s)
///  - C2b  the +3s copy of payment.authorized therefore lands AFTER payment.captured whenever
///         the merchant captures promptly — a naive handler moves the payment backwards
///  - C1   payment.authorized is dispatched ~200ms after creation while POST /v2/payments does
///         not return for ~800ms, so the webhook can arrive before the caller knows the id
///  - D3   the signature covers the RAW serialised bytes, so the re-serialising sample in the
///         docs never verifies
///  - D4   retries are 3 attempts at ~5s, not the "5 times over 24 hours" the docs promise
/// </summary>
public sealed class WebhookDispatcher : BackgroundService
{
    public const int DuplicateDelayMs = 3000;

    /// <summary>
    /// The duplicate of an authorisation is held back further so that it lands after any capture
    /// activity. A handler that applies events in arrival order and does not dedupe will finish
    /// with the payment sitting in AUTHORIZED when it is in fact CAPTURED.
    /// </summary>
    public const int AuthorizedDuplicateDelayMs = 6000;

    public const int MaxRetries = 3;
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private sealed record Scheduled(
        DateTimeOffset DueAt,
        string EventId,
        string EventType,
        string Body,
        int Attempt,
        bool IsDuplicate);

    private readonly ConcurrentDictionary<Guid, Scheduled> _queue = new();
    private readonly ConcurrentQueue<DeliveryLogEntry> _log = new();

    private readonly PaymentStore _store;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebhookDispatcher> _logger;
    private readonly string _secret;

    public WebhookDispatcher(
        PaymentStore store,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<WebhookDispatcher> logger)
    {
        _store = store;
        _httpFactory = httpFactory;
        _logger = logger;
        _secret = config["PayFlow:WebhookSecret"]
                  ?? "whsec_test_6e2c90b148af4d73a5c1e07b9f36d284";
    }

    public IReadOnlyList<DeliveryLogEntry> RecentDeliveries => _log.ToArray();

    /// <summary>Queues an event for delivery, plus its duplicate.</summary>
    public void Enqueue(string eventType, Payment payment, int initialDelayMs)
    {
        var eventId = Ids.New("evt_");
        var body = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["eventId"] = eventId,
            ["eventType"] = eventType,
            ["occurredAt"] = PaymentProjection.IsoMs(DateTimeOffset.UtcNow),
            ["data"] = PaymentProjection.ForWebhook(payment)
        }, PaymentProjection.Json);

        var now = DateTimeOffset.UtcNow;

        var duplicateDelay = eventType == EventType.Authorized
            ? AuthorizedDuplicateDelayMs
            : DuplicateDelayMs;

        _queue[Guid.NewGuid()] = new Scheduled(
            now.AddMilliseconds(initialDelayMs), eventId, eventType, body, 1, false);

        _queue[Guid.NewGuid()] = new Scheduled(
            now.AddMilliseconds(initialDelayMs + duplicateDelay), eventId, eventType, body, 1, true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var (id, item) in _queue)
            {
                if (item.DueAt > now) continue;
                if (!_queue.TryRemove(id, out _)) continue;

                _ = DeliverAsync(item, stoppingToken);
            }

            try { await Task.Delay(100, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task DeliverAsync(Scheduled item, CancellationToken ct)
    {
        foreach (var endpoint in _store.Endpoints)
        {
            int? status = null;
            string? error = null;

            try
            {
                var client = _httpFactory.CreateClient("webhooks");
                client.Timeout = TimeSpan.FromSeconds(10);

                // Sign the exact bytes we are about to transmit (divergence D3).
                var raw = Encoding.UTF8.GetBytes(item.Body);
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var signature = Sign(timestamp, raw);

                using var content = new ByteArrayContent(raw);
                content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
                {
                    Content = content
                };
                request.Headers.TryAddWithoutValidation("X-PayFlow-Signature", signature);
                request.Headers.TryAddWithoutValidation("X-PayFlow-Event-Id", item.EventId);

                using var response = await client.SendAsync(request, ct);
                status = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode && !item.IsDuplicate && item.Attempt < MaxRetries)
                    Reschedule(item);
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;

                if (!item.IsDuplicate && item.Attempt < MaxRetries)
                    Reschedule(item);
            }

            Record(new DeliveryLogEntry(
                Ids.New("dlv_"),
                DateTimeOffset.UtcNow,
                item.EventId,
                item.EventType,
                endpoint.Url,
                item.Attempt,
                item.IsDuplicate,
                status,
                error));

            _logger.LogInformation(
                "[sandbox] {EventType} {EventId} -> {Url} attempt={Attempt} duplicate={Dup} status={Status}",
                item.EventType, item.EventId, endpoint.Url, item.Attempt, item.IsDuplicate,
                status?.ToString() ?? error);
        }
    }

    private void Reschedule(Scheduled item) =>
        _queue[Guid.NewGuid()] = item with
        {
            DueAt = DateTimeOffset.UtcNow.Add(RetryInterval),
            Attempt = item.Attempt + 1
        };

    /// <summary>
    /// HMAC-SHA256 over "{timestamp}.{raw body bytes}". Nothing is parsed or re-serialised,
    /// which is why the sample in docs/03-webhooks.md cannot reproduce this value.
    /// </summary>
    private string Sign(long timestamp, byte[] rawBody)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");

        var signed = new byte[prefix.Length + rawBody.Length];
        Buffer.BlockCopy(prefix, 0, signed, 0, prefix.Length);
        Buffer.BlockCopy(rawBody, 0, signed, prefix.Length, rawBody.Length);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var hash = hmac.ComputeHash(signed);

        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private void Record(DeliveryLogEntry entry)
    {
        _log.Enqueue(entry);
        while (_log.Count > 200) _log.TryDequeue(out _);
    }
}
