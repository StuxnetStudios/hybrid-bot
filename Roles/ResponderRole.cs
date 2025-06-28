using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HybridBot.Core;

namespace HybridBot.Roles
{
    /// <summary>
    /// Role responsible for generating contextual responses to user inputs.
    /// Supports multiple response strategies, tone adaptation, and conversation flow management.
    /// 
    /// Frontmatter Configuration:
    /// - response_style: formal|casual|technical|friendly
    /// - max_response_length: number of words
    /// - include_followup: true|false
    /// - knowledge_domains: list of domains this responder handles
    /// - fallback_enabled: true|false
    /// </summary>
    public class ResponderRole : BaseBotRole
    {
        public override string RoleId => "responder";
        public override string Name => "Contextual Responder";
        
        private string _responseStyle = "friendly";
        private int _maxResponseLength = 200;
        private bool _includeFollowup = true;
        private List<string> _knowledgeDomains = new();
        private bool _fallbackEnabled = true;
        private Dictionary<string, List<string>> _responseTemplates = new();
        
        public ResponderRole(ILogger<ResponderRole> logger) : base(logger)
        {
            Tags = new List<string> { "response", "conversation", "interaction", "dialogue" };
            Priority = 60;
            InitializeResponseTemplates();
        }
        
        protected override async Task OnInitializeAsync(IDictionary<string, object> config)
        {
            // Load configuration from frontmatter
            if (config.TryGetValue("response_style", out var styleValue))
            {
                _responseStyle = styleValue.ToString().ToLower();
            }
            
            if (config.TryGetValue("max_response_length", out var lengthValue) && 
                int.TryParse(lengthValue.ToString(), out var maxLength))
            {
                _maxResponseLength = maxLength;
            }
            
            if (config.TryGetValue("include_followup", out var followupValue) && 
                bool.TryParse(followupValue.ToString(), out var includeFollowup))
            {
                _includeFollowup = includeFollowup;
            }
            
            if (config.TryGetValue("knowledge_domains", out var domainsValue) && 
                domainsValue is IEnumerable<object> domains)
            {
                _knowledgeDomains = domains.Select(d => d.ToString()).ToList();
            }
            
            if (config.TryGetValue("fallback_enabled", out var fallbackValue) && 
                bool.TryParse(fallbackValue.ToString(), out var fallbackEnabled))
            {
                _fallbackEnabled = fallbackEnabled;
            }
            
            _logger.LogInformation("Responder configured: style={Style}, domains={Domains}, fallback={Fallback}", 
                _responseStyle, string.Join(",", _knowledgeDomains), _fallbackEnabled);
            
            await Task.CompletedTask;
        }
        
        public override bool CanHandle(BotContext context)
        {
            if (string.IsNullOrEmpty(context.Input))
                return false;
            
            // If specific knowledge domains are configured, check if input matches
            if (_knowledgeDomains.Any())
            {
                var input = context.Input.ToLower();
                var matchesDomain = _knowledgeDomains.Any(domain => 
                    input.Contains(domain.ToLower()));
                
                if (!matchesDomain && !_fallbackEnabled)
                    return false;
            }
            
            // Check for question patterns or conversational cues
            var inputText = context.Input.ToLower();
            var questionIndicators = new[] { "?", "what", "how", "when", "where", "why", "can you", "could you", "please" };
            var hasQuestionPattern = questionIndicators.Any(indicator => inputText.Contains(indicator));
            
            // Always handle if it's a direct question or if fallback is enabled
            return hasQuestionPattern || _fallbackEnabled;
        }
        
        protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
        {
            var intentCategory = ClassifyIntent(context.Input);
            var conversationTone = DetermineConversationTone(context);
            
            var responseContent = await GenerateResponseAsync(context, intentCategory, conversationTone);
            
            var response = new BotResponse
            {
                Content = responseContent,
                IsComplete = true,
                ResponseType = "conversational",
                UpdatedState = new Dictionary<string, object>
                {
                    ["last_response_intent"] = intentCategory,
                    ["conversation_tone"] = conversationTone,
                    ["response_timestamp"] = DateTime.UtcNow,
                    ["response_length"] = responseContent.Length
                }
            };
            
            // Add follow-up suggestions if enabled
            if (_includeFollowup)
            {
                var followups = GenerateFollowupSuggestions(intentCategory, context);
                if (followups.Any())
                {
                    response.UpdatedState["suggested_followups"] = followups;
                    response.Metadata["followup_suggestions"] = followups;
                }
            }
            
            // Update conversation history
            UpdateConversationHistory(context, responseContent);
            
            response.Metadata["intent_category"] = intentCategory;
            response.Metadata["conversation_tone"] = conversationTone;
            response.Metadata["response_style"] = _responseStyle;
            
            return response;
        }
        
        private string ClassifyIntent(string input)
        {
            var input_lower = input.ToLower();
            
            // Simple intent classification
            if (input_lower.Contains("hello") || input_lower.Contains("hi") || input_lower.Contains("greet"))
                return "greeting";
            
            if (input_lower.Contains("bye") || input_lower.Contains("goodbye") || input_lower.Contains("exit"))
                return "farewell";
            
            if (input_lower.Contains("help") || input_lower.Contains("assist") || input_lower.Contains("support"))
                return "help_request";
            
            if (input_lower.Contains("what") || input_lower.Contains("define") || input_lower.Contains("explain"))
                return "information_request";
            
            if (input_lower.Contains("how") || input_lower.Contains("step") || input_lower.Contains("guide"))
                return "instruction_request";
            
            if (input_lower.Contains("thank") || input_lower.Contains("appreciate"))
                return "gratitude";
            
            return "general_inquiry";
        }
        
        private string DetermineConversationTone(BotContext context)
        {
            // Analyze conversation history and current input for tone
            var input = context.Input.ToLower();
            
            if (input.Contains("urgent") || input.Contains("immediate") || input.Contains("asap"))
                return "urgent";
            
            if (input.Contains("please") || input.Contains("thank") || input.Contains("appreciate"))
                return "polite";
            
            if (input.Contains("confused") || input.Contains("don't understand") || input.Contains("unclear"))
                return "confused";
            
            if (context.State.TryGetValue("conversation_tone", out var previousTone))
                return previousTone.ToString();
            
            return "neutral";
        }
        
        private async Task<string> GenerateResponseAsync(BotContext context, string intentCategory, string conversationTone)
        {
            var baseResponse = GenerateBaseResponse(intentCategory, context);
            var styledResponse = ApplyResponseStyle(baseResponse, conversationTone);
            var finalResponse = await EnhanceWithContext(styledResponse, context);
            
            return TruncateToMaxLength(finalResponse);
        }
        
        private string GenerateBaseResponse(string intentCategory, BotContext context)
        {
            if (_responseTemplates.TryGetValue(intentCategory, out var templates))
            {
                var random = new Random();
                var template = templates[random.Next(templates.Count)];
                return PersonalizeTemplate(template, context);
            }
            
            // Fallback response generation
            return intentCategory switch
            {
                "greeting" => "Hello! How can I help you today?",
                "farewell" => "Goodbye! Feel free to reach out if you need anything else.",
                "help_request" => "I'm here to help! Could you provide more details about what you need assistance with?",
                "information_request" => "I'd be happy to provide information. Could you be more specific about what you'd like to know?",
                "instruction_request" => "I can guide you through that process. Let me break it down step by step.",
                "gratitude" => "You're very welcome! I'm glad I could help.",
                _ => "I understand you're looking for assistance. Could you provide more details so I can better help you?"
            };
        }
        
        private string ApplyResponseStyle(string baseResponse, string conversationTone)
        {
            return (_responseStyle, conversationTone) switch
            {
                ("formal", _) => MakeFormal(baseResponse),
                ("casual", _) => MakeCasual(baseResponse),
                ("technical", _) => MakeTechnical(baseResponse),
                (_, "urgent") => AddUrgency(baseResponse),
                (_, "confused") => MakeReassuring(baseResponse),
                _ => baseResponse
            };
        }
        
