using Ledger.WebApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLedger(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseLedger();
await app.RunAsync();
