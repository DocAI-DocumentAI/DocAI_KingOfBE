
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using OpenApiSecurityScheme = NSwag.OpenApiSecurityScheme;
using AI.API.Extensions;
using AI.API.Background;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up!");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog as the logging provider
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new ExpressionTemplate(
            "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
            theme: TemplateTheme.Code)));

    // Add services to the container
    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();

    builder.Services
       .AddDatabase()
       .AddApplicationServices(builder.Configuration)
       .AddHuggingFaceServices(builder.Configuration)
       .AddAutoMapperProfiles()
       .AddBackgroundServices()
       .AddCorsPolicy(builder.Configuration)
       .AddJwtAuthentication(builder.Configuration)
       .AddSemanticKernel()
       .AddOpenAIEmbeddingService(builder.Configuration);

    builder.Services.AddControllers();

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddOpenApiDocument(options =>
    {
        options.Title = "DocAI AI API";
        options.Version = "v1";

        options.AddSecurity("Bearer", new OpenApiSecurityScheme
        {
            Type = OpenApiSecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Name = "Authorization",
            In = OpenApiSecurityApiKeyLocation.Header,
        });

        options.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    });

    //builder.Services.AddHealthChecks()
    // .AddCheck<AIServiceHealthCheck>("ai-models");

    builder.Services.Configure<HostOptions>(hostOptions =>
    {
        hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

    builder.Services.AddOpenApi();

    builder.Services.AddHostedService<MetricsCleanupService>();
 

    // CORS policy configured in DependencyService.AddCorsPolicy()


    var app = builder.Build();
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    });


    app.UseHttpsRedirection();
    app.UseCors();

    // Add rate limiting middleware
    app.UseMiddleware<AI.API.Middlewares.SimpleRateLimitMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();


    app.MapControllers();

    app.MapHealthChecks("/health");

    await app.RunAsync();
    Log.Information("Stopped cleanly");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
