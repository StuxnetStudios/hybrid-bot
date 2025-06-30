using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using HybridBot.Core;
using HybridBot.Capabilities;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

namespace HybridBot
{
    /// <summary>
    /// Main program entry point for the Hybrid Bot System.
    /// Uses the new architecture where HybridBot inherits from SK Agent and composes role capabilities.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args ?? Array.Empty<string>())
                .Build();

            // Create host with dependency injection
            var host = CreateHostBuilder(args ?? Array.Empty<string>(), configuration).Build();
            
            // Get services and run demo
            using var scope = host.Services.CreateScope();
            var demo = scope.ServiceProvider.GetRequiredService<BotDemo>();
            
            await demo.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configuration) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Add logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddDebug();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });

                    // Add configuration
                    services.AddSingleton(configuration);

                    // Register Skynet-lite configuration as a singleton
                    services.AddSingleton<SkynetLiteConfig>(provider =>
                    {
                        return configuration.GetSection("SkynetLite").Get<SkynetLiteConfig>() ?? new SkynetLiteConfig();
                    });

                    // Add HTTP client for Skynet-lite
                    services.AddHttpClient<SkynetLiteConnector>(client =>
                    {
                        var skynetConfig = configuration.GetSection("SkynetLite").Get<SkynetLiteConfig>() ?? new SkynetLiteConfig();
                        client.BaseAddress = new Uri(skynetConfig.BaseUrl);
                        client.Timeout = TimeSpan.FromSeconds(skynetConfig.TimeoutSeconds);
                        
                        if (!string.IsNullOrEmpty(skynetConfig.ApiKey))
                        {
                            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {skynetConfig.ApiKey}");
                        }
                    });
                    
                    // Configure Semantic Kernel with Skynet-lite only
                    services.AddSingleton<Kernel>(serviceProvider =>
                    {
                        var kernelBuilder = Kernel.CreateBuilder();
                        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                        
                        // Get Skynet-lite configuration from DI
                        var skynetConfig = serviceProvider.GetRequiredService<SkynetLiteConfig>();
                        
                        // Create and register Skynet-lite connector
                        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
                        var skynetLogger = serviceProvider.GetRequiredService<ILogger<SkynetLiteConnector>>();
                        var skynetConnector = new SkynetLiteConnector(httpClient, skynetLogger, skynetConfig);
                        
                        kernelBuilder.Services.AddSingleton<IChatCompletionService>(skynetConnector);
                        
                        if (skynetConfig.MockMode || string.IsNullOrEmpty(skynetConfig.ApiKey))
                        {
                            logger.LogInformation("Configured Semantic Kernel with Skynet-lite service (Mock Mode)");
                        }
                        else
                        {
                            logger.LogInformation("Configured Semantic Kernel with Skynet-lite service");
                        }
                        
                        return kernelBuilder.Build();
                    });

                    // Add core services
                    services.AddSingleton<StateManager>();
                    services.AddSingleton<SkynetLiteConnector>();

                    // Add capabilities
                    services.AddTransient<SummarizerCapability>();
                    services.AddTransient<ResponderCapability>();

                    // Add the main StuxBot (composition-based agent)
                    services.AddSingleton<StuxBotAgent>(serviceProvider =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<StuxBotAgent>>();
                        var kernel = serviceProvider.GetRequiredService<Kernel>();
                        var stateManager = serviceProvider.GetRequiredService<StateManager>();
                        var skynetConnector = serviceProvider.GetRequiredService<SkynetLiteConnector>();
                        
                        // Create capabilities
                        var capabilities = new List<IRoleCapability>
                        {
                            serviceProvider.GetRequiredService<SummarizerCapability>(),
                            serviceProvider.GetRequiredService<ResponderCapability>()
                        };
                        
                        return new StuxBotAgent(logger, kernel, stateManager, skynetConnector, capabilities);
                    });

                    // Add demo service
                    services.AddTransient<BotDemo>();
                });
    }

    /// <summary>
    /// Demonstration class showing the new StuxBot system in action
    /// </summary>
    public class BotDemo
    {
        private readonly StuxBotAgent _stuxBot;
        private readonly ILogger<BotDemo> _logger;

        public BotDemo(StuxBotAgent stuxBot, ILogger<BotDemo> logger)
        {
            _stuxBot = stuxBot;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            try
            {
                Console.WriteLine("🤖 Hybrid Bot System (Agent-Based Architecture)");
                Console.WriteLine("=================================================");
                Console.WriteLine();

                // Initialize the bot
                await InitializeSystemAsync();

                // Run demo scenarios
                await RunDemoScenariosAsync();

                // Interactive mode
                await RunInteractiveModeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running bot demo");
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        private async Task InitializeSystemAsync()
        {
            Console.WriteLine("🔧 Initializing StuxBot...");
            
            // Initialize the bot and its capabilities
            await _stuxBot.InitializeAsync();
            
            // Display capabilities
            Console.WriteLine($"✅ StuxBot initialized with {_stuxBot.Capabilities.Count} capabilities:");
            foreach (var capability in _stuxBot.Capabilities.Values)
            {
                Console.WriteLine($"   • {capability.Name} ({capability.CapabilityId}) - Priority: {capability.Priority}");
                Console.WriteLine($"     Tags: {string.Join(", ", capability.Tags)}");
            }
            Console.WriteLine();
        }

        private async Task RunDemoScenariosAsync()
        {
            var scenarios = new[]
            {
                new DemoScenario
                {
                    Name = "Simple Greeting",
                    Message = "Hello! I'm new here and need some help getting started."
                },
                new DemoScenario
                {
                    Name = "Content Summarization",
                    Message = "Please summarize this: Machine learning is a subset of artificial intelligence that enables computers to learn and make decisions from data without being explicitly programmed for every scenario. It involves algorithms that can identify patterns, make predictions, and improve their performance over time through experience."
                },
                new DemoScenario
                {
                    Name = "Help Request",
                    Message = "I'm confused about how this system works. Can you help me understand?"
                },
                new DemoScenario
                {
                    Name = "Technical Question",
                    Message = "What are the benefits of using Semantic Kernel agents in chatbot development?"
                }
            };

            Console.WriteLine("🎭 Running Demo Scenarios...");
            Console.WriteLine();

            for (int i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];
                Console.WriteLine($"📝 Scenario {i + 1}: {scenario.Name}");
                Console.WriteLine($"Input: \"{scenario.Message}\"");
                Console.WriteLine();

                var context = new BotContext
                {
                    Input = scenario.Message,
                    Timestamp = DateTime.UtcNow,
                    RequestId = $"demo-request-{i + 1}",
                    ConversationId = $"demo-session-{i + 1}",
                    UserId = "demo-user"
                };

                var response = await _stuxBot.ProcessMessageAsync(scenario.Message, context);

                Console.WriteLine("🤖 Bot Response:");
                Console.WriteLine(response.Content);
                Console.WriteLine();

                if (response.Metadata.Any())
                {
                    Console.WriteLine("📊 Response Metadata:");
                    foreach (var kvp in response.Metadata)
                    {
                        Console.WriteLine($"   {kvp.Key}: {kvp.Value}");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("".PadRight(50, '-'));
                Console.WriteLine();

                // Wait for user input to continue
                Console.WriteLine("Press any key to continue to next scenario...");
                Console.ReadKey();
                Console.WriteLine();
            }
        }

        private async Task RunInteractiveModeAsync()
        {
            Console.WriteLine("💬 Interactive Mode - Type your messages (or 'quit' to exit):");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  'capabilities' - List all available capabilities");
            Console.WriteLine("  'quit' - Exit the application");
            Console.WriteLine();

            var sessionId = Guid.NewGuid().ToString();
            
            while (true)
            {
                Console.Write("You: ");
                var input = Console.ReadLine();

                if (string.IsNullOrEmpty(input) || input.ToLower() == "quit")
                    break;

                if (input.ToLower() == "capabilities")
                {
                    Console.WriteLine("Available Capabilities:");
                    foreach (var capability in _stuxBot.Capabilities.Values)
                    {
                        Console.WriteLine($"• {capability.Name}: {capability.GetInstructions()}");
                    }
                    Console.WriteLine();
                    continue;
                }

                var context = new BotContext
                {
                    Input = input,
                    Timestamp = DateTime.UtcNow,
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = sessionId,
                    UserId = "interactive-user"
                };

                try
                {
                    var response = await _stuxBot.ProcessMessageAsync(input, context);
                    Console.WriteLine($"Bot: {response.Content}");

                    if (!response.IsComplete && response.Metadata.ContainsKey("error"))
                    {
                        Console.WriteLine($"⚠️ Warning: {response.Metadata["error"]}");
                    }

                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    Console.WriteLine();
                }
            }
        }

        private class DemoScenario
        {
            public required string Name { get; set; }
            public required string Message { get; set; }
        }
    }
}
