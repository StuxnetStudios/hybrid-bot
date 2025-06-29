using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HybridBot.Core
{
    /// <summary>
    /// Advanced base class for contextually-aware bot roles with environmental adaptation,
    /// state machine management, and layered context processing.
    /// 
    /// Implements OOP principles:
    /// - Encapsulation: Internal state management through private methods
    /// - Inheritance: Specialized roles inherit and extend base functionality
    /// - Polymorphism: Virtual methods for role-specific behavior adaptation
    /// - Abstraction: High-level context adaptation methods hide complexity
    /// </summary>
    public abstract class ContextualBotRole : BaseBotRole, IContextualBotRole
    {
        private readonly Dictionary<BotState, List<BotState>> _stateTransitions;
        private readonly List<StateTransition> _stateHistory;
        private readonly List<VitalChange> _vitalHistory;
        
        protected ContextualBotRole(ILogger logger) : base(logger)
        {
            CurrentState = BotState.Idle;
            Vitals = new BotVitals();
            _stateTransitions = InitializeStateTransitions();
            _stateHistory = new List<StateTransition>();
            _vitalHistory = new List<VitalChange>();
        }
        
        #region IContextualBotRole Implementation
        
        public BotState CurrentState { get; private set; }
        public BotVitals Vitals { get; private set; }
        
        public virtual async Task<ContextualResponse> AdaptToContextAsync(LayeredContext context)
        {
            _logger.LogInformation("Role {RoleId} adapting to layered context", RoleId);
            
            var response = new ContextualResponse
            {
                Content = "",
                IsComplete = false,
                ContextualAdaptations = new List<string>(),
                EnvironmentalObservations = new List<string>()
            };
            
            try
            {
                // Process each context layer
                await ProcessEnvironmentalContextAsync(context.Environment, response);
                await ProcessPlayerActionContextAsync(context.PlayerActions, response);
                await ProcessBotStateContextAsync(context.BotInternalState, response);
                await ProcessTemporalContextAsync(context.Temporal, response);
                await ProcessSocialContextAsync(context.Social, response);
                
                // Determine if state transition is needed
                var suggestedState = DetermineOptimalState(context);
                if (suggestedState != CurrentState && CanTransitionTo(suggestedState))
                {
                    await TransitionToStateAsync(suggestedState, StateTransitionReason.EnvironmentalChange);
                    response.StateChange = _stateHistory.LastOrDefault() ?? new StateTransition
                    {
                        FromState = CurrentState,
                        ToState = suggestedState,
                        Reason = StateTransitionReason.EnvironmentalChange
                    };
                }
                
                // Check if vitals need adjustment
                var vitalChanges = CalculateVitalChanges(context);
                foreach (var change in vitalChanges)
                {
                    await UpdateVitalsAsync(change);
                    response.VitalChanges.Add(change);
                }
                
                // Generate contextually adapted response
                response.Content = await GenerateContextualResponseAsync(context, response);
                response.IsComplete = true;
                response.ConfidenceLevel = CalculateConfidenceLevel(context, response);
                
                _logger.LogInformation("Role {RoleId} completed contextual adaptation", RoleId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during contextual adaptation for role {RoleId}", RoleId);
                response.Content = "I encountered an issue while adapting to the current context.";
                response.IsComplete = false;
                return response;
            }
        }
        
        public virtual async Task<bool> TransitionToStateAsync(BotState newState, StateTransitionReason reason)
        {
            if (!CanTransitionTo(newState))
            {
                _logger.LogWarning("Invalid state transition from {FromState} to {ToState} for role {RoleId}", 
                    CurrentState, newState, RoleId);
                return false;
            }
            
            var transition = new StateTransition
            {
                FromState = CurrentState,
                ToState = newState,
                Reason = reason,
                Description = $"Transitioned from {CurrentState} to {newState} due to {reason}"
            };
            
            _logger.LogInformation("Role {RoleId} transitioning from {FromState} to {ToState}", 
                RoleId, CurrentState, newState);
            
            // Allow derived classes to handle state change logic
            await OnStateTransitionAsync(transition);
            
            CurrentState = newState;
            _stateHistory.Add(transition);
            
            // Limit state history to prevent memory issues
            if (_stateHistory.Count > 100)
            {
                _stateHistory.RemoveRange(0, 50);
            }
            
            return true;
        }
        
        public virtual async Task UpdateVitalsAsync(VitalChange change)
        {
            switch (change.VitalType.ToLower())
            {
                case "health":
                    Vitals.Health = Math.Clamp(Vitals.Health + change.Delta, 0, 100);
                    break;
                case "energy":
                    Vitals.Energy = Math.Clamp(Vitals.Energy + change.Delta, 0, 100);
                    break;
                case "confidence":
                    Vitals.Confidence = Math.Clamp(Vitals.Confidence + change.Delta, 0, 100);
                    break;
                case "learning":
                    Vitals.Learning = Math.Clamp(Vitals.Learning + change.Delta, 0, 100);
                    break;
                case "socialbattery":
                    Vitals.SocialBattery = Math.Clamp(Vitals.SocialBattery + change.Delta, 0, 100);
                    break;
                default:
                    if (Vitals.CustomVitals.ContainsKey(change.VitalType))
                    {
                        Vitals.CustomVitals[change.VitalType] = Math.Clamp(
                            Vitals.CustomVitals[change.VitalType] + change.Delta, 0, 100);
                    }
                    break;
            }
            
            _vitalHistory.Add(change);
            
            // Limit vital history
            if (_vitalHistory.Count > 200)
            {
                _vitalHistory.RemoveRange(0, 100);
            }
            
            _logger.LogDebug("Role {RoleId} vital {VitalType} changed by {Delta}: {Reason}", 
                RoleId, change.VitalType, change.Delta, change.Reason);
            
            // Allow derived classes to react to vital changes
            await OnVitalChangeAsync(change);
        }
        
        #endregion
        
        #region Enhanced Context Processing
        
        protected virtual async Task ProcessEnvironmentalContextAsync(EnvironmentalData environment, ContextualResponse response)
        {
            // Example environmental adaptations
            if (environment.ThreatLevels.Any(t => t.Value > 0.7))
            {
                response.ContextualAdaptations.Add("Activated defensive mode due to high threat levels");
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Energy", 
                    Delta = -5, 
                    Reason = "High threat environment" 
                });
            }
            
            if (environment.OpportunityScores.Any(o => o.Value > 0.8))
            {
                response.ContextualAdaptations.Add("Detected high-value learning opportunity");
                response.EnvironmentalObservations.Add($"Favorable conditions detected: {string.Join(", ", environment.OpportunityScores.Where(o => o.Value > 0.8).Select(o => o.Key))}");
            }
            
            // Environmental resource availability
            if (environment.AvailableResources.Any())
            {
                response.ContextualAdaptations.Add($"Utilizing available resources: {string.Join(", ", environment.AvailableResources.Take(3))}");
            }
            
            await Task.CompletedTask;
        }
        
        protected virtual async Task ProcessPlayerActionContextAsync(PlayerActionData playerActions, ContextualResponse response)
        {
            // Adapt to user engagement level
            if (playerActions.EngagementLevel < 0.3)
            {
                response.ContextualAdaptations.Add("Adjusting for low user engagement - using more interactive approach");
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "SocialBattery", 
                    Delta = -3, 
                    Reason = "Low user engagement" 
                });
            }
            else if (playerActions.EngagementLevel > 0.8)
            {
                response.ContextualAdaptations.Add("High user engagement detected - maintaining current interaction style");
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Energy", 
                    Delta = 2, 
                    Reason = "High user engagement boost" 
                });
            }
            
            // Adapt to user's preferred interaction styles
            if (playerActions.PreferredInteractionStyles.Any())
            {
                var preferredStyle = playerActions.PreferredInteractionStyles.First();
                response.ContextualAdaptations.Add($"Adapting to preferred interaction style: {preferredStyle}");
            }
            
            await Task.CompletedTask;
        }
        
        protected virtual async Task ProcessBotStateContextAsync(BotStateData botState, ContextualResponse response)
        {
            // Adjust confidence based on capability levels
            var avgCapability = botState.CapabilityLevels.Any() ? botState.CapabilityLevels.Values.Average() : 0.5;
            if (avgCapability < Vitals.Confidence)
            {
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Confidence", 
                    Delta = (avgCapability - Vitals.Confidence) * 0.1, 
                    Reason = "Capability assessment" 
                });
            }
            
            // Process active goals
            if (botState.ActiveGoals.Any())
            {
                response.ContextualAdaptations.Add($"Working towards goals: {string.Join(", ", botState.ActiveGoals.Take(2))}");
            }
            
            await Task.CompletedTask;
        }
        
        protected virtual async Task ProcessTemporalContextAsync(TemporalContext temporal, ContextualResponse response)
        {
            // Long session fatigue
            if (temporal.SessionDuration.TotalMinutes > 30)
            {
                response.ContextualAdaptations.Add("Adjusting for extended session duration");
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Energy", 
                    Delta = -1, 
                    Reason = "Extended session fatigue" 
                });
            }
            
            // Historical pattern recognition
            if (temporal.HistoricalPatterns.Any())
            {
                response.ContextualAdaptations.Add($"Leveraging historical patterns: {temporal.HistoricalPatterns.First()}");
            }
            
            await Task.CompletedTask;
        }
        
        protected virtual async Task ProcessSocialContextAsync(SocialContext social, ContextualResponse response)
        {
            // Relationship-based adaptations
            switch (social.UserRelationshipLevel)
            {
                case "new":
                    response.ContextualAdaptations.Add("Using introductory interaction style for new user");
                    break;
                case "familiar":
                    response.ContextualAdaptations.Add("Adapting to familiar user with personalized approach");
                    break;
                case "expert":
                    response.ContextualAdaptations.Add("Using advanced interaction style for expert user");
                    break;
            }
            
            // Sentiment-based energy adjustment
            if (social.ConversationSentiment < -0.5)
            {
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "SocialBattery", 
                    Delta = -5, 
                    Reason = "Negative conversation sentiment" 
                });
            }
            else if (social.ConversationSentiment > 0.5)
            {
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "SocialBattery", 
                    Delta = 3, 
                    Reason = "Positive conversation sentiment" 
                });
            }
            
            await Task.CompletedTask;
        }
        
        #endregion
        
        #region State Machine Management
        
        protected virtual Dictionary<BotState, List<BotState>> InitializeStateTransitions()
        {
            return new Dictionary<BotState, List<BotState>>
            {
                [BotState.Idle] = new List<BotState> { BotState.Listening, BotState.Exploring, BotState.Learning },
                [BotState.Listening] = new List<BotState> { BotState.Processing, BotState.Idle, BotState.Focused },
                [BotState.Processing] = new List<BotState> { BotState.Responding, BotState.Analyzing, BotState.Learning },
                [BotState.Responding] = new List<BotState> { BotState.Idle, BotState.Listening, BotState.Collaborating },
                [BotState.Learning] = new List<BotState> { BotState.Processing, BotState.Analyzing, BotState.Adapting },
                [BotState.Adapting] = new List<BotState> { BotState.Idle, BotState.Processing, BotState.Creative },
                [BotState.Exploring] = new List<BotState> { BotState.Learning, BotState.Analyzing, BotState.Focused },
                [BotState.Focused] = new List<BotState> { BotState.Processing, BotState.Responding, BotState.Analyzing },
                [BotState.Retreating] = new List<BotState> { BotState.Idle, BotState.Learning, BotState.Defensive },
                [BotState.Collaborating] = new List<BotState> { BotState.Responding, BotState.Supportive, BotState.Learning },
                [BotState.Analyzing] = new List<BotState> { BotState.Processing, BotState.Learning, BotState.Creative },
                [BotState.Creative] = new List<BotState> { BotState.Responding, BotState.Collaborating, BotState.Exploring },
                [BotState.Defensive] = new List<BotState> { BotState.Retreating, BotState.Idle, BotState.Analyzing },
                [BotState.Aggressive] = new List<BotState> { BotState.Focused, BotState.Defensive, BotState.Analyzing },
                [BotState.Supportive] = new List<BotState> { BotState.Collaborating, BotState.Responding, BotState.Learning }
            };
        }
        
        protected virtual bool CanTransitionTo(BotState newState)
        {
            return _stateTransitions.ContainsKey(CurrentState) && 
                   _stateTransitions[CurrentState].Contains(newState);
        }
        
        protected virtual BotState DetermineOptimalState(LayeredContext context)
        {
            // State determination logic based on context
            if (Vitals.Health < 30 || Vitals.Energy < 20)
                return BotState.Retreating;
                
            if (context.PlayerActions.EngagementLevel > 0.8)
                return BotState.Collaborating;
                
            if (context.Environment.ThreatLevels.Any(t => t.Value > 0.7))
                return BotState.Defensive;
                
            if (context.Environment.OpportunityScores.Any(o => o.Value > 0.8))
                return BotState.Exploring;
                
            if (context.BotInternalState.ActiveGoals.Any())
                return BotState.Focused;
                
            return BotState.Processing; // Default processing state
        }
        
        #endregion
        
        #region Vital Management
        
        protected virtual List<VitalChange> CalculateVitalChanges(LayeredContext context)
        {
            var changes = new List<VitalChange>();
            
            // Energy consumption based on processing complexity
            var complexityScore = CalculateContextComplexity(context);
            if (complexityScore > 0.7)
            {
                changes.Add(new VitalChange 
                { 
                    VitalType = "Energy", 
                    Delta = -2, 
                    Reason = "High complexity processing" 
                });
            }
            
            // Learning opportunities
            if (context.Environment.OpportunityScores.ContainsKey("learning") && 
                context.Environment.OpportunityScores["learning"] > 0.6)
            {
                changes.Add(new VitalChange 
                { 
                    VitalType = "Learning", 
                    Delta = 3, 
                    Reason = "Learning opportunity detected" 
                });
            }
            
            // Social interaction effects
            if (context.Social.ConversationSentiment > 0.3)
            {
                changes.Add(new VitalChange 
                { 
                    VitalType = "SocialBattery", 
                    Delta = 1, 
                    Reason = "Positive social interaction" 
                });
            }
            
            return changes;
        }
        
        private double CalculateContextComplexity(LayeredContext context)
        {
            var factors = new List<double>
            {
                context.Environment.Conditions.Count * 0.1,
                context.PlayerActions.RecentActions.Count * 0.05,
                context.BotInternalState.ActiveGoals.Count * 0.15,
                context.Social.ConversationTopics.Count * 0.1
            };
            
            return Math.Min(factors.Sum(), 1.0);
        }
        
        #endregion
        
        #region Virtual Methods for Derived Classes
        
        /// <summary>
        /// Override to implement role-specific state transition logic
        /// </summary>
        protected virtual async Task OnStateTransitionAsync(StateTransition transition)
        {
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Override to implement role-specific vital change reactions
        /// </summary>
        protected virtual async Task OnVitalChangeAsync(VitalChange change)
        {
            await Task.CompletedTask;
        }
        
        /// <summary>
        /// Override to implement role-specific contextual response generation
        /// </summary>
        protected virtual Task<string> GenerateContextualResponseAsync(LayeredContext context, ContextualResponse partialResponse)
        {
            // Default implementation - derived classes should override
            var adaptations = partialResponse.ContextualAdaptations.Any() 
                ? $" (Adaptations: {string.Join(", ", partialResponse.ContextualAdaptations)})"
                : "";
                
            return Task.FromResult($"I'm processing your request while adapting to the current context.{adaptations}");
        }
        
        /// <summary>
        /// Calculate confidence level in the contextual response
        /// </summary>
        protected virtual double CalculateConfidenceLevel(LayeredContext context, ContextualResponse response)
        {
            var baseConfidence = Vitals.Confidence / 100.0;
            var contextFamiliarity = CalculateContextFamiliarity(context);
            var adaptationSuccess = response.ContextualAdaptations.Count > 0 ? 1.1 : 1.0;
            
            return Math.Min(baseConfidence * contextFamiliarity * adaptationSuccess, 1.0);
        }
        
        private double CalculateContextFamiliarity(LayeredContext context)
        {
            // Calculate how familiar the bot is with this type of context
            // This would typically use historical data and pattern recognition
            return 0.8; // Placeholder implementation
        }
        
        #endregion
        
        #region Enhanced CanHandle with Context Awareness
        
        public override bool CanHandle(BotContext context)
        {
            // Check vitals - if too low, may not be able to handle complex requests
            if (Vitals.ShouldAdaptBehavior() && IsComplexRequest(context))
            {
                _logger.LogDebug("Role {RoleId} declining complex request due to low vitals", RoleId);
                return false;
            }
            
            // Check current state compatibility
            if (!IsStateCompatibleWithRequest(CurrentState, context))
            {
                _logger.LogDebug("Role {RoleId} current state {State} not compatible with request", RoleId, CurrentState);
                return false;
            }
            
            // Call base implementation for standard checks
            return base.CanHandle(context);
        }
        
        private bool IsComplexRequest(BotContext context)
        {
            return context.Input?.Length > 200 || 
                   context.State.Count > 10 ||
                   context.Input?.Contains("complex") == true ||
                   context.Input?.Contains("analyze") == true;
        }
        
        private bool IsStateCompatibleWithRequest(BotState state, BotContext context)
        {
            // Simple state compatibility check
            return state switch
            {
                BotState.Retreating => false, // Can't handle requests while retreating
                BotState.Defensive => IsSimpleRequest(context), // Only simple requests in defensive mode
                _ => true
            };
        }
        
        private bool IsSimpleRequest(BotContext context)
        {
            return context.Input?.Length < 50 && context.State.Count < 3;
        }
        
        #endregion
    }
}
