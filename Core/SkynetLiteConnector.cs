using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;

namespace HybridBot.Core
{
    /// <summary>
    /// Custom Semantic Kernel connector for Skynet-lite LLM service.
    /// Implements the IChatCompletionService interface to integrate with Semantic Kernel.
    /// </summary>
    public class SkynetLiteConnector : IChatCompletionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SkynetLiteConnector> _logger;
        private readonly SkynetLiteConfig _config;

        public SkynetLiteConnector(
            HttpClient httpClient,
            ILogger<SkynetLiteConnector> logger,
            SkynetLiteConfig config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Configure HttpClient
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            }
        }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
        {
            { AIServiceExtensions.ModelIdKey, _config.ModelId }
        };

        public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Sending chat completion request to Skynet-lite");

            // Handle mock mode when no real API is available
            if (_config.MockMode || string.IsNullOrEmpty(_config.ApiKey))
            {
                return await GetMockResponseAsync(chatHistory, cancellationToken);
            }

            try
            {
                var request = CreateChatRequest(chatHistory, executionSettings);
                var jsonRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_config.ChatEndpoint, httpContent, cancellationToken);

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var chatResponse = JsonSerializer.Deserialize<SkynetLiteChatResponse>(responseContent, 
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                if (chatResponse?.Choices?.Count > 0)
                {
                    var choice = chatResponse.Choices[0];
                    var messageContent = new ChatMessageContent(
                        role: AuthorRole.Assistant,
                        content: choice.Message?.Content ?? string.Empty,
                        modelId: _config.ModelId);

                    return new List<ChatMessageContent> { messageContent };
                }

                _logger.LogWarning("No choices returned from Skynet-lite API");
                return new List<ChatMessageContent>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error calling Skynet-lite API, falling back to mock mode");
                return await GetMockResponseAsync(chatHistory, cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Skynet-lite API response");
                throw new InvalidOperationException("Invalid response format from Skynet-lite service", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Skynet-lite API");
                throw;
            }
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Streaming chat completion not implemented for Skynet-lite, falling back to regular completion");
            
            // For now, fall back to non-streaming and yield the result
            var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            
            foreach (var result in results)
            {
                yield return new StreamingChatMessageContent(
                    role: result.Role,
                    content: result.Content,
                    modelId: result.ModelId);
            }
        }

        private SkynetLiteChatRequest CreateChatRequest(ChatHistory chatHistory, PromptExecutionSettings? executionSettings)
        {
            var messages = new List<SkynetLiteMessage>();
            
            foreach (var message in chatHistory)
            {
                messages.Add(new SkynetLiteMessage
                {
                    Role = message.Role.Label.ToLowerInvariant(),
                    Content = message.Content ?? string.Empty
                });
            }

            var request = new SkynetLiteChatRequest
            {
                Model = _config.ModelId,
                Messages = messages,
                MaxTokens = _config.MaxTokens,
                Temperature = _config.Temperature,
                TopP = _config.TopP,
                FrequencyPenalty = _config.FrequencyPenalty,
                PresencePenalty = _config.PresencePenalty
            };

            // Apply execution settings if provided
            if (executionSettings is SkynetLitePromptExecutionSettings skynetSettings)
            {
                request.MaxTokens = skynetSettings.MaxTokens ?? request.MaxTokens;
                request.Temperature = skynetSettings.Temperature ?? request.Temperature;
                request.TopP = skynetSettings.TopP ?? request.TopP;
                request.FrequencyPenalty = skynetSettings.FrequencyPenalty ?? request.FrequencyPenalty;
                request.PresencePenalty = skynetSettings.PresencePenalty ?? request.PresencePenalty;
            }

            return request;
        }

        /// <summary>
        /// Generates mock responses for testing when no real API is available
        /// </summary>
        private async Task<IReadOnlyList<ChatMessageContent>> GetMockResponseAsync(
            ChatHistory chatHistory, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Using mock mode for Skynet-lite response");
            
            // Simulate some processing delay
            await Task.Delay(100, cancellationToken);
            
            // Get the last user message for context
            var lastUserMessage = chatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
            var userInput = lastUserMessage?.Content ?? "Hello";
            
            // Generate contextual mock responses
            string mockResponse = GenerateMockResponse(userInput);
            
            var messageContent = new ChatMessageContent(
                role: AuthorRole.Assistant,
                content: mockResponse,
                modelId: _config.ModelId + "-mock");

            return new List<ChatMessageContent> { messageContent };
        }
        
        /// <summary>
        /// Generates contextual mock responses based on user input
        /// </summary>
        private string GenerateMockResponse(string userInput)
        {
            var input = userInput.ToLowerInvariant();
            
            // Summarization requests
            if (input.Contains("summarize") || input.Contains("summary"))
            {
                return "Here's a summary: The main points include key concepts, important details, and relevant conclusions that capture the essence of the content in a concise format.";
            }
            
            // Help requests
            if (input.Contains("help") || input.Contains("how") || input.Contains("what"))
            {
                return "I'm here to help! I'm a HybridBot with multiple capabilities including content summarization, general conversation, and more. What would you like assistance with?";
            }
            
            // Greetings
            if (input.Contains("hello") || input.Contains("hi") || input.Contains("greeting"))
            {
                return "Hello! Welcome to the HybridBot system. I'm an AI agent with multiple specialized capabilities. How can I assist you today?";
            }
            
            // Technical questions
            if (input.Contains("semantic kernel") || input.Contains("agent") || input.Contains("architecture"))
            {
                return "Semantic Kernel provides a powerful framework for building AI agents. The agent-based architecture allows for modular, composable AI capabilities that can be combined to create sophisticated AI systems.";
            }
            
            // Default response
            return $"Thank you for your message: '{userInput}'. I'm a mock Skynet-lite service running in demo mode. In a real implementation, I would provide more sophisticated responses using actual AI models.";
        }
    }

    /// <summary>
    /// Configuration class for Skynet-lite LLM service
    /// </summary>
    public class SkynetLiteConfig
    {
        public string BaseUrl { get; set; } = "https://api.skynet-lite.com";
        public string ChatEndpoint { get; set; } = "/v1/chat/completions";
        public string ModelId { get; set; } = "skynet-lite-v1";
        public string? ApiKey { get; set; }
        public int MaxTokens { get; set; } = 1000;
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 1.0;
        public double FrequencyPenalty { get; set; } = 0.0;
        public double PresencePenalty { get; set; } = 0.0;
        public int TimeoutSeconds { get; set; } = 30;
        public bool MockMode { get; set; } = false;
    }

    /// <summary>
    /// Custom prompt execution settings for Skynet-lite
    /// </summary>
    public class SkynetLitePromptExecutionSettings : PromptExecutionSettings
    {
        public int? MaxTokens { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public double? FrequencyPenalty { get; set; }
        public double? PresencePenalty { get; set; }
    }

    // Request/Response DTOs for Skynet-lite API
    internal class SkynetLiteChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<SkynetLiteMessage> Messages { get; set; } = new();
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public double FrequencyPenalty { get; set; }
        public double PresencePenalty { get; set; }
    }

    internal class SkynetLiteMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    internal class SkynetLiteChatResponse
    {
        public List<SkynetLiteChoice> Choices { get; set; } = new();
        public SkynetLiteUsage? Usage { get; set; }
    }

    internal class SkynetLiteChoice
    {
        public SkynetLiteMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    internal class SkynetLiteUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
