using Balenthiran.Snipit.Abstractions.Services;
using Balenthiran.Snipit.Services;
using Balenthiran.Snipit.Services.Cutting;
using Balenthiran.Snipit.Services.Infrastructure;
using Balenthiran.Snipit.Services.Preview;
using Balenthiran.Snipit.Services.Transcription;
using Balenthiran.Snipit.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Balenthiran.Snipit.WebApi;

public static class ServiceRegistration
{
    public static void AddBackendServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("[WARNING] No database connection string configured — database features are disabled.");
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Balenthiran.Snipit.Database")));
        }

        services.AddAutoMapper(cfg => cfg.AddMaps(AppDomain.CurrentDomain.GetAssemblies()));
        services.AddScoped<IStatusService, StatusService>();

        // Scratch file storage. There is no volume behind this (#22) — it is the pod's own
        // ephemeral disk, and every file written to it is deleted by the job or response it
        // belongs to. RootPath is left unset in the cluster so it falls back to the temp path.
        services.Configure<FileStorageOptions>(configuration.GetSection("FileStorage"));
        services.AddSingleton<IFileStorageService, LocalDiskFileStorageService>();

        // Background job queue (in-process, single worker — see docs/specs for rationale)
        services.AddSingleton<IBackgroundJobQueue, BackgroundJobQueue>();
        services.AddHostedService<QueuedHostedService>();

        // Public-preview limits. snip-it is deliberately open — no sign-in (#13) — so these are
        // the only bound on what an unattended day can cost. Singleton because the upstream
        // cooldown has to outlive the request that discovered it.
        services.Configure<PreviewOptions>(configuration.GetSection(PreviewOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPreviewQuotaService, PreviewQuotaService>();

        // Transcription pipeline
        services.Configure<GroqOptions>(options =>
        {
            configuration.GetSection(GroqOptions.SectionName).Bind(options);
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty;
            }
        });
        services.AddHttpClient<IGroqTranscriptionClient, GroqTranscriptionClient>();
        services.AddSingleton<IFfmpegAudioExtractionArgumentsBuilder, FfmpegAudioExtractionArgumentsBuilder>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddScoped<IAudioExtractionService, AudioExtractionService>();
        services.AddScoped<ITranscriptionService, TranscriptionService>();
        services.AddScoped<ITranscriptionJobProcessor, TranscriptionJobProcessor>();

        // Cutting pipeline
        services.AddSingleton<IFfmpegCutArgumentsBuilder, FfmpegCutArgumentsBuilder>();
        services.AddSingleton<IKeepRangeCalculator, KeepRangeCalculator>();
        services.AddScoped<IVideoCutService, VideoCutService>();
        services.AddScoped<ICutService, CutService>();
        services.AddScoped<ICutJobProcessor, CutJobProcessor>();
    }
}
