namespace PayFlow.MockServer;

/// <summary>
/// Maps a test PAN to the outcome the sandbox produces.
/// Mirrors the table in docs/05-sandbox.md — note that 4111… DECLINES, which contradicts the
/// quickstart snippet in that same document (planted defect A6).
/// </summary>
public static class CardBehaviour
{
    private static readonly Dictionary<string, CardOutcome> Map = new()
    {
        ["4242424242424242"] = new("approve", null, false),
        ["5555555555554444"] = new("approve", null, false),

        ["4111111111111111"] = new("decline", "insufficient_funds", false),
        ["4000000000000002"] = new("decline", "do_not_honor", false),
        ["4000000000000069"] = new("decline", "card_expired", false),
        ["4000000000000101"] = new("decline", "invalid_cvv", false),
        ["4000000000000127"] = new("decline", "suspected_fraud", false),

        ["4000000000003220"] = new("approve", null, true),
        ["4000000000003238"] = new("decline", "3ds_authentication_failed", true),

        ["4000000000009995"] = new("acquirer_unavailable", null, false)
    };

    public static CardOutcome Resolve(string? pan)
    {
        var digits = new string((pan ?? string.Empty).Where(char.IsDigit).ToArray());
        return Map.TryGetValue(digits, out var outcome)
            ? outcome
            : new CardOutcome("approve", null, false);
    }
}
