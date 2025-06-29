using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridBot.Core
{
    /// <summary>
    /// Enhanced interface for contextually-aware bot roles with environmental adaptation.
    /// Extends the base bot role with layered context inputs and state machine support.
    /// </summary>
    public interface IContextualBotRole : IBotRole
    {
        /// <summary>
        /// Current bot state for state machine management
        /// </summary>
        BotState CurrentState { get; }
        
        /// <summary>
        /// Health/Energy levels for dynamic behavior adaptation
        /// </summary>
        BotVitals Vitals { get; }
        
        /// <summary>
        /// Evaluate environmental context and adapt behavior accordingly
        /// </summary>
        Task<ContextualResponse> AdaptToContextAsync(LayeredContext context);
        
        /// <summary>
        /// Transition to a new state based on conditions
        /// </summary>
        Task<bool> TransitionToStateAsync(BotState newState, StateTransitionReason reason);
        
        /// <summary>
        /// Update bot vitals based on interactions and environmental factors
        /// </summary>
        Task UpdateVitalsAsync(VitalChange change);
    }
    
    /// <summary>
    /// Layered context containing environmental data, player actions, and bot states
    /// </summary>
    public class LayeredContext
    {
        /// <summary>
        /// Environmental factors affecting bot behavior
        /// </summary>
        public EnvironmentalData Environment { get; set; } = new();
        
        /// <summary>
        /// User/Player actions and behavioral patterns
        /// </summary>
        public PlayerActionData PlayerActions { get; set; } = new();
        
        /// <summary>
        /// Bot's internal state and capabilities
        /// </summary>
        public BotStateData BotInternalState { get; set; } = new();
        
        /// <summary>
        /// Temporal context (time-based patterns)
        /// </summary>
        public TemporalContext Temporal { get; set; } = new();
        
        /// <summary>
        /// Social context (conversation patterns, user relationships)
        /// </summary>
        public SocialContext Social { get; set; } = new();
        
        /// <summary>
        /// Original bot context for backward compatibility
        /// </summary>
        public required BotContext BaseContext { get; set; }
    }
    
    /// <summary>
    /// Environmental data influencing bot behavior
    /// </summary>
    public class EnvironmentalData
    {
        public Dictionary<string, object> Conditions { get; set; } = new();
        public string CurrentEnvironment { get; set; } = "normal";
        public List<string> AvailableResources { get; set; } = new();
        public Dictionary<string, double> ThreatLevels { get; set; } = new();
        public Dictionary<string, double> OpportunityScores { get; set; } = new();
    }
    
    /// <summary>
    /// Player/User action tracking for behavioral adaptation
    /// </summary>
    public class PlayerActionData
    {
        public List<string> RecentActions { get; set; } = new();
        public Dictionary<string, int> ActionFrequency { get; set; } = new();
        public string CurrentActivity { get; set; } = "idle";
        public double EngagementLevel { get; set; } = 0.5;
        public List<string> PreferredInteractionStyles { get; set; } = new();
    }
    
    /// <summary>
    /// Bot's internal state and self-awareness data
    /// </summary>
    public class BotStateData
    {
        public string CurrentMode { get; set; } = "normal";
        public Dictionary<string, double> CapabilityLevels { get; set; } = new();
        public List<string> ActiveGoals { get; set; } = new();
        public Dictionary<string, object> Memory { get; set; } = new();
        public double ConfidenceLevel { get; set; } = 0.8;
    }
    
    /// <summary>
    /// Temporal context for time-based behavior patterns
    /// </summary>
    public class TemporalContext
    {
        public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
        public TimeSpan SessionDuration { get; set; }
        public List<string> HistoricalPatterns { get; set; } = new();
        public Dictionary<string, DateTime> LastEventTimes { get; set; } = new();
    }
    
    /// <summary>
    /// Social context for relationship and conversation management
    /// </summary>
    public class SocialContext
    {
        public string UserRelationshipLevel { get; set; } = "new";
        public List<string> ConversationTopics { get; set; } = new();
        public Dictionary<string, double> UserPreferences { get; set; } = new();
        public double ConversationSentiment { get; set; } = 0.0;
        public List<string> SharedExperiences { get; set; } = new();
    }
    
    /// <summary>
    /// Bot state enumeration for state machine management
    /// </summary>
    public enum BotState
    {
        Idle,
        Listening,
        Processing,
        Responding,
        Learning,
        Adapting,
        Exploring,
        Focused,
        Retreating,
        Collaborating,
        Analyzing,
        Creative,
        Defensive,
        Aggressive,
        Supportive
    }
    
    /// <summary>
    /// Bot vitals for health/energy-based behavior adaptation
    /// </summary>
    public class BotVitals
    {
        public double Health { get; set; } = 100.0;
        public double Energy { get; set; } = 100.0;
        public double Confidence { get; set; } = 80.0;
        public double Learning { get; set; } = 50.0;
        public double SocialBattery { get; set; } = 100.0;
        public Dictionary<string, double> CustomVitals { get; set; } = new();
        
        /// <summary>
        /// Calculate overall vitality score
        /// </summary>
        public double OverallVitality => (Health + Energy + Confidence + Learning + SocialBattery) / 5.0;
        
        /// <summary>
        /// Check if bot should change behavior based on vitals
        /// </summary>
        public bool ShouldAdaptBehavior()
        {
            return Health < 30 || Energy < 20 || SocialBattery < 25 || OverallVitality < 40;
        }
    }
    
    /// <summary>
    /// Vital change information for updating bot health/energy
    /// </summary>
    public class VitalChange
    {
        public required string VitalType { get; set; }
        public double Delta { get; set; }
        public required string Reason { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Reasons for state transitions
    /// </summary>
    public enum StateTransitionReason
    {
        UserRequest,
        EnvironmentalChange,
        VitalChange,
        TimeBased,
        GoalCompletion,
        ErrorRecovery,
        LearningOpportunity,
        SocialCue,
        ResourceAvailability
    }
    
    /// <summary>
    /// Enhanced response with contextual adaptations
    /// </summary>
    public class ContextualResponse : BotResponse
    {
        /// <summary>
        /// Adaptations made based on context
        /// </summary>
        public List<string> ContextualAdaptations { get; set; } = new();
        
        /// <summary>
        /// State transition that occurred during processing
        /// </summary>
        public StateTransition? StateChange { get; set; }
        
        /// <summary>
        /// Vital changes that occurred during processing
        /// </summary>
        public List<VitalChange> VitalChanges { get; set; } = new();
        
        /// <summary>
        /// Environmental observations made during processing
        /// </summary>
        public List<string> EnvironmentalObservations { get; set; } = new();
        
        /// <summary>
        /// Confidence level in the response
        /// </summary>
        public double ConfidenceLevel { get; set; } = 0.8;
    }
    
    /// <summary>
    /// State transition information
    /// </summary>
    public class StateTransition
    {
        public BotState FromState { get; set; }
        public BotState ToState { get; set; }
        public StateTransitionReason Reason { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = "";
    }
}
