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
        public async Task<BotResponse> ProcessWithSemanticKernelAsync(BotContext context, SemanticOrchestrationConfig config = null)
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
            
            // Use Semantic Kernel to intelligently select and prioritize roles
            var roleSelectionPrompt = $"""
                Given the intent analysis and available roles, select the optimal roles for this request:
                
                Intent Analysis: {analysis}
                
                Available Roles:
                {string.Join("\n", availableRoles.Select(r => $"- {r.RoleId}: {r.Name} (Tags: {string.Join(", ", r.Tags)}, Priority: {r.Priority})"))}
                
                Selection Criteria:
                - Relevance to user intent
                - Role capabilities and tags
                - Execution efficiency
                - Expected output quality
                
                Return the selected role IDs in order of execution priority.
                """;
                
            var selectionResult = await _kernel.InvokePromptAsync(roleSelectionPrompt);
            var selectedRoleIds = ParseSelectedRoles(selectionResult.ToString());
            
            return selectedRoleIds.Select(id => _roleRegistry.GetRole(id))
                                 .Where(role => role != null)
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
}
