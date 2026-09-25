# PayFlow v2 Integration — Take-Home Exercise

Thanks for taking the time. This is built to look like a normal week on the Payment Providers
team: a new PSP, a documentation pack from their solutions engineer, a sandbox, and a deadline.

---

## The situation

We are onboarding a new payment service provider, **PayFlow**, for card payments in the EU.
Their solutions engineer sent over the integration pack in `docs/` and gave us sandbox access.

Your job is to build the first vertical slice so our Order service can take a card payment.

---

## What we are asking for

**Four things, and nothing else is required:**

1. Create a payment against PayFlow
2. Handle the 3-D Secure redirect step
3. Receive and process PayFlow's webhooks
4. Expose the current payment status to our Order service

Those are the three `501` endpoints in `starter/src/Integration.Api/Program.cs`.

**This should take about two hours.** We have deliberately kept it small. We would rather read
two focused hours than eight unfocused ones, and we are not interested in how much of your
weekend you are willing to spend.

### Out of scope — do not build these

- A UI of any kind
- A real database (in-memory is fine)
- Authentication for our own API
- Multi-PSP abstraction. One PSP. No plugin architecture.

---

## If you have more time

These are **genuinely optional**. Not doing them costs you nothing, and we would rather see the
core done well than all of this done thinly. If you do pick something up, we will ask why you
picked that one.

- Capture and refund
- Tests
- Anything else you consider part of doing this properly

---

## What to hand back

A zip or a git bundle containing:

**1. Your code.** C# / .NET. A starter solution is in `starter/` with a Clean Architecture
skeleton matching our house layout — use it or restructure it, your call.

**2. `DECISIONS.md`.**

- What you decided, and why
- What you are unsure about
- What you would do next with more time
- **Where you used AI, and where you overrode it**

We read this before we read the code. If you only have time for one thing, write this.

**3. Any questions you sent to PayFlow support**, and the replies.

---

## AI tools

**Use them.** Claude, Copilot, Cursor, whatever you normally work with. We use them daily and we
are not interested in how you perform with one hand tied behind your back.

Two conditions:

- **Disclose it** in `DECISIONS.md` — roughly what you used and for what.
- **Own the output.** In the debrief we will ask you to explain any line of your submission. "The
  model wrote that" is not an answer we can do anything with.

---

## Asking questions

For this exercise, PayFlow's integration support is reachable at **pp@playnirvana.com**.

The mailbox is monitored during working hours and answered as a PSP's support team would answer.
Ask anything you would ask a real provider.

**Ask early.** Replies take a few working hours, and you have a small budget of working time —
sending a question on your last evening is not going to help you. There is no penalty for
asking and no bonus for guessing.

---

## Time and deadline

**About two hours** of working time on the core. **5 calendar days** to return it.

If you run out of time, stop and write down what you would have done next. A submission that
ends with a clear, honest list of what is missing reads better here than one that quietly leaves
gaps.

---

## Getting started

```bash
cd starter
docker compose up          # starts the PayFlow sandbox on http://localhost:8080
```

or, without Docker:

```bash
cd starter/src/PayFlow.MockServer
dotnet run
```

Sandbox credentials and test cards are in `docs/05-sandbox.md`.
A Postman collection is in `starter/postman/`.

Start with `docs/01-overview.md`.

---

## How we will assess it

We read the code, then spend about seventy minutes with you going through it. In that session we
will put some concrete situations in front of you and ask what your integration does.

We are looking at how you reason when you have to make a call, how well you understand what you
submitted, and the quality of the engineering — structure, error handling, tests.

**We weight the reasoning above the volume of code.** That is not a platitude; it is how the
scoring actually works.

Good luck.
