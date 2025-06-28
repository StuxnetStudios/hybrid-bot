using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;

namespace HybridBot.Core
{
    /// <summary>
    /// Registry for managing bot roles with tag-based filtering and metadata support.
    /// Supports loading roles from configuration files and runtime registration.
    /// </summary>
    public class RoleRegistry
    {
        private readonly Dictionary<string, IBotRole> _roles = new();
        private readonly Dictionary<string, List<IBotRole>> _tagIndex = new();
        private readonly ILogger<RoleRegistry> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        public RoleRegistry(ILogger<RoleRegistry> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        /// <summary>
        /// Register a role instance
        /// </summary>
        public async Task RegisterRoleAsync(IBotRole role, IDictionary<string, object> config = null)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            
            if (_roles.ContainsKey(role.RoleId))
            {
                _logger.LogWarning("Role {RoleId} is already registered, replacing", role.RoleId);
                await UnregisterRoleAsync(role.RoleId);
            }
            
            // Initialize the role if config is provided
            if (config != null)
            {
                await role.InitializeAsync(config);
            }
            
            _roles[role.RoleId] = role;
            
            // Update tag index
            foreach (var tag in role.Tags)
            {
                if (!_tagIndex.ContainsKey(tag))
                {
                    _tagIndex[tag] = new List<IBotRole>();
                }
                _tagIndex[tag].Add(role);
            }
            
            _logger.LogInformation("Registered role {RoleId} with tags: {Tags}", 
                role.RoleId, string.Join(", ", role.Tags));
        }
        
        /// <summary>
        /// Unregister a role by ID
        /// </summary>
        public async Task UnregisterRoleAsync(string roleId)
        {
            if (_roles.TryGetValue(roleId, out var role))
            {
                // Remove from tag index
                foreach (var tag in role.Tags)
                {
                    if (_tagIndex.ContainsKey(tag))
                    {
                        _tagIndex[tag].Remove(role);
                        if (_tagIndex[tag].Count == 0)
                        {
                            _tagIndex.Remove(tag);
                        }
                    }
                }
                
                // Dispose the role
                await role.DisposeAsync();
                
                _roles.Remove(roleId);
                _logger.LogInformation("Unregistered role {RoleId}", roleId);
            }
        }
        
        /// <summary>
        /// Get role by ID
        /// </summary>
        public IBotRole GetRole(string roleId)
        {
            return _roles.TryGetValue(roleId, out var role) ? role : null;
        }
        
        /// <summary>
        /// Get all roles
        /// </summary>
        public IEnumerable<IBotRole> GetAllRoles()
        {
            return _roles.Values;
        }
        
        /// <summary>
        /// Get roles by tag
        /// </summary>
        public IEnumerable<IBotRole> GetRolesByTag(string tag)
        {
            return _tagIndex.TryGetValue(tag, out var roles) ? roles : Enumerable.Empty<IBotRole>();
        }
        
        /// <summary>
        /// Get roles that match any of the provided tags
        /// </summary>
        public IEnumerable<IBotRole> GetRolesByTags(params string[] tags)
        {
            return tags.SelectMany(GetRolesByTag).Distinct();
        }
        
        /// <summary>
        /// Get roles that can handle the given context, ordered by priority
        /// </summary>
        public IEnumerable<IBotRole> GetCapableRoles(BotContext context)
        {
            return _roles.Values
                .Where(role => role.CanHandle(context))
                .OrderByDescending(role => role.Priority);
        }
        
        /// <summary>
        /// Load role configurations from a JSON file
        /// </summary>
        public async Task LoadFromConfigFileAsync(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                _logger.LogWarning("Config file not found: {ConfigFilePath}", configFilePath);
                return;
            }
            
            try
            {
                var json = await File.ReadAllTextAsync(configFilePath);
                var config = JsonSerializer.Deserialize<RoleRegistryConfig>(json);
                
                foreach (var roleConfig in config.Roles)
                {
                    await LoadRoleFromConfigAsync(roleConfig);
                }
                
                _logger.LogInformation("Loaded {Count} roles from config file", config.Roles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config file: {ConfigFilePath}", configFilePath);
                throw;
            }
        }
        
        private async Task LoadRoleFromConfigAsync(RoleConfig roleConfig)
        {
            try
            {
                // Use reflection or DI to create role instance
                var roleType = Type.GetType(roleConfig.TypeName);
                if (roleType == null)
                {
                    _logger.LogError("Role type not found: {TypeName}", roleConfig.TypeName);
                    return;
                }
                
                var role = (IBotRole)_serviceProvider.GetService(roleType);
                if (role == null)
                {
                    _logger.LogError("Failed to create role instance: {TypeName}", roleConfig.TypeName);
                    return;
                }
                
                await RegisterRoleAsync(role, roleConfig.Configuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load role: {RoleId}", roleConfig.RoleId);
            }
        }
        
        /// <summary>
        /// Dispose all registered roles
        /// </summary>
        public async Task DisposeAllAsync()
        {
            var disposeTasks = _roles.Values.Select(role => role.DisposeAsync());
            await Task.WhenAll(disposeTasks);
            
            _roles.Clear();
            _tagIndex.Clear();
            
            _logger.LogInformation("Disposed all roles");
        }
    }
    
    /// <summary>
    /// Configuration structure for role registry
    /// </summary>
    public class RoleRegistryConfig
    {
        public List<RoleConfig> Roles { get; set; } = new();
    }
    
    /// <summary>
    /// Configuration structure for individual roles
    /// </summary>
    public class RoleConfig
    {
        public string RoleId { get; set; }
        public string TypeName { get; set; }
        public IDictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
    }
}
