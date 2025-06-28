# Hybrid Bot Orchestration System

A sophisticated hybrid bot design that combines class-based bot logic with tag-annotated roles for maximum flexibility, clarity, and runtime interoperability. This system provides structured code organization while enabling dynamic role composition and execution.

## 🏗️ Architecture Overview

### Core Concepts

- **Hybrid Design**: Combines structured OOP with flexible metadata-driven configuration
- **Tag-Based Organization**: Roles are categorized and filtered using tag annotations
- **Runtime Interoperability**: Dynamic role loading and execution without restart
- **State Management**: Persistent conversation state with cleanup capabilities
- **Orchestration Patterns**: Multiple execution modes (FirstMatch, Sequential, Parallel, Pipeline)

### Project Structure

```
HybridBot/
├── Core/                           # Core framework components
│   ├── IBotRole.cs                 # Primary role interface
│   ├── BaseBotRole.cs              # Abstract base implementation
│   ├── RoleRegistry.cs             # Role management and discovery
│   ├── BotOrchestrator.cs          # Execution orchestration
│   └── StateManager.cs             # Persistent state management
├── Roles/                          # Concrete role implementations
│   ├── SummarizerRole.cs           # Content summarization
│   └── ResponderRole.cs            # Contextual responses
├── Config/                         # Configuration files
│   ├── role_registry.json          # Role registration config
│   └── summarizer_state.yaml       # Role-specific state config
├── Program.cs                      # Application entry point
├── appsettings.json               # Application configuration
└── README.md                      # This file
```

## 🚀 Key Features

### 1. **Tag-Annotated Role System**
- Roles are organized using semantic tags (e.g., "summarization", "response", "analysis")
- Runtime filtering and selection based on tags
- Metadata-driven configuration through frontmatter

### 2. **Multiple Execution Modes**
- **FirstMatch**: Execute the first capable role
- **Sequential**: Execute all capable roles in order
- **Parallel**: Execute multiple roles concurrently
- **Pipeline**: Chain roles, passing output between them

### 3. **State Management**
- Persistent conversation state across sessions
- Automatic cleanup of old conversations
- In-memory caching with file-based persistence
- State versioning and migration support

### 4. **Flexible Configuration**
- JSON configuration for role registry
- YAML frontmatter for role-specific settings
- Environment variable support
- Runtime reconfiguration capabilities

## 🛠️ Getting Started

### Prerequisites

- .NET 8.0 or later
- Visual Studio 2022 or VS Code with C# extension

### Building and Running

1. **Clone and Build**:
   ```bash
   git clone <repository-url>
   cd HybridBot
   dotnet restore
   dotnet build
   ```

2. **Run the Demo**:
   ```bash
   dotnet run
   ```

3. **Run Specific Scenarios**:
   ```bash
   dotnet run -- --scenario greeting
   dotnet run -- --scenario summarization
   ```

### Configuration

#### Role Registry (`Config/role_registry.json`)
```json
{
  "roles": [
    {
      "roleId": "summarizer",
      "typeName": "HybridBot.Roles.SummarizerRole",
      "configuration": {
        "summary_length": "medium",
        "include_sentiment": true,
        "output_format": "structured"
      }
    }
  ]
}
```

#### Role State Config (`Config/summarizer_state.yaml`)
```yaml
role_id: summarizer
state_config:
  persistence_enabled: true
  auto_cleanup: true
  cleanup_age_days: 30

behavior:
  auto_summarize_threshold: 500
  cache_enabled: true
```

## 📝 Creating Custom Roles

### 1. Implement the Base Class

```csharp
public class MyCustomRole : BaseBotRole
{
    public override string RoleId => "my-custom-role";
    public override string Name => "My Custom Role";
    
    public MyCustomRole(ILogger<MyCustomRole> logger) : base(logger)
    {
        Tags = new List<string> { "custom", "specialized" };
        Priority = 50;
    }
    
    protected override async Task<BotResponse> OnExecuteAsync(BotContext context)
    {
        // Your role logic here
        return new BotResponse
        {
            Content = "Custom response",
            IsComplete = true
        };
    }
    
    public override bool CanHandle(BotContext context)
    {
        // Define when this role should handle requests
        return context.Input.Contains("custom");
    }
}
```

