using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace HybridBot.Core
{
    /// <summary>
    /// Main HybridBot class that uses Semantic Kernel functionality.
    /// Composes multiple role capabilities to provide a unified AI agent experience.
    /// This is the single agent that orchestrates all role-based behaviors.
    /// Uses composition instead of inheritance for cleaner architecture.
    /// </summary>
    public class HybridBotAgent
    {
        private readonly ILogger<HybridBotAgent> _logger;
        private readonly Dictionary<string, IRoleCapability> _capabilities;
        private readonly StateManager _stateManager;
        private readonly SkynetLiteConnector _skynetConnector;
        private readonly Kernel _kernel;
        
        // Agent configuration
        public string Name => "HybridBot";
        public string Description => "AI agent with multiple specialized role capabilities";
        public string Id { get; }
        
        // Capability management
        public IReadOnlyDictionary<string, IRoleCapability> Capabilities => _capabilities;
        
        public HybridBotAgent(
            ILogger<HybridBotAgent> logger,
            Kernel kernel,
            StateManager stateManager,
            SkynetLiteConnector skynetConnector,
            IEnumerable<IRoleCapability>? capabilities = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _skynetConnector = skynetConnector ?? throw new ArgumentNullException(nameof(skynetConnector));
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            
            // Initialize with unique ID
            Id = Guid.NewGuid().ToString();
            
            // Initialize capabilities dictionary
            _capabilities = new Dictionary<string, IRoleCapability>();
            
            // Register initial capabilities if provided
            if (capabilities != null)
            {
                foreach (var capability in capabilities)
                {
                    RegisterCapability(capability);
                }
            }
            
            _logger.LogInformation($"HybridBot agent initialized with ID: {Id}");
        }
        
        /// <summary>
        /// Register a new role capability with the bot
        /// </summary>
        public void RegisterCapability(IRoleCapability capability)
        {
            if (capability == null)
                throw new ArgumentNullException(nameof(capability));
                
            _capabilities[capability.CapabilityId] = capability;
            
            _logger.LogInformation($"Registered capability: {capability.Name} ({capability.CapabilityId})");
        }
        
        /// <summary>
        /// Remove a capability from the bot
        /// </summary>
        public async Task<bool> UnregisterCapabilityAsync(string capabilityId)
        {
            if (_capabilities.TryGetValue(capabilityId, out var capability))
            {
                await capability.DisposeAsync();
                _capabilities.Remove(capabilityId);
                
                _logger.LogInformation($"Unregistered capability: {capabilityId}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Get a specific capability by ID
        /// </summary>
        public T? GetCapability<T>(string capabilityId) where T : class, IRoleCapability
        {
            _capabilities.TryGetValue(capabilityId, out var capability);
            return capability as T;
        }
        
        /// <summary>
        /// Get all capabilities of a specific type
        /// </summary>
        public IEnumerable<T> GetCapabilities<T>() where T : class, IRoleCapability
        {
            return _capabilities.Values.OfType<T>();
        }
        
        /// <summary>
        /// Process a user message through the appropriate capabilities
        /// </summary>
        public async Task<BotResponse> ProcessMessageAsync(string message, BotContext? context = null)
        {
            try
            {
                // Create context if not provided
                context ??= new BotContext 
                { 
                    Input = message, 
                    Timestamp = DateTime.UtcNow,
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = Guid.NewGuid().ToString(),
                    UserId = "unknown"
                };
                
                // Find applicable capabilities
                var applicableCapabilities = _capabilities.Values
                    .Where(c => c.CanHandle(context))
                    .OrderByDescending(c => c.Priority)
                    .ToList();
                
                if (!applicableCapabilities.Any())
                {
                    _logger.LogWarning("No capabilities can handle the context");
                    return new BotResponse
                    {
                        Content = "I'm not sure how to help with that request.",
                        IsComplete = false,
                        Metadata = new Dictionary<string, object>
                        {
                            ["source"] = Name,
                            ["handled_by"] = "none"
                        }
                    };
                }
                
                // Execute the highest priority capability
                var primaryCapability = applicableCapabilities.First();
                _logger.LogInformation($"Processing with capability: {primaryCapability.Name}");
                
                var response = await primaryCapability.ExecuteAsync(context);
                
                // Update state after successful execution
                if (response.IsComplete)
                {
                    await _stateManager.SaveStateAsync(context);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                return new BotResponse
                {
                    Content = "I encountered an error while processing your request.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = Name,
                        ["error"] = ex.Message,
                        ["handled_by"] = "error_handler"
                    }
                };
            }
        }
        
        /// <summary>
        /// Initialize all registered capabilities
        /// </summary>
        public async Task InitializeAsync()
        {
            var initTasks = _capabilities.Values.Select(async capability =>
            {
                try
                {
                    await capability.InitializeAsync(capability.Metadata);
                    _logger.LogInformation($"Initialized capability: {capability.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to initialize capability: {capability.Name}");
                }
            });
            
            await Task.WhenAll(initTasks);
            _logger.LogInformation($"HybridBot initialization complete. {_capabilities.Count} capabilities ready.");
        }
        
        /// <summary>
        /// Get comprehensive instructions based on all registered capabilities
        /// </summary>
        public string GetInstructions()
        {
            var instructions = new List<string>
            {
                "You are HybridBot, an advanced AI agent with multiple specialized capabilities.",
                "You can handle various types of requests by leveraging different role-based capabilities.",
                "",
                "Your capabilities include:"
            };
            
            foreach (var capability in _capabilities.Values.OrderBy(c => c.Name))
            {
                instructions.Add($"- {capability.Name}: {capability.GetInstructions()}");
            }
            
            instructions.AddRange(new[]
            {
                "",
                "Guidelines:",
                "- Choose the most appropriate capability for each request",
                "- Provide clear, helpful, and contextually relevant responses",
                "- Maintain consistency across different capabilities",
                "- Be transparent about your capabilities and limitations",
                "- Use Skynet-lite as your exclusive language model backend"
            });
            
            return string.Join("\n", instructions);
        }
        
        /// <summary>
        /// Process a chat using Semantic Kernel integration
        /// </summary>
        public async Task<string> ProcessChatAsync(string message, CancellationToken cancellationToken = default)
        {
            var context = new BotContext
            {
                Input = message,
                Timestamp = DateTime.UtcNow,
                RequestId = Guid.NewGuid().ToString(),
                ConversationId = Guid.NewGuid().ToString(),
                UserId = "chat-user"
            };
            
            var response = await ProcessMessageAsync(message, context);
            return response.Content;
        }
        
        /// <summary>
        /// Cleanup resources
        /// </summary>
        public async Task DisposeAsync()
        {
            var disposeTasks = _capabilities.Values.Select(c => c.DisposeAsync());
            await Task.WhenAll(disposeTasks);
            
            _capabilities.Clear();
            _logger.LogInformation("HybridBot disposed successfully");
        }
    }
}
