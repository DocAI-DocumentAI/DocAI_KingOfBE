using System;
using System.Linq;
using Document.API.Constants;
using Document.API.Extensions;
using Document.API.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.KernelMemory;
using NSwag;
using NSwag.Generation.Processors.Security;
using Scalar.AspNetCore;

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
    var configuration = builder.Configuration;
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
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
    builder.Services.AddOpenApi();

    builder.Services.AddDatabase();
    builder.Services.AddRedis(configuration);
    builder.Services.AddUnitOfWork();
    builder.Services.AddServices(configuration);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddControllers();
    builder.Services.AddJwtAuthentication(configuration);
    builder.Services.AddAuthorization();
    //builder.Services.AddKernelMemory();
    builder.Services.AddKernelMemory(configuration);
    

    // Register the NSwag services
    builder.Services.AddOpenApiDocument(options =>
    {
        options.Title = "DocAI Document API";
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

    // if (app.Environment.IsDevelopment())
    // {
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger"; 
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"); 
        });

        // app.UseSwaggerUI(options =>
        // {
        //     options.SwaggerEndpoint("/openapi/v1.json", "Document API V1");
        // });
        //
        // app.UseReDoc(options =>
        // {
        //     options.SpecUrl("/openapi/v1.json");
        // });
        //
        // app.MapScalarApiReference();
    // }

    app.UseCors(CorConstant.PolicyName);

    //app.Use(async (context, next) =>
    //{
    //    if (context.Request.Method == "OPTIONS")
    //    {
    //        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
    //        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    //        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
    //        context.Response.StatusCode = 200;
    //        return;
    //    }
    //    await next();
    //});

    app.MapOpenApi();

    app.UseOpenApi();

    app.UseHttpsRedirection();

    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

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
