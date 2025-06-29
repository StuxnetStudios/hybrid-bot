using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HybridBot.Core;

namespace HybridBot.Roles
{
    /// <summary>
    /// Adaptive Gaming Assistant Role - demonstrates contextual awareness with layered inputs.
    /// 
    /// This role adapts its behavior based on:
    /// - Environmental data (game state, difficulty, available resources)
    /// - Player actions (recent moves, engagement level, skill progression)
    /// - Bot vitals (energy, confidence, learning progress)
    /// - Temporal context (session duration, time patterns)
    /// - Social context (player relationship, conversation tone)
    /// 
    /// Showcases OOP design principles:
    /// - Inheritance: Extends ContextualBotRole with specialized gaming logic
    /// - Polymorphism: Overrides context processing methods for gaming scenarios
    /// - Encapsulation: Private methods handle complex adaptation logic
    /// - Abstraction: High-level gaming strategies hide implementation details
    /// </summary>
    public class AdaptiveGamingRole : ContextualBotRole
    {
        public override string RoleId => "adaptive-gaming";
        public override string Name => "Adaptive Gaming Assistant";
        
        // Gaming-specific state
        private string _currentGameMode = "exploration";
        private double _playerSkillLevel = 0.5;
        private Dictionary<string, int> _strategySuccessRates = new();
        
        public AdaptiveGamingRole(ILogger<AdaptiveGamingRole> logger) : base(logger)
        {
            Tags = new List<string> { "gaming", "adaptive", "contextual", "strategic", "coaching" };
            Priority = 85;
            
            // Initialize gaming-specific vitals
            Vitals.CustomVitals["Strategic"] = 75.0;
            Vitals.CustomVitals["Tactical"] = 60.0;
            Vitals.CustomVitals["Motivational"] = 80.0;
        }
        
        #region BaseBotRole Implementation
        
        /// <summary>
        /// Core execution method - delegates to contextual processing
        /// </summary>
        protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
        {
            // Convert BotContext to LayeredContext for enhanced processing
            var layeredContext = CreateLayeredContext(context);
            
            // Use the contextual adaptation system
            var contextualResponse = await AdaptToContextAsync(layeredContext);
            
            // Convert back to standard BotResponse for compatibility
            return new BotResponse
            {
                Content = contextualResponse.Content,
                ResponseType = contextualResponse.ResponseType,
                IsComplete = contextualResponse.IsComplete,
                UpdatedState = contextualResponse.UpdatedState,
                NextRoles = contextualResponse.NextRoles,
                Metadata = contextualResponse.Metadata
            };
        }
        
        /// <summary>
        /// Convert standard BotContext to enhanced LayeredContext
        /// </summary>
        private LayeredContext CreateLayeredContext(BotContext context)
        {
            var layeredContext = new LayeredContext
            {
                BaseContext = context,
                Environment = ExtractEnvironmentalData(context),
                PlayerActions = ExtractPlayerActionData(context),
                BotInternalState = ExtractBotStateData(context),
                Temporal = CreateTemporalContext(context),
                Social = CreateSocialContext(context)
            };
            
            return layeredContext;
        }
        
        private EnvironmentalData ExtractEnvironmentalData(BotContext context)
        {
            var envData = new EnvironmentalData();
            
            // Extract environmental data from context state
            if (context.State.ContainsKey("environment"))
            {
                if (context.State["environment"] is Dictionary<string, object> envDict)
                {
                    envData.Conditions = envDict;
                }
            }
            
            if (context.State.ContainsKey("game_environment"))
            {
                envData.CurrentEnvironment = context.State["game_environment"].ToString() ?? "normal";
            }
            
            if (context.State.ContainsKey("threats"))
            {
                if (context.State["threats"] is Dictionary<string, double> threats)
                {
                    envData.ThreatLevels = threats;
                }
            }
            
            return envData;
        }
        
        private PlayerActionData ExtractPlayerActionData(BotContext context)
        {
            var playerData = new PlayerActionData();
            
            if (context.State.ContainsKey("recent_actions"))
            {
                if (context.State["recent_actions"] is List<string> actions)
                {
                    playerData.RecentActions = actions;
                }
            }
            
            if (context.State.ContainsKey("engagement_level"))
            {
                if (double.TryParse(context.State["engagement_level"].ToString(), out double engagement))
                {
                    playerData.EngagementLevel = engagement;
                }
            }
            
            return playerData;
        }
        
