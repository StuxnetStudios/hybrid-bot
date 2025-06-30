using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using HybridBot.Core;

namespace HybridBot.Roles
{
    /// <summary>
    /// Skynet-lite powered agent role that inherits from BaseAgentRole.
    /// Combines Semantic Kernel Agent capabilities with Skynet-lite LLM integration.
    /// 
    /// Frontmatter Configuration:
    /// - model_id: skynet-lite-v1|skynet-lite-pro|custom
    /// - max_tokens: number
    /// - temperature: 0.0-2.0
    /// - system_prompt: custom system prompt
    /// - enable_context_memory: true|false
    /// - response_style: conversational|technical|creative
    /// </summary>
    public class SkynetLiteAgentRole : BaseAgentRole
    {
        public override string RoleId => "skynet-lite-agent";
        public override string Name => "Skynet-lite AI Agent";
        
        private readonly IChatCompletionService _chatService;
        
        private string _modelId = "skynet-lite-v1";
        private int _maxTokens = 2000;
        private double _temperature = 0.7;
        private bool _enableContextMemory = true;
        private string _responseStyle = "conversational";
        
        protected Dictionary<string, object>? Configuration { get; set; }
        
        public SkynetLiteAgentRole(ILogger<SkynetLiteAgentRole> logger, Kernel kernel) 
            : base(logger, kernel)
        {
            Tags = new List<string> { "ai", "skynet-lite", "agent", "conversation", "advanced", "intelligent" };
            Priority = 95; // Very high priority for Skynet-lite agent responses
            
            // Get chat completion service - this is required for operation
            try
            {
                _chatService = kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Skynet-lite agent chat completion service initialized successfully");
                
                // Set agent-specific instructions
                Instructions = "You are Skynet-lite, an advanced AI agent integrated into a hybrid bot system. " +
                              "Provide helpful, accurate, and contextually appropriate responses. " +
                              "You have access to the full capabilities of the Semantic Kernel agent framework.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Skynet-lite agent chat completion service");
                throw new InvalidOperationException("Skynet-lite chat completion service is required but not available. Please check your configuration.", ex);
            }
        }

        protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
        {
            _logger.LogInformation("Processing request with Skynet-lite Agent for user {UserId}", context.UserId);

            try
            {
                // Load configuration from frontmatter if available
                LoadConfiguration(context);

                // Use Semantic Kernel Agent for AI-powered response
                return await InvokeAgentAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request with Skynet-lite agent role");
                return new BotResponse
                {
                    Content = "I encountered an error while processing your request. Please try again.",
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

        protected override async Task AddConversationContext(ChatHistory chatHistory, BotContext context)
        {
            if (!_enableContextMemory)
                return;

            try
            {
                // Add recent conversation history for context
                // This would typically come from your StateManager
                var recentMessages = new List<string>(); // Get from StateManager
                
                foreach (var message in recentMessages.Take(3)) // Last 3 messages for context
                {
                    chatHistory.AddAssistantMessage(message);
                }

                // Add user-specific context
                chatHistory.AddSystemMessage($"User ID: {context.UserId}, Conversation ID: {context.ConversationId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load conversation context for agent {AgentId}", Id);
            }
            
            await Task.CompletedTask;
        }

        protected override async Task OnInitializeAsync(IDictionary<string, object> config)
        {
            await base.OnInitializeAsync(config);

            // Update agent instructions based on configuration
            if (Configuration != null)
            {
                _modelId = GetConfigValue("model_id", _modelId);
                _maxTokens = GetConfigValue("max_tokens", _maxTokens);
                _temperature = GetConfigValue("temperature", _temperature);
                _enableContextMemory = GetConfigValue("enable_context_memory", _enableContextMemory);
                _responseStyle = GetConfigValue("response_style", _responseStyle);

                // Update agent instructions with configuration
                var systemPrompt = GetConfigValue("system_prompt", "");
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    Instructions = systemPrompt;
                }

                _logger.LogDebug("Loaded Skynet-lite agent configuration: Model={Model}, MaxTokens={MaxTokens}, Style={Style}", 
                    _modelId, _maxTokens, _responseStyle);
            }
        }

        private void LoadConfiguration(BotContext context)
        {
            // In a real implementation, this would load from frontmatter or configuration
            // For now, we'll use default values that can be overridden
            
            if (Configuration != null)
            {
                _modelId = GetConfigValue("model_id", _modelId);
                _maxTokens = GetConfigValue("max_tokens", _maxTokens);
                _temperature = GetConfigValue("temperature", _temperature);
                _enableContextMemory = GetConfigValue("enable_context_memory", _enableContextMemory);
                _responseStyle = GetConfigValue("response_style", _responseStyle);

                _logger.LogDebug("Loaded Skynet-lite agent configuration: Model={Model}, MaxTokens={MaxTokens}, Style={Style}", 
                    _modelId, _maxTokens, _responseStyle);
            }
        }

        private T GetConfigValue<T>(string key, T defaultValue)
        {
            if (Configuration?.TryGetValue(key, out var value) == true)
            {
                try
                {
                    if (value is T directValue)
                        return directValue;
                    
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert config value {Key} to type {Type}", key, typeof(T).Name);
                }
            }
            
            return defaultValue;
        }

        public override bool CanHandle(BotContext context)
        {
            // Skynet-lite agent can handle most conversational requests
            var canHandle = !string.IsNullOrWhiteSpace(context.Input) &&
                           context.Input.Length > 2; // Minimum meaningful input

            _logger.LogDebug("Skynet-lite agent role can handle request: {CanHandle}", canHandle);
            return canHandle;
        }

        protected override async Task OnDisposeAsync()
        {
            _logger.LogInformation("Disposing Skynet-lite agent {AgentId}", Id);
            
            // Perform any cleanup specific to Skynet-lite agent
            
            await base.OnDisposeAsync();
        }
    }
}
