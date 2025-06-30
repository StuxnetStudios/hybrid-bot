using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HybridBot.Core
{
    /// <summary>
    /// Abstract base class for role capabilities that provides common functionality.
    /// Implements IRoleCapability with shared behavior patterns.
    /// </summary>
    public abstract class BaseRoleCapability : IRoleCapability
    {
        protected readonly ILogger _logger;
        
        public abstract string CapabilityId { get; }
        public abstract string Name { get; }
        public virtual IReadOnlyList<string> Tags { get; protected set; } = new List<string>();
        public virtual int Priority { get; protected set; } = 50;
        public virtual IDictionary<string, object> Metadata { get; protected set; } = new Dictionary<string, object>();
        
        protected bool _isInitialized = false;
        
        protected BaseRoleCapability(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public virtual async Task InitializeAsync(IDictionary<string, object> config)
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogInformation($"Capability {Name} is already initialized");
                    return;
                }
                
                // Apply configuration
                if (config != null)
                {
                    foreach (var kvp in config)
                    {
                        Metadata[kvp.Key] = kvp.Value;
                    }
                }
                
                // Extract common configuration
                if (Metadata.TryGetValue("priority", out var priorityObj) && int.TryParse(priorityObj?.ToString(), out var priority))
                {
                    Priority = priority;
                }
                
                if (Metadata.TryGetValue("tags", out var tagsObj) && tagsObj is IEnumerable<string> tags)
                {
                    Tags = new List<string>(tags);
                }
                
                await OnInitializeAsync(config);
                
                _isInitialized = true;
                _logger.LogInformation($"Capability {Name} initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize capability {Name}");
                throw;
            }
        }
        
        /// <summary>
        /// Override this method to provide capability-specific initialization logic
        /// </summary>
        protected virtual Task OnInitializeAsync(IDictionary<string, object> config)
        {
            return Task.CompletedTask;
        }
        
        public abstract Task<BotResponse> ExecuteAsync(BotContext context);
        
        public abstract bool CanHandle(BotContext context);
        
        public abstract string GetInstructions();
        
        public virtual async Task<string> ProcessAsync(string input, BotContext context)
        {
            var response = await ExecuteAsync(context);
            return response.Content;
        }
        
        public virtual Task DisposeAsync()
        {
            _logger.LogInformation($"Disposing capability {Name}");
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Helper method to check if context contains specific keywords
        /// </summary>
        protected bool ContainsKeywords(BotContext context, params string[] keywords)
        {
            if (string.IsNullOrEmpty(context.Input))
                return false;
                
            var message = context.Input.ToLowerInvariant();
            return Array.Exists(keywords, keyword => message.Contains(keyword.ToLowerInvariant()));
        }
        
        /// <summary>
        /// Helper method to check if context has specific tags (checks State for tags)
        /// </summary>
        protected bool HasTags(BotContext context, params string[] requiredTags)
        {
            if (context.State == null || !context.State.ContainsKey("tags"))
                return false;
                
            if (context.State["tags"] is IEnumerable<string> contextTags)
            {
                return Array.Exists(requiredTags, tag => contextTags.Contains(tag, StringComparer.OrdinalIgnoreCase));
            }
                
            return false;
        }
        
        /// <summary>
        /// Helper method to extract metadata value with type conversion
        /// </summary>
        protected T? GetMetadataValue<T>(string key, T? defaultValue = default)
        {
            if (!Metadata.TryGetValue(key, out var value))
                return defaultValue;
                
            try
            {
                if (value is T directValue)
                    return directValue;
                    
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
