using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HybridBot.Core;

namespace HybridBot.Roles
{
    /// <summary>
    /// Role responsible for summarizing conversations, text, or context.
    /// Supports configurable summarization strategies and output formats.
    /// 
    /// Frontmatter Configuration:
    /// - summary_length: short|medium|long
    /// - include_sentiment: true|false
    /// - focus_keywords: list of keywords to emphasize
    /// - output_format: text|bullet_points|structured
    /// </summary>
    public class SummarizerRole : BaseBotRole
    {
        public override string RoleId => "summarizer";
        public override string Name => "Content Summarizer";
        
        private string _summaryLength = "medium";
        private bool _includeSentiment = false;
        private List<string> _focusKeywords = new();
        private string _outputFormat = "text";
        private int _maxSummaryWords = 150;
        
        public SummarizerRole(ILogger<SummarizerRole> logger) : base(logger)
        {
            Tags = new List<string> { "summarization", "content", "analysis", "text-processing" };
            Priority = 70;
        }
        
        protected override async Task OnInitializeAsync(IDictionary<string, object> config)
        {
            // Load configuration from frontmatter
            if (config.TryGetValue("summary_length", out var lengthValue))
            {
                _summaryLength = lengthValue.ToString().ToLower();
            }
            
            if (config.TryGetValue("include_sentiment", out var sentimentValue) && 
                bool.TryParse(sentimentValue.ToString(), out var includeSentiment))
            {
                _includeSentiment = includeSentiment;
            }
            
            if (config.TryGetValue("focus_keywords", out var keywordsValue) && 
                keywordsValue is IEnumerable<object> keywords)
            {
                _focusKeywords = keywords.Select(k => k.ToString()).ToList();
            }
            
            if (config.TryGetValue("output_format", out var formatValue))
            {
                _outputFormat = formatValue.ToString().ToLower();
            }
            
            // Set max words based on summary length
            _maxSummaryWords = _summaryLength switch
            {
                "short" => 75,
                "medium" => 150,
                "long" => 300,
                _ => 150
            };
            
            _logger.LogInformation("Summarizer configured: length={Length}, sentiment={Sentiment}, format={Format}", 
                _summaryLength, _includeSentiment, _outputFormat);
            
            await Task.CompletedTask;
        }
        
        public override bool CanHandle(BotContext context)
        {
            if (string.IsNullOrEmpty(context.Input))
                return false;
            
            // Check for summarization keywords or explicit request
            var input = context.Input.ToLower();
            var triggerWords = new[] { "summarize", "summary", "tldr", "brief", "overview", "key points" };
            
            return triggerWords.Any(word => input.Contains(word)) ||
                   context.Input.Length > 500 || // Auto-summarize long content
                   context.State.ContainsKey("request_summary");
        }
        
        protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
        {
            var content = ExtractContentToSummarize(context);
            
            if (string.IsNullOrEmpty(content))
            {
                return new BotResponse
                {
                    Content = "No content found to summarize.",
                    IsComplete = false
                };
            }
            
            var summary = await GenerateSummaryAsync(content, context);
            
            var response = new BotResponse
            {
                Content = FormatSummary(summary, context),
                IsComplete = true,
                ResponseType = "summary",
                UpdatedState = new Dictionary<string, object>
                {
                    ["last_summary"] = summary,
                    ["summary_timestamp"] = DateTime.UtcNow,
                    ["summarized_length"] = content.Length
                }
            };
            
            // Add metadata about the summarization
            response.Metadata["original_length"] = content.Length;
            response.Metadata["summary_length"] = summary.Length;
            response.Metadata["compression_ratio"] = (double)summary.Length / content.Length;
            response.Metadata["focus_keywords"] = _focusKeywords;
            
            return response;
        }
        
        private string ExtractContentToSummarize(BotContext context)
        {
            // Extract content from various sources
            var content = context.Input;
            
            // Check if there's conversation history to summarize
            if (context.State.TryGetValue("conversation_history", out var historyValue) && 
                historyValue is List<string> history)
            {
                content = string.Join("\n", history);
            }
            
            // Check for specific content in session data
            if (context.SessionData.TryGetValue("content_to_summarize", out var sessionContent))
            {
                content = sessionContent.ToString();
            }
            
            return content;
        }
        
