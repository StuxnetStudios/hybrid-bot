# HybridBot - Semantic Kernel AI Agent System

A modern AI agent system built on Microsoft Semantic Kernel with composition-based architecture, privacy-first design, and flexible capability management. Integrates with Skynet-lite for local AI inference while maintaining full offline capability.

## 🚀 Key Features

### 🔒 **Privacy & Security First**
- **Offline by Default**: Mock mode ensures no data leaves your system
- **No API Keys Required**: Operates completely without external authentication
- **Local Processing**: Can run entirely air-gapped for sensitive environments
- **Explicit Opt-in**: External connections only when deliberately configured

### 🏗️ **Modern Architecture**
- **Composition over Inheritance**: Clean capability-based design
- **Microsoft Semantic Kernel**: Industry-standard AI orchestration framework
- **Dependency Injection**: Full DI container support with .NET 8
- **Async/Await**: Modern asynchronous processing throughout

### 🎯 **Flexible Capabilities**
- **Modular Design**: Add/remove capabilities without code changes
- **Tag-based Routing**: Smart content-based capability selection
- **State Management**: Persistent conversation state with cleanup
- **Mock Mode**: Contextual responses for development and testing

## 📁 Project Structure

```
HybridBot/
├── Core/                              # Core framework
│   ├── HybridBot.cs                   # Main agent class (composition)
│   ├── IRoleCapability.cs             # Capability interface
│   ├── BaseRoleCapability.cs          # Abstract capability base
│   ├── SkynetLiteConnector.cs         # Skynet-lite integration
│   └── StateManager.cs                # Conversation state management
├── Capabilities/                      # Concrete capabilities
│   ├── SummarizerCapability.cs        # Content summarization
│   └── ResponderCapability.cs         # General conversation
├── Config/                            # Configuration files
│   ├── role_registry.json             # Capability registration
│   └── summarizer_state.yaml          # Capability-specific state
├── Roles/                             # Legacy role classes (deprecated)
├── Program.cs                         # Application entry point
├── appsettings.json                   # Application configuration
└── HybridBot.csproj                   # Project file
```

## 🛠️ Quick Start

### Prerequisites
- **.NET 8.0 SDK** or later
- **Visual Studio 2022** / **VS Code** with C# extension
- **Optional**: Local Skynet-lite server for non-mock responses

### Installation & Running

1. **Clone and Build**:
   ```bash
   git clone <repository-url>
   cd HybridBot
   dotnet restore
   dotnet build
   ```

2. **Run with Mock Mode** (Default - No external dependencies):
   ```bash
   dotnet run
   ```

3. **Run with Local Skynet-lite** (Requires server at localhost:8080):
   ```bash
   # Edit appsettings.json: "MockMode": false
   dotnet run
   ```

### First Run Experience

The application will:
1. **Initialize** Semantic Kernel with Skynet-lite connector
2. **Register** available capabilities (Summarizer, Responder)
3. **Run** demonstration scenarios automatically
4. **Enter** interactive mode for live testing

## ⚙️ Configuration

### Core Configuration (`appsettings.json`)

```json
{
  "SkynetLite": {
    "BaseUrl": "http://localhost:8080",
    "ApiKey": "",                    // No API key required
    "MockMode": true,                // Privacy-first: offline by default
    "MaxTokens": 2000,
    "Temperature": 0.7
  },
  "HybridBot": {
    "StateDirectory": "./State",
    "MaxConcurrentRoles": 5,
    "EnableStatePersistence": true
  }
}
```

### Privacy Modes

| Mode | Description | Data Transmission | Use Case |
|------|-------------|-------------------|----------|
| **Mock** | Contextual offline responses | None | Development, Privacy-sensitive |
| **Local** | Connect to local Skynet-lite | Local only | Local AI inference |
| **Remote** | External API (future) | External | Production with API |

## 🎯 Architecture Overview

### Composition-Based Design

```csharp
// Main agent composes capabilities, doesn't inherit
public class HybridBotAgent
{
    private readonly Dictionary<string, IRoleCapability> _capabilities;
    
    // Intelligently routes to appropriate capability
    public async Task<BotResponse> ProcessAsync(BotContext context)
    {
        var capability = SelectBestCapability(context);
        return await capability.ExecuteAsync(context);
    }
}
```

### Capability Interface

```csharp
public interface IRoleCapability
{
    string CapabilityId { get; }
    string Name { get; }
    IReadOnlyList<string> Tags { get; }
    int Priority { get; }
    
    Task<bool> CanHandleAsync(BotContext context);
    Task<BotResponse> ExecuteAsync(BotContext context);
}
```

### Smart Routing

The system automatically selects capabilities based on:
- **Content Analysis**: Keywords and patterns in user input
- **Tag Matching**: Capability tags vs. required functionality
- **Priority Levels**: Higher priority capabilities get preference
- **Context History**: Previous conversation context

## 🔧 Creating Custom Capabilities

### 1. Implement IRoleCapability

```csharp
public class MyCustomCapability : BaseRoleCapability
{
    public override string CapabilityId => "custom-processor";
    public override string Name => "Custom Processor";
    protected override List<string> CapabilityTags => new() { "custom", "processing" };
    protected override int CapabilityPriority => 60;

    public override async Task<bool> CanHandleAsync(BotContext context)
    {
        return context.Input.Contains("custom", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<BotResponse> ExecuteInternalAsync(BotContext context)
    {
        // Your custom logic here
        return new BotResponse
        {
            Content = "Custom processing complete!",
            IsComplete = true,
            Source = CapabilityId
        };
    }
}
```

### 2. Register in Program.cs

