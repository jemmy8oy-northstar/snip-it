using Scalar.AspNetCore;
using Balenthiran.Snipit.WebApi;
using Balenthiran.Snipit.WebApi.Routes;
using Balenthiran.Snipit.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBackendServices(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar/v1");
}

// When the build-time OpenAPI generator (GetDocument.Insider) loads the app purely to emit the
// OpenAPI document it runs this top-level code but never serves requests — it must not touch a
// database, or a Debug build would try to migrate against whatever connection string is configured.
var generatingOpenApiDocument =
    System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

if (!generatingOpenApiDocument)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
    if (dbContext is null)
        app.Logger.LogWarning("Skipping database migration — no connection string configured.");
    else
        dbContext.Database.Migrate();
}

app.UseHttpsRedirection();

app.MapGroup("/api")
    .MapStatusRoutes()
    .MapTranscriptionRoutes()
    .MapCutRoutes()
    .WithOpenApi();

app.Run();
