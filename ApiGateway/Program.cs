using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSwag;
using NSwag.Generation.Processors.Security;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
// Register the NSwag services
builder.Services.AddOpenApiDocument(options =>
{
    options.Title = "DocAI API Gateway";
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

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
    app.MapOpenApi();
    
    app.UseOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger"; 
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1"); 
    });

//     app.UseReDoc(options =>
//     {
//         options.SpecUrl("/openapi/v1.json");
//     });
//
//     app.MapScalarApiReference();
// }

app.UseHttpsRedirection();

app.MapReverseProxy();

app.Run();