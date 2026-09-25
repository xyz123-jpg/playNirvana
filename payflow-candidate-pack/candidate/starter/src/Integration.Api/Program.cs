using Integration.Application;
using Integration.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PayFlowOptions>(builder.Configuration.GetSection("PayFlow"));
builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();

builder.Services.AddHttpClient<IPayFlowGateway, PayFlowGateway>(client =>
{
    var options = builder.Configuration.GetSection("PayFlow").Get<PayFlowOptions>()!;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("X-PayFlow-Key", options.ApiKey);
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// -------------------------------------------------------------------------
// Take a card payment for an order.
// -------------------------------------------------------------------------
app.MapPost("/orders/{orderId}/pay", (string orderId) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

// -------------------------------------------------------------------------
// What our Order service asks when it wants the current state of a payment.
// -------------------------------------------------------------------------
app.MapGet("/orders/{orderId}/payment", (string orderId) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

// -------------------------------------------------------------------------
// Where PayFlow delivers events. Register this URL with the sandbox — see
// docs/03-webhooks.md for how.
// -------------------------------------------------------------------------
app.MapPost("/webhooks/payflow", () =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

app.Run();
