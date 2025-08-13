using Notification.API.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Notification.API.Constants;
using Notification.API.Consumers;
using Notification.API.Hubs;
using Notification.API.Middlewares;
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

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
            // Include trace and span ids when present.
            "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
            theme: TemplateTheme.Code)));

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorConstant.PolicyName,
            policy => policy
                .AllowAnyOrigin() // You had .WithOrigins("*") which is invalid. Use AllowAnyOrigin() instead.
                .AllowAnyHeader()
                .AllowAnyMethod());
    });

    builder.Services.AddOpenApi();
    builder.Services.AddDatabase();
    builder.Services.AddRedis(builder.Configuration);
    builder.Services.AddUnitOfWork();
    builder.Services.AddServices(builder.Configuration);
    builder.Services.AddAutoMapper();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddQuartzJobs(builder.Configuration);
    builder.Services.AddMassTransit(builder.Configuration);

    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddAuthorization();

    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.Configure<HostOptions>(hostOptions =>
    {
        hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

    builder.Services.AddOpenApiDocument(options =>
    {
        options.Title = "DocAI Notification API";
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

    var app = builder.Build();
    app.MapOpenApi();
    app.UseOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    });

    app.UseHttpsRedirection();

    app.UseSerilogRequestLogging();
    app.UseCors(CorConstant.PolicyName);
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.MapHub<NotificationHub>("/notificationHub");


    app.Run();

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