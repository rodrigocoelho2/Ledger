using Ledger.Website;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLedgerWebsite(builder.Configuration);

var app = builder.Build();
app.UseLedgerWebsite();
await app.RunAsync();
