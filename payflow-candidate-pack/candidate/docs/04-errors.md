# PayFlow API v2 — Errors

**Document version:** 2.4.1

---

## Error response shape

Every non-2xx response has the same body:

```json
{
  "error": {
    "code": "invalid_amount_format",
    "message": "amount must be an integer in minor units",
    "requestId": "req_c81f4a09e73b2d6"
  }
}
```

| Field | Description |
|---|---|
| `code` | Stable machine-readable identifier. Branch on this, never on `message`. |
| `message` | Human-readable. May change without notice. |
| `requestId` | Quote this in support tickets. |

---

## Error codes

| HTTP | Code | Meaning |
|---|---|---|
| 400 | `malformed_request` | Body is not valid JSON |
| 400 | `missing_field` | A required field was absent |
| 401 | `invalid_api_key` | Key missing, malformed or revoked |
| 401 | `environment_mismatch` | Sandbox key used against production, or vice versa |
| 403 | `currency_not_enabled` | Currency not enabled on your account |
| 404 | `payment_not_found` | No payment with that id |
| 409 | `payment_not_captured` | The operation requires a captured payment |
| 409 | `idempotency_key_reuse` | Key already used with a different request body |
| 422 | `invalid_amount_format` | Amount was not a valid integer |
| 422 | `capture_exceeds_authorized` | Capture total would exceed the authorised amount |
| 422 | `refund_exceeds_captured` | Refund total would exceed the captured amount |
| 429 | `rate_limited` | Too many requests |
| 500 | `internal_error` | Something went wrong on our side |
| 503 | `acquirer_unavailable` | The upstream acquirer is not responding |

---

## Declines are not errors

An issuer decline is a **successful API call**. You will receive `201 Created` with
`"status": "DECLINED"` and a `declineReason`, not a 4xx.

| `declineReason` | Meaning | Retryable? |
|---|---|---|
| `insufficient_funds` | Not enough balance | Only after the shopper acts |
| `card_expired` | Expiry date has passed | No |
| `do_not_honor` | Issuer refused without a specific reason | Sometimes |
| `suspected_fraud` | Issuer risk rules triggered | No — do not retry |
| `invalid_cvv` | CVV did not match | No |
| `3ds_authentication_failed` | Shopper failed the challenge | Shopper may retry |

Treating a decline as a technical failure is the single most common integration mistake we see.
It results in shoppers being shown "something went wrong" when the correct message is "your card
was declined, please try another card".

---

## Retrying

`500`, `503` and `429` may be retried.

`4xx` other than `429` will not succeed on retry — the request itself is the problem.

---

## Webhook delivery

Webhook delivery is attempted **once**. If your endpoint is unavailable at that moment the event
is lost. For this reason we recommend reconciling payment state by polling
`GET /v2/payments/{paymentId}` for any payment whose final state you have not observed.

---

## Idempotency

See `06-changelog.md` for the current state of idempotency support.

---

*Questions: pp@playnirvana.com*
