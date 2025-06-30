 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using HybridBot.Core;

namespace HybridBot.Core
{
    /// <summary>
    /// Semantic Kernel-powered orchestrator that enhances the hybrid bot system
    /// with AI-driven role selection, planning, and intelligent conversation management.
    /// </summary>
    public class SemanticOrchestrator
    {
        private readonly RoleRegistry _roleRegistry;
        private readonly StateManager _stateManager;
        private readonly ILogger<SemanticOrchestrator> _logger;
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatService;
        
        public SemanticOrchestrator(
            RoleRegistry roleRegistry,
            StateManager stateManager, 
            ILogger<SemanticOrchestrator> logger,
            Kernel kernel)
        {
            _roleRegistry = roleRegistry ?? throw new ArgumentNullException(nameof(roleRegistry));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            RegisterKernelFunctions();
        }
        
        /// <summary>
        /// Process a request using Semantic Kernel for intelligent orchestration
        /// </summary>
        public async Task<BotResponse> ProcessWithSemanticKernelAsync(BotContext context, SemanticOrchestrationConfig? config = null)
        {
            config ??= new SemanticOrchestrationConfig();
            
            _logger.LogInformation("Processing request {RequestId} with Semantic Kernel orchestration", context.RequestId);
            
            // Load conversation state
            await _stateManager.LoadStateAsync(context);
            
            try
            {
                // Step 1: Analyze intent and determine optimal role selection strategy
                var intentAnalysis = await AnalyzeIntentAsync(context);
                
                // Step 2: Use SK to select and prioritize roles
                var selectedRoles = await SelectRolesWithSemanticKernelAsync(context, intentAnalysis, config);
                
                // Step 3: Create execution plan
                var executionPlan = await CreateExecutionPlanAsync(context, selectedRoles, config);
                
                // Step 4: Execute plan with SK coordination
                var response = await ExecutePlanAsync(context, executionPlan, config);
                
                // Step 5: Post-process response with SK enhancement
                var enhancedResponse = await EnhanceResponseAsync(context, response, config);
                
                // Save updated state
                await _stateManager.SaveStateAsync(context);
                
                _logger.LogInformation("Completed Semantic Kernel processing for request {RequestId}", context.RequestId);
                return enhancedResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Semantic Kernel orchestration for request {RequestId}", context.RequestId);
                
                // Fallback to basic orchestration
                return await FallbackToBasicOrchestrationAsync(context);
            }
        }
        
        private async Task<IntentAnalysis> AnalyzeIntentAsync(BotContext context)
        {
            var prompt = $"""
                Analyze the following user input and conversation context to determine intent and appropriate bot role selection:
                
                User Input: "{context.Input}"
                Conversation ID: {context.ConversationId ?? "N/A"}
                Previous Context: {GetContextSummary(context)}
                
                Available Roles: {GetAvailableRolesDescription()}
                
                Provide analysis in the following format:
                - Primary Intent: [intent category]
                - Confidence: [0.0-1.0]
                - Recommended Roles: [comma-separated role IDs]
                - Execution Strategy: [sequential|parallel|pipeline]
                - Reasoning: [brief explanation]
                """;
                
            var result = await _kernel.InvokePromptAsync(prompt);
            
            return ParseIntentAnalysis(result.ToString());
        }
        
