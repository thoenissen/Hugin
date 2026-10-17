using System.Threading.Tasks;

using Discord.Interactions;

using Hugin.Data;
using Hugin.Discord.CommandHandler;

namespace Hugin.Discord.SlashCommands;

/// <summary>
/// Slash command handler for server commands
/// </summary>
[Group("server", "Server management")]
public sealed class ServerSlashCommands : SlashCommandsBase
{
    #region Properties

    /// <summary>
    /// Command Handler
    /// </summary>
    public ServerCommandHandler CommandHandler { get; set; }

    #endregion // Properties

    #region Commands

    /// <summary>
    /// Get the current status of the server
    /// </summary>
    /// <param name="server">Server</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [SlashCommand("status", "Current server status")]
    public async Task Status([Summary("server", "Name of the server")] Server server = null)
    {
        await CommandHandler.PostStatus(Context, server)
                            .ConfigureAwait(false);
    }

    /// <summary>
    /// Get the server logs
    /// </summary>
    /// <param name="server">Server</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [SlashCommand("logs", "Server logs")]
    public async Task Logs([Summary("server", "Name of the server")] Server server = null)
    {
        await CommandHandler.PostLogs(Context, server)
                            .ConfigureAwait(false);
    }

    /// <summary>
    /// Restarts the server
    /// </summary>
    /// <param name="server">Server</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [SlashCommand("restart", "Restart the server")]
    public async Task Restart([Summary("server", "Name of the server")] Server server = null)
    {
        await CommandHandler.RestartServer(Context, server)
                            .ConfigureAwait(false);
    }

    #endregion // Commands
}