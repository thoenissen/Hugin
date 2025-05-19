using System.Threading.Tasks;

using Discord.Interactions;

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
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [SlashCommand("status", "Current server status")]
    public async Task Status()
    {
        await CommandHandler.PostStatus(Context)
                            .ConfigureAwait(false);
    }

    /// <summary>
    /// Get the server logs
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [SlashCommand("logs", "Server logs")]
    public async Task Logs()
    {
        await CommandHandler.PostLogs(Context)
                            .ConfigureAwait(false);
    }

    /// <summary>
    /// Restarts the server
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    [SlashCommand("restart", "Restart the server")]
    public async Task Restart()
    {
        await CommandHandler.RestartServer(Context)
                            .ConfigureAwait(false);
    }

    #endregion // Commands
}