        private async Task<string> GenerateSummaryAsync(string content, BotContext context)
        {
            // This is a simplified summarization algorithm
            // In a real implementation, you might use AI services, NLP libraries, or external APIs
            
            var sentences = SplitIntoSentences(content);
            var importantSentences = ExtractImportantSentences(sentences);
            
            // Apply focus keywords if specified
            if (_focusKeywords.Any())
            {
                importantSentences = PrioritizeSentencesByKeywords(importantSentences);
            }
            
            // Limit to target word count
            var summary = TruncateToWordLimit(string.Join(" ", importantSentences), _maxSummaryWords);
            
            // Add sentiment analysis if requested
            if (_includeSentiment)
            {
                var sentiment = AnalyzeSentiment(content);
                summary += $"\n\nOverall sentiment: {sentiment}";
            }
            
            return summary;
        }
        
        private List<string> SplitIntoSentences(string content)
        {
            // Simple sentence splitting (in real implementation, use proper NLP library)
            return content.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => !string.IsNullOrEmpty(s))
                         .ToList();
        }
        
        private List<string> ExtractImportantSentences(List<string> sentences)
        {
            // Simple importance scoring based on length and position
            var scored = sentences.Select((sentence, index) => new
            {
                Sentence = sentence,
                Score = CalculateSentenceScore(sentence, index, sentences.Count)
            })
            .OrderByDescending(s => s.Score)
            .Take(Math.Max(3, sentences.Count / 3)) // Take top third, minimum 3
            .Select(s => s.Sentence)
            .ToList();
            
            return scored;
        }
        
        private double CalculateSentenceScore(string sentence, int position, int totalSentences)
        {
            double score = 0;
            
            // Position-based scoring (first and last sentences are often important)
            if (position == 0 || position == totalSentences - 1)
                score += 2;
            
            // Length-based scoring (avoid very short or very long sentences)
            var words = sentence.Split(' ').Length;
            if (words >= 10 && words <= 30)
                score += 1;
            
            // Keyword-based scoring
            foreach (var keyword in _focusKeywords)
            {
                if (sentence.ToLower().Contains(keyword.ToLower()))
                    score += 3;
            }
            
            return score;
        }
        
        private List<string> PrioritizeSentencesByKeywords(List<string> sentences)
        {
            return sentences.OrderByDescending(sentence => 
                _focusKeywords.Count(keyword => 
                    sentence.ToLower().Contains(keyword.ToLower())))
                .ToList();
        }
        
        private string TruncateToWordLimit(string text, int maxWords)
        {
            var words = text.Split(' ');
            if (words.Length <= maxWords)
                return text;
            
            return string.Join(" ", words.Take(maxWords)) + "...";
        }
        
        private string AnalyzeSentiment(string content)
        {
            // Simplified sentiment analysis
            var positiveWords = new[] { "good", "great", "excellent", "positive", "happy", "success" };
            var negativeWords = new[] { "bad", "terrible", "negative", "sad", "failure", "problem" };
            
            var words = content.ToLower().Split(' ');
            var positiveCount = words.Count(w => positiveWords.Contains(w));
            var negativeCount = words.Count(w => negativeWords.Contains(w));
            
            if (positiveCount > negativeCount)
                return "Positive";
            else if (negativeCount > positiveCount)
                return "Negative";
            else
                return "Neutral";
        }
        
        private string FormatSummary(string summary, BotContext context)
        {
            return _outputFormat switch
            {
                "bullet_points" => FormatAsBulletPoints(summary),
                "structured" => FormatAsStructured(summary, context),
                _ => $"**Summary:**\n\n{summary}"
            };
        }
        
        private string FormatAsBulletPoints(string summary)
        {
            var sentences = SplitIntoSentences(summary);
            var bullets = sentences.Select(s => $"• {s}").ToList();
            return $"**Key Points:**\n\n{string.Join("\n", bullets)}";
        }
        
        private string FormatAsStructured(string summary, BotContext context)
        {
            return $"""
                **Summary Report**
                
                **Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC
                **Length:** {_summaryLength}
                **Format:** Structured
                
                **Content:**
                {summary}
                
                **Context:** {context.ConversationId ?? "N/A"}
                """;
        }
    }
}