        private async Task<List<IBotRole>> SelectRolesWithSemanticKernelAsync(
            BotContext context, 
            IntentAnalysis analysis, 
            SemanticOrchestrationConfig config)
        {
            var availableRoles = _roleRegistry.GetCapableRoles(context).ToList();
            
            if (!availableRoles.Any())
            {
                _logger.LogWarning("No capable roles found for request {RequestId}", context.RequestId);
                return new List<IBotRole>();
            }
            
            // Enhanced role selection for contextual bots
            var contextualRoles = availableRoles.OfType<IContextualBotRole>().ToList();
            var traditionalRoles = availableRoles.Except(contextualRoles.Cast<IBotRole>()).ToList();
            
            // Use Semantic Kernel to intelligently select and prioritize roles
            var roleSelectionPrompt = $"""
                Given the intent analysis and available roles, select the optimal roles for this request:
                
                Intent Analysis: {analysis}
                
                Traditional Roles:
                {string.Join("\n", traditionalRoles.Select(r => $"- {r.RoleId}: {r.Name} (Tags: {string.Join(", ", r.Tags)}, Priority: {r.Priority})"))}
                
                Contextual-Aware Roles (Enhanced capabilities):
                {string.Join("\n", contextualRoles.Select(r => $"- {r.RoleId}: {r.Name} (Tags: {string.Join(", ", r.Tags)}, Priority: {r.Priority}, State: {r.CurrentState}, Vitals: Health={r.Vitals.Health:F0}, Energy={r.Vitals.Energy:F0})"))}
                
                Selection Criteria:
                - Relevance to user intent
                - Role capabilities and tags
                - Current bot state and vital levels (for contextual roles)
                - Execution efficiency
                - Expected output quality
                - Contextual awareness benefits
                
                Prefer contextual roles when:
                - Complex environmental adaptation is needed
                - User engagement tracking is important
                - State-based behavior is beneficial
                - Location/spatial awareness adds value
                
                Return the selected role IDs in order of execution priority.
                """;
                
            var selectionResult = await _kernel.InvokePromptAsync(roleSelectionPrompt);
            var selectedRoleIds = ParseSelectedRoles(selectionResult.ToString());
            
            return selectedRoleIds.Select(id => _roleRegistry.GetRole(id))
                                 .Where(role => role != null)
                                 .Cast<IBotRole>()
                                 .ToList();
        }
        
        private async Task<ExecutionPlan> CreateExecutionPlanAsync(
            BotContext context, 
            List<IBotRole> selectedRoles, 
            SemanticOrchestrationConfig config)
        {
            if (!selectedRoles.Any())
            {
                return new ExecutionPlan { Steps = new List<ExecutionStep>() };
            }
            
            var planningPrompt = $"""
                Create an execution plan for the following roles to handle this user request:
                
                User Request: "{context.Input}"
                Selected Roles: {string.Join(", ", selectedRoles.Select(r => r.RoleId))}
                
                Consider:
                - Dependencies between roles
                - Optimal execution order
                - Data flow between roles
                - Error handling strategies
                
                Create a step-by-step execution plan with role assignments and data flow.
                """;
                
            var planResult = await _kernel.InvokePromptAsync(planningPrompt);
            
            return ParseExecutionPlan(planResult.ToString(), selectedRoles);
        }
        
