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

## Code Completion Tests:

### In TestRole.cs, try typing these comments and see what Copilot suggests:

1. `// Load configuration from frontmatter`
2. `// Check if input contains test keywords`
3. `// Generate response with timestamp and echo`
4. `// Validate input length and format`
5. `// Add delay if configured`

### In any role file, try:

1. `// Create a new role that handles`
2. `// Implement async pattern for`
3. `// Add logging for performance monitoring`
4. `// Parse YAML configuration for`

## Expected Behaviors:

✅ Copilot should understand your hybrid architecture
✅ Suggestions should follow your established patterns
✅ Code should integrate with existing interfaces
✅ Configuration suggestions should match your YAML/JSON structure
✅ Error handling should use your logging framework
