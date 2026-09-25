using Integration.Domain;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Placeholder so the project runs out of the box. Replace it.
///
/// We are not looking for coverage. One or two tests that would actually catch a regression in
/// the part of the integration you consider riskiest tells us far more than a suite of
/// assertions on getters.
/// </summary>
public class PlaceholderTests
{
    [Fact]
    public void Payment_starts_in_the_new_state()
    {
        var payment = new Payment
        {
            OrderId = "ORDER-1",
            Amount = 1999,
            Currency = "EUR"
        };

        Assert.Equal(PaymentState.New, payment.State);
    }
}
