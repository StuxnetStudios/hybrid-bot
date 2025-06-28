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
                _logger.LogError(ex, "HTTP error calling Skynet-lite API");
                throw new InvalidOperationException("Failed to communicate with Skynet-lite service", ex);
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
