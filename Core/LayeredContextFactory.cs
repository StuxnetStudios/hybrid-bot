using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using HybridBot.Core;

namespace HybridBot.Core
{
    /// <summary>
    /// Factory for creating layered contexts from various input sources.
    /// Provides intelligent context extraction and environmental awareness.
    /// </summary>
    public class LayeredContextFactory
    {
        private readonly ILogger<LayeredContextFactory> _logger;
        
        public LayeredContextFactory(ILogger<LayeredContextFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Create a layered context from a standard BotContext with intelligent inference
        /// </summary>
        public LayeredContext CreateFromBotContext(BotContext botContext)
        {
            _logger.LogDebug("Creating layered context from BotContext {RequestId}", botContext.RequestId);
            
            return new LayeredContext
            {
                BaseContext = botContext,
                Environment = ExtractEnvironmentalData(botContext),
                PlayerActions = ExtractPlayerActionData(botContext),
                BotInternalState = ExtractBotStateData(botContext),
                Temporal = CreateTemporalContext(botContext),
                Social = CreateSocialContext(botContext)
            };
        }
        
        /// <summary>
        /// Create a layered context with explicit environmental conditions
        /// </summary>
        public LayeredContext CreateWithEnvironment(BotContext botContext, Dictionary<string, object> environmentConditions)
        {
            var layeredContext = CreateFromBotContext(botContext);
            layeredContext.Environment.Conditions = environmentConditions;
            
            // Infer additional environmental data
            InferEnvironmentalThreats(layeredContext.Environment);
            InferEnvironmentalOpportunities(layeredContext.Environment);
            
            return layeredContext;
        }
        
        /// <summary>
        /// Create a gaming-specific layered context
        /// </summary>
        public LayeredContext CreateGamingContext(BotContext botContext, string gameMode = "normal")
        {
            var layeredContext = CreateFromBotContext(botContext);
            
            // Enhanced gaming environment
            layeredContext.Environment.CurrentEnvironment = gameMode;
            layeredContext.Environment.Conditions["game_mode"] = gameMode;
            
            // Extract gaming-specific data from input
            ExtractGamingEnvironmentalData(botContext.Input, layeredContext.Environment);
            ExtractGamingPlayerActions(botContext, layeredContext.PlayerActions);
            
            return layeredContext;
        }
        
        #region Environmental Data Extraction
        
        private EnvironmentalData ExtractEnvironmentalData(BotContext context)
        {
            var envData = new EnvironmentalData();
            
            // Extract from context state
            if (context.State.ContainsKey("environment"))
            {
                if (context.State["environment"] is Dictionary<string, object> envDict)
                {
                    envData.Conditions = new Dictionary<string, object>(envDict);
                }
            }
            
            // Extract from input text using keyword analysis
            ExtractEnvironmentalKeywords(context.Input, envData);
            
            // Set current environment based on context clues
            envData.CurrentEnvironment = DetermineEnvironmentType(context);
            
            // Extract resource availability
            ExtractResourceAvailability(context, envData);
            
            return envData;
        }
        
        private void ExtractEnvironmentalKeywords(string? input, EnvironmentalData envData)
        {
            if (string.IsNullOrEmpty(input)) return;
            
            var inputLower = input.ToLower();
            
            // Weather detection
            var weatherKeywords = new Dictionary<string, string>
            {
                ["sunny"] = "sunny",
                ["rain"] = "rainy",
                ["storm"] = "stormy",
                ["fog"] = "foggy",
                ["wind"] = "windy",
                ["snow"] = "snowy"
            };
            
            foreach (var (keyword, weather) in weatherKeywords)
            {
                if (inputLower.Contains(keyword))
                {
                    envData.Conditions["weather"] = weather;
                    break;
                }
            }
            
            // Terrain detection
            var terrainKeywords = new Dictionary<string, string>
            {
                ["mountain"] = "mountain",
                ["forest"] = "forest",
                ["desert"] = "desert",
                ["swamp"] = "swamp",
                ["city"] = "urban",
                ["building"] = "urban",
                ["cave"] = "underground"
            };
            
            foreach (var (keyword, terrain) in terrainKeywords)
            {
                if (inputLower.Contains(keyword))
                {
                    envData.Conditions["terrain"] = terrain;
                    break;
                }
            }
        }
        
        private string DetermineEnvironmentType(BotContext context)
        {
            var input = context.Input?.ToLower() ?? "";
            
            if (input.Contains("game") || input.Contains("play") || input.Contains("level"))
                return "gaming";
            
            if (input.Contains("work") || input.Contains("office") || input.Contains("business"))
                return "professional";
            
            if (input.Contains("learn") || input.Contains("study") || input.Contains("education"))
                return "educational";
            
            if (input.Contains("chat") || input.Contains("talk") || input.Contains("conversation"))
                return "social";
            
            return "normal";
        }
        
        private void ExtractResourceAvailability(BotContext context, EnvironmentalData envData)
        {
            if (context.State.ContainsKey("available_resources"))
            {
                if (context.State["available_resources"] is List<string> resources)
                {
                    envData.AvailableResources = new List<string>(resources);
                }
            }
            
            // Infer resources from input
            var input = context.Input?.ToLower() ?? "";
            if (input.Contains("help") || input.Contains("assist"))
                envData.AvailableResources.Add("assistance");
            
            if (input.Contains("information") || input.Contains("data"))
                envData.AvailableResources.Add("information");
        }
        
        private void InferEnvironmentalThreats(EnvironmentalData envData)
        {
            // Analyze conditions to infer threat levels
            if (envData.Conditions.ContainsKey("weather"))
            {
                var weather = envData.Conditions["weather"]?.ToString();
                if (!string.IsNullOrEmpty(weather))
                {
                    envData.ThreatLevels[weather] = weather switch
                    {
                        "stormy" => 0.8,
                        "rainy" => 0.4,
                        "foggy" => 0.6,
                        "windy" => 0.3,
                        _ => 0.1
                    };
                }
            }
            
            if (envData.Conditions.ContainsKey("terrain"))
            {
                var terrain = envData.Conditions["terrain"]?.ToString();
                if (!string.IsNullOrEmpty(terrain))
                {
                    envData.ThreatLevels[terrain] = terrain switch
                    {
                        "mountain" => 0.6,
                        "swamp" => 0.7,
                        "desert" => 0.5,
                        "underground" => 0.8,
                        _ => 0.2
                    };
                }
            }
        }
        
        private void InferEnvironmentalOpportunities(EnvironmentalData envData)
        {
            // Analyze conditions to infer opportunities
            if (envData.Conditions.ContainsKey("weather"))
            {
                var weather = envData.Conditions["weather"]?.ToString();
                if (!string.IsNullOrEmpty(weather))
                {
                    envData.OpportunityScores[weather] = weather switch
                    {
                        "sunny" => 0.8,
                        "rainy" => 0.3,
                        "foggy" => 0.2,
                        _ => 0.5
                    };
                }
            }
            
            // Learning opportunities
            if (envData.AvailableResources.Contains("information") || envData.AvailableResources.Contains("assistance"))
            {
                envData.OpportunityScores["learning"] = 0.7;
            }
        }
        
        #endregion
        
        #region Player Action Data Extraction
        
        private PlayerActionData ExtractPlayerActionData(BotContext context)
        {
            var playerData = new PlayerActionData();
            
            // Extract recent actions from state
            if (context.State.ContainsKey("recent_actions"))
            {
                if (context.State["recent_actions"] is List<string> actions)
                {
                    playerData.RecentActions = new List<string>(actions);
                }
            }
            
            // Calculate engagement level based on various factors
            playerData.EngagementLevel = CalculateEngagementLevel(context);
            
            // Extract current activity
            playerData.CurrentActivity = DetermineCurrentActivity(context);
            
            // Analyze action patterns
            AnalyzeActionFrequency(context, playerData);
            
            return playerData;
        }
        
        private double CalculateEngagementLevel(BotContext context)
        {
            var baseEngagement = 0.5;
            var input = context.Input ?? "";
            
            // Positive engagement indicators
            if (input.Contains("!") || input.Contains("?"))
                baseEngagement += 0.2;
            
            if (input.Length > 50)
                baseEngagement += 0.1;
            
            if (input.Contains("please") || input.Contains("help"))
                baseEngagement += 0.1;
            
            // Negative engagement indicators
            if (input.Length < 10)
                baseEngagement -= 0.2;
            
            if (input.ToLower().Contains("whatever") || input.ToLower().Contains("fine"))
                baseEngagement -= 0.3;
            
            return Math.Clamp(baseEngagement, 0.0, 1.0);
        }
        
        private string DetermineCurrentActivity(BotContext context)
        {
            var input = context.Input?.ToLower() ?? "";
            
            if (input.Contains("question") || input.Contains("ask"))
                return "questioning";
            
            if (input.Contains("help") || input.Contains("assist"))
                return "seeking_help";
            
            if (input.Contains("learn") || input.Contains("understand"))
                return "learning";
            
            if (input.Contains("play") || input.Contains("game"))
                return "gaming";
            
            return "conversing";
        }
        
        private void AnalyzeActionFrequency(BotContext context, PlayerActionData playerData)
        {
            if (context.State.ContainsKey("action_history"))
            {
                if (context.State["action_history"] is Dictionary<string, int> actionFreq)
                {
                    playerData.ActionFrequency = new Dictionary<string, int>(actionFreq);
                }
            }
            
            // Update with current action
            var currentAction = DetermineActionType(context.Input);
            if (playerData.ActionFrequency.ContainsKey(currentAction))
            {
                playerData.ActionFrequency[currentAction]++;
            }
            else
            {
                playerData.ActionFrequency[currentAction] = 1;
            }
        }
        
        private string DetermineActionType(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "unknown";
            
            var inputLower = input.ToLower();
            
            if (inputLower.Contains("?")) return "question";
            if (inputLower.Contains("help")) return "help_request";
            if (inputLower.Contains("explain")) return "explanation_request";
            if (inputLower.Contains("thanks") || inputLower.Contains("thank")) return "gratitude";
            
            return "statement";
        }
        
        #endregion
        
        #region Gaming-Specific Extraction
        
        private void ExtractGamingEnvironmentalData(string? input, EnvironmentalData envData)
        {
            if (string.IsNullOrEmpty(input)) return;
            
            var inputLower = input.ToLower();
            
            // Gaming environment types
            var gameEnvironments = new Dictionary<string, string>
            {
                ["dungeon"] = "dungeon",
                ["boss"] = "boss_room",
                ["village"] = "safe_zone",
                ["battlefield"] = "combat_zone",
                ["shop"] = "merchant_area"
            };
            
            foreach (var (keyword, environment) in gameEnvironments)
            {
                if (inputLower.Contains(keyword))
                {
                    envData.CurrentEnvironment = environment;
                    envData.Conditions["location_type"] = environment;
                    break;
                }
            }
            
            // Gaming threats
            var threats = new Dictionary<string, double>
            {
                ["enemy"] = 0.6,
                ["boss"] = 0.9,
                ["trap"] = 0.7,
                ["monster"] = 0.6
            };
            
            foreach (var (keyword, threatLevel) in threats)
            {
                if (inputLower.Contains(keyword))
                {
                    envData.ThreatLevels[keyword] = threatLevel;
                }
            }
            
            // Gaming resources
            var resources = new List<string> { "health_potion", "mana_crystal", "weapon", "armor", "gold" };
            foreach (var resource in resources)
            {
                if (inputLower.Contains(resource.Replace("_", " ")) || inputLower.Contains(resource))
                {
                    envData.AvailableResources.Add(resource);
                }
            }
        }
        
        private void ExtractGamingPlayerActions(BotContext context, PlayerActionData playerData)
        {
            var input = context.Input?.ToLower() ?? "";
            
            // Gaming action types
            var gamingActions = new List<string>
            {
                "attack", "defend", "cast", "heal", "explore", "search", "craft", "upgrade", "trade"
            };
            
            foreach (var action in gamingActions)
            {
                if (input.Contains(action))
                {
                    playerData.RecentActions.Add(action);
                    if (playerData.ActionFrequency.ContainsKey(action))
                    {
                        playerData.ActionFrequency[action]++;
                    }
                    else
                    {
                        playerData.ActionFrequency[action] = 1;
                    }
                }
            }
            
            // Gaming engagement patterns
            if (input.Contains("difficult") || input.Contains("hard") || input.Contains("stuck"))
            {
                playerData.EngagementLevel = Math.Max(0.2, playerData.EngagementLevel - 0.3);
            }
            
            if (input.Contains("fun") || input.Contains("awesome") || input.Contains("love"))
            {
                playerData.EngagementLevel = Math.Min(1.0, playerData.EngagementLevel + 0.3);
            }
        }
        
        #endregion
        
        #region Other Context Creation
        
        private BotStateData ExtractBotStateData(BotContext context)
        {
            var botData = new BotStateData();
            
            // Extract bot goals from state
            if (context.State.ContainsKey("bot_goals"))
            {
                if (context.State["bot_goals"] is List<string> goals)
                {
                    botData.ActiveGoals = new List<string>(goals);
                }
            }
            
            // Copy state as memory
            botData.Memory = new Dictionary<string, object>(context.State);
            
            // Determine current mode based on context
            botData.CurrentMode = DetermineEnvironmentType(context);
            
            return botData;
        }
        
        private TemporalContext CreateTemporalContext(BotContext context)
        {
            var temporal = new TemporalContext
            {
                CurrentTime = DateTime.UtcNow,
                SessionDuration = DateTime.UtcNow - context.Timestamp
            };
            
            // Extract historical patterns from state
            if (context.State.ContainsKey("interaction_patterns"))
            {
                if (context.State["interaction_patterns"] is List<string> patterns)
                {
                    temporal.HistoricalPatterns = new List<string>(patterns);
                }
            }
            
            return temporal;
        }
        
        private SocialContext CreateSocialContext(BotContext context)
        {
            var social = new SocialContext();
            
            // Determine relationship level
            social.UserRelationshipLevel = DetermineRelationshipLevel(context);
            
            // Extract conversation sentiment
            social.ConversationSentiment = AnalyzeSentiment(context.Input);
            
            // Extract conversation topics
            social.ConversationTopics = ExtractTopics(context);
            
            return social;
        }
        
        private string DetermineRelationshipLevel(BotContext context)
        {
            if (context.State.ContainsKey("interaction_count"))
            {
                if (int.TryParse(context.State["interaction_count"].ToString(), out int count))
                {
                    return count switch
                    {
                        < 3 => "new",
                        < 10 => "familiar",
                        < 50 => "regular",
                        _ => "expert"
                    };
                }
            }
            
            return "new";
        }
        
        private double AnalyzeSentiment(string? input)
        {
            if (string.IsNullOrEmpty(input)) return 0.0;
            
            var inputLower = input.ToLower();
            var sentiment = 0.0;
            
            // Positive indicators
            var positiveWords = new[] { "good", "great", "awesome", "love", "like", "happy", "excellent", "perfect" };
            var negativeWords = new[] { "bad", "terrible", "hate", "dislike", "sad", "awful", "horrible", "worst" };
            
            sentiment += positiveWords.Count(word => inputLower.Contains(word)) * 0.2;
            sentiment -= negativeWords.Count(word => inputLower.Contains(word)) * 0.2;
            
            return Math.Clamp(sentiment, -1.0, 1.0);
        }
        
        private List<string> ExtractTopics(BotContext context)
        {
            var topics = new List<string>();
            var input = context.Input?.ToLower() ?? "";
            
            var topicKeywords = new Dictionary<string, string>
            {
                ["game"] = "gaming",
                ["help"] = "assistance",
                ["learn"] = "education",
                ["work"] = "professional",
                ["chat"] = "social"
            };
            
            foreach (var (keyword, topic) in topicKeywords)
            {
                if (input.Contains(keyword))
                {
                    topics.Add(topic);
                }
            }
            
            return topics;
        }
        
        #endregion
    }
}