        private BotStateData ExtractBotStateData(BotContext context)
        {
            var botData = new BotStateData();
            
            if (context.State.ContainsKey("bot_goals"))
            {
                if (context.State["bot_goals"] is List<string> goals)
                {
                    botData.ActiveGoals = goals;
                }
            }
            
            botData.Memory = context.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            botData.ConfidenceLevel = Vitals.Confidence / 100.0;
            
            return botData;
        }
        
        private TemporalContext CreateTemporalContext(BotContext context)
        {
            return new TemporalContext
            {
                CurrentTime = DateTime.UtcNow,
                SessionDuration = DateTime.UtcNow - context.Timestamp
            };
        }
        
        private SocialContext CreateSocialContext(BotContext context)
        {
            var socialContext = new SocialContext();
            
            if (context.State.ContainsKey("user_relationship"))
            {
                socialContext.UserRelationshipLevel = context.State["user_relationship"].ToString() ?? "new";
            }
            
            if (context.State.ContainsKey("conversation_sentiment"))
            {
                if (double.TryParse(context.State["conversation_sentiment"].ToString(), out double sentiment))
                {
                    socialContext.ConversationSentiment = sentiment;
                }
            }
            
            return socialContext;
        }
        
        #endregion
        
        #region Overridden Context Processing
        
        protected override async Task ProcessEnvironmentalContextAsync(EnvironmentalData environment, ContextualResponse response)
        {
            // Process gaming environment (terrain, weather, resources, threats)
            await ProcessGameEnvironmentAsync(environment, response);
            
            // Call base implementation for general environmental processing
            await base.ProcessEnvironmentalContextAsync(environment, response);
        }
        
        protected override async Task ProcessPlayerActionContextAsync(PlayerActionData playerActions, ContextualResponse response)
        {
            // Gaming-specific player action analysis
            await AnalyzeGamingPatternsAsync(playerActions, response);
            
            // Update player skill assessment
            await UpdatePlayerSkillAssessmentAsync(playerActions);
            
            // Call base implementation
            await base.ProcessPlayerActionContextAsync(playerActions, response);
        }
        
        protected override async Task ProcessBotStateContextAsync(BotStateData botState, ContextualResponse response)
        {
            // Gaming strategy adaptation based on bot state
            await AdaptGamingStrategyAsync(botState, response);
            
            await base.ProcessBotStateContextAsync(botState, response);
        }
        
        #endregion
        
        #region Gaming-Specific Context Processing
        
        private async Task ProcessGameEnvironmentAsync(EnvironmentalData environment, ContextualResponse response)
        {
            // Terrain analysis for strategic advice
            if (environment.Conditions.ContainsKey("terrain"))
            {
                var terrain = environment.Conditions["terrain"]?.ToString();
                if (!string.IsNullOrEmpty(terrain))
                {
                    var terrainAdvice = GetTerrainStrategy(terrain);
                    response.ContextualAdaptations.Add($"Terrain adaptation: {terrainAdvice}");
                    response.EnvironmentalObservations.Add($"Detected {terrain} terrain - adjusting strategy");
                }
            }
            
            // Weather impact on gameplay
            if (environment.Conditions.ContainsKey("weather"))
            {
                var weather = environment.Conditions["weather"]?.ToString();
                if (!string.IsNullOrEmpty(weather))
                {
                    var weatherStrategy = GetWeatherStrategy(weather);
                    response.ContextualAdaptations.Add($"Weather strategy: {weatherStrategy}");
                    
                    // Weather affects energy differently
                    if (weather.Contains("storm"))
                    {
                        await UpdateVitalsAsync(new VitalChange 
                        { 
                            VitalType = "Energy", 
                            Delta = -3, 
                            Reason = "Storm conditions require more focus" 
                        });
                    }
                }
            }
            
            // Resource availability impacts strategy
            if (environment.AvailableResources.Any())
            {
                var resourceStrategy = PlanResourceUtilization(environment.AvailableResources);
                response.ContextualAdaptations.Add($"Resource strategy: {resourceStrategy}");
                
                // Abundant resources boost confidence
                if (environment.AvailableResources.Count > 5)
                {
                    await UpdateVitalsAsync(new VitalChange 
                    { 
                        VitalType = "Confidence", 
                        Delta = 2, 
                        Reason = "Abundant resources available" 
                    });
                }
            }
            
            // Threat assessment and response
            if (environment.ThreatLevels.Any())
            {
                var highestThreat = environment.ThreatLevels.OrderByDescending(t => t.Value).First();
                if (highestThreat.Value > 0.7)
                {
                    var threatResponse = GenerateThreatResponse(highestThreat.Key, highestThreat.Value);
                    response.ContextualAdaptations.Add($"Threat response: {threatResponse}");
                    
                    // High threats may require state change to defensive
                    if (CurrentState != BotState.Defensive && highestThreat.Value > 0.8)
                    {
                        await TransitionToStateAsync(BotState.Defensive, StateTransitionReason.EnvironmentalChange);
                    }
                }
            }
            
            await Task.CompletedTask;
        }
        
