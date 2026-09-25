using System.Text.Json;
using PayFlow.MockServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PaymentStore>();
builder.Services.AddSingleton<WebhookDispatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookDispatcher>());
builder.Services.AddHostedService<ThreeDsExpirySweeper>();

var app = builder.Build();

var store = app.Services.GetRequiredService<PaymentStore>();
var webhooks = app.Services.GetRequiredService<WebhookDispatcher>();
var config = app.Configuration;

var apiKey = config["PayFlow:ApiKey"] ?? "pf_test_4b81d0f7a2c94e36b5170ac8e93d2f61";
var json = PaymentProjection.Json;

// Timings that produce the planted races. See WebhookDispatcher for the full explanation.
const int CreateResponseDelayMs = 800;   // POST /v2/payments is slow to return ...
const int AuthorizedWebhookDelayMs = 200; // ... but the webhook is already on its way (C1)
// The FIRST capture on a payment is announced late and later captures promptly, so a
// partial-capture sequence delivers its cumulative `capturedAmount` values out of order with
// distinct event ids. Deduplication alone does not save a handler from this — only ordering by
// `occurredAt`, or a monotonic update, does (planted defect C2).
const int FirstCaptureWebhookDelayMs = 2500;
const int LaterCaptureWebhookDelayMs = 150;
const int RefundWebhookDelayMs = 150;

// ---------------------------------------------------------------- helpers

IResult Error(int status, string code, string message) =>
    Results.Json(new
    {
        error = new { code, message, requestId = Ids.New("req_") }
    }, json, statusCode: status);

bool TryAuthenticate(HttpContext ctx, out IResult? failure)
{
    var provided = ctx.Request.Headers["X-PayFlow-Key"].ToString();

    if (string.IsNullOrWhiteSpace(provided))
    {
        failure = Error(401, "invalid_api_key", "X-PayFlow-Key header is required");
        return false;
    }

    if (provided.StartsWith("pf_live_", StringComparison.Ordinal))
    {
        failure = Error(401, "environment_mismatch",
            "A production key was presented to a sandbox endpoint");
        return false;
    }

    if (provided != apiKey)
    {
        failure = Error(401, "invalid_api_key", "The supplied API key is not valid");
        return false;
    }

    failure = null;
    return true;
}

string? ReadString(JsonElement root, string name) =>
    root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
        ? el.GetString()
        : null;

// ------------------------------------------------------- rate limiting (B5)

app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/v2") && store.IsRateLimited())
    {
        // Retry-After is sent but documented nowhere.
        ctx.Response.Headers["Retry-After"] = "2";
        await Error(429, "rate_limited", "Too many requests").ExecuteAsync(ctx);
        return;
    }

    await next();
});

// ------------------------------------------------------------------ health

app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    service = "payflow-sandbox",
    apiVersion = "2.4.1",
    serverTime = PaymentProjection.Iso(DateTimeOffset.UtcNow)
}, json));

// -------------------------------------------------------- POST /v2/payments