```csharp
// Add to dependency injection
services.AddTransient<MyCustomCapability>();

// Include in capability list
var capabilities = new List<IRoleCapability>
{
    serviceProvider.GetRequiredService<SummarizerCapability>(),
    serviceProvider.GetRequiredService<ResponderCapability>(),
    serviceProvider.GetRequiredService<MyCustomCapability>()  // Add here
};
```

### 3. Configure (Optional)

Create `Config/custom_capability_state.yaml`:
```yaml
role_id: custom-processor
state_version: "1.0.0"
behavior:
  processing_mode: "advanced"
  cache_enabled: true
```

## 🔍 Usage Examples

### Basic Conversation
```csharp
var context = new BotContext
{
    RequestId = Guid.NewGuid().ToString(),
    ConversationId = "user-session-123",
    UserId = "user-456",
    Input = "Hello, can you help me summarize this document?"
};

var response = await hybridBot.ProcessAsync(context);
Console.WriteLine($"Response: {response.Content}");
Console.WriteLine($"Handled by: {response.Source}");
```

### Interactive Mode Commands
```bash
# Start interactive mode
dotnet run

# Available commands in interactive mode:
You: hello                          # General conversation
You: summarize this text...         # Content summarization  
You: capabilities                   # List available capabilities
You: help                          # Get help information
You: quit                          # Exit application
```

## 📊 Built-in Capabilities

### 1. **SummarizerCapability**
- **Purpose**: Content summarization and analysis
- **Tags**: `summarization`, `content`, `analysis`, `text-processing`
- **Triggers**: "summarize", "summary", content analysis keywords
- **Features**: Configurable length, format options, caching

### 2. **ResponderCapability** 
- **Purpose**: General conversation and assistance
- **Tags**: `conversation`, `general`, `response`, `chat`
- **Triggers**: Greetings, help requests, general questions
- **Features**: Contextual responses, personality traits

## 🧪 Testing & Development

### Mock Responses
The system provides intelligent mock responses for development:

```json
{
  "Input": "summarize this text...",
  "MockResponse": "Here's a summary: The main points include key concepts..."
}
```

### Debug Mode
```bash
# Enable detailed logging
export DOTNET_ENVIRONMENT=Development
dotnet run

# View logs
tail -f logs/hybridbot-*.log
```

### Unit Testing
```csharp
[Test]
public async Task SummarizerCapability_ShouldHandleSummarizationRequests()
{
    var capability = new SummarizerCapability(logger, config);
    var context = new BotContext { Input = "Please summarize this content" };
    
    var canHandle = await capability.CanHandleAsync(context);
    Assert.IsTrue(canHandle);
}
```

## 🔐 Security & Privacy

### Privacy-First Design
- **Default Offline**: Mock mode prevents data transmission
- **No Credentials**: No API keys or authentication required
- **Local State**: All conversation data stored locally
- **Explicit Consent**: Clear indication when data might be transmitted

### Security Features
- **Input Sanitization**: All user inputs are validated
- **State Isolation**: User sessions are isolated
- **Configurable Logging**: Control what gets logged
- **Rate Limiting**: Built-in protection against abuse

### Compliance Ready
- **GDPR**: Data minimization and local processing
- **HIPAA**: No PHI transmission in default mode
- **SOX**: Audit trails and access controls
- **Corporate**: Air-gapped operation capability

## 🚀 Deployment Options

### Local Development
```bash
dotnet run --environment Development
```

### Docker Container
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY . .
ENTRYPOINT ["dotnet", "HybridBot.dll"]
```

### Azure Container Apps
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: hybridbot
spec:
  replicas: 1
  template:
    spec:
      containers:
      - name: hybridbot
        image: hybridbot:latest
        env:
        - name: SkynetLite__MockMode
          value: "false"
```

## 📈 Monitoring & Observability

### Built-in Metrics
- **Response Times**: Per-capability execution metrics
- **Success Rates**: Capability success/failure tracking
- **Usage Patterns**: Most used capabilities and features
- **State Growth**: Conversation state size monitoring

### Logging Integration
```json
{
  "Logging": {
    "LogLevel": {
      "HybridBot": "Information",
      "HybridBot.Capabilities": "Debug"
    }
  }
}
```

### Health Checks
```csharp
// Built-in health check endpoint
GET /health
{
  "status": "healthy",
  "capabilities": ["summarizer", "responder"],
  "mode": "mock"
}
```

## 🛣️ Roadmap

### Version 1.1
- [ ] **Plugin System**: Dynamic capability loading
- [ ] **REST API**: HTTP API for external integration
- [ ] **Web UI**: Browser-based interface
- [ ] **Metrics Dashboard**: Real-time monitoring

### Version 1.2
- [ ] **Multi-tenant**: Support multiple bot instances
- [ ] **Workflow Engine**: Complex capability chaining
- [ ] **Vector Search**: Semantic search capabilities
- [ ] **Export/Import**: Configuration and state management

## 🤝 Contributing

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/amazing-capability`
3. **Implement** your capability following the established patterns
4. **Add** comprehensive tests
5. **Update** documentation
6. **Submit** a pull request

### Development Guidelines
- Follow **C# conventions** and **async/await** patterns
- Include **XML documentation** for public APIs
- Write **unit tests** for new capabilities
- Update **configuration examples** in README
- Maintain **privacy-first** principles

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- **Documentation**: [Wiki](../../wiki)
- **Issues**: [GitHub Issues](../../issues)
- **Discussions**: [GitHub Discussions](../../discussions)
- **Security**: [Security Policy](SECURITY.md)

---

**🤖 Built with Microsoft Semantic Kernel • 🔒 Privacy-First Design • 🚀 Production Ready**

> *"AI that respects your privacy while delivering powerful capabilities"*
