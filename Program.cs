using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using HybridBot.Core;
using HybridBot.Roles;
using System;
using System.Threading.Tasks;
using System.IO;

namespace HybridBot
{
    /// <summary>
    /// Main program entry point for the Hybrid Bot Orchestration System.
    /// Demonstrates the tag-annotated role architecture with class-based implementations
    /// and Semantic Kernel integration for intelligent orchestration.
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

                    // Add Semantic Kernel
                    services.AddKernel();
                    
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
                        
                        // Get Skynet-lite configuration
                        var skynetConfig = configuration.GetSection("SkynetLite").Get<SkynetLiteConfig>();
                        
                        if (skynetConfig != null && !string.IsNullOrEmpty(skynetConfig.ApiKey))
                        {
                            // Create and register Skynet-lite connector
                            var httpClient = serviceProvider.GetRequiredService<HttpClient>();
                            var skynetLogger = serviceProvider.GetRequiredService<ILogger<SkynetLiteConnector>>();
                            var skynetConnector = new SkynetLiteConnector(httpClient, skynetLogger, skynetConfig);
                            
                            kernelBuilder.Services.AddSingleton<IChatCompletionService>(skynetConnector);
                            logger.LogInformation("Configured Semantic Kernel with Skynet-lite service");
                        }
                        else
                        {
                            logger.LogError("Skynet-lite API key is required. Please configure the SkynetLite:ApiKey setting.");
                            throw new InvalidOperationException("Skynet-lite API key is not configured. The application requires a valid API key to function.");
                        }
                        
