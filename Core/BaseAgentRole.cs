using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace HybridBot.Core
{
    /// <summary>
    /// Abstract base class that bridges Semantic Kernel Agent with our hybrid bot role system.
    /// Inherits from SK Agent while maintaining compatibility with IBotRole interface.
    /// </summary>
    public abstract class BaseAgentRole : Agent, IBotRole
    {
        protected readonly ILogger _logger;
        
        // IBotRole properties
        public abstract string RoleId { get; }
        public abstract string RoleName { get; } // Using different name to avoid conflict with Agent.Name
        public virtual IReadOnlyList<string> Tags { get; protected set; } = new List<string>();
        public virtual int Priority { get; protected set; } = 50;
        public virtual IDictionary<string, object> Metadata { get; protected set; } = new Dictionary<string, object>();
        
        protected bool _isInitialized = false;
        
        protected BaseAgentRole(ILogger logger, Kernel kernel) : base()
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize SK Agent properties
            Id = Guid.NewGuid().ToString();
            Kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            
            // Set default instructions - can be overridden by derived classes
            Instructions = $"You are {RoleName}, a specialized AI agent in a hybrid bot system.";
        }

        #region IBotRole Implementation

        // IBotRole.Name maps to our RoleName property
        string IBotRole.Name => RoleName;

        public virtual async Task InitializeAsync(IDictionary<string, object> config)
        {
            try
            {
                _logger.LogInformation("Initializing agent role {RoleId}", RoleId);
                
                // Extract common configuration
                if (config.TryGetValue("priority", out var priorityValue) && 
                    int.TryParse(priorityValue.ToString(), out var priority))
                {
                    Priority = priority;
                }
                
                if (config.TryGetValue("tags", out var tagsValue) && tagsValue is IEnumerable<object> tags)
                {
                    Tags = tags.Select(t => t.ToString()!).ToList();
                }
                
                if (config.TryGetValue("metadata", out var metadataValue) && 
                    metadataValue is IDictionary<string, object> metadata)
                {
                    Metadata = metadata;
                }

                // Update SK Agent instructions if provided
                if (config.TryGetValue("instructions", out var instructionsValue) && 
                    instructionsValue is string instructions)
                {
                    Instructions = instructions;
                }
                
                // Allow derived classes to perform custom initialization
                await OnInitializeAsync(config);
                
                _isInitialized = true;
                _logger.LogInformation("Agent role {RoleId} initialized successfully", RoleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize agent role {RoleId}", RoleId);
                throw;
            }
        }

        public async Task<BotResponse> ExecuteAsync(BotContext context)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException($"Agent role {RoleId} has not been initialized");
            }
            
            if (!CanHandle(context))
            {
                _logger.LogDebug("Agent role {RoleId} cannot handle context {RequestId}", RoleId, context.RequestId);
                return new BotResponse 
                { 
                    Content = $"Agent {RoleName} cannot handle this request",
                    IsComplete = false
                };
            }

            try
            {
                _logger.LogInformation("Executing agent role {RoleId} for request {RequestId}", RoleId, context.RequestId);
                
                var response = await OnExecuteAsync(context);
                
                // Add role metadata to response
                response.Metadata["executedRole"] = RoleId;
                response.Metadata["agentId"] = Id;
                response.Metadata["executionTime"] = DateTime.UtcNow;
                
                _logger.LogInformation("Agent role {RoleId} completed execution for request {RequestId}", RoleId, context.RequestId);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing agent role {RoleId} for request {RequestId}", RoleId, context.RequestId);
                
                return new BotResponse
                {
                    Content = "An error occurred while processing your request.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["executedRole"] = RoleId,
                        ["agentId"] = Id
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
                _logger.LogInformation("Disposing agent role {RoleId}", RoleId);
                await OnDisposeAsync();
                _logger.LogInformation("Agent role {RoleId} disposed successfully", RoleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing agent role {RoleId}", RoleId);
            }
        }

        #endregion

        #region Semantic Kernel Agent Abstract Methods Implementation

        public override async IAsyncEnumerable<ChatMessageContent> InvokeAsync(
            ICollection<ChatMessageContent> history, 
            AgentThread? thread = null, 
            AgentInvokeOptions? options = null, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var chatHistory = new ChatHistory(history);
            
            // Add agent instructions if not already present
            if (!string.IsNullOrEmpty(Instructions) && !history.Any(m => m.Role == AuthorRole.System))
            {
                chatHistory.Insert(0, new ChatMessageContent(AuthorRole.System, Instructions));
            }

            var chatService = Kernel.GetRequiredService<IChatCompletionService>();
            var responses = await chatService.GetChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken);
            
            foreach (var response in responses)
            {
                yield return response;
            }
        }

        public override async IAsyncEnumerable<StreamingChatMessageContent> InvokeStreamingAsync(
            ICollection<ChatMessageContent> history,
            AgentThread? thread = null,
            AgentInvokeOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var chatHistory = new ChatHistory(history);
            
            // Add agent instructions if not already present
            if (!string.IsNullOrEmpty(Instructions) && !history.Any(m => m.Role == AuthorRole.System))
            {
                chatHistory.Insert(0, new ChatMessageContent(AuthorRole.System, Instructions));
            }

            var chatService = Kernel.GetRequiredService<IChatCompletionService>();
            
            await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken))
            {
                yield return chunk;
            }
        }

        public override Task<AgentChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            // Simple channel implementation - can be enhanced for more complex scenarios
            var channel = new AgentChannel();
            return Task.FromResult(channel);
        }

        public override IAsyncEnumerable<string> GetChannelKeys()
        {
            // Simple implementation - return empty for now
            return AsyncEnumerable.Empty<string>();
        }

        public override Task<AgentChannel> RestoreChannelAsync(string channelKey, CancellationToken cancellationToken = default)
        {
            // Simple implementation - create new channel
            return CreateChannelAsync(cancellationToken);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Uses Semantic Kernel Agent for AI-powered conversation
        /// </summary>
        protected async Task<BotResponse> InvokeAgentAsync(BotContext context)
        {
            try
            {
                // Create chat history from the context
                var chatHistory = new List<ChatMessageContent>();
                
                // Add system message with agent instructions
                if (!string.IsNullOrEmpty(Instructions))
                {
                    chatHistory.Add(new ChatMessageContent(AuthorRole.System, Instructions));
                }
                
                // Add conversation context if available
                await AddConversationContext(chatHistory, context);
                
                // Add the current user message
                chatHistory.Add(new ChatMessageContent(AuthorRole.User, context.Input));

                // Invoke the agent using SK's agent framework
                await foreach (var message in InvokeAsync(chatHistory))
                {
                    if (message.Role == AuthorRole.Assistant && !string.IsNullOrEmpty(message.Content))
                    {
                        return new BotResponse
                        {
                            Content = message.Content,
                            IsComplete = true,
                            Metadata = new Dictionary<string, object>
                            {
                                ["agentId"] = Id,
                                ["agentName"] = RoleName,
                                ["messageId"] = message.GetType().Name,
                                ["executedRole"] = RoleId
                            }
                        };
                    }
                }

                // If no assistant message was generated
                _logger.LogWarning("Agent {AgentId} did not generate a response", Id);
                return new BotResponse
                {
                    Content = "I couldn't generate a response at this time.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["agentId"] = Id,
                        ["executedRole"] = RoleId,
                        ["issue"] = "no_response_generated"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking Semantic Kernel agent {AgentId}", Id);
                throw;
            }
        }

        /// <summary>
        /// Add conversation context to chat history
        /// </summary>
        protected virtual async Task AddConversationContext(IList<ChatMessageContent> chatHistory, BotContext context)
        {
            // Override in derived classes to add specific context
            await Task.CompletedTask;
        }

        #endregion

        #region Abstract and Virtual Methods

        /// <summary>
        /// Override this method to implement role-specific initialization logic
        /// </summary>
        protected virtual Task OnInitializeAsync(IDictionary<string, object> config)
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Override this method to implement the core role logic
        /// Can use InvokeAgentAsync() for SK Agent-powered responses
        /// </summary>
        protected abstract Task<BotResponse> OnExecuteAsync(BotContext context);
        
        /// <summary>
        /// Override this method to implement role-specific cleanup logic
        /// </summary>
        protected virtual Task OnDisposeAsync()
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
