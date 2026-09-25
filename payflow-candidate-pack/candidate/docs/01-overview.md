# PayFlow API v2 — Overview

**Document version:** 2.4.1
**Last reviewed:** 2026-02-28

Welcome to PayFlow. This pack covers everything you need to integrate card payments.

| Document | Contents |
|---|---|
| 01-overview.md | Concepts, payment lifecycle, environments (you are here) |
| 02-api-reference.md | Endpoint reference |
| 03-webhooks.md | Event delivery and signature verification |
| 04-errors.md | Error codes and handling |
| 05-sandbox.md | Sandbox credentials and test data |
| 06-changelog.md | API changelog |

---

## 1. Concepts

### Payment

A **payment** is the top-level object. It is created when you take a card from a shopper, and it
moves through a lifecycle from creation to settlement. Every payment has a PayFlow-assigned
`paymentId` (prefix `pay_`) and carries your own reference so you can match it to your order.

### Authorisation and capture

PayFlow uses a two-step model, standard for card acquiring:

- **Authorisation** reserves the funds on the shopper's card. No money moves.
- **Capture** instructs the issuer to actually transfer the funds.

Merchants shipping physical goods typically capture at dispatch. Merchants delivering instantly
usually capture immediately after authorisation.

### 3-D Secure

Most EU card payments require Strong Customer Authentication under PSD2. When PayFlow determines
that a payment needs it, the payment is returned in a pending state along with a URL you must
redirect the shopper to. After the shopper completes (or abandons) the challenge, PayFlow
finishes processing the payment.

### Amounts

**All amounts in the PayFlow API are expressed in the currency's minor unit.** EUR 19.99 is
sent as `1999`. This avoids floating-point rounding errors and is consistent across every
endpoint and every webhook.

---

## 2. Payment lifecycle

```
                    POST /v2/payments
                            |
                            v
                     +--------------+
                     |   CREATED    |
                     +--------------+
                       |          |
           3DS required|          |no 3DS required
                       v          v
              +--------------+   |
              | PENDING_3DS  |   |
              +--------------+   |
                       |          |
       shopper completes|         |
                       v          v
                     +--------------+          +--------------+
                     |  AUTHORIZED  |--------->|   DECLINED   |
                     +--------------+          +--------------+
                            |
        POST /captures      |
                            v
                     +--------------+
                     |   CAPTURED   |
                     +--------------+
                            |
        POST /refunds       |
                            v
                     +--------------+
                     |   REFUNDED   |
                     +--------------+
```

---

## 3. Environments

| Environment | Base URL |
|---|---|
| Sandbox | `https://sandbox.payflow.example/v2` |
| Production | `https://api.payflow.example/v2` |

Sandbox is functionally identical to production apart from the fact that no real money moves and
no real card networks are contacted. Anything that works in sandbox will work in production.

> **Note for this integration:** your sandbox instance is provisioned locally. See
> `05-sandbox.md` for the address to use.

---

## 4. Authentication

Every request must carry your API key in the `X-PayFlow-Key` header.

```http
POST /v2/payments HTTP/1.1
Host: sandbox.payflow.example
X-PayFlow-Key: pf_test_...
Content-Type: application/json
```

Keys are environment-scoped. A sandbox key will not work against production.

---

## 5. Supported currencies

PayFlow v2 supports the following currencies for card acquiring:

`EUR`, `USD`, `GBP`, `CHF`, `SEK`, `NOK`, `DKK`, `PLN`, `CZK`, `HUF`, `RON`, `HRK`, `JPY`

Additional currencies can be enabled on request. Contact your account manager.

---

## 6. Rate limits

The API is rate limited per API key. If you exceed the limit you will receive `429`. Please
implement sensible client-side throttling.

---

## 7. Support

Integration questions go to **pp@playnirvana.com**.

We are happy to clarify anything in this pack — if something reads ambiguously to you, it
probably is, and we would rather fix the document than have you guess. Include the document name
and section in your message and we will get back to you within a few working hours.

Account and commercial questions go to your account manager.