app.MapPost("/v2/payments", async (HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, out var authFailure)) return authFailure!;

    using var reader = new StreamReader(ctx.Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    JsonElement root;
    try
    {
        root = JsonDocument.Parse(rawBody).RootElement;
    }
    catch (JsonException)
    {
        return Error(400, "malformed_request", "Request body is not valid JSON");
    }

    // --- Idempotency-Key (divergence D1) --------------------------------
    var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].ToString();
    var hasIdempotencyKey = !string.IsNullOrWhiteSpace(idempotencyKey);

    if (hasIdempotencyKey)
    {
        var outcome = store.CheckIdempotency(idempotencyKey, rawBody, out var existingId);

        if (outcome == PaymentStore.IdempotencyResult.Conflict)
            return Error(409, "idempotency_key_reuse",
                "This Idempotency-Key was already used with a different request body");

        if (outcome == PaymentStore.IdempotencyResult.Replay)
        {
            var original = store.Get(existingId!)!;
            ctx.Response.Headers["Idempotency-Replayed"] = "true";

            var replayRedirect = original.Status == PaymentStatus.Pending3ds
                ? $"http://localhost:8080/3ds/challenge/{original.PaymentId}"
                : null;

            return Results.Json(PaymentProjection.ForCreate(original, replayRedirect), json,
                statusCode: 201);
        }
    }

    // --- validation ------------------------------------------------------
    if (!root.TryGetProperty("amount", out var amountEl))
        return Error(400, "missing_field", "amount is required");

    if (amountEl.ValueKind != JsonValueKind.Number || !amountEl.TryGetInt64(out var amount))
        return Error(422, "invalid_amount_format",
            "amount must be an integer in minor units");

    if (amount <= 0)
        return Error(422, "invalid_amount_format", "amount must be greater than zero");

    var currency = ReadString(root, "currency");
    if (string.IsNullOrWhiteSpace(currency))
        return Error(400, "missing_field", "currency is required");

    if (string.IsNullOrWhiteSpace(ReadString(root, "returnUrl")))
        return Error(400, "missing_field", "returnUrl is required");

    if (!root.TryGetProperty("card", out var cardEl) || cardEl.ValueKind != JsonValueKind.Object)
        return Error(400, "missing_field", "card is required");

    var pan = ReadString(cardEl, "number");
    if (string.IsNullOrWhiteSpace(pan))
        return Error(400, "missing_field", "card.number is required");

    // Planted defect A3: only the post-v2.3.0 name is read. `merchantReference` is accepted
    // by the parser and silently dropped, so the payment carries a null reference forever.
    var merchantOrderRef = ReadString(root, "merchantOrderRef");

    var outcomeForCard = CardBehaviour.Resolve(pan);

    if (outcomeForCard.Kind == "acquirer_unavailable")
        return Error(503, "acquirer_unavailable", "The acquirer did not respond");

    var payment = new Payment
    {
        PaymentId = Ids.New("pay_"),
        Amount = amount,
        Currency = currency!.ToUpperInvariant(),
        MerchantOrderRef = merchantOrderRef,
        ReturnUrl = ReadString(root, "returnUrl"),
        PendingOutcome = outcomeForCard.Requires3ds ? outcomeForCard : null
    };

    string? redirectUrl = null;

    if (outcomeForCard.Requires3ds)
    {
        payment.Status = PaymentStatus.Pending3ds;
        redirectUrl = $"http://localhost:8080/3ds/challenge/{payment.PaymentId}";
        // No webhook yet — nothing has been decided.
    }
    else if (outcomeForCard.Kind == "approve")
    {
        payment.Status = PaymentStatus.Authorized;
        webhooks.Enqueue(EventType.Authorized, payment, AuthorizedWebhookDelayMs);
    }
    else
    {
        payment.Status = PaymentStatus.Declined;
        payment.DeclineReason = outcomeForCard.DeclineReason;
        webhooks.Enqueue(EventType.Declined, payment, AuthorizedWebhookDelayMs);
    }

    store.Add(payment);

    if (hasIdempotencyKey)
        store.RecordIdempotency(idempotencyKey, rawBody, payment.PaymentId);

    // Planted race C1: the webhook above is already in flight while we sit here.
    await Task.Delay(CreateResponseDelayMs);

    return Results.Json(PaymentProjection.ForCreate(payment, redirectUrl), json, statusCode: 201);
});

// --------------------------------------------------- GET /v2/payments/{id}

app.MapGet("/v2/payments/{paymentId}", (HttpContext ctx, string paymentId) =>
{
    if (!TryAuthenticate(ctx, out var authFailure)) return authFailure!;

    var payment = store.Get(paymentId);
    if (payment is null)
        return Error(404, "payment_not_found", $"No payment with id {paymentId}");

    var redirectUrl = payment.Status == PaymentStatus.Pending3ds
        ? $"http://localhost:8080/3ds/challenge/{payment.PaymentId}"
        : null;

    return Results.Json(PaymentProjection.ForGet(payment, redirectUrl), json);
});

// ------------------------------------------ POST /v2/payments/{id}/captures