        private async Task<BotResponse> ExecutePlanAsync(
            BotContext context, 
            ExecutionPlan plan, 
            SemanticOrchestrationConfig config)
        {
            var aggregatedResponse = new BotResponse
            {
                Content = "",
                Metadata = new Dictionary<string, object>(),
                UpdatedState = new Dictionary<string, object>()
            };
            
            foreach (var step in plan.Steps)
            {
                try
                {
                    _logger.LogDebug("Executing step: {StepId} with role: {RoleId}", step.StepId, step.Role.RoleId);
                    
                    // Execute the role
                    var stepResponse = await step.Role.ExecuteAsync(context);
                    
                    // Merge responses
                    await MergeResponsesAsync(aggregatedResponse, stepResponse, step);
                    
                    // Update context for next step
                    foreach (var kvp in stepResponse.UpdatedState)
                    {
                        context.State[kvp.Key] = kvp.Value;
                    }
                    
                    // Use SK to determine if we should continue or modify the plan
                    if (config.EnableAdaptivePlanning)
                    {
                        var shouldContinue = await EvaluateStepResultAsync(context, stepResponse, plan, step);
                        if (!shouldContinue) break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing step {StepId} with role {RoleId}", step.StepId, step.Role.RoleId);
                    
                    if (config.StopOnStepFailure)
                        break;
                }
            }
            
            return aggregatedResponse;
        }
        
        private async Task<BotResponse> EnhanceResponseAsync(
            BotContext context, 
            BotResponse response, 
            SemanticOrchestrationConfig config)
        {
            if (!config.EnableResponseEnhancement)
                return response;
                
            var enhancementPrompt = $"""
                Enhance the following bot response for better user experience:
                
                Original Response: "{response.Content}"
                User Context: "{context.Input}"
                Response Metadata: {string.Join(", ", response.Metadata.Select(m => $"{m.Key}={m.Value}"))}
                
                Enhancement goals:
                - Improve clarity and coherence
                - Ensure appropriate tone
                - Add helpful context if needed
                - Maintain technical accuracy
                
                Return the enhanced response content.
                """;
                
            var enhancedContent = await _kernel.InvokePromptAsync(enhancementPrompt);
            
            response.Content = enhancedContent.ToString();
            response.Metadata["enhanced_by_semantic_kernel"] = true;
            response.Metadata["enhancement_timestamp"] = DateTime.UtcNow;
            
            return response;
        }
        
        private void RegisterKernelFunctions()
        {
            // Register hybrid bot functions that SK can use
            var hybridBotPlugin = _kernel.CreatePluginFromType<HybridBotPlugin>();
            _kernel.Plugins.Add(hybridBotPlugin);
        }
        
        private async Task<BotResponse> FallbackToBasicOrchestrationAsync(BotContext context)
        {
            _logger.LogInformation("Falling back to basic orchestration for request {RequestId}", context.RequestId);
            
            var capableRoles = _roleRegistry.GetCapableRoles(context);
            var firstRole = capableRoles.FirstOrDefault();
            
            if (firstRole == null)
            {
                return new BotResponse
                {
                    Content = "I'm unable to process your request at the moment. Please try again later.",
                    IsComplete = false,
                    Metadata = new Dictionary<string, object> { ["fallback_used"] = true }
                };
            }
            
            return await firstRole.ExecuteAsync(context);
        }
        
        // Helper methods for parsing and processing
        private string GetContextSummary(BotContext context)
        {
            if (context.State.TryGetValue("conversation_history", out var history) && history is List<string> historyList)
            {
                return string.Join("; ", historyList.TakeLast(3));
            }
            return "No previous context";
        }
        
        private string GetAvailableRolesDescription()
        {
            var roles = _roleRegistry.GetAllRoles();
            return string.Join(", ", roles.Select(r => $"{r.RoleId}({string.Join(",", r.Tags)})"));
        }
        
        private IntentAnalysis ParseIntentAnalysis(string analysisResult)
        {
            // Simple parsing - in production, use more robust parsing
            return new IntentAnalysis
            {
                PrimaryIntent = "general_inquiry",
                Confidence = 0.8,
                RecommendedRoles = new List<string> { "responder" },
                ExecutionStrategy = ExecutionMode.FirstMatch,
                Reasoning = analysisResult
            };
        }
        
        private List<string> ParseSelectedRoles(string selectionResult)
        {
            // Extract role IDs from the SK response
            var roles = new List<string>();
            var lines = selectionResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (line.Contains("responder") || line.Contains("summarizer") || line.Contains("test"))
                {
                    if (line.Contains("responder")) roles.Add("responder");
                    if (line.Contains("summarizer")) roles.Add("summarizer");
                    if (line.Contains("test")) roles.Add("test");
                }
            }
            
            return roles.Any() ? roles : new List<string> { "responder" };
        }
        
        private ExecutionPlan ParseExecutionPlan(string planResult, List<IBotRole> selectedRoles)
        {
            var steps = selectedRoles.Select((role, index) => new ExecutionStep
            {
                StepId = $"step_{index + 1}",
                Role = role,
                Dependencies = new List<string>(),
                ExpectedOutput = $"Output from {role.Name}"
            }).ToList();
            
            return new ExecutionPlan { Steps = steps };
        }
        
        private async Task MergeResponsesAsync(BotResponse aggregated, BotResponse stepResponse, ExecutionStep step)
        {
            aggregated.Content += stepResponse.Content + "\n\n";
            
            foreach (var kvp in stepResponse.Metadata)
            {
                aggregated.Metadata[$"{step.Role.RoleId}_{kvp.Key}"] = kvp.Value;
            }
            
            foreach (var kvp in stepResponse.UpdatedState)
            {
                aggregated.UpdatedState[kvp.Key] = kvp.Value;
            }
        }
        