                        return kernelBuilder.Build();
                    });

                    // Add core services
                    services.AddSingleton<StateManager>();
                    services.AddSingleton<RoleRegistry>();
                    services.AddSingleton<BotOrchestrator>();
                    services.AddSingleton<SemanticOrchestrator>();

                    // Add contextual awareness services
                    services.AddSingleton<LayeredContextFactory>();

                    // Add traditional and contextual roles
                    services.AddTransient<SummarizerRole>();
                    services.AddTransient<ResponderRole>();
                    services.AddTransient<TestRole>();
                    services.AddTransient<SemanticKernelRole>();
                    services.AddTransient<SkynetLiteRole>();
                    
                    // Add new contextual roles
                    services.AddTransient<AdaptiveGamingRole>();

                    // Add demo service
                    services.AddTransient<BotDemo>();
                });
    }

    /// <summary>
    /// Demonstration class showing the hybrid bot system in action
    /// </summary>
    public class BotDemo
    {
        private readonly BotOrchestrator _orchestrator;
        private readonly RoleRegistry _roleRegistry;
        private readonly ILogger<BotDemo> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly LayeredContextFactory _contextFactory;

        public BotDemo(
            BotOrchestrator orchestrator,
            RoleRegistry roleRegistry,
            ILogger<BotDemo> logger,
            IServiceProvider serviceProvider,
            LayeredContextFactory contextFactory)
        {
            _orchestrator = orchestrator;
            _roleRegistry = roleRegistry;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _contextFactory = contextFactory;
        }

        public async Task RunAsync()
        {
            try
            {
                Console.WriteLine("🤖 Hybrid Bot Orchestration System");
                Console.WriteLine("===================================");
                Console.WriteLine();

                // Initialize the system
                await InitializeSystemAsync();

                // Run interactive demo
                await RunInteractiveDemoAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running bot demo");
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private async Task InitializeSystemAsync()
        {
            Console.WriteLine("🔧 Initializing Bot System...");

            // Load roles from configuration
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", "role_registry.json");
            var rolesLoadedFromConfig = false;
            
            if (File.Exists(configPath))
            {
                await _roleRegistry.LoadFromConfigFileAsync(configPath);
                var configLoadedRoles = _roleRegistry.GetAllRoles().ToList();
                rolesLoadedFromConfig = configLoadedRoles.Count > 0;
                
                if (rolesLoadedFromConfig)
                {
                    Console.WriteLine($"✅ Loaded {configLoadedRoles.Count} roles from configuration file");
                }
                else
                {
                    Console.WriteLine("⚠️ Configuration file exists but no roles were loaded");
                }
            }
            else
            {
                Console.WriteLine("⚠️ Configuration file not found, will use manual registration");
            }

            // If no roles were loaded from config, do manual registration
            if (!rolesLoadedFromConfig)
            {
                await RegisterRolesManuallyAsync();
            }

            // Display registered roles
            var roles = _roleRegistry.GetAllRoles();
            Console.WriteLine($"📋 Registered {roles.Count()} roles:");
            foreach (var role in roles)
            {
                Console.WriteLine($"   • {role.Name} ({role.RoleId}) - Tags: {string.Join(", ", role.Tags)}");
            }
            Console.WriteLine();
        }

        private async Task RegisterRolesManuallyAsync()
        {
            // Register Summarizer Role
            var summarizer = _serviceProvider.GetRequiredService<SummarizerRole>();
            var summarizerConfig = new Dictionary<string, object>
            {
                ["summary_length"] = "medium",
                ["include_sentiment"] = true,
                ["output_format"] = "structured",
                ["focus_keywords"] = new[] { "important", "key", "critical" }
            };
            await _roleRegistry.RegisterRoleAsync(summarizer, summarizerConfig);

            // Register Responder Role
            var responder = _serviceProvider.GetRequiredService<ResponderRole>();
            var responderConfig = new Dictionary<string, object>
            {
                ["response_style"] = "friendly",
                ["max_response_length"] = 150,
                ["include_followup"] = true,
                ["knowledge_domains"] = new[] { "general", "technology" }
            };
            await _roleRegistry.RegisterRoleAsync(responder, responderConfig);

            // Register Test Role
            var testRole = _serviceProvider.GetRequiredService<TestRole>();
            var testConfig = new Dictionary<string, object>
            {
                ["test_mode"] = "demo",
                ["response_delay"] = 0,
                ["echo_input"] = true
            };
            await _roleRegistry.RegisterRoleAsync(testRole, testConfig);

            // Register Semantic Kernel Role (if available)
            try
            {
                var semanticRole = _serviceProvider.GetRequiredService<SemanticKernelRole>();
                var semanticConfig = new Dictionary<string, object>
                {
                    ["ai_model"] = "gpt-3.5-turbo",
                    ["max_tokens"] = 1000,
                    ["temperature"] = 0.7,
                    ["enable_planning"] = false
                };
                await _roleRegistry.RegisterRoleAsync(semanticRole, semanticConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Semantic Kernel role not available: {ex.Message}");
            }

            // Register Skynet-Lite Role (if available)
            try
            {
                var skynetRole = _serviceProvider.GetRequiredService<SkynetLiteRole>();
                var skynetConfig = new Dictionary<string, object>
                {
                    ["model_id"] = "skynet-lite-v1",
                    ["max_tokens"] = 2000,
                    ["temperature"] = 0.7,
                    ["response_style"] = "conversational"
                };
                await _roleRegistry.RegisterRoleAsync(skynetRole, skynetConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Skynet-Lite role not available: {ex.Message}");
            }

            // Register Adaptive Gaming Role
            var gamingRole = _serviceProvider.GetRequiredService<AdaptiveGamingRole>();
            var gamingConfig = new Dictionary<string, object>
            {
                ["game_mode"] = "adaptive",
                ["difficulty_scaling"] = true,
                ["coaching_style"] = "supportive",
                ["strategy_focus"] = new[] { "survival", "exploration", "skill_building" },
                ["environmental_awareness"] = true
            };
            await _roleRegistry.RegisterRoleAsync(gamingRole, gamingConfig);

            Console.WriteLine("✅ Manually registered roles");
        }

        private async Task RunInteractiveDemoAsync()
        {
            var scenarios = new[]
            {
                CreateGreetingScenario(),
                CreateSummarizationScenario(),
                CreateHelpRequestScenario(),
                CreateComplexConversationScenario(),
                CreateGamingContextScenario(),
                CreateEnvironmentalAdaptationScenario()
            };

            Console.WriteLine("🎭 Running Demo Scenarios...");
            Console.WriteLine();

            for (int i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];
                Console.WriteLine($"📝 Scenario {i + 1}: {scenario.Name}");
                Console.WriteLine($"Input: \"{scenario.Input}\"");
                Console.WriteLine();

                var response = await _orchestrator.ProcessAsync(scenario.Context, scenario.Config);

                Console.WriteLine("🤖 Bot Response:");
                Console.WriteLine(response.Content);
                Console.WriteLine();

                if (response.Metadata.Any())
                {
                    Console.WriteLine("📊 Metadata:");
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

            // Interactive mode
            await RunInteractiveModeAsync();
        }

        private async Task RunInteractiveModeAsync()
        {
            Console.WriteLine("💬 Interactive Mode - Type your messages (or 'quit' to exit):");
            Console.WriteLine();

            var conversationId = Guid.NewGuid().ToString();
            
            while (true)
            {
                Console.Write("You: ");
                var input = Console.ReadLine();

                if (string.IsNullOrEmpty(input) || input.ToLower() == "quit")
                    break;

                var context = new BotContext
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = conversationId,
                    UserId = "demo-user",
                    Input = input
                };

                try
                {
                    var response = await _orchestrator.ProcessAsync(context);
                    Console.WriteLine($"Bot: {response.Content}");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    Console.WriteLine();
                }
            }
        }

        private DemoScenario CreateGreetingScenario()
        {
            return new DemoScenario
            {
                Name = "Simple Greeting",
                Input = "Hello! I'm new here and need some help getting started.",
                Context = new BotContext
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = "demo-conv-1",
                    UserId = "demo-user",
                    Input = "Hello! I'm new here and need some help getting started."
                },
                Config = new OrchestrationConfig
                {
                    ExecutionMode = ExecutionMode.FirstMatch
                }
            };
        }

        private DemoScenario CreateSummarizationScenario()
        {
            var longContent = """
                Machine learning is a subset of artificial intelligence that enables computers to learn and make decisions from data without being explicitly programmed for every scenario. It involves algorithms that can identify patterns, make predictions, and improve their performance over time through experience. There are three main types of machine learning: supervised learning, where algorithms learn from labeled training data; unsupervised learning, where algorithms find hidden patterns in data without labeled examples; and reinforcement learning, where algorithms learn through trial and error by receiving rewards or penalties for their actions. Common applications include recommendation systems, image recognition, natural language processing, fraud detection, and autonomous vehicles. The field has grown rapidly due to advances in computing power, the availability of large datasets, and improvements in algorithms, making it a crucial technology for modern businesses and scientific research.
                """;

            return new DemoScenario
            {
                Name = "Content Summarization",
                Input = $"Please summarize this content: {longContent}",
                Context = new BotContext
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = "demo-conv-2",
                    UserId = "demo-user",
                    Input = $"Please summarize this content: {longContent}"
                },
                Config = new OrchestrationConfig
                {
                    ExecutionMode = ExecutionMode.FirstMatch,
                    RequiredTags = new List<string> { "summarization" }
                }
            };
        }

        private DemoScenario CreateHelpRequestScenario()
        {
            return new DemoScenario
            {
                Name = "Help Request",
                Input = "I'm confused about how to use this system. Can you help me understand the different roles?",
                Context = new BotContext
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = "demo-conv-3",
                    UserId = "demo-user",
                    Input = "I'm confused about how to use this system. Can you help me understand the different roles?"
                },
                Config = new OrchestrationConfig
                {
                    ExecutionMode = ExecutionMode.Sequential
                }
            };
        }

        private DemoScenario CreateComplexConversationScenario()
        {
            return new DemoScenario
            {
                Name = "Complex Multi-Role Interaction",
                Input = "Can you summarize our conversation so far and then help me understand what we discussed?",
                Context = new BotContext
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = "demo-conv-4",
                    UserId = "demo-user",
                    Input = "Can you summarize our conversation so far and then help me understand what we discussed?",
                    State = new Dictionary<string, object>
                    {
                        ["conversation_history"] = new List<string>
                        {
                            "User: Hello! I'm new here.",
                            "Bot: Welcome! I'm here to help you get started.",
                            "User: What can this system do?",
                            "Bot: This is a hybrid bot system with multiple specialized roles.",
                            "User: That sounds interesting!"
                        }
                    }
                },
                Config = new OrchestrationConfig
                {
                    ExecutionMode = ExecutionMode.Pipeline
                }
            };
        }

        private DemoScenario CreateGamingContextScenario()
        {
            return new DemoScenario
            {
                Name = "Gaming Context Adaptation",
                Input = "I'm stuck in a stormy mountain dungeon with low health and facing a boss enemy. What should I do?",
                Context = new BotContext
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = "gaming-demo-1",
                    UserId = "gamer-user",
                    Input = "I'm stuck in a stormy mountain dungeon with low health and facing a boss enemy. What should I do?",
                    State = new Dictionary<string, object>
                    {
                        ["game_environment"] = "dungeon",
                        ["environment"] = new Dictionary<string, object>
                        {
                            ["terrain"] = "mountain",
                            ["weather"] = "stormy",
                            ["location_type"] = "boss_room"
                        },
                        ["threats"] = new Dictionary<string, double>
                        {
                            ["boss"] = 0.9,
                            ["weather"] = 0.7
                        },
                        ["available_resources"] = new List<string> { "health_potion", "mana_crystal" },
                        ["recent_actions"] = new List<string> { "attack", "defend", "retreat", "attack", "defend" },
                        ["engagement_level"] = 0.4, // Low due to difficulty
                        ["player_skill_level"] = 0.6,
                        ["bot_goals"] = new List<string> { "survival", "strategy_guidance" }
                    }
                },
                Config = new OrchestrationConfig
                {
                    ExecutionMode = ExecutionMode.FirstMatch,
                    RequiredTags = new List<string> { "gaming", "adaptive" }
                }
            };
        }

        private DemoScenario CreateEnvironmentalAdaptationScenario()
        {
            return new DemoScenario
            {
                Name = "Environmental Context Adaptation",
                Input = "Help me plan a strategy for exploring this new foggy forest area I discovered.",
                Context = new BotContext
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = "exploration-demo-1",
                    UserId = "explorer-user",
                    Input = "Help me plan a strategy for exploring this new foggy forest area I discovered.",
                    State = new Dictionary<string, object>
                    {
                        ["environment"] = new Dictionary<string, object>
                        {
                            ["terrain"] = "forest",
                            ["weather"] = "foggy",
                            ["time_of_day"] = "dusk"
                        },
                        ["threats"] = new Dictionary<string, double>
                        {
                            ["visibility"] = 0.6,
                            ["unknown_creatures"] = 0.4
                        },
                        ["available_resources"] = new List<string> { "torch", "rope", "food_supplies", "compass" },
                        ["recent_actions"] = new List<string> { "explore", "search", "investigate", "observe" },
                        ["engagement_level"] = 0.8, // High due to discovery excitement
                        ["user_relationship"] = "familiar",
                        ["conversation_sentiment"] = 0.3, // Slightly positive
                        ["bot_goals"] = new List<string> { "exploration_guidance", "safety_planning" },
                        ["interaction_count"] = 15
                    }
                },
                Config = new OrchestrationConfig
                {
                    ExecutionMode = ExecutionMode.FirstMatch,
                    RequiredTags = new List<string> { "contextual", "adaptive" }
                }
            };
        }

        private async Task CleanupAsync()
        {
            Console.WriteLine("🧹 Cleaning up...");
            await _roleRegistry.DisposeAllAsync();
            Console.WriteLine("✅ Cleanup complete");
        }

        private class DemoScenario
        {
            public required string Name { get; set; }
            public required string Input { get; set; }
            public required BotContext Context { get; set; }
            public required OrchestrationConfig Config { get; set; }
        }
    }
}