        private string MakeFormal(string response)
        {
            return response.Replace("I'm", "I am")
                          .Replace("you're", "you are")
                          .Replace("can't", "cannot")
                          .Replace("won't", "will not");
        }
        
        private string MakeCasual(string response)
        {
            var casualPhrases = new Dictionary<string, string>
            {
                { "How can I help you", "What can I do for you" },
                { "I would be happy to", "I'd love to" },
                { "Please provide", "Just let me know" }
            };
            
            foreach (var phrase in casualPhrases)
            {
                response = response.Replace(phrase.Key, phrase.Value);
            }
            
            return response;
        }
        
        private string MakeTechnical(string response)
        {
            return response + " I can provide detailed technical specifications and implementation details if needed.";
        }
        
        private string AddUrgency(string response)
        {
            return "I understand this is urgent. " + response + " Let me prioritize this for you.";
        }
        
        private string MakeReassuring(string response)
        {
            return "No worries! " + response + " Take your time, and I'll help clarify anything that's unclear.";
        }
        
        private async Task<string> EnhanceWithContext(string response, BotContext context)
        {
            // Add contextual information if available
            if (context.State.TryGetValue("user_name", out var userName))
            {
                response = response.Replace("Hello!", $"Hello, {userName}!");
            }
            
            // Add conversation continuity
            if (context.State.TryGetValue("last_topic", out var lastTopic))
            {
                response += $" Following up on our previous discussion about {lastTopic}.";
            }
            
            return response;
        }
        
        private string PersonalizeTemplate(string template, BotContext context)
        {
            var personalized = template;
            
            if (context.State.TryGetValue("user_name", out var userName))
            {
                personalized = personalized.Replace("{user_name}", userName.ToString());
            }
            
            if (context.State.TryGetValue("last_interaction", out var lastInteraction))
            {
                personalized = personalized.Replace("{last_interaction}", lastInteraction.ToString());
            }
            
            return personalized;
        }
        
        private List<string> GenerateFollowupSuggestions(string intentCategory, BotContext context)
        {
            return intentCategory switch
            {
                "greeting" => new List<string> { "What can I help you with?", "Tell me about your project", "Any specific questions?" },
                "information_request" => new List<string> { "Would you like more details?", "Any other questions?", "Need examples?" },
                "instruction_request" => new List<string> { "Need clarification on any step?", "Ready for the next part?", "Any questions so far?" },
                "help_request" => new List<string> { "What else can I help with?", "Need additional resources?", "Want to explore alternatives?" },
                _ => new List<string> { "Anything else I can help with?", "Any follow-up questions?", "Need more information?" }
            };
        }
        
        private void UpdateConversationHistory(BotContext context, string response)
        {
            if (!context.State.ContainsKey("conversation_history"))
            {
                context.State["conversation_history"] = new List<string>();
            }
            
            if (context.State["conversation_history"] is List<string> history)
            {
                history.Add($"Bot: {response}");
                
                // Keep only last 10 exchanges
                if (history.Count > 20)
                {
                    history.RemoveRange(0, history.Count - 20);
                }
            }
        }
        
        private string TruncateToMaxLength(string response)
        {
            var words = response.Split(' ');
            if (words.Length <= _maxResponseLength)
                return response;
            
            return string.Join(" ", words.Take(_maxResponseLength)) + "...";
        }
        
        private void InitializeResponseTemplates()
        {
            _responseTemplates = new Dictionary<string, List<string>>
            {
                ["greeting"] = new List<string>
                {
                    "Hello! Great to see you here. How can I assist you today?",
                    "Hi there! I'm ready to help. What would you like to work on?",
                    "Welcome! I'm here to support you. What's on your mind?"
                },
                ["help_request"] = new List<string>
                {
                    "I'm here to help! Could you share more details about what you need?",
                    "Absolutely! I'd love to assist. What specific area would you like help with?",
                    "Of course! Let me know what you're working on and I'll do my best to help."
                },
                ["information_request"] = new List<string>
                {
                    "I'd be happy to provide that information. Could you be more specific?",
                    "Great question! Let me help you understand that better.",
                    "I can definitely explain that. What aspect would you like to focus on?"
                }
            };
        }
    }
}
