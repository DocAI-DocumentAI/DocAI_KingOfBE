using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Thêm các API project với proxy để tự động chọn port
var docAPI = builder.AddProject<Projects.Document_API>("apiservice-doc")
    .WithHttpEndpoint(name: "doc-endpoint");
var authAPI = builder.AddProject<Projects.Auth_API>("apiservice-auth")
    .WithHttpEndpoint(name: "auth-endpoint");
var notiAPI = builder.AddProject<Projects.Notification_API>("apiservice-noti")
    .WithHttpEndpoint(name: "noti-endpoint");
var chatboxAPI = builder.AddProject<Projects.ChatBox_API>("apiservice-chatbox")
    .WithHttpEndpoint(name: "chatbox-endpoint");

var apigatewayAPI = builder.AddProject<Projects.ApiGateway>("apiservice-apigateway")
    .WithHttpEndpoint(name: "apigateway-endpoint")
    .WithReference(docAPI)
    .WithReference(authAPI)
    .WithReference(notiAPI)
    .WithReference(chatboxAPI);

// Build và chạy ứng dụng
builder.Build().Run();