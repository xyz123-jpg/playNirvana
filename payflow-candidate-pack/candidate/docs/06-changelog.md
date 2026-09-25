# PayFlow API v2 — Changelog

Newest first. Breaking changes are marked **[BREAKING]**.

---

## v2.4.1 — 2026-02-28

- Documentation pack reorganised into the current six documents.
- `X-PayFlow-Request-Id` now returned on error responses as well as success responses.

## v2.4.0 — 2026-01-19

- Added `shopperEmail` to `POST /v2/payments` for risk scoring.
- Added `X-PayFlow-Trace-Id` request header for customer-side correlation.
- `GET /v2/payments/{id}` now returns `capturedAmount` and `refundedAmount`.

## v2.3.2 — 2025-12-02

- Fixed an issue where `payment.refunded` webhooks could omit `refundedAmount`.
- Improved 429 handling under burst load.

## v2.3.0 — 2025-11-11

- **[BREAKING]** The `merchantReference` field is renamed to `merchantOrderRef` across all
  endpoints and all webhook payloads. The old name is no longer read. Requests sending the old
  name will be accepted but the reference will not be stored.
- Added `captureMode` to `POST /v2/payments`.
- Refund `reason` increased from 64 to 128 characters.

## v2.2.0 — 2025-09-30

- Added support for the `Idempotency-Key` request header.
- `payment.failed` webhook introduced to distinguish technical failures from issuer declines.

## v2.1.0 — 2025-08-04

- Partial captures supported on `POST /v2/payments/{id}/captures`.
- `EXPIRED` status introduced.

## v2.0.0 — 2025-06-16

- PayFlow API v2 general availability.
- **[BREAKING]** All amounts move from decimal to minor units.
- **[BREAKING]** Webhook signature scheme moves from `X-PayFlow-Hmac` (body only) to
  `X-PayFlow-Signature` (timestamp + body).
- v1 deprecated. v1 sunset date: 2026-06-30.

---

## Deprecation notices

| Feature | Deprecated | Sunset |
|---|---|---|
| API v1 | 2025-06-16 | 2026-06-30 |
| `X-PayFlow-Hmac` webhook header | 2025-06-16 | 2026-06-30 |

---

*Questions: pp@playnirvana.com*
