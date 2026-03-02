using System;
using System.IO;
using System.Linq;
using Asp.Versioning;
using HealthChecks.UI.Client;
using HotelListing.Api.Application.Interfaces;
using HotelListing.Api.Application.MappingProfiles;
using HotelListing.Api.Application.Services;
using HotelListing.Api.Common.Models.Config;
using HotelListing.Api.Domain;
using HotelListing.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Swashbuckle.AspNetCore.Filters;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

// logging logic
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext() // enrich logs with context information
    .WriteTo.Console() // log to console
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day) // log to file with daily rolling
    .CreateBootstrapLogger(); // create a bootstrap logger for early logging
try
{
    Log.Information("Starting Hotel Listing API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging  --- go to appsettings.development for more info
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add services to the IoC container.
    var connectionString = builder.Configuration.GetConnectionString("HotelListingDbConnectionString");

    builder.Services.AddDbContextPool<HotelListingDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.CommandTimeout(30);  // works for just sqlserver
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging(); // to see parameterized queries on logs
            options.EnableDetailedErrors(); // 
        }

    }, poolSize: 128); // poolSize moved here, outside UseSqlServer

    builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<HotelListingDbContext>();

    builder.Services.AddHttpContextAccessor();
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings")); // bind JwtSettings section
    var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
    if (string.IsNullOrWhiteSpace(jwtSettings.Key))
    {
        Log.Fatal("JwtSettings: Key is not configured.");
        throw new InvalidOperationException("JwtSettings: Key is not configured. ");
    }
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Key ?? string.Empty)),
            ClockSkew = TimeSpan.Zero // default is 5 minutes
        };
    });

    builder.Services.AddAuthorization();

    builder.Services.AddScoped<ICountriesService, CountriesService>();
    builder.Services.AddScoped<IHotelService, HotelService>();
    builder.Services.AddScoped<IUsersService, UsersService>();
    builder.Services.AddScoped<IBookingService, BookingService>();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();


    builder.Services.AddAutoMapper(cfg => { }, typeof(HotelMappingProfile));
    builder.Services.AddControllers()
        .AddNewtonsoftJson()
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    // builder.Services.AddMemoryCache(); // in-memory caching
    builder.Services.AddOutputCache();

    // Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("fixed", opt =>
        {
            opt.PermitLimit = 1;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
            }

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                Error = "Too many requests",
                Message = "You have exceeded the allowed number of requests. Please try again later.",
                retryAfter = retryAfter.TotalSeconds
            }, cancellationToken: cancellationToken);
        };
    });

    // Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["api"]) // basic liveness check
        .AddDbContextCheck<HotelListingDbContext>(                      // database connectivity check
        name: "HotelListingDbContext-check",
        failureStatus: HealthStatus.Unhealthy,                         // mark unhealthy if check fails
        tags: ["db", "sql"]);                                          // tags for filtering

    builder.Services.AddHealthChecksUI(setup =>
    {
        setup.SetEvaluationTimeInSeconds(10); //check health every 10 seconds
        setup.MaximumHistoryEntriesPerEndpoint(50); // keep history for 50 entries
        setup.AddHealthCheckEndpoint("Hotel Listing API", "/healthz"); //map health check endpoint
    })
    .AddInMemoryStorage(); // use in-memory storage for health check results

    // API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0); // default to v1.0
        options.AssumeDefaultVersionWhenUnspecified = true; // assume default if no version specified
        options.ReportApiVersions = true; // add API versions to response headers
        options.ApiVersionReader = new UrlSegmentApiVersionReader(); // read version from URL segment
    })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV"; // format for API version group names  i.e. v1, v1.1
            options.SubstituteApiVersionInUrl = true; // substitute version in URL
        });

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        // API Information for Swagger UI
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "Hotel Listing API",
            Description = "An API for managing hotels, countries, and bookings",
            Contact = new OpenApiContact
            {
                Name = "Support Team",
                Email = "adeoyelukman08@hotellisting.com",
            },
            License = new OpenApiLicense
            {
                Name = "MIT License",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        });

        // Include XML comments for better documentation (make sure to generate XML docs in project settings)
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Enable annotations for better documentation (e.g., [SwaggerOperation], [SwaggerResponse])
        options.EnableAnnotations();

        // JWT Bearer authentication support in Swagger UI
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        // Add security requirement to include JWT token in Swagger UI
        // This is commented out because we're using the SecurityRequirementsOperationFilter instead
        // which applies the requirement only to endpoints with [Authorize] attribute
        //options.AddSecurityRequirement(new OpenApiSecurityRequirement
        //{
        //    {
        //        new OpenApiSecurityScheme
        //        {
        //            Reference = new OpenApiReference
        //            {
        //                Type = ReferenceType.SecurityScheme,
        //                Id = "Bearer"
        //            }
        //        },
        //        new string[] {}
        //    }

        //});

        // Add operation filters for example
        options.ExampleFilters();

        // Custom operation filter for handling multiple auth schemes
        options.OperationFilter<HotelListing.Api.Filters.SecurityRequirementsOperationFilter>();

        // Order actions by method
        options.OrderActionsBy((apiDesc) => $"{apiDesc.HttpMethod}_{apiDesc.RelativePath}");
    });

    builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>(); // register example providers for Swagger

    var app = builder.Build();

    app.UseExceptionHandler(); // global exception handler middleware

    app.UseSerilogRequestLogging(options => {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";

        // customize log level based on status code
        options.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                ? LogEventLevel.Warning
                : LogEventLevel.Information;

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>  // add custom properties to log context
        {
            diagnosticContext.Set("UserName", httpContext.User?.Identity?.Name ?? "anonymous");
            diagnosticContext.Set("RemoteIP",
              httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value ?? "unknown");
            }
        };

    }); // log HTTP requests

    app.MapGroup("api/defaltauth").MapIdentityApi<ApplicationUser>();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Listing API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Hotel Listing API Documentation";
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
            options.EnableValidator();
        });
    }

    app.UseHttpsRedirection();

    // Health Checks endpoint
    //app.MapHealthChecks("/healthz", new HealthCheckOptions // customize health check response
    //{
    //    ResponseWriter = async (context, report) =>
    //    {
    //        context.Response.ContentType = "application/json";

    //        var response = new
    //        {
    //            status = report.Status.ToString(),              // overall status
    //            checks = report.Entries.Select(entry => new     // individual check details
    //            {
    //                name = entry.Key,                           // name of the health check
    //                status = entry.Value.Status.ToString(),     // status of the check
    //                description = entry.Value.Description,
    //                duration = entry.Value.Duration.TotalMilliseconds, // time taken for the check
    //                exception = entry.Value.Exception?.Message, // exception message if any
    //                data = entry.Value.Data                     // additional data from the check  

    //            }),
    //            totalDuration = report.TotalDuration.TotalMilliseconds // total time taken
    //        }; 
    //        await context.Response.WriteAsync(JsonSerializer.Serialize(response, 
    //            new JsonSerializerOptions
    //            {
    //                WriteIndented = true                        // pretty print for readability
    //            }));
    //    }
    //});

    app.MapHealthChecks("/healthz", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    // Liveness probe endpoint - always returns healthy
    app.MapHealthChecks("/readyz/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    // Readiness probe endpoint - checks database connectivity
    app.MapHealthChecks("healthz/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("db") // only include checks with "db" tag
    });

    // map health checks UI endpoint
    app.MapHealthChecksUI(options =>
    {
        options.UIPath = "/healthchecks-ui"; // endpoint for health checks UI
        options.ApiPath = "/healthchecks-api"; // endpoint for health checks UI API
    });
    app.UseRateLimiter(); // enable rate limiting middleware

    app.UseAuthentication();

    app.UseAuthorization();

    app.UseOutputCache();

    app.MapControllers();

    Log.Information("Hotel Listing API started successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush(); // ensure all logs are flushed before application exit
}