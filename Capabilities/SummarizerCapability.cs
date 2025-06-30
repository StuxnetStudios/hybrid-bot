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
    /// Summarization capability for the HybridBot.
    /// Provides intelligent content summarization with configurable output formats.
    /// </summary>
    public class SummarizerCapability : BaseRoleCapability
    {
        private readonly SkynetLiteConnector _skynetConnector;
        
        public override string CapabilityId => "summarizer";
        public override string Name => "Content Summarizer";
        
        private string _summaryLength = "medium";
        private bool _includeSentiment = false;
        private List<string> _focusKeywords = new();
        private string _outputFormat = "text";
        private int _maxSummaryWords = 150;
        
        // Summarization patterns
        private readonly Dictionary<string, string> _summaryPrompts = new()
        {
            ["short"] = "Provide a concise 1-2 sentence summary",
            ["medium"] = "Provide a comprehensive summary in 3-4 sentences", 
            ["long"] = "Provide a detailed summary with key points and context"
        };
        
        public SummarizerCapability(ILogger<SummarizerCapability> logger, SkynetLiteConnector skynetConnector) 
            : base(logger)
        {
            _skynetConnector = skynetConnector ?? throw new ArgumentNullException(nameof(skynetConnector));
            Tags = new List<string> { "summarization", "content", "analysis", "text-processing" };
            Priority = 70;
        }
        
        protected override async Task OnInitializeAsync(IDictionary<string, object> config)
        {
            // Load configuration from metadata
            _summaryLength = GetMetadataValue("summary_length", "medium").ToLower();
            _includeSentiment = GetMetadataValue("include_sentiment", false);
            _outputFormat = GetMetadataValue("output_format", "text").ToLower();
            
            if (config.TryGetValue("focus_keywords", out var keywordsValue) && keywordsValue is IEnumerable<string> keywords)
            {
                _focusKeywords = new List<string>(keywords);
            }
            
            // Set word count based on length
            _maxSummaryWords = _summaryLength switch
            {
                "short" => 50,
                "medium" => 150,
                "long" => 300,
                _ => 150
            };
            
            _logger.LogInformation($"Summarizer capability configured: Length={_summaryLength}, Format={_outputFormat}, Sentiment={_includeSentiment}");
        }
        
        public override bool CanHandle(BotContext context)
        {
            if (string.IsNullOrEmpty(context.Input))
                return false;
                
            // Check for summarization keywords
            var summaryKeywords = new[] { "summarize", "summary", "sum up", "brief", "overview", "recap", "tldr", "tl;dr" };
            
            return ContainsKeywords(context, summaryKeywords) ||
                   HasTags(context, "summarize", "summary") ||
                   (context.Input.Length > 500); // Auto-summarize long content
        }
        
        public override string GetInstructions()
        {
            return "Intelligently summarize text content, conversations, or documents with configurable length and format options.";
        }
        
        public override async Task<BotResponse> ExecuteAsync(BotContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(context.Input))
                {
                    return new BotResponse
                    {
                        Content = "I need some content to summarize. Please provide text, a conversation, or a document.",
                        IsComplete = false,
                        Metadata = new Dictionary<string, object> { ["source"] = Name, ["capability_id"] = CapabilityId }
                    };
                }
                
                // Build summarization prompt
                var prompt = BuildSummarizationPrompt(context.Input);
                
                // Call Skynet-lite for summarization
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(prompt);
                
                var responseMessages = await _skynetConnector.GetChatMessageContentsAsync(chatHistory);
                var summary = responseMessages.FirstOrDefault()?.Content ?? "Unable to generate summary.";
                
                // Format the response
                var formattedSummary = FormatSummary(summary, context);
                
                return new BotResponse
                {
                    Content = formattedSummary,
                    IsComplete = true,
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = Name,
                        ["capability_id"] = CapabilityId,
                        ["original_length"] = context.Input.Length,
                        ["summary_length"] = formattedSummary.Length,
                        ["compression_ratio"] = (double)formattedSummary.Length / context.Input.Length,
                        ["format"] = _outputFormat,
                        ["sentiment_included"] = _includeSentiment
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during summarization");
                return new BotResponse
                {
                    Content = "I encountered an error while summarizing the content. Please try again.",
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
        
        private string BuildSummarizationPrompt(string content)
        {
            var promptParts = new List<string>
            {
                "You are an expert content summarizer. Your task is to create a clear, accurate summary.",
                "",
                $"Content to summarize:",
                $"```",
                content,
                $"```",
                "",
                $"Requirements:",
                $"- {_summaryPrompts[_summaryLength]}",
                $"- Maximum {_maxSummaryWords} words",
                $"- Format: {_outputFormat}"
            };
            
            if (_focusKeywords.Any())
            {
                promptParts.Add($"- Focus on these keywords: {string.Join(", ", _focusKeywords)}");
            }
            
            if (_includeSentiment)
            {
                promptParts.Add("- Include overall sentiment analysis");
            }
            
            promptParts.AddRange(new[]
            {
                "",
                "Provide only the summary without additional commentary:"
            });
            
            return string.Join("\n", promptParts);
        }
        
        private string FormatSummary(string summary, BotContext context)
        {
            return _outputFormat switch
            {
                "bullet_points" => ConvertToBulletPoints(summary),
                "structured" => CreateStructuredSummary(summary, context),
                _ => summary.Trim()
            };
        }
        
        private string ConvertToBulletPoints(string summary)
        {
            var sentences = summary.Split('.', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim())
                                  .Where(s => !string.IsNullOrEmpty(s))
                                  .ToList();
            
            if (sentences.Count <= 1)
                return summary;
                
            return "**Summary:**\n" + string.Join("\n", sentences.Select(s => $"• {s}"));
        }
        
        private string CreateStructuredSummary(string summary, BotContext context)
        {
            var result = new List<string>
            {
                "## Summary",
                "",
                summary,
                "",
                "### Details",
                $"- **Original Length:** {context.Input.Length:N0} characters",
                $"- **Summary Length:** {summary.Length:N0} characters",
                $"- **Compression:** {(double)summary.Length / context.Input.Length:P1}"
            };
            
            if (_includeSentiment)
            {
                // Add sentiment placeholder - would need actual sentiment analysis
                result.AddRange(new[]
                {
                    "",
                    "### Sentiment",
                    "- **Overall Tone:** Neutral (analysis pending)"
                });
            }
            
            return string.Join("\n", result);
        }
        
        public override async Task<string> ProcessAsync(string input, BotContext context)
        {
            var response = await ExecuteAsync(context);
            return response.IsComplete ? response.Content : "Unable to summarize content.";
        }
    }
}
