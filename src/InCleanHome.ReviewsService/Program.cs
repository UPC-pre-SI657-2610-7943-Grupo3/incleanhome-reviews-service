using InCleanHome.ReviewsService.Application.Internal.CommandServices;
using InCleanHome.ReviewsService.Application.Internal.QueryServices;
using InCleanHome.ReviewsService.Configuration;
using InCleanHome.ReviewsService.Discovery;
using InCleanHome.ReviewsService.Domain.Repositories;
using InCleanHome.ReviewsService.Domain.Services;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.BookingService;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.IamService;
using InCleanHome.ReviewsService.Infrastructure.ExternalServices.ProfileService;
using InCleanHome.ReviewsService.Infrastructure.Messaging.Consumers;
using InCleanHome.ReviewsService.Infrastructure.Persistence;
using InCleanHome.ReviewsService.Infrastructure.Persistence.Repositories;
using InCleanHome.ReviewsService.Infrastructure.Pipeline;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Information().CreateLogger();

try
{
    Log.Information("Starting InCleanHome Reviews Service");
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var consulAddress = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? "http://consul:8500";
    var serviceName   = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "reviews-service";
    var serviceHost   = Environment.GetEnvironmentVariable("SERVICE_HOST") ?? serviceName;
    var servicePort   = int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var p) ? p : 5006;

    var dbConnection = Environment.GetEnvironmentVariable("REVIEWS_DB_CONNECTION")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("REVIEWS_DB_CONNECTION env var is required.");

    var rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL") ?? string.Empty;
    var rabbitMqEnabled = !string.IsNullOrWhiteSpace(rabbitMqUrl)
                         && !rabbitMqUrl.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

    var loadedFromConsul = await ConsulConfigurationLoader.LoadFromConsulAsync(
        builder.Configuration, consulAddress, serviceName);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "InCleanHome Reviews Service", Version = "v1",
            Description = "Reviews & Evaluation — Review + Report + SuspensionAppeal"
        });
        opts.EnableAnnotations();
        opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Example: 'Bearer eyJhbGciOi...'",
            Name = "Authorization", In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey, Scheme = "Bearer"
        });
        opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
        });
    });

    builder.Services.AddDbContext<ReviewsDbContext>(opts => opts.UseNpgsql(dbConnection));

    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
    builder.Services.AddScoped<IReportRepository, ReportRepository>();
    builder.Services.AddScoped<ISuspensionAppealRepository, SuspensionAppealRepository>();
    builder.Services.AddScoped<IReportAppealRepository, ReportAppealRepository>();

    builder.Services.AddScoped<IReviewCommandService, ReviewCommandService>();
    builder.Services.AddScoped<IReviewQueryService, ReviewQueryService>();
    builder.Services.AddScoped<IReportCommandService, ReportCommandService>();
    builder.Services.AddScoped<IReportQueryService, ReportQueryService>();
    builder.Services.AddScoped<ISuspensionAppealCommandService, SuspensionAppealCommandService>();
    builder.Services.AddScoped<ISuspensionAppealQueryService, SuspensionAppealQueryService>();
    builder.Services.AddScoped<IReportAppealCommandService, ReportAppealCommandService>();
    builder.Services.AddScoped<IReportAppealQueryService, ReportAppealQueryService>();

    builder.Services.AddHttpClient<IIamServiceClient, IamServiceClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
    builder.Services.AddHttpClient<IBookingServiceClient, BookingServiceClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
    builder.Services.AddHttpClient<IProfileServiceClient, ProfileServiceClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
    builder.Services.AddHttpClient<
        InCleanHome.ReviewsService.Infrastructure.ExternalServices.CommunicationService.ICommunicationServiceClient,
        InCleanHome.ReviewsService.Infrastructure.ExternalServices.CommunicationService.CommunicationServiceClient
    >(c => c.Timeout = TimeSpan.FromSeconds(10));

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<BookingCompletedConsumer>();
        if (rabbitMqEnabled)
            x.UsingRabbitMq((context, cfg) => { cfg.Host(new Uri(rabbitMqUrl)); cfg.ConfigureEndpoints(context); });
        else
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    });

    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:8080" };
    builder.Services.AddCors(opts => opts.AddDefaultPolicy(
        p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    var registrationOptions = new ConsulRegistrationOptions
    {
        ConsulAddress = consulAddress, ServiceName = serviceName,
        ServiceId = $"{serviceName}-{Environment.MachineName}",
        Host = serviceHost, Port = servicePort,
        Tags = new[] { "reviews", "dotnet" },
        HealthCheckUrl = $"http://{serviceHost}:{servicePort}/health"
    };
    builder.Services.AddSingleton(Options.Create(registrationOptions));
    builder.Services.AddHttpClient<ConsulServiceRegistration>(c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddHostedService<ConsulRegistrationHostedService>();

    builder.Services.AddHealthChecks().AddDbContextCheck<ReviewsDbContext>("reviews-db");

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Database schema ensured.");
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = check => check.Name != "masstransit-bus"
    });
    app.MapGet("/", () => Results.Ok(new
    {
        service = serviceName, status = "running",
        configSource = loadedFromConsul ? "consul" : "appsettings.json",
        broker = rabbitMqEnabled ? "configured" : "disabled"
    }));
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Reviews Service v1"); c.RoutePrefix = "swagger"; });
    app.UseJwtAuth();
    app.MapControllers();

    Log.Information("InCleanHome Reviews Service ready on port {Port}", servicePort);
    await app.RunAsync();
}
catch (Exception ex) { Log.Fatal(ex, "Reviews Service terminated unexpectedly"); throw; }
finally { Log.CloseAndFlush(); }
