# GitHub Copilot Test Prompts for Hybrid Bot

## Test these prompts with GitHub Copilot Chat:

### 1. Architecture Understanding
"Explain how the IBotRole interface supports tag-based role organization in this hybrid bot system."

### 2. Role Creation
"Create a new ValidationRole that checks if user input meets certain criteria. It should use tags ['validation', 'security'] and priority 80."

### 3. Configuration Enhancement
"How can I extend the role_registry.json to support role dependencies and conditional loading?"

### 4. Orchestration Patterns
"Suggest improvements to the BotOrchestrator to support weighted role selection based on confidence scores."

### 5. State Management
"Create a method in StateManager to export conversation history for analytics while preserving user privacy."

### 6. Performance Optimization
"How can I implement caching in the RoleRegistry to improve role lookup performance?"

### 7. Error Handling
"Add comprehensive error handling to the ResponderRole for network timeouts and API failures."

### 8. Testing
"Generate unit tests for the SummarizerRole focusing on different content types and edge cases."

### 9. Skynet-lite Integration
"How can I optimize the SkynetLiteRole for better performance and implement custom response formatting?"

### 10. Error Handling Enhancement
"Add comprehensive error handling to the SkynetLiteRole for API failures and timeout scenarios without fallback logic."

### 11. Custom Connectors
"Extend the SkynetLiteConnector to support streaming responses and custom authentication methods."

### 12. Configuration Validation
"Create a configuration validator to ensure all required Skynet-lite settings are properly configured before startup."

## Code Completion Tests

### In TestRole.cs, try typing these comments and see what Copilot suggests

1. `// Load configuration from frontmatter`
2. `// Check if input contains test keywords`
3. `// Generate response with timestamp and echo`
4. `// Validate input length and format`
5. `// Add delay if configured`

### In any role file, try

1. `// Create a new role that handles`
2. `// Implement async pattern for`
3. `// Add logging for performance monitoring`
4. `// Parse YAML configuration for`

### In SkynetLiteRole.cs, try

1. `// Configure Skynet-lite specific parameters`
2. `// Handle streaming responses from Skynet-lite`
3. `// Implement custom authentication for Skynet-lite API`
4. `// Add response post-processing for different styles`

## Expected Behaviors

✅ Copilot should understand your hybrid architecture
✅ Suggestions should follow your established patterns
✅ Code should integrate with existing interfaces
✅ Configuration suggestions should match your YAML/JSON structure
✅ Error handling should use your logging framework
