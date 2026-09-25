# PayFlow API v2 — Endpoint Reference

**Document version:** 2.4.1

All endpoints are relative to the base URL for your environment (see `01-overview.md`).
All requests and responses are `application/json; charset=utf-8`.

**Amount encoding:** amounts are integers in the currency's minor unit.
`minor units = major units × 100`.

---

## POST /v2/payments

Creates a payment and attempts authorisation.

### Request

| Field | Type | Required | Description |
|---|---|---|---|
| `amount` | integer | yes | Amount in minor units |
| `currency` | string | yes | ISO 4217 three-letter code, uppercase |
| `merchantReference` | string | yes | Your own reference for this payment. Max 64 chars. Echoed back on the payment object and on every webhook. |
| `card` | object | yes | Card details — see below |
| `returnUrl` | string | yes | Where PayFlow returns the shopper after a 3-D Secure challenge |
| `shopperEmail` | string | no | Used for risk scoring. Improves approval rates. |
| `captureMode` | string | no | `manual` (default) or `automatic` |

#### `card` object

| Field | Type | Required | Description |
|---|---|---|---|
| `number` | string | yes | PAN, digits only, no spaces |
| `expiryMonth` | integer | yes | 1–12 |
| `expiryYear` | integer | yes | Four digits |
| `cvv` | string | yes | 3 or 4 digits |
| `holderName` | string | no | As printed on the card |

### Example request

```json
POST /v2/payments
X-PayFlow-Key: pf_test_...
Content-Type: application/json

{
  "amount": 19.99,
  "currency": "EUR",
  "merchantReference": "ORDER-10045",
  "card": {
    "number": "4242424242424242",
    "expiryMonth": 3,
    "expiryYear": 2030,
    "cvv": "737",
    "holderName": "J. Horvat"
  },
  "returnUrl": "https://shop.example/payment/return",
  "shopperEmail": "jhorvat@example.com"
}
```

### Response — 201 Created

| Field | Type | Description |
|---|---|---|
| `paymentId` | string | PayFlow identifier, prefix `pay_` |
| `status` | string | See status values below |
| `amount` | integer | Echoed |
| `currency` | string | Echoed |
| `merchantReference` | string | Echoed |
| `createdAt` | integer | Unix epoch seconds |
| `redirectUrl` | string | Present only when `status` is `PENDING_3DS` |
| `declineReason` | string | Present only when `status` is `DECLINED` |

```json
{
  "paymentId": "pay_8c41f0d2a7b94e6",
  "status": "AUTHORIZED",
  "amount": 1999,
  "currency": "EUR",
  "merchantReference": "ORDER-10045",
  "createdAt": "2026-03-14T09:12:33Z"
}
```

With a 3-D Secure challenge:

```json
{
  "paymentId": "pay_8c41f0d2a7b94e6",
  "status": "PENDING_3DS",
  "amount": 1999,
  "currency": "EUR",
  "merchantReference": "ORDER-10045",
  "createdAt": "2026-03-14T09:12:33Z",
  "redirectUrl": "http://localhost:8080/3ds/challenge/pay_8c41f0d2a7b94e6"
}
```

Redirect the shopper's browser to `redirectUrl`. When the challenge completes, PayFlow redirects
the shopper back to the `returnUrl` you supplied and continues processing the payment. The
authorisation result is delivered by webhook.

### Status values

| Value | Meaning |
|---|---|
| `CREATED` | Payment record exists, processing not finished |
| `PENDING_3DS` | Waiting for the shopper to complete the 3-D Secure challenge |
| `AUTHORISED` | Funds reserved on the shopper's card |
| `DECLINED` | The issuer refused the authorisation |
| `CAPTURED` | Funds captured |
| `REFUNDED` | Funds returned to the shopper |
| `EXPIRED` | The authorisation is no longer valid |

---

> ### 💡 Logging tip
>
> When you are first bringing the integration up, log the full request object. It saves a lot of
> time when our support team is helping you debug.
>
> ```csharp
> _logger.LogInformation("Creating payment: {@Request}", request);
> var response = await _http.PostAsJsonAsync("/v2/payments", request);
> _logger.LogInformation("PayFlow responded: {Status}", response.StatusCode);
> ```

---

## GET /v2/payments/{paymentId}

Returns the current state of a payment.

### Response — 200 OK

Same shape as the create response, plus:

| Field | Type | Description |
|---|---|---|
| `capturedAmount` | integer | Total captured so far, minor units |
| `refundedAmount` | integer | Total refunded so far, minor units |
| `updatedAt` | integer | Unix epoch seconds |

```json
{
  "paymentId": "pay_8c41f0d2a7b94e6",
  "status": "CAPTURED",
  "amount": 1999,
  "currency": "EUR",
  "merchantReference": "ORDER-10045",
  "capturedAmount": 1999,
  "refundedAmount": 0,
  "createdAt": "2026-03-14T09:12:33Z",
  "updatedAt": "2026-03-14T09:41:02Z"
}
```

Returns `404 payment_not_found` for an unknown `paymentId`.

---

## POST /v2/payments/{paymentId}/captures

Captures an authorised payment.

### Request

| Field | Type | Required | Description |
|---|---|---|---|
| `amount` | integer | yes | Amount to capture, minor units |

```json
{
  "amount": 1999
}
```

### Response — 201 Created

| Field | Type | Description |
|---|---|---|
| `captureId` | string | Prefix `cap_` |
| `paymentId` | string | |
| `amount` | integer | |
| `status` | string | `CAPTURED` |
| `createdAt` | integer | Unix epoch seconds |

```json
{
  "captureId": "cap_3b7e9f14c2a0d85",
  "paymentId": "pay_8c41f0d2a7b94e6",
  "amount": 1999,
  "status": "CAPTURED",
  "createdAt": "2026-03-14T09:41:02Z"
}
```

A `payment.captured` webhook follows.

---

## POST /v2/payments/{paymentId}/refunds

Refunds a payment.

### Request

| Field | Type | Required | Description |
|---|---|---|---|
| `amount` | integer | yes | Amount to refund, minor units |
| `reason` | string | no | Free text, max 128 chars. Shown in the PayFlow dashboard. |

```json
{
  "amount": 1999,
  "reason": "Customer returned item"
}
```

### Response — 201 Created

| Field | Type | Description |
|---|---|---|
| `refundId` | string | Prefix `ref_` |
| `paymentId` | string | |
| `amount` | integer | |
| `status` | string | `REFUNDED` |
| `createdAt` | integer | Unix epoch seconds |

A `payment.refunded` webhook follows.

Refunds settle on the shopper's card within 3–5 business days depending on the issuer.

---

## Request headers

| Header | Required | Description |
|---|---|---|
| `X-PayFlow-Key` | yes | Your environment-scoped API key |
| `Content-Type` | yes | `application/json` |
| `X-PayFlow-Trace-Id` | no | Your own correlation id. Echoed in our logs; quote it when contacting support. |

---

## Response headers

| Header | Description |
|---|---|
| `X-PayFlow-Request-Id` | PayFlow's identifier for this request. Always quote it in support tickets. |

---

*Questions about anything on this page: pp@playnirvana.com*