        private async Task AnalyzeGamingPatternsAsync(PlayerActionData playerActions, ContextualResponse response)
        {
            // Analyze recent gaming actions for patterns
            var recentActions = playerActions.RecentActions.Take(10).ToList();
            var actionPatterns = AnalyzeActionPatterns(recentActions);
            
            if (actionPatterns.ContainsKey("aggressive_play") && actionPatterns["aggressive_play"] > 0.6)
            {
                response.ContextualAdaptations.Add("Detected aggressive playstyle - providing tactical support");
                _currentGameMode = "combat";
                
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Tactical", 
                    Delta = 3, 
                    Reason = "Aggressive play pattern detected" 
                });
            }
            else if (actionPatterns.ContainsKey("exploration") && actionPatterns["exploration"] > 0.7)
            {
                response.ContextualAdaptations.Add("Detected exploration focus - providing discovery guidance");
                _currentGameMode = "exploration";
                
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Learning", 
                    Delta = 2, 
                    Reason = "Exploration activity boost" 
                });
            }
            
            // Check for signs of frustration or difficulty
            if (playerActions.EngagementLevel < 0.3 && 
                playerActions.ActionFrequency.ContainsKey("retry") && 
                playerActions.ActionFrequency["retry"] > 5)
            {
                response.ContextualAdaptations.Add("Detected potential frustration - switching to supportive mode");
                await TransitionToStateAsync(BotState.Supportive, StateTransitionReason.SocialCue);
                
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Motivational", 
                    Delta = 5, 
                    Reason = "Player needs encouragement" 
                });
            }
        }
        
        private async Task AdaptGamingStrategyAsync(BotStateData botState, ContextualResponse response)
        {
            // Adapt strategy based on active goals
            if (botState.ActiveGoals.Contains("skill_improvement"))
            {
                response.ContextualAdaptations.Add("Focusing on skill development guidance");
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Learning", 
                    Delta = 2, 
                    Reason = "Skill improvement focus" 
                });
            }
            
            if (botState.ActiveGoals.Contains("achievement_unlock"))
            {
                response.ContextualAdaptations.Add("Optimizing for achievement completion");
                await UpdateVitalsAsync(new VitalChange 
                { 
                    VitalType = "Strategic", 
                    Delta = 3, 
                    Reason = "Achievement-focused strategy" 
                });
            }
            
            // Adjust confidence based on recent strategy success
            if (botState.Memory.ContainsKey("recent_strategy_success"))
            {
                var successRate = (double)botState.Memory["recent_strategy_success"];
                if (successRate > 0.8)
                {
                    await UpdateVitalsAsync(new VitalChange 
                    { 
                        VitalType = "Confidence", 
                        Delta = 3, 
                        Reason = "High strategy success rate" 
                    });
                }
                else if (successRate < 0.4)
                {
                    await UpdateVitalsAsync(new VitalChange 
                    { 
                        VitalType = "Confidence", 
                        Delta = -2, 
                        Reason = "Low strategy success rate" 
                    });
                    response.ContextualAdaptations.Add("Recalibrating strategy based on recent performance");
                }
            }
        }
        
        private async Task UpdatePlayerSkillAssessmentAsync(PlayerActionData playerActions)
        {
            // Simple skill assessment based on action complexity and success
            if (playerActions.ActionFrequency.ContainsKey("advanced_combo") && 
                playerActions.ActionFrequency["advanced_combo"] > 0)
            {
                _playerSkillLevel = Math.Min(_playerSkillLevel + 0.05, 1.0);
            }
            
            if (playerActions.ActionFrequency.ContainsKey("failed_action") && 
                playerActions.ActionFrequency["failed_action"] > 10)
            {
                _playerSkillLevel = Math.Max(_playerSkillLevel - 0.02, 0.1);
            }
            
            await Task.CompletedTask;
        }
        
        #endregion
        
        #region Gaming Strategy Methods
        
        private string GetTerrainStrategy(string terrain)
        {
            return terrain.ToLower() switch
            {
                "mountain" => "Use high ground advantage, watch for avalanche risks",
                "forest" => "Utilize cover, beware of limited visibility",
                "desert" => "Conserve water, avoid midday travel",
                "swamp" => "Move slowly, watch for quicksand and creatures",
                "urban" => "Use building cover, check for alternate routes",
                _ => "Adapt movement to terrain conditions"
            };
        }
        
        private string GetWeatherStrategy(string weather)
        {
            return weather.ToLower() switch
            {
                "sunny" => "Optimal visibility, good for exploration",
                "rainy" => "Reduced visibility, slippery surfaces",
                "stormy" => "Seek shelter, avoid metal objects",
                "foggy" => "Move cautiously, use sound cues",
                "windy" => "Projectiles affected, secure loose items",
                _ => "Monitor weather conditions closely"
            };
        }
        
        private string PlanResourceUtilization(List<string> resources)
        {
            var strategy = new List<string>();
            
            if (resources.Contains("health_potion"))
                strategy.Add("Save health potions for emergency situations");
            
            if (resources.Contains("mana_crystal"))
                strategy.Add("Use mana crystals for powerful spells");
            
            if (resources.Contains("crafting_materials"))
                strategy.Add("Craft equipment upgrades when available");
            
            return strategy.Any() ? string.Join(", ", strategy) : "Optimize resource usage";
        }
        
        private string GenerateThreatResponse(string threatType, double threatLevel)
        {
            var intensity = threatLevel switch
            {
                > 0.8 => "immediate",
                > 0.6 => "serious",
                > 0.4 => "moderate",
                _ => "minor"
            };
            
            return $"Detected {intensity} {threatType} threat - " + threatType.ToLower() switch
            {
                "enemy" => "prepare for combat, check equipment",
                "trap" => "advance carefully, look for disarm mechanisms",
                "environmental" => "find safe route or shelter",
                "boss" => "ensure full preparation before engagement",
                _ => "assess situation and adapt accordingly"
            };
        }
        
        private Dictionary<string, double> AnalyzeActionPatterns(List<string> actions)
        {
            var patterns = new Dictionary<string, double>();
            
            // Combat pattern analysis
            var combatActions = actions.Count(a => a.Contains("attack") || a.Contains("defend") || a.Contains("cast"));
            patterns["aggressive_play"] = (double)combatActions / actions.Count;
            
            // Exploration pattern analysis
            var explorationActions = actions.Count(a => a.Contains("explore") || a.Contains("search") || a.Contains("investigate"));
            patterns["exploration"] = (double)explorationActions / actions.Count;
            
            // Preparation pattern analysis
            var prepActions = actions.Count(a => a.Contains("craft") || a.Contains("upgrade") || a.Contains("organize"));
            patterns["preparation"] = (double)prepActions / actions.Count;
            
            return patterns;
        }
        
        #endregion
        
        #region State-Specific Overrides
        
        protected override async Task OnStateTransitionAsync(StateTransition transition)
        {
            _logger.LogInformation("Gaming role transitioning from {FromState} to {ToState} due to {Reason}", 
                transition.FromState, transition.ToState, transition.Reason);
            
            // Gaming-specific state transition logic
            switch (transition.ToState)
            {
                case BotState.Defensive:
                    await UpdateVitalsAsync(new VitalChange 
                    { 
                        VitalType = "Tactical", 
                        Delta = 5, 
                        Reason = "Defensive mode activated" 
                    });
                    break;
                    
                case BotState.Supportive:
                    await UpdateVitalsAsync(new VitalChange 
                    { 
                        VitalType = "Motivational", 
                        Delta = 3, 
                        Reason = "Supportive mode activated" 
                    });
                    break;
                    
                case BotState.Exploring:
                    await UpdateVitalsAsync(new VitalChange 
                    { 
                        VitalType = "Learning", 
                        Delta = 2, 
                        Reason = "Exploration mode activated" 
                    });
                    break;
            }
            
            await base.OnStateTransitionAsync(transition);
        }
        
        protected override async Task OnVitalChangeAsync(VitalChange change)
        {
            // React to vital changes with gaming-specific logic
            if (change.VitalType == "Energy" && Vitals.Energy < 25)
            {
                _logger.LogInformation("Gaming role energy low - suggesting rest or energy restoration");
            }
            
            if (change.VitalType == "Confidence" && Vitals.Confidence < 40)
            {
                _logger.LogInformation("Gaming role confidence low - switching to easier strategies");
            }
            
            await base.OnVitalChangeAsync(change);
        }
        
        #endregion
        
        #region Enhanced Response Generation
        
        protected override Task<string> GenerateContextualResponseAsync(LayeredContext context, ContextualResponse partialResponse)
        {
            var response = new List<string>();
            
            // Gaming-specific response based on current state and context
            switch (CurrentState)
            {
                case BotState.Defensive:
                    response.Add($"⚔️ **Defensive Strategy Active**");
                    response.Add(GenerateDefensiveAdvice(context));
                    break;
                    
                case BotState.Exploring:
                    response.Add($"🗺️ **Exploration Mode**");
                    response.Add(GenerateExplorationAdvice(context));
                    break;
                    
                case BotState.Supportive:
                    response.Add($"🤝 **Supportive Guidance**");
                    response.Add(GenerateSupportiveAdvice(context));
                    break;
                    
                case BotState.Focused:
                    response.Add($"🎯 **Focused Strategy**");
                    response.Add(GenerateFocusedAdvice(context));
                    break;
                    
                default:
                    response.Add($"🎮 **Gaming Assistant Ready**");
                    response.Add(GenerateGeneralGamingAdvice(context));
                    break;
            }
            
            // Add vital status if concerning
            if (Vitals.ShouldAdaptBehavior())
            {
                response.Add($"\n*Status: {GetVitalStatusMessage()}*");
            }
            
            // Add contextual adaptations
            if (partialResponse.ContextualAdaptations.Any())
            {
                response.Add($"\n**Adaptations**: {string.Join(" • ", partialResponse.ContextualAdaptations)}");
            }
            
            return Task.FromResult(string.Join("\n", response));
        }
        
        private string GenerateDefensiveAdvice(LayeredContext context)
        {
            return "Focus on survival - find cover, assess threats, and plan your next move carefully.";
        }
        
        private string GenerateExplorationAdvice(LayeredContext context)
        {
            return "Great time to discover new areas! Check your map, gather resources, and look for hidden secrets.";
        }
        
        private string GenerateSupportiveAdvice(LayeredContext context)
        {
            return "Take your time - every expert was once a beginner. Focus on small improvements and celebrate progress!";
        }
        
        private string GenerateFocusedAdvice(LayeredContext context)
        {
            return "Lock in on your objective. Eliminate distractions and execute your plan with precision.";
        }
        
        private string GenerateGeneralGamingAdvice(LayeredContext context)
        {
            return $"Ready to assist with your gaming session! Current mode: {_currentGameMode}. What's your next move?";
        }
        
        private string GetVitalStatusMessage()
        {
            if (Vitals.Energy < 25) return "Running low on energy";
            if (Vitals.Confidence < 40) return "Need confidence boost";
            if (Vitals.SocialBattery < 30) return "Social battery running low";
            return "Monitoring vitals";
        }
        
        #endregion
        
        #region Enhanced CanHandle for Gaming Context
        
        public override bool CanHandle(BotContext context)
        {
            if (!base.CanHandle(context))
                return false;
            
            // Gaming-specific handling criteria
            var input = context.Input?.ToLower() ?? "";
            var gamingKeywords = new[] { "game", "play", "strategy", "level", "boss", "quest", "achievement", "skill" };
            
            var isGamingRelated = gamingKeywords.Any(keyword => input.Contains(keyword));
            
            // Check if we're in a gaming context based on conversation history
            if (context.State.ContainsKey("gaming_session") && (bool)context.State["gaming_session"])
            {
                return true;
            }
            
            // Check player skill level compatibility
            if (context.State.ContainsKey("player_skill_level"))
            {
                var playerSkill = (double)context.State["player_skill_level"];
                // Always handle if we can provide appropriate level guidance
                return true;
            }
            
            return isGamingRelated;
        }
        
        #endregion
    }
}
