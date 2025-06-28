using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HybridBot.Core;

namespace HybridBot.Roles
{
    /// <summary>
    /// Test role to verify GitHub Copilot integration with the hybrid bot system.
    /// This role demonstrates how Copilot can assist with creating new bot roles.
    /// 
    /// Frontmatter Configuration:
    /// - test_mode: true|false
    /// - response_delay: milliseconds
    /// - echo_input: true|false
    /// </summary>
    public class TestRole : BaseBotRole
    {
        public override string RoleId => "test";
        public override string Name => "Test Role";
        
        private bool _testMode = true;
        private int _responseDelay = 0;
        private bool _echoInput = true;
        
        public TestRole(ILogger<TestRole> logger) : base(logger)
        {
            Tags = new List<string> { "test", "demo", "validation" };
            Priority = 10; // Low priority for testing
        }
        
        protected override async Task OnInitializeAsync(IDictionary<string, object> config)
        {
            // TODO: GitHub Copilot should suggest configuration loading here
            // Try typing a comment like "// Load test_mode from config" and see Copilot suggestions
            
            await Task.CompletedTask;
        }
        
        public override bool CanHandle(BotContext context)
        {
            // TODO: GitHub Copilot should suggest test pattern matching
            // Try typing a comment like "// Handle test requests" and see suggestions
            
            return false; // Placeholder - Copilot should suggest implementation
        }
        
        protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
        {
            // TODO: GitHub Copilot should suggest test response generation
            // Try typing a comment like "// Generate test response with echo" and see suggestions
            
            return new BotResponse
            {
                Content = "Test response placeholder",
                IsComplete = true
            };
        }
    }
}
