using System;
using System.Linq;
using ChatBox.API.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSwag;
using NSwag.Generation.Processors.Security;
using Serilog;
using Serilog.Events;
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

    builder.Services.AddOpenApi();
    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddDatabase();
    builder.Services.AddControllers();
    builder.Services.AddServices(builder.Configuration);
    builder.Services.Configure<HostOptions>(hostOptions =>
    {
        hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

    builder.Services.AddOpenApiDocument(options =>
    {
        options.Title = "DocAI ChatBox API";
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
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSerilogRequestLogging();
    app.MapControllers();

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