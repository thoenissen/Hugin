using System.Collections.Generic;

namespace Hugin.Data;

/// <summary>
/// Configuration
/// </summary>
internal class Configuration
{
    /// <summary>
    /// Active configuration
    /// </summary>
    public static Configuration Current { get; set; }

    /// <summary>
    /// Discord token
    /// </summary>
    public string DiscordToken { get; set; }

    /// <summary>
    /// Docker endpoint
    /// </summary>
    public string DockerEndpoint { get; set; }

    /// <summary>
    /// Guild ID
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Servers
    /// </summary>
    public List<Server> Servers { get; set; }
}