        private async Task<bool> EvaluateStepResultAsync(BotContext context, BotResponse stepResponse, ExecutionPlan plan, ExecutionStep currentStep)
        {
            var evaluationPrompt = $"""
                Evaluate if the current execution should continue based on this step result:
                
                Step: {currentStep.StepId} ({currentStep.Role.Name})
                Result: "{stepResponse.Content}"
                Success: {stepResponse.IsComplete}
                
                Should execution continue? Consider:
                - Quality of current result
                - Remaining steps value
                - User satisfaction potential
                
                Return: CONTINUE or STOP with brief reasoning.
                """;
                
            var evaluation = await _kernel.InvokePromptAsync(evaluationPrompt);
            return evaluation.ToString().ToUpper().Contains("CONTINUE");
        }
        
        /// <summary>
        /// Enhanced processing specifically for contextual bots with layered context adaptation
        /// </summary>
        public async Task<BotResponse> ProcessWithContextualAdaptationAsync(
            BotContext context, 
            LayeredContext? layeredContext = null,
            SemanticOrchestrationConfig? config = null)
        {
            config ??= new SemanticOrchestrationConfig();
            
            _logger.LogInformation("Processing request {RequestId} with contextual adaptation", context.RequestId);
            
            // Load conversation state
            await _stateManager.LoadStateAsync(context);
            
            try
            {
                // Step 1: Create or enhance layered context if not provided
                if (layeredContext == null)
                {
                    layeredContext = await CreateLayeredContextFromStandardContextAsync(context);
                }
                
                // Step 2: Analyze context complexity and recommend contextual vs traditional roles
                var contextAnalysis = await AnalyzeContextualComplexityAsync(context, layeredContext);
                
                // Step 3: Select contextual roles with enhanced awareness
                var selectedRoles = await SelectContextualRolesAsync(context, layeredContext, contextAnalysis, config);
                
                // Step 4: Execute with contextual adaptation
                var response = await ExecuteWithContextualAdaptationAsync(context, layeredContext, selectedRoles, config);
                
                // Step 5: Enhance response with contextual insights
                var enhancedResponse = await EnhanceContextualResponseAsync(context, layeredContext, response, config);
                
                // Save updated state
                await _stateManager.SaveStateAsync(context);
                
                _logger.LogInformation("Completed contextual adaptation processing for request {RequestId}", context.RequestId);
                return enhancedResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in contextual adaptation for request {RequestId}", context.RequestId);
                
                // Fallback to standard semantic processing
                return await ProcessWithSemanticKernelAsync(context, config);
            }
        }
        
        private async Task<LayeredContext> CreateLayeredContextFromStandardContextAsync(BotContext context)
        {
            // Use Semantic Kernel to extract layered context from standard context
            var contextExtractionPrompt = $"""
                Extract detailed contextual information from the following bot context:
                
                User Input: "{context.Input}"
                Conversation State: {string.Join(", ", context.State.Select(kvp => $"{kvp.Key}={kvp.Value}"))}
                User ID: {context.UserId}
                
                Extract and categorize information into:
                1. Environmental factors (weather, location, conditions, resources, threats)
                2. Player/User actions (recent activities, engagement level, preferences)
                3. Temporal context (time patterns, session duration, historical data)
                4. Social context (relationship level, sentiment, conversation topics)
                
                Return structured data that can be used for contextual bot adaptation.
                """;
                
            var extractionResult = await _kernel.InvokePromptAsync(contextExtractionPrompt);
            
            // Parse the result and create LayeredContext
            return ParseLayeredContextFromSemanticResult(extractionResult.ToString(), context);
        }
        
