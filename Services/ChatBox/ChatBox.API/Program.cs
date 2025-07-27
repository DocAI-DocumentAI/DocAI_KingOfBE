using System;
using System.Linq;
using System.Text.Json;
using ChatBox.API.Constants;
using ChatBox.API.Extensions;
using ChatBox.API.Hubs;
using ChatBox.API.Middlewares;
using ChatBox.Domain.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
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

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new ExpressionTemplate(
            // Include trace and span ids when present.
            "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
            theme: TemplateTheme.Code)));

    builder.Services.AddControllers()
     .AddJsonOptions(options =>
     {
         options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
         options.JsonSerializerOptions.WriteIndented = false;
         options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
     });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(1);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorConstant.PolicyName,
            policy => policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
    });



    builder.Services.AddHttpContextAccessor();

    builder.Services.AddAuthorization();

    builder.Services.AddOpenApiDocument(options =>
    {
        options.Title = "DocAI Auth API";
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

    builder.Services.AddOpenApi();

    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.Configure<HostOptions>(hostOptions =>
    {
        hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });
    var app = builder.Build();
    app.UseExceptionHandling();

    app.MapOpenApi();
    app.UseOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    });
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();

    app.UseCors(CorConstant.PolicyName);

    app.UseCustomMiddlewares();

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    // Map SignalR Hub
    app.MapHub<ChatHub>("/chatHub");

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