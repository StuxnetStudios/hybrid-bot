# Skynet-lite Integration Test

## Quick Test Commands

### Test the application
```bash
dotnet run
```

### Test with Skynet-lite API Key
1. Add your Skynet-lite API key to `appsettings.json`:
   ```json
   "SkynetLite": {
     "ApiKey": "your-skynet-lite-api-key-here"
   }
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

### Test Configuration Options

You can customize Skynet-lite behavior in `appsettings.json`:

```json
"SkynetLite": {
  "BaseUrl": "https://your-skynet-lite-endpoint.com",
  "ChatEndpoint": "/v1/chat/completions",
  "ModelId": "skynet-lite-pro",
  "ApiKey": "your-api-key",
  "MaxTokens": 2000,
  "Temperature": 0.8,
  "TopP": 0.9,
  "TimeoutSeconds": 45
}
```

## Integration Features

✅ **Skynet-lite Connector** - Custom Semantic Kernel connector for Skynet-lite API
✅ **SkynetLiteRole** - Specialized bot role optimized for Skynet-lite
✅ **Required Configuration** - Application requires valid Skynet-lite API key to function
✅ **Configurable Parameters** - Full control over model behavior
✅ **Error Handling** - Robust error handling with clear error messages
✅ **Response Styles** - Support for different response styles (conversational, technical, creative)

## Expected Behavior

1. **With Skynet-lite API Key**: The bot will use Skynet-lite for AI-powered responses
2. **Without API Key**: The application will fail to start with a clear error message
3. **API Failures**: Clear error messages will be returned instead of fallback responses

## Testing Different Scenarios

### Test Skynet-lite Specific Features
Try these inputs when running the demo:
- "Hello, how are you?" (Basic conversation)
- "Explain quantum computing" (Technical response)
- "Write a creative story" (Creative response)
- "Help me debug this code" (Technical assistance)
