using System;
using Auth.API.Constants;
using Auth.API.Extensions;
using Auth.API.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NSwag;
using NSwag.Generation.Processors.Security;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;
using OpenApiSecurityScheme = NSwag.OpenApiSecurityScheme;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotNetEnv;
using FluentValidation;
using FluentValidation.AspNetCore;
using Auth.API.Services.Interface;
using Auth.API.Services.Implement;

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

    // Tải file .env
    DotNetEnv.Env.Load();
    
    builder.Services.AddMemoryCache();
    
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

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

    var app = builder.Build();

    app.MapOpenApi();
    app.UseOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    });

    app.UseCors(CorConstant.PolicyName);

    // Thêm CORS middleware tùy chỉnh trước các middleware khác
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

    // Thêm Rate Limiting Middleware trước Authentication
    app.UseMiddleware<RateLimitingMiddleware>();

    app.UseAuthentication();

    // Thêm JWT Blacklist Middleware sau Authentication
    app.UseMiddleware<JwtBlacklistMiddleware>();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
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
