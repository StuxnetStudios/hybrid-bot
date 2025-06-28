using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HybridBot.Core
{
    /// <summary>
    /// Orchestrates execution of bot roles based on context and configuration.
    /// Supports sequential and parallel execution patterns with state management.
    /// </summary>
    public class BotOrchestrator
    {
        private readonly RoleRegistry _roleRegistry;
        private readonly ILogger<BotOrchestrator> _logger;
        private readonly StateManager _stateManager;
        
        public BotOrchestrator(
            RoleRegistry roleRegistry, 
            ILogger<BotOrchestrator> logger,
            StateManager stateManager)
        {
            _roleRegistry = roleRegistry ?? throw new ArgumentNullException(nameof(roleRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        }
        
        /// <summary>
        /// Process a request through the orchestration pipeline
        /// </summary>
        public async Task<BotResponse> ProcessAsync(BotContext context, OrchestrationConfig config = null)
        {
            config ??= new OrchestrationConfig();
            
            _logger.LogInformation("Processing request {RequestId} with orchestration mode: {Mode}", 
                context.RequestId, config.ExecutionMode);
            
            // Load conversation state
            await _stateManager.LoadStateAsync(context);
            
            try
            {
                var response = config.ExecutionMode switch
                {
                    ExecutionMode.FirstMatch => await ExecuteFirstMatchAsync(context, config),
                    ExecutionMode.Sequential => await ExecuteSequentialAsync(context, config),
                    ExecutionMode.Parallel => await ExecuteParallelAsync(context, config),
                    ExecutionMode.Pipeline => await ExecutePipelineAsync(context, config),
                    _ => throw new ArgumentException($"Unknown execution mode: {config.ExecutionMode}")
                };
                
                // Save updated state
                if (response.UpdatedState.Any())
                {
                    foreach (var kvp in response.UpdatedState)
                    {
                        context.State[kvp.Key] = kvp.Value;
                    }
                }
                
                await _stateManager.SaveStateAsync(context);
                
                _logger.LogInformation("Completed processing request {RequestId}", context.RequestId);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request {RequestId}", context.RequestId);
                throw;
            }
        }
        
        private async Task<BotResponse> ExecuteFirstMatchAsync(BotContext context, OrchestrationConfig config)
        {
            var capableRoles = GetFilteredRoles(context, config);
            var firstRole = capableRoles.FirstOrDefault();
            
            if (firstRole == null)
            {
                return new BotResponse
                {
                    Content = "No suitable role found to handle this request.",
                    IsComplete = false
                };
            }
            
            return await firstRole.ExecuteAsync(context);
        }
        
        private async Task<BotResponse> ExecuteSequentialAsync(BotContext context, OrchestrationConfig config)
        {
            var capableRoles = GetFilteredRoles(context, config).ToList();
            
            if (!capableRoles.Any())
            {
                return new BotResponse
                {
                    Content = "No suitable roles found to handle this request.",
                    IsComplete = false
                };
            }
            
            var aggregatedResponse = new BotResponse
            {
                Content = "",
                Metadata = new Dictionary<string, object>(),
                UpdatedState = new Dictionary<string, object>()
            };
            
            foreach (var role in capableRoles)
            {
                var response = await role.ExecuteAsync(context);
                
                // Aggregate responses
                aggregatedResponse.Content += response.Content + "\n\n";
                
                // Merge metadata
                foreach (var kvp in response.Metadata)
                {
                    aggregatedResponse.Metadata[$"{role.RoleId}_{kvp.Key}"] = kvp.Value;
                }
                
                // Merge state updates
                foreach (var kvp in response.UpdatedState)
                {
                    aggregatedResponse.UpdatedState[kvp.Key] = kvp.Value;
                    context.State[kvp.Key] = kvp.Value; // Update context for next role
                }
                
                // Check if execution should stop
                if (!response.IsComplete && config.StopOnFirstFailure)
                {
                    aggregatedResponse.IsComplete = false;
                    break;
                }
            }
            
            return aggregatedResponse;
        }
        
        private async Task<BotResponse> ExecuteParallelAsync(BotContext context, OrchestrationConfig config)
        {
            var capableRoles = GetFilteredRoles(context, config).ToList();
            
            if (!capableRoles.Any())
            {
                return new BotResponse
                {
                    Content = "No suitable roles found to handle this request.",
                    IsComplete = false
                };
            }
            
            var tasks = capableRoles.Select(role => role.ExecuteAsync(context)).ToArray();
            var responses = await Task.WhenAll(tasks);
            
            var aggregatedResponse = new BotResponse
            {
                Content = string.Join("\n\n", responses.Select(r => r.Content)),
                Metadata = new Dictionary<string, object>(),
                UpdatedState = new Dictionary<string, object>()
            };
            
            // Merge all responses
            for (int i = 0; i < responses.Length; i++)
            {
                var response = responses[i];
                var role = capableRoles[i];
                
                // Merge metadata with role prefix
                foreach (var kvp in response.Metadata)
                {
                    aggregatedResponse.Metadata[$"{role.RoleId}_{kvp.Key}"] = kvp.Value;
                }
                
                // Merge state updates (last write wins for conflicts)
                foreach (var kvp in response.UpdatedState)
                {
                    aggregatedResponse.UpdatedState[kvp.Key] = kvp.Value;
                }
            }
            
            aggregatedResponse.IsComplete = responses.All(r => r.IsComplete);
            
            return aggregatedResponse;
        }
        
        private async Task<BotResponse> ExecutePipelineAsync(BotContext context, OrchestrationConfig config)
        {
            var capableRoles = GetFilteredRoles(context, config).ToList();
            
            if (!capableRoles.Any())
            {
                return new BotResponse
                {
                    Content = "No suitable roles found to handle this request.",
                    IsComplete = false
                };
            }
            
            var currentContext = context;
            BotResponse lastResponse = null;
            
            foreach (var role in capableRoles)
            {
                var response = await role.ExecuteAsync(currentContext);
                
                // Update context for next role in pipeline
                foreach (var kvp in response.UpdatedState)
                {
                    currentContext.State[kvp.Key] = kvp.Value;
                }
                
                // Use response content as input for next role
                if (response.NextRoles.Any())
                {
                    currentContext.Input = response.Content;
                }
                
                lastResponse = response;
                
                // Break pipeline if role indicates completion
                if (response.IsComplete && !response.NextRoles.Any())
                {
                    break;
                }
            }
            
            return lastResponse ?? new BotResponse
            {
                Content = "Pipeline execution completed with no output.",
                IsComplete = true
            };
        }
        
        private IEnumerable<IBotRole> GetFilteredRoles(BotContext context, OrchestrationConfig config)
        {
            IEnumerable<IBotRole> roles;
            
            if (config.SpecificRoles?.Any() == true)
            {
                // Use specific roles if provided
                roles = config.SpecificRoles.Select(roleId => _roleRegistry.GetRole(roleId))
                    .Where(role => role != null);
            }
            else if (config.RequiredTags?.Any() == true)
            {
                // Filter by required tags
                roles = _roleRegistry.GetRolesByTags(config.RequiredTags.ToArray());
            }
            else
            {
                // Use all capable roles
                roles = _roleRegistry.GetCapableRoles(context);
            }
            
            // Apply additional filters
            if (config.ExcludedTags?.Any() == true)
            {
                roles = roles.Where(role => !role.Tags.Any(tag => config.ExcludedTags.Contains(tag)));
            }
            
            return roles;
        }
    }
    
    /// <summary>
    /// Configuration for orchestration execution
    /// </summary>
    public class OrchestrationConfig
    {
        public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.FirstMatch;
        public List<string> SpecificRoles { get; set; }
        public List<string> RequiredTags { get; set; }
        public List<string> ExcludedTags { get; set; }
        public bool StopOnFirstFailure { get; set; } = false;
        public int MaxConcurrency { get; set; } = 10;
    }
    
    /// <summary>
    /// Execution modes for orchestration
    /// </summary>
    public enum ExecutionMode
    {
        FirstMatch,  // Execute first capable role
        Sequential,  // Execute all capable roles in sequence
        Parallel,    // Execute all capable roles in parallel
        Pipeline     // Execute roles in pipeline, passing output to next
    }
}
