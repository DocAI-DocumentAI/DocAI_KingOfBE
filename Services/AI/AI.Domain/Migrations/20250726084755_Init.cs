using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AI.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIModelConfigs",
                columns: table => new
                {
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    TopP = table.Column<double>(type: "double precision", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ConfiguredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestResult = table.Column<bool>(type: "boolean", nullable: true),
                    LastTestMessage = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModelConfigs", x => x.ModelId);
                });

            migrationBuilder.CreateTable(
                name: "AIModelConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrganizationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApiVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsTestedSuccessfully = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTestError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AverageResponseTime = table.Column<double>(type: "double precision", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModelConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIRequestLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceService = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelType = table.Column<int>(type: "integer", nullable: false),
                    RequestContent = table.Column<string>(type: "text", nullable: true),
                    ResponseContent = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIRequestLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceService = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ModelType = table.Column<int>(type: "integer", nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageMetrics", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AIModelConfigurations",
                columns: new[] { "Id", "ApiKey", "ApiVersion", "AverageResponseTime", "CreatedAt", "Description", "Endpoint", "IsActive", "IsEnabled", "IsTestedSuccessfully", "LastTestError", "LastTestedAt", "LastUsedAt", "ModelId", "Name", "OrganizationId", "ProviderType" },
                values: new object[,]
                {
                    { 1, null, null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OpenAI GPT-4 model for advanced text generation - Requires API Key configuration", "https://api.openai.com/v1", false, true, false, null, null, null, "gpt-4", "OpenAI GPT-4", null, 1 },
                    { 2, null, null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "OpenAI GPT-3.5 Turbo model for fast text generation - Requires API Key configuration", "https://api.openai.com/v1", false, true, false, null, null, null, "gpt-3.5-turbo", "OpenAI GPT-3.5 Turbo", null, 1 },
                    { 3, null, null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Mistral AI Large model for high-quality text generation - Requires API Key configuration", "https://api.mistral.ai/v1", false, true, false, null, null, null, "mistral-large-latest", "Mistral Large", null, 2 },
                    { 4, null, null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Google Gemini Pro model for versatile text generation - Requires API Key configuration", "https://generativelanguage.googleapis.com/v1beta", false, true, false, null, null, null, "gemini-pro", "Google Gemini Pro", null, 3 },
                    { 5, null, null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Azure AI Inference service for enterprise text generation - Requires API Key and Endpoint configuration", null, false, true, false, null, null, null, "gpt-4", "Azure AI Inference", null, 4 },
                    { 6, "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", null, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "HuggingFace fallback model - Pre-configured and ready to use", "https://router.huggingface.co/v1/chat/completions", true, true, true, null, null, null, "moonshotai/Kimi-K2-Instruct", "HuggingFace Fallback", null, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIModelConfigurations_IsActive",
                table: "AIModelConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AIModelConfigurations_IsActive_IsTestedSuccessfully",
                table: "AIModelConfigurations",
                columns: new[] { "IsActive", "IsTestedSuccessfully" });

            migrationBuilder.CreateIndex(
                name: "IX_AIModelConfigurations_IsEnabled",
                table: "AIModelConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AIModelConfigurations_IsTestedSuccessfully",
                table: "AIModelConfigurations",
                column: "IsTestedSuccessfully");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIModelConfigs");

            migrationBuilder.DropTable(
                name: "AIModelConfigurations");

            migrationBuilder.DropTable(
                name: "AIRequestLogs");

            migrationBuilder.DropTable(
                name: "SystemConfigurations");

            migrationBuilder.DropTable(
                name: "UsageMetrics");
        }
    }
}
