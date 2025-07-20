using AI.API.Payload.Request;
using AI.API.Payload.Response;
using AI.API.Services.Interface;
using AI.Domain.Models;
using AI.Infrastructure.Repository.Interfaces;
using AutoMapper;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AI.API.Services.Implement
{
    public class PromptTemplateService : IPromptTemplateService
    {
        private readonly IUnitOfWork<DocAIDbContext> _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PromptTemplateService> _logger;
        private const string TEMPLATE_CACHE_PREFIX = "template:";
        private const int CACHE_DURATION_MINUTES = 20;
        private static readonly Regex VariablePattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        public PromptTemplateService(
           IUnitOfWork<DocAIDbContext> unitOfWork,
           IMapper mapper,
           ICacheService cacheService,
           ILogger<PromptTemplateService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PromptTemplateResponse> GetTemplateAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Template name cannot be empty", nameof(name));

            try
            {
                // Normalize name for cache key
                var normalizedName = name.ToLowerInvariant();
                var cacheKey = $"{TEMPLATE_CACHE_PREFIX}name:{normalizedName}";

                var cached = await _cacheService.GetAsync<PromptTemplateResponse>(cacheKey);
                if (cached != null)
                {
                    _logger.LogDebug("Template {Name} loaded from cache", name);
                    return cached;
                }

                // Get from database
                var repo = _unitOfWork.GetRepository<PromptTemplate>();
                var template = await repo.SingleOrDefaultAsync(
                    predicate: t => t.Name.ToLower() == normalizedName && t.IsActive);

                if (template == null)
                {
                    _logger.LogWarning("Template {Name} not found or inactive", name);
                    return new PromptTemplateResponse
                    {
                        Success = false,
                        Message = $"Template '{name}' not found or inactive"
                    };
                }

                var response = _mapper.Map<PromptTemplateResponse>(template);

                // Cache the response
                await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template {Name}", name);
                throw;
            }
        }

        public async Task<PromptTemplateResponse> GetTemplateByIdAsync(int id)
        {
            try
            {
                var cacheKey = $"{TEMPLATE_CACHE_PREFIX}id:{id}";
                var cached = await _cacheService.GetAsync<PromptTemplateResponse>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var repo = _unitOfWork.GetRepository<PromptTemplate>();
                var template = await repo.SingleOrDefaultAsync(predicate: t => t.Id == id);

                if (template == null)
                {
                    _logger.LogWarning("Template with ID {Id} not found", id);
                    return new PromptTemplateResponse
                    {
                        Success = false,
                        Message = $"Template with ID {id} not found"
                    };
                }

                var response = _mapper.Map<PromptTemplateResponse>(template);

                // Only cache if active
                if (template.IsActive)
                {
                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template by ID {Id}", id);
                throw;
            }
        }

        public async Task<List<PromptTemplateSummary>> GetAllTemplatesAsync(string category = null, bool activeOnly = true)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<PromptTemplate>();
                var templates = await repo.GetListAsync(
                    predicate: t => (!activeOnly || t.IsActive) &&
                                   (string.IsNullOrEmpty(category) || t.Category == category),
                    orderBy: q => q.OrderBy(t => t.Category).ThenBy(t => t.Name));

                return _mapper.Map<List<PromptTemplateSummary>>(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all templates");
                throw;
            }
        }

        public async Task<PromptTemplateResponse> CreateTemplateAsync(CreatePromptTemplateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                // Validate template syntax
                var validationResult = ValidateTemplateSyntax(request.Template);
                if (!validationResult.IsValid)
                {
                    return new PromptTemplateResponse
                    {
                        Success = false,
                        Message = $"Template validation failed: {validationResult.ErrorMessage}"
                    };
                }

                // Check for duplicate name
                var repo = _unitOfWork.GetRepository<PromptTemplate>();
                var existing = await repo.SingleOrDefaultAsync(
                    predicate: t => t.Name.ToLower() == request.Name.ToLower());

                if (existing != null)
                {
                    return new PromptTemplateResponse
                    {
                        Success = false,
                        Message = $"Template with name '{request.Name}' already exists"
                    };
                }

                // Create template
                var template = _mapper.Map<PromptTemplate>(request);

                // Extract and validate variables
                var extractedVars = ExtractVariables(template.Template);
                var variables = new Dictionary<string, string>();

                foreach (var varName in extractedVars)
                {
                    variables[varName] = request.Variables?.GetValueOrDefault(varName) ?? "";
                }

                template.Variables = JsonSerializer.Serialize(variables);

                await repo.InsertAsync(template);
                await _unitOfWork.CommitAsync();

                // Clear template list cache
                await _cacheService.RemoveByPrefixAsync(TEMPLATE_CACHE_PREFIX);

                _logger.LogInformation("Created template {Name} with {VarCount} variables",
                    template.Name, variables.Count);

                return _mapper.Map<PromptTemplateResponse>(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating template");
                throw;
            }
        }

        public async Task<PromptTemplateResponse> UpdateTemplateAsync(int id, UpdatePromptTemplateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var repo = _unitOfWork.GetRepository<PromptTemplate>();
                var template = await repo.SingleOrDefaultAsync(predicate: t => t.Id == id);

                if (template == null)
                {
                    return new PromptTemplateResponse
                    {
                        Success = false,
                        Message = $"Template with ID {id} not found"
                    };
                }

                // Validate template syntax
                var validationResult = ValidateTemplateSyntax(request.Template);
                if (!validationResult.IsValid)
                {
                    return new PromptTemplateResponse
                    {
                        Success = false,
                        Message = $"Template validation failed: {validationResult.ErrorMessage}"
                    };
                }

                // Check name uniqueness if changed
                if (template.Name != request.Name)
                {
                    var existing = await repo.SingleOrDefaultAsync(
                        predicate: t => t.Name.ToLower() == request.Name.ToLower() && t.Id != id);

                    if (existing != null)
                    {
                        return new PromptTemplateResponse
                        {
                            Success = false,
                            Message = $"Template with name '{request.Name}' already exists"
                        };
                    }
                }

                // Log changes
                var originalName = template.Name;
                var originalActive = template.IsActive;

                // Update template
                template.Name = request.Name;
                template.Template = request.Template;
                template.Category = request.Category ?? template.Category;
                template.IsActive = request.IsActive;
                template.UpdatedAt = DateTime.UtcNow;

                // Update variables
                var extractedVars = ExtractVariables(request.Template);
                var variables = new Dictionary<string, string>();

                foreach (var varName in extractedVars)
                {
                    variables[varName] = request.Variables?.GetValueOrDefault(varName) ?? "";
                }

                template.Variables = JsonSerializer.Serialize(variables);

                repo.UpdateAsync(template);
                await _unitOfWork.CommitAsync();

                // Clear cache
                await _cacheService.RemoveByPrefixAsync(TEMPLATE_CACHE_PREFIX);

                _logger.LogInformation("Updated template {Id}: {OldName} -> {NewName}, Active: {OldActive} -> {NewActive}",
                    id, originalName, template.Name, originalActive, template.IsActive);

                return _mapper.Map<PromptTemplateResponse>(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating template {Id}", id);
                throw;
            }
        }

        public async Task<bool> DeleteTemplateAsync(int id)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<PromptTemplate>();
                var template = await repo.SingleOrDefaultAsync(predicate: t => t.Id == id);

                if (template == null)
                {
                    _logger.LogWarning("Template {Id} not found for deletion", id);
                    return false;
                }

                // Soft delete by deactivating first
                if (template.IsActive)
                {
                    template.IsActive = false;
                    template.UpdatedAt = DateTime.UtcNow;
                    repo.UpdateAsync(template);
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("Deactivated template {Id} - {Name}", id, template.Name);

                    // Give warning about permanent deletion
                    throw new InvalidOperationException(
                        "Template has been deactivated. Call delete again to permanently remove it.");
                }

                // Permanent delete
                repo.DeleteAsync(template);
                await _unitOfWork.CommitAsync();

                // Clear cache
                await _cacheService.RemoveByPrefixAsync(TEMPLATE_CACHE_PREFIX);

                _logger.LogInformation("Permanently deleted template {Id} - {Name}", id, template.Name);
                return true;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting template {Id}", id);
                throw;
            }
        }

        public async Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException("Template name cannot be empty", nameof(templateName));

            variables ??= new Dictionary<string, string>();

            try
            {
                var template = await GetTemplateAsync(templateName);
                if (template == null || !template.Success)
                {
                    throw new KeyNotFoundException($"Template '{templateName}' not found or inactive");
                }

                var rendered = template.Template;
                var missingVariables = new List<string>();

                // Get all variables in template
                var templateVariables = ExtractVariables(template.Template);

                // Replace variables
                foreach (var varName in templateVariables)
                {
                    var placeholder = $"{{{varName}}}";

                    if (variables.TryGetValue(varName, out var value))
                    {
                        rendered = rendered.Replace(placeholder, value ?? string.Empty);
                    }
                    else
                    {
                        missingVariables.Add(varName);
                        // Use default value if available
                        if (template.Variables?.TryGetValue(varName, out var defaultValue) == true)
                        {
                            rendered = rendered.Replace(placeholder, defaultValue ?? string.Empty);
                        }
                    }
                }

                if (missingVariables.Any())
                {
                    _logger.LogWarning("Template {Name} rendered with missing variables: {Variables}",
                        templateName, string.Join(", ", missingVariables));
                }

                return rendered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering template {Name}", templateName);
                throw;
            }
        }

        public async Task<bool> ValidateTemplateAsync(string template, Dictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(template))
                return false;

            try
            {
                var validationResult = ValidateTemplateSyntax(template);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Template syntax validation failed: {Error}", validationResult.ErrorMessage);
                    return false;
                }

                // Check if all variables in template are provided or have defaults
                var templateVariables = ExtractVariables(template);
                var providedVariables = variables?.Keys ?? Enumerable.Empty<string>();
                var missingVariables = templateVariables.Except(providedVariables).ToList();

                if (missingVariables.Any())
                {
                    _logger.LogWarning("Template validation: missing variables {Variables}",
                        string.Join(", ", missingVariables));
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Template validation failed");
                return false;
            }
        }

        #region Private Methods

        private HashSet<string> ExtractVariables(string template)
        {
            var matches = VariablePattern.Matches(template);
            return matches.Select(m => m.Groups[1].Value).ToHashSet();
        }

        private (bool IsValid, string ErrorMessage) ValidateTemplateSyntax(string template)
        {
            var openBraces = 0;
            var closeBraces = 0;
            var inVariable = false;

            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] == '{')
                {
                    if (inVariable)
                        return (false, "Nested variables are not allowed");
                    openBraces++;
                    inVariable = true;
                }
                else if (template[i] == '}')
                {
                    closeBraces++;
                    inVariable = false;
                }
            }

            if (openBraces != closeBraces)
                return (false, "Unbalanced braces in template");

            // Check variable naming
            var variables = ExtractVariables(template);
            foreach (var varName in variables)
            {
                if (!Regex.IsMatch(varName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                    return (false, $"Invalid variable name: {varName}. Variables must start with letter or underscore and contain only letters, numbers, and underscores.");
            }

            return (true, null);
        }

        #endregion
    }
}