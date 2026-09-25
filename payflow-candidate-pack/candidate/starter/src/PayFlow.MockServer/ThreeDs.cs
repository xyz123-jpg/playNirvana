using System.Net;

namespace PayFlow.MockServer;

/// <summary>
/// Stand-in for the issuer's 3-D Secure challenge screen.
///
/// Note what is NOT here: there is no path that reports abandonment. If the shopper never
/// presses a button, <see cref="ThreeDsExpirySweeper"/> flips the payment to EXPIRED after
/// 90 seconds and no webhook is ever sent (planted defect C5).
/// </summary>
public static class ThreeDs
{
    public static void Map(
        WebApplication app,
        PaymentStore store,
        WebhookDispatcher webhooks,
        int webhookDelayMs)
    {
        app.MapGet("/3ds/challenge/{paymentId}", (string paymentId) =>
        {
            var payment = store.Get(paymentId);

            if (payment is null)
                return Results.Content(Page("Unknown payment",
                    "<p>No payment with that identifier.</p>"), "text/html");

            if (payment.Status != PaymentStatus.Pending3ds)
                return Results.Content(Page("Challenge unavailable",
                    $"<p>This payment is <strong>{WebUtility.HtmlEncode(payment.Status)}</strong> " +
                    "and is no longer awaiting authentication.</p>"), "text/html");

            var body = $"""
                <p class="amount">{payment.Amount / 100m:0.00} {WebUtility.HtmlEncode(payment.Currency)}</p>
                <p class="ref">{WebUtility.HtmlEncode(payment.PaymentId)}</p>
                <p>Your bank would normally ask you for a one-time code here.</p>
                <form method="post" action="/3ds/challenge/{payment.PaymentId}/complete">
                  <button name="outcome" value="approve" class="ok">Approve</button>
                  <button name="outcome" value="fail" class="no">Fail</button>
                </form>
                <p class="hint">Closing this window without choosing is also a valid test.</p>
                """;

            return Results.Content(Page("3-D Secure", body), "text/html");
        });

        app.MapPost("/3ds/challenge/{paymentId}/complete", async (HttpContext ctx, string paymentId) =>
        {
            var payment = store.Get(paymentId);

            if (payment is null || payment.Status != PaymentStatus.Pending3ds)
                return Results.Content(Page("Challenge unavailable",
                    "<p>This challenge can no longer be completed.</p>"), "text/html");

            var form = await ctx.Request.ReadFormAsync();
            var chose = form["outcome"].ToString();

            var shopperApproved = chose == "approve";
            var cardWouldApprove = payment.PendingOutcome?.Kind == "approve";

            if (shopperApproved && cardWouldApprove)
            {
                payment.Status = PaymentStatus.Authorized;
                payment.UpdatedAt = DateTimeOffset.UtcNow;
                webhooks.Enqueue(EventType.Authorized, payment, webhookDelayMs);
            }
            else
            {
                payment.Status = PaymentStatus.Declined;
                payment.DeclineReason = shopperApproved
                    ? payment.PendingOutcome?.DeclineReason ?? "do_not_honor"
                    : "3ds_authentication_failed";
                payment.UpdatedAt = DateTimeOffset.UtcNow;
                webhooks.Enqueue(EventType.Declined, payment, webhookDelayMs);
            }

            if (!string.IsNullOrWhiteSpace(payment.ReturnUrl))
            {
                var separator = payment.ReturnUrl.Contains('?') ? "&" : "?";
                return Results.Redirect(
                    $"{payment.ReturnUrl}{separator}paymentId={payment.PaymentId}");
            }

            return Results.Content(Page("Done",
                $"<p>Payment is now <strong>{WebUtility.HtmlEncode(payment.Status)}</strong>.</p>" +
                "<p>No returnUrl was supplied, so there is nowhere to send you.</p>"), "text/html");
        });
    }

    private const string Template = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>__TITLE__ — PayFlow Sandbox</title>
          <style>
            :root { color-scheme: light dark; }
            body {
              font-family: ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
              max-width: 26rem; margin: 4rem auto; padding: 0 1rem; line-height: 1.55;
            }
            .card { border: 1px solid #8884; border-radius: 12px; padding: 1.5rem 1.75rem; }
            h1 { font-size: 1.05rem; letter-spacing: .02em; text-transform: uppercase;
                 opacity: .6; margin: 0 0 1rem; }
            .amount { font-size: 2rem; font-weight: 600; margin: .25rem 0; }
            .ref { font-family: ui-monospace, monospace; font-size: .8rem; opacity: .55;
                   margin: 0 0 1.25rem; }
            button { font: inherit; padding: .6rem 1.2rem; border-radius: 8px;
                     border: 1px solid #8886; cursor: pointer; margin-right: .5rem; }
            .ok { background: #1a7f4b; color: #fff; border-color: #1a7f4b; }
            .no { background: transparent; }
            .hint { font-size: .8rem; opacity: .55; margin-top: 1.5rem; }
          </style>
        </head>
        <body>
          <div class="card">
            <h1>PayFlow Sandbox · 3-D Secure</h1>
            __BODY__
          </div>
        </body>
        </html>
        """;

    private static string Page(string title, string body) =>
        Template.Replace("__TITLE__", WebUtility.HtmlEncode(title))
                .Replace("__BODY__", body);
}