        private async Task<ContextAnalysis> AnalyzeContextualComplexityAsync(BotContext context, LayeredContext layeredContext)
        {
            var complexityPrompt = $"""
                Analyze the complexity and contextual requirements for this request:
                
                User Input: "{context.Input}"
                Environmental Complexity: {layeredContext.Environment.Conditions.Count} conditions, {layeredContext.Environment.ThreatLevels.Count} threats
                Player Engagement: {layeredContext.PlayerActions.EngagementLevel}
                Social Complexity: {layeredContext.Social.ConversationTopics.Count} topics
                
                Determine:
                1. Does this request benefit from contextual awareness? (yes/no)
                2. Complexity Score (0.0-1.0)
                3. Recommended bot capabilities (state management, vital tracking, geospatial, etc.)
                4. Priority for contextual vs traditional roles
                
                Return structured analysis.
                """;
                
            var analysisResult = await _kernel.InvokePromptAsync(complexityPrompt);
            return ParseContextAnalysis(analysisResult.ToString());
        }
        
        private async Task<List<IBotRole>> SelectContextualRolesAsync(
            BotContext context, 
            LayeredContext layeredContext, 
            ContextAnalysis analysis,
            SemanticOrchestrationConfig config)
        {
            var availableRoles = _roleRegistry.GetCapableRoles(context).ToList();
            var contextualRoles = availableRoles.OfType<IContextualBotRole>().ToList();
            
            if (!contextualRoles.Any())
            {
                _logger.LogWarning("No contextual roles available, falling back to standard role selection");
                return await SelectRolesWithSemanticKernelAsync(context, new IntentAnalysis(), config);
            }
            
            var selectionPrompt = $"""
                Select optimal contextual roles for this complex request:
                
                Context Analysis: {analysis}
                User Intent: "{context.Input}"
                
                Available Contextual Roles:
                {string.Join("\n", contextualRoles.Select(r => 
                    $"- {r.RoleId}: {r.Name} " +
                    $"(State: {r.CurrentState}, Health: {r.Vitals.Health:F0}, Energy: {r.Vitals.Energy:F0}, " +
                    $"Tags: {string.Join(", ", r.Tags)})"))}
                
                Consider:
                - Current bot states and vital levels
                - Contextual adaptation capabilities
                - Environmental awareness needs
                - Player engagement patterns
                - Geospatial requirements
                
                Select roles that can best leverage contextual information.
                """;
                
            var selectionResult = await _kernel.InvokePromptAsync(selectionPrompt);
            var selectedRoleIds = ParseSelectedRoles(selectionResult.ToString());
            
            return selectedRoleIds.Select(id => _roleRegistry.GetRole(id))
                                 .Where(role => role != null)
                                 .Cast<IBotRole>()
                                 .ToList();
        }
        
