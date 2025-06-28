using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HybridBot.Core
{
    /// <summary>
    /// Abstract base class providing common functionality for bot roles.
    /// Implements logging, error handling, and configuration management.
    /// </summary>
    public abstract class BaseBotRole : IBotRole
    {
        protected readonly ILogger _logger;
        
        public abstract string RoleId { get; }
        public abstract string Name { get; }
        public virtual IReadOnlyList<string> Tags { get; protected set; } = new List<string>();
        public virtual int Priority { get; protected set; } = 50;
        public virtual IDictionary<string, object> Metadata { get; protected set; } = new Dictionary<string, object>();
        
        protected bool _isInitialized = false;
        
        protected BaseBotRole(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public virtual async Task InitializeAsync(IDictionary<string, object> config)
        {
            try
            {
                _logger.LogInformation("Initializing role {RoleId}", RoleId);
                
                // Extract common configuration
                if (config.TryGetValue("priority", out var priorityValue) && 
                    int.TryParse(priorityValue.ToString(), out var priority))
                {
                    Priority = priority;
                }
                
                if (config.TryGetValue("tags", out var tagsValue) && tagsValue is IEnumerable<object> tags)
                {
                    Tags = tags.Select(t => t.ToString()).ToList();
                }
                
                if (config.TryGetValue("metadata", out var metadataValue) && 
                    metadataValue is IDictionary<string, object> metadata)
                {
                    Metadata = metadata;
                }
                
                // Allow derived classes to perform custom initialization
                await OnInitializeAsync(config);
                
                _isInitialized = true;
                _logger.LogInformation("Role {RoleId} initialized successfully", RoleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize role {RoleId}", RoleId);
                throw;
            }
        }
        
        public async Task<BotResponse> ExecuteAsync(BotContext context)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException($"Role {RoleId} has not been initialized");
            }
            
            if (!CanHandle(context))
            {
                _logger.LogDebug("Role {RoleId} cannot handle context {RequestId}", RoleId, context.RequestId);
                return new BotResponse 
                { 
                    Content = $"Role {Name} cannot handle this request",
                    IsComplete = false
                };
            }
            
            try
            {
                _logger.LogInformation("Executing role {RoleId} for request {RequestId}", RoleId, context.RequestId);
                
                var response = await OnExecuteAsync(context);
                
                // Add role metadata to response
                response.Metadata["executedRole"] = RoleId;
                response.Metadata["executionTime"] = DateTime.UtcNow;
                
                _logger.LogInformation("Role {RoleId} completed execution for request {RequestId}", RoleId, context.RequestId);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing role {RoleId} for request {RequestId}", RoleId, context.RequestId);
                
                return new BotResponse
                {
                    Content = "An error occurred while processing your request.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["executedRole"] = RoleId
                    }
                };
            }
        }
        
        public virtual bool CanHandle(BotContext context)
        {
            // Default implementation - derived classes should override
            return true;
        }
        
        public virtual async Task DisposeAsync()
        {
            try
            {
                _logger.LogInformation("Disposing role {RoleId}", RoleId);
                await OnDisposeAsync();
                _logger.LogInformation("Role {RoleId} disposed successfully", RoleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing role {RoleId}", RoleId);
            }
        }
        
        /// <summary>
        /// Override this method to implement role-specific initialization logic
        /// </summary>
        protected virtual Task OnInitializeAsync(IDictionary<string, object> config)
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Override this method to implement the core role logic
        /// </summary>
        protected abstract Task<BotResponse> OnExecuteAsync(BotContext context);
        
        /// <summary>
        /// Override this method to implement role-specific cleanup logic
        /// </summary>
        protected virtual Task OnDisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
