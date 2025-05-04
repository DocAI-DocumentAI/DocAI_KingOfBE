using Auth.API.Constants;
using Auth.API.Extensions;
using Auth.API.Middlewares;
using Microsoft.OpenApi.Models;
using NSwag;
using NSwag.Generation.Processors.Security;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;
using OpenApiSecurityScheme = NSwag.OpenApiSecurityScheme;

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
    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
    builder.Services.AddEndpointsApiExplorer(); 
    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    // builder.Services.AddSwaggerGen();
    builder.Services.AddHttpContextAccessor();
    
    builder.Services.Configure<HostOptions>(hostOptions =>
    {
        hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });
    // Register the NSwag services
    builder.Services.AddOpenApiDocument(options =>
    {
        // Title và version
        options.Title = "DocAI System";
        options.Version = "v1";

        // Security
        options.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
        {
            Type = NSwag.OpenApiSecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            Name = "Authorization",
            In = NSwag.OpenApiSecurityApiKeyLocation.Header,
            Description = "Please enter a valid token"
        });

        options.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    });


    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseOpenApi();
        app.UseSwaggerUi();

        // app.UseSwaggerUI(options =>
        // {
        //     options.SwaggerEndpoint("/openapi/v1.json", "Auth API V1");
        // });
        //
        // app.UseReDoc(options =>
        // {
        //     options.SpecUrl("/openapi/v1.json");
        // });
        //
        // app.MapScalarApiReference();
    }
    
    
    
    app.UseAuthentication();
    
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    
    app.UseAuthorization();

    app.UseHttpsRedirection();
    
    app.UseSerilogRequestLogging();
    
    app.MapControllers();
    
    app.UseCors(CorConstant.PolicyName);

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