### 2. Register in DI Container

```csharp
services.AddTransient<MyCustomRole>();
```

### 3. Add to Role Registry Config

```json
{
  "roleId": "my-custom-role",
  "typeName": "HybridBot.Roles.MyCustomRole",
  "configuration": {
    "my_setting": "value"
  }
}
```

## 🎯 Usage Examples

### Basic Conversation
```csharp
var context = new BotContext
{
    RequestId = Guid.NewGuid().ToString(),
    ConversationId = "user-123",
    Input = "Hello, can you help me?"
};

var response = await orchestrator.ProcessAsync(context);
Console.WriteLine(response.Content);
```

### Tag-Based Role Selection
```csharp
var config = new OrchestrationConfig
{
    ExecutionMode = ExecutionMode.FirstMatch,
    RequiredTags = new List<string> { "summarization" }
};

var response = await orchestrator.ProcessAsync(context, config);
```

### Pipeline Execution
```csharp
var config = new OrchestrationConfig
{
    ExecutionMode = ExecutionMode.Pipeline,
    SpecificRoles = new List<string> { "analyzer", "summarizer", "responder" }
};

var response = await orchestrator.ProcessAsync(context, config);
```

## 🔧 Advanced Configuration

### Environment Variables
```bash
export HYBRIDBOT__StateDirectory="/custom/state/path"
export HYBRIDBOT__MaxConcurrentRoles="10"
export HYBRIDBOT__EnableStatePersistence="true"
```

### Logging Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "HybridBot": "Debug",
      "HybridBot.Core.BotOrchestrator": "Information"
    }
  }
}
```

## 📊 Monitoring and Metrics

The system provides built-in metrics tracking:

- **Role Execution Times**: Track performance of individual roles
- **Success/Failure Rates**: Monitor role reliability
- **State Usage**: Track conversation state growth
- **Tag Utilization**: Analyze which tags are most commonly used

Access metrics through the response metadata:
```csharp
var executionTime = response.Metadata["executionTime"];
var roleUsed = response.Metadata["executedRole"];
```

## 🚀 Deployment Options

### Local Development
```bash
dotnet run --environment Development
```

### Docker Deployment
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY . /app
WORKDIR /app
ENTRYPOINT ["dotnet", "HybridBot.dll"]
```

### Azure Container Apps
- Supports auto-scaling based on conversation volume
- Persistent state using Azure Storage
- Integrated logging with Application Insights

## 🔐 Security Considerations

- **Input Validation**: All user inputs are sanitized
- **State Isolation**: Conversation states are isolated by user/session
- **Configuration Security**: Sensitive config stored in Key Vault
- **Rate Limiting**: Built-in protection against abuse

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-role`
3. Implement your role following the established patterns
4. Add comprehensive tests
5. Submit a pull request

### Code Standards
- Follow C# naming conventions
- Include XML documentation for public APIs
- Write unit tests for new roles
- Update configuration examples

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🎯 Roadmap

- [ ] **Azure AI Integration**: Direct integration with Azure Cognitive Services
- [ ] **GraphQL API**: Expose orchestration through GraphQL
- [ ] **Role Marketplace**: Community-driven role sharing
- [ ] **Visual Designer**: GUI for creating role pipelines
- [ ] **Performance Dashboard**: Real-time monitoring and analytics
- [ ] **Multi-tenant Support**: Support for multiple bot instances

## 🆘 Support

- **Documentation**: [Wiki Pages](../../wiki)
- **Issues**: [GitHub Issues](../../issues)
- **Discussions**: [GitHub Discussions](../../discussions)
- **Examples**: [Examples Repository](../examples)

---

**Built with ❤️ for the GitHub Copilot community**
