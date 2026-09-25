# PayFlow API v2 — Webhooks

**Document version:** 2.4.1

PayFlow notifies your system of payment state changes by HTTP POST to an endpoint you configure.

---

## 1. Configuring your endpoint

For this sandbox, register your receiver at startup:

```bash
curl -X POST http://localhost:8080/v2/webhook-endpoints \
  -H "X-PayFlow-Key: pf_test_..." \
  -H "Content-Type: application/json" \
  -d '{"url": "http://host.docker.internal:5099/webhooks/payflow"}'
```

In production this is configured once in the PayFlow dashboard.

Your endpoint must:

- be reachable over HTTPS (HTTP is permitted in sandbox only)
- respond within **10 seconds**
- return a `2xx` status to acknowledge receipt

---

## 2. Event types

| Event | Fired when |
|---|---|
| `payment.authorized` | Authorisation succeeded |
| `payment.declined` | The issuer declined the authorisation |
| `payment.captured` | A capture completed |
| `payment.refunded` | A refund completed |
| `payment.failed` | Processing failed for a technical reason |

---

## 3. Event payload

```json
{
  "eventId": "evt_5d9a1c3e78b24f0",
  "eventType": "payment.authorized",
  "occurredAt": "2026-03-14T09:12:34Z",
  "data": {
    "paymentId": "pay_8c41f0d2a7b94e6",
    "status": "AUTHORIZED",
    "amount": 1999,
    "currency": "EUR",
    "merchantOrderRef": "ORDER-10045",
    "capturedAmount": 0,
    "refundedAmount": 0
  }
}
```

| Field | Type | Description |
|---|---|---|
| `eventId` | string | Unique identifier for this event, prefix `evt_` |
| `eventType` | string | See event types above |
| `occurredAt` | string | ISO-8601 UTC timestamp of the state change |
| `data` | object | The payment object as it stands after the change |

---

## 4. Delivery guarantees

Each event is **delivered exactly once, in the order it occurred**. You do not need to handle
duplicates or reordering.

If your endpoint does not return `2xx`, we retry up to **5 times over 24 hours** with exponential
backoff. After the final attempt the event is marked undeliverable and is visible in the
dashboard.

---

## 5. Signature verification

Every webhook carries a signature header:

```http
X-PayFlow-Signature: t=1773478354,v1=8f3c2a91b7e04d6c5a8f1e2b9d7c4a6038e5b1f9c2d7a4e8b6f3c1a9d5e2b7f4
```

| Component | Meaning |
|---|---|
| `t` | Unix timestamp at which the signature was generated |
| `v1` | Lowercase hex HMAC-SHA256 |

The signature is computed as:

```
HMAC-SHA256(key = your webhook secret, message = "{t}.{raw request body}")
```

where **raw request body** is the exact bytes we sent, before any parsing.

Your webhook secret is separate from your API key. Find it in `05-sandbox.md`.

### Verification sample

```csharp
public static bool VerifySignature(WebhookEvent evt, string header, string secret)
{
    var parts = header.Split(',');
    var timestamp = parts[0].Substring(2);
    var expected = parts[1].Substring(3);

    var body = JsonSerializer.Serialize(evt);
    var signedPayload = $"{timestamp}.{body}";

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
    var computed = Convert.ToHexString(hash).ToLowerInvariant();

    return computed == expected;
}
```

---

## 6. Quickstart receiver

The fastest way to get webhooks flowing end to end:

```csharp
app.MapPost("/webhooks/payflow", async (WebhookEvent evt, IOrderService orders) =>
{
    switch (evt.EventType)
    {
        case "payment.authorized":
            await orders.MarkAuthorizedAsync(evt.Data.PaymentId, evt.Data.Amount);
            break;
        case "payment.captured":
            await orders.MarkCapturedAsync(evt.Data.PaymentId, evt.Data.Amount);
            break;
        case "payment.declined":
        case "payment.failed":
            await orders.MarkFailedAsync(evt.Data.PaymentId);
            break;
        case "payment.refunded":
            await orders.MarkRefundedAsync(evt.Data.PaymentId, evt.Data.Amount);
            break;
    }

    return Results.Ok();
});
```

That is enough to see events arriving and to confirm your endpoint registration is correct.

---

## 7. Troubleshooting

**No events arriving.** Check your endpoint is registered
(`GET /v2/webhook-endpoints`) and reachable from the sandbox container. If you are running the
sandbox in Docker and your receiver on the host, use `host.docker.internal` rather than
`localhost`.

**Events arriving but signature check fails.** Confirm you are using the webhook secret and not
the API key — they are different values and this is the most common cause.

**Endpoint timing out.** Do your processing asynchronously. Acknowledge first, work after.

---

*Questions: pp@playnirvana.com*
