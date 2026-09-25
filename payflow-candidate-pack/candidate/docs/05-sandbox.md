# PayFlow API v2 — Sandbox Guide

**Document version:** 2.4.1

---

## 1. Your sandbox instance

A dedicated sandbox has been provisioned for this integration and runs locally.

| | |
|---|---|
| **Base URL** | `http://localhost:8080/v2` |
| **API key** | `pf_test_4b81d0f7a2c94e36b5170ac8e93d2f61` |
| **Webhook secret** | `whsec_test_6e2c90b148af4d73a5c1e07b9f36d284` |

All sandbox API keys carry the `pf_test_` prefix. Production keys carry `pf_live_`. If you ever
see a `pf_live_` key outside a production secret store, treat it as compromised and contact us
immediately.

Start it with:

```bash
docker compose up
```

or:

```bash
cd src/PayFlow.MockServer && dotnet run
```

Health check: `GET http://localhost:8080/health`

---

## 2. Quickstart

Create your first payment:

```bash
curl -X POST http://localhost:8080/v2/payments \
  -H "X-PayFlow-Key: pf_test_4b81d0f7a2c94e36b5170ac8e93d2f61" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 1999,
    "currency": "EUR",
    "merchantOrderRef": "ORDER-10045",
    "card": {
      "number": "4111111111111111",
      "expiryMonth": 3,
      "expiryYear": 2030,
      "cvv": "737"
    },
    "returnUrl": "http://localhost:5099/return"
  }'
```

You should see an approved authorisation come back with `"status": "AUTHORIZED"`.

Register a webhook endpoint so you start receiving events:

```bash
curl -X POST http://localhost:8080/v2/webhook-endpoints \
  -H "X-PayFlow-Key: pf_test_4b81d0f7a2c94e36b5170ac8e93d2f61" \
  -H "Content-Type: application/json" \
  -d '{"url": "http://localhost:5099/webhooks/payflow"}'
```

---

## 3. Test cards

Sandbox card behaviour is driven entirely by the card number. Any future expiry date and any
3–4 digit CVV will do.

| Card number | Behaviour |
|---|---|
| `4242 4242 4242 4242` | Authorisation approved |
| `4111 1111 1111 1111` | Declined — `insufficient_funds` |
| `4000 0000 0000 0002` | Declined — `do_not_honor` |
| `4000 0000 0000 0069` | Declined — `card_expired` |
| `4000 0000 0000 0101` | Declined — `invalid_cvv` |
| `4000 0000 0000 0127` | Declined — `suspected_fraud` |
| `4000 0000 0000 3220` | Requires 3-D Secure, then approves |
| `4000 0000 0000 3238` | Requires 3-D Secure, then declines — `3ds_authentication_failed` |
| `4000 0000 0000 9995` | `503 acquirer_unavailable` |
| `5555 5555 5555 4444` | Authorisation approved (Mastercard) |

---

## 4. The 3-D Secure simulator

When a payment returns `PENDING_3DS`, open the `redirectUrl` in a browser. You will get a simple
page with **Approve** and **Fail** buttons standing in for the issuer's challenge screen.

Choosing either one redirects the shopper back to your `returnUrl` and PayFlow continues
processing the payment.

---

## 5. Sandbox limitations

The sandbox does not:

- contact real card networks
- perform real risk scoring (`shopperEmail` is accepted and ignored)
- settle funds, so there is no settlement reporting
- persist data across restarts — everything is in memory

---

## 6. Useful sandbox-only endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health` | Liveness |
| `GET /v2/webhook-endpoints` | List registered endpoints |
| `DELETE /v2/webhook-endpoints` | Clear all registered endpoints |
| `GET /__sandbox/deliveries` | Recent webhook delivery attempts, with response codes |

`/__sandbox/deliveries` is the fastest way to see whether your receiver is acknowledging
correctly.

---

*Questions: pp@playnirvana.com*
