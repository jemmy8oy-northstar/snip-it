using Scalar.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Balenthiran.Snipit.WebApi;
using Balenthiran.Snipit.WebApi.Routes;
using Balenthiran.Snipit.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Kestrel caps a request body at 30 MB and multipart form sections at 128 MB by default — both
// well under the size of the screen recordings this app exists to cut, and both surface as a
// bare 413 with no hint of which limit tripped. The ingress has its own cap (proxy-body-size in
// helm/values.yaml); an upload has to clear all three.
var maxUploadBytes = builder.Configuration.GetValue<long?>("Uploads:MaxBytes") ?? 2L * 1024 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxUploadBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxUploadBytes);

// Enum names instead of ordinals, and strict numbers — see SnipitJsonOptions for why each one
// matters to the generated client. Also drives the build-time OpenAPI document, so the schema
// and the wire format cannot drift apart.
builder.Services.ConfigureHttpJsonOptions(options => SnipitJsonOptions.Configure(options.SerializerOptions));

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

// In the cluster the app is served from a sub-path (balenthiran.co.uk/snipit) because the host
// is shared with the other apps, and the ingress forwards the whole path — so the prefix has to
// come off before routing or every route 404s. Unset locally, where the app owns the root.
var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseHttpsRedirection();

app.MapGroup("/api")
    .MapStatusRoutes()
    .MapTranscriptionRoutes()
    .MapCutRoutes()
    .WithOpenApi();

app.Run();
