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
    /// AI-powered role that uses Semantic Kernel for intelligent conversation and task execution.
    /// Demonstrates integration between the hybrid bot framework and Semantic Kernel.
    /// 
    /// Frontmatter Configuration:
    /// - ai_model: gpt-3.5-turbo|gpt-4|custom
    /// - max_tokens: number
    /// - temperature: 0.0-2.0
    /// - system_prompt: custom system prompt
    /// - enable_planning: true|false
    /// </summary>
    public class SemanticKernelRole : BaseBotRole
    {
        public override string RoleId => "semantic-kernel";
        public override string Name => "AI-Powered Assistant";
        
        private readonly Kernel _kernel;
        private readonly IChatCompletionService? _chatService;
        
        private string _aiModel = "gpt-3.5-turbo";
        private int _maxTokens = 1000;
        private double _temperature = 0.7;
        private string _systemPrompt = "You are a helpful AI assistant integrated into a hybrid bot system.";
        private bool _enablePlanning = true;
        
        public SemanticKernelRole(ILogger<SemanticKernelRole> logger, Kernel kernel) : base(logger)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            Tags = new List<string> { "ai", "semantic-kernel", "conversation", "planning", "intelligent" };
            Priority = 90; // High priority for AI-powered responses
            
            // Try to get chat completion service
            try
            {
                _chatService = _kernel.GetRequiredService<IChatCompletionService>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chat completion service not available. Role will operate in limited mode.");
            }
        }
        
        protected override async Task OnInitializeAsync(IDictionary<string, object> config)
        {
            // Load configuration from frontmatter
            if (config.TryGetValue("ai_model", out var modelValue))
            {
                _aiModel = modelValue.ToString() ?? _aiModel;
            }
            
            if (config.TryGetValue("max_tokens", out var tokensValue) && 
                int.TryParse(tokensValue.ToString(), out var maxTokens))
            {
                _maxTokens = maxTokens;
            }
            
            if (config.TryGetValue("temperature", out var tempValue) && 
                double.TryParse(tempValue.ToString(), out var temperature))
            {
                _temperature = Math.Clamp(temperature, 0.0, 2.0);
            }
            
            if (config.TryGetValue("system_prompt", out var promptValue))
            {
                _systemPrompt = promptValue.ToString() ?? _systemPrompt;
            }
            
            if (config.TryGetValue("enable_planning", out var planningValue) && 
                bool.TryParse(planningValue.ToString(), out var enablePlanning))
            {
                _enablePlanning = enablePlanning;
            }
            
            _logger.LogInformation("SemanticKernel role configured: model={Model}, tokens={Tokens}, temp={Temp}, planning={Planning}", 
                _aiModel, _maxTokens, _temperature, _enablePlanning);
            
            await Task.CompletedTask;
        }
        
        public override bool CanHandle(BotContext context)
        {
            if (string.IsNullOrEmpty(context.Input))
                return false;
            
            // This role can handle complex queries, planning requests, or when AI assistance is explicitly requested
            var input = context.Input.ToLower();
            var aiTriggers = new[] { "plan", "analyze", "explain", "help me understand", "what should i", "how can i", "ai", "assistant" };
            
            // Check for AI-specific requests
            var requiresAI = aiTriggers.Any(trigger => input.Contains(trigger));
            
            // Check if other roles have failed or if this is a complex multi-step request
            var isComplex = context.Input.Length > 100 || input.Contains("step") || input.Contains("process");
            
            // Always handle if chat service is available and it's a conversational request
            var isConversational = input.Contains("?") || input.StartsWith("can you") || input.StartsWith("could you");
            
            return _chatService != null && (requiresAI || isComplex || isConversational);
        }
        
        protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
        {
            if (_chatService == null)
            {
                return new BotResponse
                {
                    Content = "AI services are not available. Please configure OpenAI API key or another chat completion service.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object> { ["error"] = "no_chat_service" }
                };
            }
            
            try
            {
                // Prepare conversation context
                var conversationHistory = GetConversationHistory(context);
                var enhancedPrompt = BuildEnhancedPrompt(context, conversationHistory);
                
                // Use Semantic Kernel for response generation
                BotResponse response;
                
                if (_enablePlanning && ShouldUsePlanning(context))
                {
                    response = await ExecuteWithPlanningAsync(context, enhancedPrompt);
                }
                else
                {
                    response = await ExecuteDirectChatAsync(context, enhancedPrompt);
                }
                
                // Enhance response with metadata
                response.Metadata["ai_model"] = _aiModel;
                response.Metadata["tokens_used"] = "estimated"; // Would be actual token count in production
                response.Metadata["temperature"] = _temperature;
                response.Metadata["used_planning"] = _enablePlanning && ShouldUsePlanning(context);
                
                // Update conversation state
                UpdateConversationState(context, response);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Semantic Kernel role for request {RequestId}", context.RequestId);
                
                return new BotResponse
                {
                    Content = "I encountered an error while processing your request. Please try rephrasing or contact support if the issue persists.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object> 
                    { 
                        ["error"] = ex.Message,
                        ["error_type"] = ex.GetType().Name
                    }
                };
            }
        }
        
        private async Task<BotResponse> ExecuteDirectChatAsync(BotContext context, string enhancedPrompt)
        {
            var result = await _kernel.InvokePromptAsync(enhancedPrompt);
            
            return new BotResponse
            {
                Content = result.ToString(),
                IsComplete = true,
                ResponseType = "ai_chat",
                UpdatedState = new Dictionary<string, object>
                {
                    ["last_ai_interaction"] = DateTime.UtcNow,
                    ["ai_response_length"] = result.ToString().Length
                }
            };
        }
        
        private async Task<BotResponse> ExecuteWithPlanningAsync(BotContext context, string enhancedPrompt)
        {
            // Create a more sophisticated prompt for planning
            var planningPrompt = $"""
                {_systemPrompt}
                
                User Request: {context.Input}
                Context: {enhancedPrompt}
                
                Please create a step-by-step plan to address this request, then execute the plan.
                Format your response as:
                
                ## Plan
                1. [Step 1]
                2. [Step 2]
                ...
                
                ## Execution
                [Detailed response based on the plan]
                """;
                
            var result = await _kernel.InvokePromptAsync(planningPrompt);
            
            return new BotResponse
            {
                Content = result.ToString(),
                IsComplete = true,
                ResponseType = "ai_planned_response",
                UpdatedState = new Dictionary<string, object>
                {
                    ["last_ai_interaction"] = DateTime.UtcNow,
                    ["used_planning"] = true,
                    ["plan_generated"] = true
                },
                NextRoles = new List<string>() // Could suggest follow-up roles based on the plan
            };
        }
        
        private string GetConversationHistory(BotContext context)
        {
            if (context.State.TryGetValue("conversation_history", out var historyValue) && 
                historyValue is List<string> history)
            {
                // Get last few exchanges for context
                var recentHistory = history.TakeLast(6).ToList();
                return string.Join("\n", recentHistory);
            }
            
            return "No previous conversation history.";
        }
        
        private string BuildEnhancedPrompt(BotContext context, string conversationHistory)
        {
            return $"""
                {_systemPrompt}
                
                You are part of a hybrid bot system with multiple specialized roles. Your role is to provide intelligent, AI-powered responses.
                
                Current User Request: "{context.Input}"
                
                Conversation History:
                {conversationHistory}
                
                User Context:
                - Conversation ID: {context.ConversationId ?? "N/A"}
                - User ID: {context.UserId ?? "Anonymous"}
                - Timestamp: {context.Timestamp:yyyy-MM-dd HH:mm:ss} UTC
                
                Please provide a helpful, accurate, and contextually appropriate response. Consider the conversation history and be conversational but professional.
                """;
        }
        
        private bool ShouldUsePlanning(BotContext context)
        {
            if (!_enablePlanning) return false;
            
            var input = context.Input.ToLower();
            var planningKeywords = new[] { "plan", "steps", "how to", "process", "strategy", "approach", "method" };
            
            return planningKeywords.Any(keyword => input.Contains(keyword)) || context.Input.Length > 200;
        }
        
        private void UpdateConversationState(BotContext context, BotResponse response)
        {
            // Add this interaction to conversation history
            if (!context.State.ContainsKey("conversation_history"))
            {
                context.State["conversation_history"] = new List<string>();
            }
            
            if (context.State["conversation_history"] is List<string> history)
            {
                history.Add($"User: {context.Input}");
                history.Add($"AI Assistant: {response.Content}");
                
                // Keep only last 20 exchanges (40 entries)
                if (history.Count > 40)
                {
                    history.RemoveRange(0, history.Count - 40);
                }
            }
            
            // Update AI interaction statistics
            context.State["ai_interactions_count"] = context.State.ContainsKey("ai_interactions_count") ? 
                (int)context.State["ai_interactions_count"] + 1 : 1;
                
            context.State["last_ai_model_used"] = _aiModel;
        }
    }
}
