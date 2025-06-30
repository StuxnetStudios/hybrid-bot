using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using HybridBot.Core;

namespace HybridBot.Capabilities
{
    /// <summary>
    /// General conversation and response capability for the HybridBot.
    /// Handles general queries, conversations, and provides contextual responses.
    /// </summary>
    public class ResponderCapability : BaseRoleCapability
    {
        private readonly SkynetLiteConnector _skynetConnector;
        
        public override string CapabilityId => "responder";
        public override string Name => "General Responder";
        
        private string _responseStyle = "helpful";
        private bool _includeContext = true;
        private int _maxResponseLength = 500;
        private List<string> _personalityTraits = new();
        
        // Response style templates
        private readonly Dictionary<string, string> _stylePrompts = new()
        {
            ["helpful"] = "Be helpful, informative, and friendly in your responses",
            ["concise"] = "Provide brief, direct answers without unnecessary elaboration",
            ["detailed"] = "Give comprehensive, thorough explanations with examples",
            ["casual"] = "Use a relaxed, conversational tone",
            ["professional"] = "Maintain a formal, business-appropriate tone"
        };
        
        public ResponderCapability(ILogger<ResponderCapability> logger, SkynetLiteConnector skynetConnector) 
            : base(logger)
        {
            _skynetConnector = skynetConnector ?? throw new ArgumentNullException(nameof(skynetConnector));
            Tags = new List<string> { "conversation", "general", "response", "chat" };
            Priority = 30; // Lower priority - fallback for unhandled requests
        }
        
        protected override async Task OnInitializeAsync(IDictionary<string, object> config)
        {
            _responseStyle = GetMetadataValue("response_style", "helpful").ToLower();
            _includeContext = GetMetadataValue("include_context", true);
            _maxResponseLength = GetMetadataValue("max_response_length", 500);
            
            if (config.TryGetValue("personality_traits", out var traitsValue) && traitsValue is IEnumerable<string> traits)
            {
                _personalityTraits = new List<string>(traits);
            }
            
            _logger.LogInformation($"Responder capability configured: Style={_responseStyle}, MaxLength={_maxResponseLength}");
        }
        
        public override bool CanHandle(BotContext context)
        {
            // Responder is a fallback capability - it can handle any text input
            // But it should have lower priority than specialized capabilities
            return !string.IsNullOrEmpty(context.Input);
        }
        
        public override string GetInstructions()
        {
            return "Handle general conversations, questions, and provide helpful contextual responses on any topic.";
        }
        
        public override async Task<BotResponse> ExecuteAsync(BotContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(context.Input))
                {
                    return new BotResponse
                    {
                        Content = "Hello! How can I help you today?",
                        IsComplete = true,
                        Metadata = new Dictionary<string, object>
                        {
                            ["source"] = Name,
                            ["capability_id"] = CapabilityId
                        }
                    };
                }
                
                // Build conversation prompt
                var prompt = BuildConversationPrompt(context);
                
                // Call Skynet-lite for response generation using ChatCompletion interface
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(prompt);
                chatHistory.AddUserMessage(context.Input);
                
                var responseMessages = await _skynetConnector.GetChatMessageContentsAsync(chatHistory);
                var response = responseMessages.FirstOrDefault()?.Content ?? "I'm not sure how to respond.";
                
                // Apply response formatting and length limits
                var formattedResponse = FormatResponse(response);
                
                return new BotResponse
                {
                    Content = formattedResponse,
                    IsComplete = true,
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = Name,
                        ["capability_id"] = CapabilityId,
                        ["response_style"] = _responseStyle,
                        ["context_included"] = _includeContext,
                        ["response_length"] = formattedResponse.Length,
                        ["personality_traits"] = _personalityTraits.ToArray()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response");
                return new BotResponse
                {
                    Content = "I apologize, but I encountered an error while processing your request. Please try again.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = Name,
                        ["capability_id"] = CapabilityId,
                        ["error"] = ex.Message
                    }
                };
            }
        }
        
        private string BuildConversationPrompt(BotContext context)
        {
            var promptParts = new List<string>
            {
                "You are HybridBot, an intelligent AI assistant.",
                "",
                $"Response Guidelines:",
                $"- {_stylePrompts.GetValueOrDefault(_responseStyle, _stylePrompts["helpful"])}",
                $"- Keep responses under {_maxResponseLength} characters",
                $"- Be accurate and helpful"
            };
            
            if (_personalityTraits.Any())
            {
                promptParts.Add($"- Personality traits: {string.Join(", ", _personalityTraits)}");
            }
            
            // Remove or simplify the context inclusion for now since BotContext doesn't have ChatHistory
            // TODO: Add ChatHistory to BotContext if needed
            
            promptParts.AddRange(new[]
            {
                "",
                "Current user message:",
                context.Input,
                "",
                "Your response:"
            });
            
            return string.Join("\n", promptParts);
        }
        
        private string FormatResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return "I'm not sure how to respond to that. Could you please rephrase your question?";
            
            // Apply length limits
            if (response.Length > _maxResponseLength)
            {
                var truncated = response.Substring(0, _maxResponseLength);
                var lastSpace = truncated.LastIndexOf(' ');
                if (lastSpace > _maxResponseLength * 0.8) // Only truncate at word boundary if it's not too far back
                {
                    truncated = truncated.Substring(0, lastSpace);
                }
                response = truncated + "...";
            }
            
            return response.Trim();
        }
        
        public override async Task<string> ProcessAsync(string input, BotContext context)
        {
            context.Input = input;
            var response = await ExecuteAsync(context);
            return response.IsComplete ? response.Content : "I couldn't process that request.";
        }
        
        /// <summary>
        /// Update personality traits dynamically
        /// </summary>
        public void UpdatePersonalityTraits(IEnumerable<string> traits)
        {
            _personalityTraits = new List<string>(traits ?? Enumerable.Empty<string>());
            _logger.LogInformation($"Updated personality traits: {string.Join(", ", _personalityTraits)}");
        }
        
        /// <summary>
        /// Change response style dynamically
        /// </summary>
        public void SetResponseStyle(string style)
        {
            if (_stylePrompts.ContainsKey(style.ToLower()))
            {
                _responseStyle = style.ToLower();
                _logger.LogInformation($"Updated response style to: {_responseStyle}");
            }
            else
            {
                _logger.LogWarning($"Unknown response style: {style}. Available styles: {string.Join(", ", _stylePrompts.Keys)}");
            }
        }
    }
}