        private async Task<BotResponse> ExecuteWithContextualAdaptationAsync(
            BotContext context, 
            LayeredContext layeredContext, 
            List<IBotRole> selectedRoles,
            SemanticOrchestrationConfig config)
        {
            var aggregatedResponse = new BotResponse
            {
                Content = "",
                Metadata = new Dictionary<string, object>(),
                UpdatedState = new Dictionary<string, object>()
            };
            
            foreach (var role in selectedRoles)
            {
                try
                {
                    BotResponse stepResponse;
                    
                    // If it's a contextual role, use enhanced adaptation
                    if (role is IContextualBotRole contextualRole)
                    {
                        _logger.LogDebug("Executing contextual role {RoleId} with layered context", role.RoleId);
                        var contextualResponse = await contextualRole.AdaptToContextAsync(layeredContext);
                        
                        // Convert ContextualResponse to BotResponse
                        stepResponse = new BotResponse
                        {
                            Content = contextualResponse.Content,
                            IsComplete = contextualResponse.IsComplete,
                            Metadata = new Dictionary<string, object>
                            {
                                ["contextual_adaptations"] = contextualResponse.ContextualAdaptations,
                                ["environmental_observations"] = contextualResponse.EnvironmentalObservations,
                                ["confidence_level"] = contextualResponse.ConfidenceLevel,
                                ["state_change"] = contextualResponse.StateChange?.ToString() ?? "None",
                                ["vital_changes"] = contextualResponse.VitalChanges.Count
                            }
                        };
                    }
                    else
                    {
                        // Standard execution for traditional roles
                        _logger.LogDebug("Executing traditional role {RoleId}", role.RoleId);
                        stepResponse = await role.ExecuteAsync(context);
                    }
                    
                    // Merge responses
                    await MergeResponsesAsync(aggregatedResponse, stepResponse, new ExecutionStep { Role = role });
                    
                    // Update context for next role
                    foreach (var kvp in stepResponse.UpdatedState)
                    {
                        context.State[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing role {RoleId} with contextual adaptation", role.RoleId);
                    
                    if (config.StopOnStepFailure)
                        break;
                }
            }
            
            return aggregatedResponse;
        }
        
        private async Task<BotResponse> EnhanceContextualResponseAsync(
            BotContext context, 
            LayeredContext layeredContext, 
            BotResponse response,
            SemanticOrchestrationConfig config)
        {
            if (!config.EnableResponseEnhancement)
                return response;
                
            var enhancementPrompt = $"""
                Enhance this contextually-adapted bot response:
                
                Original Response: "{response.Content}"
                User Context: "{context.Input}"
                Environmental Context: {layeredContext.Environment.Conditions.Count} conditions
                Player Engagement: {layeredContext.PlayerActions.EngagementLevel}
                Contextual Adaptations: {response.Metadata.GetValueOrDefault("contextual_adaptations", "None")}
                
                Enhancement goals:
                - Leverage contextual insights for more relevant responses
                - Highlight environmental awareness and adaptations
                - Ensure appropriate engagement level matching
                - Maintain contextual coherence
                - Add value from layered context processing
                
                Return enhanced response that showcases contextual intelligence.
                """;
                
            var enhancedContent = await _kernel.InvokePromptAsync(enhancementPrompt);
            
            response.Content = enhancedContent.ToString();
            response.Metadata["enhanced_with_contextual_awareness"] = true;
            response.Metadata["enhancement_timestamp"] = DateTime.UtcNow;
            
            return response;
        }
    }
    
    /// <summary>
    /// Configuration for Semantic Kernel orchestration
    /// </summary>
    public class SemanticOrchestrationConfig
    {
        public bool EnableAdaptivePlanning { get; set; } = true;
        public bool EnableResponseEnhancement { get; set; } = true;
        public bool StopOnStepFailure { get; set; } = false;
        public double MinConfidenceThreshold { get; set; } = 0.7;
        public int MaxExecutionSteps { get; set; } = 5;
    }
    
    /// <summary>
    /// Intent analysis result from Semantic Kernel
    /// </summary>
    public class IntentAnalysis
    {
        public string PrimaryIntent { get; set; } = "";
        public double Confidence { get; set; }
        public List<string> RecommendedRoles { get; set; } = new();
        public ExecutionMode ExecutionStrategy { get; set; }
        public string Reasoning { get; set; } = "";
    }
    
    /// <summary>
    /// Execution plan for role orchestration
    /// </summary>
    public class ExecutionPlan
    {
        public List<ExecutionStep> Steps { get; set; } = new();
    }
    
    /// <summary>
    /// Individual step in execution plan
    /// </summary>
    public class ExecutionStep
    {
        public string StepId { get; set; } = "";
        public IBotRole Role { get; set; } = null!;
        public List<string> Dependencies { get; set; } = new();
        public string ExpectedOutput { get; set; } = "";
    }
}

/// <summary>
/// Analysis of contextual complexity and requirements
/// </summary>
public class ContextAnalysis
{
    public bool RequiresContextualAwareness { get; set; }
    public double ComplexityScore { get; set; }
    public List<string> RecommendedCapabilities { get; set; } = new();
    public bool PreferContextualRoles { get; set; }
    public string Reasoning { get; set; } = "";
}

/// <summary>
/// Semantic Kernel plugin exposing hybrid bot functions
/// </summary>
public class HybridBotPlugin
{
    [KernelFunction, Description("Get information about available bot roles")]
    public string GetAvailableRoles(
        [Description("Optional tag filter")] string? tagFilter = null)
    {
        // This would be injected with actual role registry
        return "responder, summarizer, test";
    }
    
    [KernelFunction, Description("Analyze conversation context for role selection")]
    public string AnalyzeConversationContext(
        [Description("User input")] string userInput,
        [Description("Conversation history")] string? history = null)
    {
        return $"Analyzed input: {userInput}. Suggested approach: conversational response.";
    }
}
