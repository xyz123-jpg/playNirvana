# Forwarded: PayFlow integration kickoff

> This is the email thread that came with the documentation pack. Forwarded for context.

---

**From:** Payment Integrations <pp@playnirvana.com>
**To:** Integration team
**Date:** Mon, 9 March 2026 08:14
**Subject:** Fwd: PayFlow integration — docs + sandbox

Forwarding the pack from PayFlow. Everything they sent is in `docs/`. Sandbox is provisioned.

---

**From:** Tomas Lindqvist <t.lindqvist@payflow.example>
**To:** Payment Integrations <pp@playnirvana.com>
**Date:** Fri, 6 March 2026 16:52
**Subject:** PayFlow integration — docs + sandbox

Hi,

Great call yesterday. As promised, attached is our v2 integration pack and your sandbox
credentials.

A few notes from my side to save your team some time:

**Getting started.** Point everything at `http://localhost:8080/api/v2` and you should be up in
under an hour. Most of our merchants have the create-payment flow working the same afternoon.

**Idempotency.** Don't worry about this one. Our API is naturally idempotent on payment creation —
we deduplicate internally on the merchant reference, so a retry can't double-charge. It's on the
roadmap to document properly but functionally you're covered.

**Webhook signatures.** The signature verification in section 5 of the webhooks doc is there for
production hardening. For the sandbox I'd skip it entirely and get the flow working first —
it's a common source of wasted time early on and you can always bolt it on later.

**Amounts.** Just send the value as it appears on the order and we'll handle the rest.

**3DS.** The simulator has Approve and Fail buttons. Worth testing both, obviously, but the
happy path is what most teams ship first.

**Timeline.** You mentioned wanting to be live for the June promotion. That's very comfortable —
I'd budget two weeks including UAT. We've had merchants do it in three days.

Anything at all, my team is on pp@playnirvana.com and we usually turn questions
around same day.

Looking forward to working together.

Best,

**Tomas Lindqvist**
Solutions Engineer, Northern Europe
PayFlow
t.lindqvist@payflow.example | +46 8 XXX XX XX

---

*This e-mail and any attachments are confidential and intended solely for the addressee.*
