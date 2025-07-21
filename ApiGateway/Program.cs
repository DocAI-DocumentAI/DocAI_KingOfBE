using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using System.IO;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using Swashbuckle.AspNetCore.SwaggerGen;
using ApiGateway.Middlewares;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpClient("YarpClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Thêm CORS với cấu hình chi tiết hơn
builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorConstant.PolicyName,
            policy => policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
    });

// Cấu hình JWT Authentication giống với Auth Service
string secret = builder.Configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT:Secret is missing in configuration.");
if (secret.Length < 32)
{
    throw new InvalidOperationException("JWT:Secret must be at least 32 characters long for HS256.");
}

var key = Encoding.UTF8.GetBytes(secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"] ?? "DocAI",
        IssuerSigningKey = new SymmetricSecurityKey(key),
        RoleClaimType = ClaimTypes.Role,
    };

    options.SaveToken = true;

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            Console.WriteLine($"Token received: {context.Token}");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"JWT authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"JWT token validated successfully. Claims: {string.Join(", ", context.Principal.Claims.Select(c => $"{c.Type}: {c.Value}"))}");
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddHttpClient();

// Thêm Authorization
builder.Services.AddAuthorization();

// Thêm HttpContextAccessor để truy cập thông tin người dùng
builder.Services.AddHttpContextAccessor();


// Cấu hình Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddTransient<IDocumentFilter, SwaggerDocumentFilter>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DocAI API Gateway",
        Version = "v1"
    });

    // JWT Bearer config...
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });

    // Gọi DocumentFilter chính xác với DI
    c.DocumentFilter<SwaggerDocumentFilter>();
});


var app = builder.Build();

// Configure the HTTP request pipeline.
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

// Thêm middleware để chuyển tiếp token JWT đến các service
app.UseMiddleware<ApiGateway.Middlewares.TokenForwardingMiddleware>();

// Thêm middleware xử lý exception
app.UseMiddleware<ExceptionHandlingMiddleware>();


app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocAI API Gateway v1");
    c.RoutePrefix = "swagger";
});

// Add this BEFORE MapReverseProxy
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    Console.WriteLine("Before fix - Authorization header: " + authHeader);

    if (!string.IsNullOrWhiteSpace(authHeader) && !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        context.Request.Headers["Authorization"] = "Bearer " + authHeader;
        Console.WriteLine("After fix - Authorization header: Bearer " + authHeader);
    }

    await next();
});


app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();

// Lớp filter để tích hợp Swagger từ các service
public class SwaggerDocumentFilter : IDocumentFilter
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public SwaggerDocumentFilter(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public void Apply(Microsoft.OpenApi.Models.OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var urls = new[]
        {
            _config["ServiceEndpoints:Auth"],
            _config["ServiceEndpoints:Document"],
            _config["ServiceEndpoints:AI"],
            _config["ServiceEndpoints:Notification"],
            _config["ServiceEndpoints:ChatBox"]
        };

        foreach (var url in urls)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var json = client.GetStringAsync($"{url}/swagger/v1/swagger.json").Result;

                var reader = new Microsoft.OpenApi.Readers.OpenApiStringReader();
                var remoteDoc = reader.Read(json, out var diagnostic);

                foreach (var path in remoteDoc.Paths)
                {
                    if (!swaggerDoc.Paths.ContainsKey(path.Key))
                    {
                        swaggerDoc.Paths.Add(path.Key, path.Value);
                    }
                }

                foreach (var schema in remoteDoc.Components.Schemas)
                {
                    if (!swaggerDoc.Components.Schemas.ContainsKey(schema.Key))
                    {
                        swaggerDoc.Components.Schemas.Add(schema.Key, schema.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Swagger Merge Error] {url}: {ex.Message}");
            }
        }
    }
}


