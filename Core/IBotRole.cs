using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridBot.Core
{
    /// <summary>
    /// Core interface for all bot roles in the hybrid orchestration system.
    /// Supports both runtime interoperability and tag-based metadata organization.
    /// </summary>
    public interface IBotRole
    {
        /// <summary>
        /// Unique identifier for the role
        /// </summary>
        string RoleId { get; }
        
        /// <summary>
        /// Human-readable name for the role
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Tags for categorization and filtering
        /// </summary>
        IReadOnlyList<string> Tags { get; }
        
        /// <summary>
        /// Priority level for role execution (higher = more important)
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Configuration metadata from YAML/JSON frontmatter
        /// </summary>
        IDictionary<string, object> Metadata { get; }
        
        /// <summary>
        /// Initialize the role with configuration
        /// </summary>
        Task InitializeAsync(IDictionary<string, object> config);
        
        /// <summary>
        /// Execute the role's primary function
        /// </summary>
        Task<BotResponse> ExecuteAsync(BotContext context);
        
        /// <summary>
        /// Check if this role can handle the given context
        /// </summary>
        bool CanHandle(BotContext context);
        
        /// <summary>
        /// Cleanup resources when role is disposed
        /// </summary>
        Task DisposeAsync();
    }
    
    /// <summary>
    /// Context object containing request data and state
    /// </summary>
    public class BotContext
    {
        public string RequestId { get; set; }
        public string UserId { get; set; }
        public string Input { get; set; }
        public string ConversationId { get; set; }
        public IDictionary<string, object> State { get; set; } = new Dictionary<string, object>();
        public IDictionary<string, object> SessionData { get; set; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Response object containing output and state changes
    /// </summary>
    public class BotResponse
    {
        public string Content { get; set; }
        public string ResponseType { get; set; } = "text";
        public bool IsComplete { get; set; } = true;
        public IDictionary<string, object> UpdatedState { get; set; } = new Dictionary<string, object>();
        public IList<string> NextRoles { get; set; } = new List<string>();
        public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