app.MapPost("/v2/payments/{paymentId}/captures", async (HttpContext ctx, string paymentId) =>
{
    if (!TryAuthenticate(ctx, out var authFailure)) return authFailure!;

    var payment = store.Get(paymentId);
    if (payment is null)
        return Error(404, "payment_not_found", $"No payment with id {paymentId}");

    using var reader = new StreamReader(ctx.Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    JsonElement root;
    try { root = JsonDocument.Parse(rawBody).RootElement; }
    catch (JsonException) { return Error(400, "malformed_request", "Request body is not valid JSON"); }

    if (!root.TryGetProperty("amount", out var amountEl))
        return Error(400, "missing_field", "amount is required");

    if (amountEl.ValueKind != JsonValueKind.Number || !amountEl.TryGetInt64(out var amount))
        return Error(422, "invalid_amount_format", "amount must be an integer in minor units");

    if (payment.Status is not (PaymentStatus.Authorized or PaymentStatus.Captured))
        return Error(409, "payment_not_captured",
            $"Payment is {payment.Status} and cannot be captured");

    // Planted C3: multiple partial captures are allowed up to the authorised total. Undocumented.
    if (payment.CapturedAmount + amount > payment.Amount)
        return Error(422, "capture_exceeds_authorized",
            $"Capturing {amount} would exceed the authorised amount of {payment.Amount}");

    var isFirstCapture = payment.Captures.Count == 0;

    var capture = new Capture(Ids.New("cap_"), amount, DateTimeOffset.UtcNow);
    payment.Captures.Add(capture);
    payment.CapturedAmount += amount;
    payment.Status = PaymentStatus.Captured;
    payment.UpdatedAt = DateTimeOffset.UtcNow;

    webhooks.Enqueue(EventType.Captured, payment,
        isFirstCapture ? FirstCaptureWebhookDelayMs : LaterCaptureWebhookDelayMs);

    return Results.Json(new Dictionary<string, object?>
    {
        ["captureId"] = capture.CaptureId,
        ["paymentId"] = payment.PaymentId,
        ["amount"] = capture.Amount,
        ["status"] = PaymentStatus.Captured,
        ["createdAt"] = PaymentProjection.Iso(capture.CreatedAt)
    }, json, statusCode: 201);
});

// ------------------------------------------- POST /v2/payments/{id}/refunds

app.MapPost("/v2/payments/{paymentId}/refunds", async (HttpContext ctx, string paymentId) =>
{
    if (!TryAuthenticate(ctx, out var authFailure)) return authFailure!;

    var payment = store.Get(paymentId);
    if (payment is null)
        return Error(404, "payment_not_found", $"No payment with id {paymentId}");

    using var reader = new StreamReader(ctx.Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    JsonElement root;
    try { root = JsonDocument.Parse(rawBody).RootElement; }
    catch (JsonException) { return Error(400, "malformed_request", "Request body is not valid JSON"); }

    if (!root.TryGetProperty("amount", out var amountEl))
        return Error(400, "missing_field", "amount is required");

    if (amountEl.ValueKind != JsonValueKind.Number || !amountEl.TryGetInt64(out var amount))
        return Error(422, "invalid_amount_format", "amount must be an integer in minor units");

    // Planted C4: there is no void/cancel endpoint anywhere in v2, so an authorisation that
    // must be cancelled before capture has no supported path at all.
    if (payment.CapturedAmount == 0)
        return Error(409, "payment_not_captured",
            "Only captured payments can be refunded");

    if (payment.RefundedAmount + amount > payment.CapturedAmount)
        return Error(422, "refund_exceeds_captured",
            $"Refunding {amount} would exceed the captured amount of {payment.CapturedAmount}");

    var refund = new Refund(Ids.New("ref_"), amount, ReadString(root, "reason"), DateTimeOffset.UtcNow);
    payment.Refunds.Add(refund);
    payment.RefundedAmount += amount;

    if (payment.RefundedAmount == payment.CapturedAmount)
        payment.Status = PaymentStatus.Refunded;

    payment.UpdatedAt = DateTimeOffset.UtcNow;

    webhooks.Enqueue(EventType.Refunded, payment, RefundWebhookDelayMs);

    return Results.Json(new Dictionary<string, object?>
    {
        ["refundId"] = refund.RefundId,
        ["paymentId"] = payment.PaymentId,
        ["amount"] = refund.Amount,
        ["status"] = PaymentStatus.Refunded,
        ["createdAt"] = PaymentProjection.Iso(refund.CreatedAt)
    }, json, statusCode: 201);
});

// ------------------------------------------------ webhook endpoint registry

app.MapPost("/v2/webhook-endpoints", async (HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, out var authFailure)) return authFailure!;

    using var reader = new StreamReader(ctx.Request.Body);
    var rawBody = await reader.ReadToEndAsync();

    JsonElement root;
    try { root = JsonDocument.Parse(rawBody).RootElement; }
    catch (JsonException) { return Error(400, "malformed_request", "Request body is not valid JSON"); }

    var url = ReadString(root, "url");
    if (string.IsNullOrWhiteSpace(url))
        return Error(400, "missing_field", "url is required");

    store.RegisterEndpoint(url);
    return Results.Json(new { url, registered = true }, json, statusCode: 201);
});

app.MapGet("/v2/webhook-endpoints", (HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, out var authFailure)) return authFailure!;

    return Results.Json(store.Endpoints.Select(e => new
    {
        url = e.Url,
        registeredAt = PaymentProjection.Iso(e.RegisteredAt)
    }), json);
});

app.MapDelete("/v2/webhook-endpoints", (HttpContext ctx) =>
{
    if (!TryAuthenticate(ctx, out var authFailure)) return authFailure!;

    store.ClearEndpoints();
    return Results.NoContent();
});

// ------------------------------------------------------- 3-D Secure sim

ThreeDs.Map(app, store, webhooks, AuthorizedWebhookDelayMs);

// ------------------------------------------------------ sandbox inspection

app.MapGet("/__sandbox/deliveries", () => Results.Json(
    webhooks.RecentDeliveries
        .OrderByDescending(d => d.AttemptedAt)
        .Select(d => new
        {
            attemptedAt = PaymentProjection.Iso(d.AttemptedAt),
            eventId = d.EventId,
            eventType = d.EventType,
            url = d.Url,
            attempt = d.Attempt,
            duplicate = d.IsDuplicate,
            responseStatus = d.ResponseStatus,
            error = d.Error
        }), json));

app.MapGet("/__sandbox/payments", () => Results.Json(
    store.All().Select(p => PaymentProjection.ForGet(p, null)), json));

app.Run();
