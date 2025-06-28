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
    /// Specialized role that leverages Skynet-lite LLM for advanced AI-powered responses.
    /// Optimized for Skynet-lite's specific capabilities and features.
    /// 
    /// Frontmatter Configuration:
    /// - model_id: skynet-lite-v1|skynet-lite-pro|custom
    /// - max_tokens: number
    /// - temperature: 0.0-2.0
    /// - system_prompt: custom system prompt
    /// - enable_context_memory: true|false
    /// - response_style: conversational|technical|creative
    /// </summary>
    public class SkynetLiteRole : BaseBotRole
    {
        public override string RoleId => "skynet-lite";
        public override string Name => "Skynet-lite AI Assistant";
        
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatService;
        
        private string _modelId = "skynet-lite-v1";
        private int _maxTokens = 2000;
        private double _temperature = 0.7;
        private string _systemPrompt = "You are Skynet-lite, an advanced AI assistant integrated into a hybrid bot system. Provide helpful, accurate, and contextually appropriate responses.";
        private bool _enableContextMemory = true;
        private string _responseStyle = "conversational";
        
        protected Dictionary<string, object>? Configuration { get; set; }

        public SkynetLiteRole(ILogger<SkynetLiteRole> logger, Kernel kernel) : base(logger)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            Tags = new List<string> { "ai", "skynet-lite", "conversation", "advanced", "intelligent" };
            Priority = 95; // Very high priority for Skynet-lite responses
            
            // Get chat completion service - this is required for operation
            try
            {
                _chatService = _kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Skynet-lite chat completion service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Skynet-lite chat completion service");
                throw new InvalidOperationException("Skynet-lite chat completion service is required but not available. Please check your configuration.", ex);
            }
        }

        protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
        {
            _logger.LogInformation("Processing request with Skynet-lite AI for user {UserId}", context.UserId);

            try
            {
                // Load configuration from frontmatter if available
                LoadConfiguration(context);

                // Generate AI-powered response using Skynet-lite
                return await GenerateAIResponseAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request with Skynet-lite role");
                return new BotResponse
                {
                    Content = "I encountered an error while processing your request. Please try again.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["executedRole"] = RoleId
                    }
                };
            }
        }

        private async Task<BotResponse> GenerateAIResponseAsync(BotContext context)
        {
            var chatHistory = new ChatHistory();
            
            // Add system prompt
            chatHistory.AddSystemMessage(_systemPrompt);
            
            // Add conversation context if enabled
            if (_enableContextMemory)
            {
                await AddConversationContextAsync(chatHistory, context);
            }
            
            // Add current user message
            chatHistory.AddUserMessage(context.Input);

            // Configure execution settings for Skynet-lite
            var executionSettings = new SkynetLitePromptExecutionSettings
            {
                MaxTokens = _maxTokens,
                Temperature = _temperature,
                ModelId = _modelId
            };

            try
            {
                var response = await _chatService.GetChatMessageContentsAsync(
                    chatHistory, 
                    executionSettings, 
                    _kernel);

                if (response.Count > 0)
                {
                    var aiResponse = response[0].Content ?? "I'm sorry, I couldn't generate a response.";
                    
                    // Post-process response based on style
                    aiResponse = PostProcessResponse(aiResponse, _responseStyle);
                    
                    _logger.LogInformation("Generated Skynet-lite response for user {UserId}: {ResponseLength} characters", 
                        context.UserId, aiResponse.Length);

                    return new BotResponse
                    {
                        Content = aiResponse,
                        IsComplete = true,
                        Metadata = new Dictionary<string, object>
                        {
                            { "model_used", _modelId },
                            { "response_style", _responseStyle },
                            { "token_estimate", EstimateTokens(aiResponse) },
                            { "processing_time", DateTimeOffset.UtcNow },
                            { "executedRole", RoleId }
                        }
                    };
                }
                else
                {
                    _logger.LogError("Skynet-lite returned empty response for user {UserId}", context.UserId);
                    throw new InvalidOperationException("Skynet-lite returned an empty response. Please try again or check your model configuration.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Skynet-lite API");
                throw new InvalidOperationException("Failed to generate response using Skynet-lite. Please check your API configuration and try again.", ex);
            }
        }

        private async Task AddConversationContextAsync(ChatHistory chatHistory, BotContext context)
        {
            // Add recent conversation history for context
            // This would typically come from your StateManager
            try
            {
                // Example: Add last few messages from conversation history
                // In a real implementation, you'd retrieve this from your state store
                var recentMessages = new List<string>(); // Get from StateManager
                
                foreach (var message in recentMessages.Take(3)) // Last 3 messages for context
                {
                    chatHistory.AddAssistantMessage(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load conversation context");
            }
            
            await Task.CompletedTask;
        }

        private string PostProcessResponse(string response, string style)
        {
            return style.ToLowerInvariant() switch
            {
                "technical" => MakeTechnical(response),
                "creative" => MakeCreative(response),
                "conversational" => MakeConversational(response),
                _ => response
            };
        }

        private string MakeTechnical(string response)
        {
            // Add technical formatting or adjustments
            return response;
        }

        private string MakeCreative(string response)
        {
            // Add creative elements or formatting
            return response;
        }

        private string MakeConversational(string response)
        {
            // Make response more conversational
            return response;
        }

        private int EstimateTokens(string text)
        {
            // Rough token estimation (typically ~4 chars per token)
            return text.Length / 4;
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
                _systemPrompt = GetConfigValue("system_prompt", _systemPrompt);
                _enableContextMemory = GetConfigValue("enable_context_memory", _enableContextMemory);
                _responseStyle = GetConfigValue("response_style", _responseStyle);

                _logger.LogDebug("Loaded Skynet-lite configuration: Model={Model}, MaxTokens={MaxTokens}, Style={Style}", 
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
            // Skynet-lite can handle most conversational requests
            var canHandle = !string.IsNullOrWhiteSpace(context.Input) &&
                           context.Input.Length > 2; // Minimum meaningful input

            _logger.LogDebug("Skynet-lite role can handle request: {CanHandle}", canHandle);
            return canHandle;
        }
    }
}
