using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridBot.Core
{
    /// <summary>
    /// Interface defining a role capability that can be composed into the HybridBot.
    /// Roles are now pure capabilities/strategies rather than agent classes.
    /// </summary>
    public interface IRoleCapability
    {
        /// <summary>
        /// Unique identifier for this capability
        /// </summary>
        string CapabilityId { get; }
        
        /// <summary>
        /// Human-readable name for this capability
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Tags for categorization and filtering
        /// </summary>
        IReadOnlyList<string> Tags { get; }
        
        /// <summary>
        /// Priority level for capability execution (higher = more important)
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Configuration metadata
        /// </summary>
        IDictionary<string, object> Metadata { get; }
        
        /// <summary>
        /// Initialize the capability with configuration
        /// </summary>
        Task InitializeAsync(IDictionary<string, object> config);
        
        /// <summary>
        /// Execute the capability's primary function
        /// </summary>
        Task<BotResponse> ExecuteAsync(BotContext context);
        
        /// <summary>
        /// Check if this capability can handle the given context
        /// </summary>
        bool CanHandle(BotContext context);
        
        /// <summary>
        /// Get the system prompt/instructions for this capability
        /// </summary>
        string GetInstructions();
        
        /// <summary>
        /// Process context and provide specialized behavior
        /// </summary>
        Task<string> ProcessAsync(string input, BotContext context);
        
        /// <summary>
        /// Cleanup resources when capability is no longer needed
        /// </summary>
        Task DisposeAsync();
    }
}
