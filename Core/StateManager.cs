using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;

namespace HybridBot.Core
{
    /// <summary>
    /// Manages persistent state for conversations and user sessions.
    /// Supports both in-memory and file-based persistence with Azure integration capabilities.
    /// </summary>
    public class StateManager
    {
        private readonly ILogger<StateManager> _logger;
        private readonly string _stateDirectory;
        private readonly Dictionary<string, Dictionary<string, object>> _memoryCache = new();
        private readonly object _cacheLock = new();
        
        public StateManager(ILogger<StateManager> logger, string stateDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateDirectory = stateDirectory ?? Path.Combine(Environment.CurrentDirectory, "state");
            
            // Ensure state directory exists
            if (!Directory.Exists(_stateDirectory))
            {
                Directory.CreateDirectory(_stateDirectory);
                _logger.LogInformation("Created state directory: {StateDirectory}", _stateDirectory);
            }
        }
        
        /// <summary>
        /// Load state for a conversation context
        /// </summary>
        public async Task LoadStateAsync(BotContext context)
        {
            if (string.IsNullOrEmpty(context.ConversationId))
            {
                _logger.LogDebug("No conversation ID provided, skipping state load");
                return;
            }
            
            try
            {
                // First check memory cache
                lock (_cacheLock)
                {
                    if (_memoryCache.TryGetValue(context.ConversationId, out var cachedState))
                    {
                        context.State = new Dictionary<string, object>(cachedState);
                        _logger.LogDebug("Loaded state from cache for conversation {ConversationId}", context.ConversationId);
                        return;
                    }
                }
                
                // Load from file if not in cache
                var stateFile = GetStateFilePath(context.ConversationId);
                if (File.Exists(stateFile))
                {
                    var json = await File.ReadAllTextAsync(stateFile);
                    var stateData = JsonSerializer.Deserialize<StateData>(json);
                    
                    context.State = stateData.State ?? new Dictionary<string, object>();
                    context.SessionData = stateData.SessionData ?? new Dictionary<string, object>();
                    
                    // Update cache
                    lock (_cacheLock)
                    {
                        _memoryCache[context.ConversationId] = new Dictionary<string, object>(context.State);
                    }
                    
                    _logger.LogDebug("Loaded state from file for conversation {ConversationId}", context.ConversationId);
                }
                else
                {
                    _logger.LogDebug("No existing state file for conversation {ConversationId}", context.ConversationId);
                    context.State = new Dictionary<string, object>();
                    context.SessionData = new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load state for conversation {ConversationId}", context.ConversationId);
                context.State = new Dictionary<string, object>();
                context.SessionData = new Dictionary<string, object>();
            }
        }
        
        /// <summary>
        /// Save state for a conversation context
        /// </summary>
        public async Task SaveStateAsync(BotContext context)
        {
            if (string.IsNullOrEmpty(context.ConversationId))
            {
                _logger.LogDebug("No conversation ID provided, skipping state save");
                return;
            }
            
            try
            {
                var stateData = new StateData
                {
                    ConversationId = context.ConversationId,
                    UserId = context.UserId,
                    State = context.State,
                    SessionData = context.SessionData,
                    LastUpdated = DateTime.UtcNow
                };
                
                var json = JsonSerializer.Serialize(stateData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                var stateFile = GetStateFilePath(context.ConversationId);
                await File.WriteAllTextAsync(stateFile, json);
                
                // Update cache
                lock (_cacheLock)
                {
                    _memoryCache[context.ConversationId] = new Dictionary<string, object>(context.State);
                }
                
                _logger.LogDebug("Saved state for conversation {ConversationId}", context.ConversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save state for conversation {ConversationId}", context.ConversationId);
                throw;
            }
        }
        
        /// <summary>
        /// Clear state for a conversation
        /// </summary>
        public async Task ClearStateAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                throw new ArgumentException("Conversation ID cannot be null or empty", nameof(conversationId));
            }
            
            try
            {
                // Remove from cache
                lock (_cacheLock)
                {
                    _memoryCache.Remove(conversationId);
                }
                
                // Remove file
                var stateFile = GetStateFilePath(conversationId);
                if (File.Exists(stateFile))
                {
                    File.Delete(stateFile);
                }
                
                _logger.LogInformation("Cleared state for conversation {ConversationId}", conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear state for conversation {ConversationId}", conversationId);
                throw;
            }
        }
        
        /// <summary>
        /// Get all conversation IDs with stored state
        /// </summary>
        public async Task<IEnumerable<string>> GetStoredConversationsAsync()
        {
            try
            {
                var conversations = new List<string>();
                
                if (Directory.Exists(_stateDirectory))
                {
                    var stateFiles = Directory.GetFiles(_stateDirectory, "*.json");
                    foreach (var file in stateFiles)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        if (fileName.StartsWith("state_"))
                        {
                            conversations.Add(fileName.Substring(6)); // Remove "state_" prefix
                        }
                    }
                }
                
                return conversations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get stored conversations");
                throw;
            }
        }
        
        /// <summary>
        /// Clean up old state files
        /// </summary>
        public async Task CleanupOldStateAsync(TimeSpan maxAge)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - maxAge;
                var deletedCount = 0;
                
                if (Directory.Exists(_stateDirectory))
                {
                    var stateFiles = Directory.GetFiles(_stateDirectory, "*.json");
                    
                    foreach (var file in stateFiles)
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(file);
                            var stateData = JsonSerializer.Deserialize<StateData>(json);
                            
                            if (stateData.LastUpdated < cutoffTime)
                            {
                                File.Delete(file);
                                deletedCount++;
                                
                                // Also remove from cache
                                var conversationId = Path.GetFileNameWithoutExtension(file);
                                if (conversationId.StartsWith("state_"))
                                {
                                    conversationId = conversationId.Substring(6);
                                    lock (_cacheLock)
                                    {
                                        _memoryCache.Remove(conversationId);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to process state file during cleanup: {File}", file);
                        }
                    }
                }
                
                _logger.LogInformation("Cleaned up {DeletedCount} old state files", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old state files");
                throw;
            }
        }
        
        private string GetStateFilePath(string conversationId)
        {
            var sanitizedId = string.Join("_", conversationId.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_stateDirectory, $"state_{sanitizedId}.json");
        }
    }
    
    /// <summary>
    /// Data structure for persisted state
    /// </summary>
    public class StateData
    {
        public string ConversationId { get; set; }
        public string UserId { get; set; }
        public IDictionary<string, object> State { get; set; } = new Dictionary<string, object>();
        public IDictionary<string, object> SessionData { get; set; } = new Dictionary<string, object>();
        public DateTime LastUpdated { get; set; }
    }
}
