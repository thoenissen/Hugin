using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Discord;

using Docker.DotNet;
using Docker.DotNet.Models;

using Hugin.Data;

namespace Hugin.Discord.CommandHandler;

/// <summary>
/// Command handler for server commands
/// </summary>
public class ServerCommandHandler
{
    #region Methods

    /// <summary>
    /// Determine the server configuration which should be used by the command
    /// </summary>
    /// <param name="context">Context</param>
    /// <param name="server">Selected server</param>
    /// <returns>The server configuration or <see langword="null"/> if no valid server could be determined</returns>
    /// <remarks>If no valid server could be determined, the original response is modified with an error message.</remarks>
    private static async Task<Server> GetServerConfiguration(IInteractionContext context, Server server)
    {
        var channelServers = Configuration.Current.Servers.FindAll(s => s.ChannelId == context.Channel.Id);

        string errorMessage = null;

        if (server != null)
        {
            if (channelServers.Contains(server) == false)
            {
                errorMessage = $"The server '{server.Name}' is not assigned to this channel.";
            }
        }
        else if (channelServers.Count == 0)
        {
            errorMessage = "No server is assigned to this channel.";
        }
        else if (channelServers.Count == 1)
        {
            server = channelServers[0];
        }
        else
        {
            errorMessage = $"Multiple servers are assigned to this channel. Please select one of the following servers: {string.Join(", ", channelServers.Select(s => s.Name))}";
        }

        if (errorMessage != null)
        {
            await context.Interaction
                         .ModifyOriginalResponseAsync(msg => msg.Content = errorMessage)
                         .ConfigureAwait(false);

            return null;
        }

        return server;
    }

    /// <summary>
    /// Post the current status of the server
    /// </summary>
    /// <param name="context">Context</param>
    /// <param name="server">Selected server</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    public async Task PostStatus(IInteractionContext context, Server server)
    {
        await context.Interaction
                     .RespondAsync("Getting server status...")
                     .ConfigureAwait(false);

        var serverConfiguration = await GetServerConfiguration(context, server).ConfigureAwait(false);

        if (serverConfiguration == null)
        {
            return;
        }

        var embed = new EmbedBuilder().WithAuthor(serverConfiguration.Name)
                                      .WithFooter("Hugin", StaticConfiguration.HuginAvatarUrl)
                                      .WithCurrentTimestamp();
        var isOnline = false;

        try
        {
            using (var dockerClient = new DockerClientConfiguration(new Uri(Configuration.Current.DockerEndpoint)).CreateClient())
            {
                var container = await dockerClient.Containers
                                                  .InspectContainerAsync(serverConfiguration.Container)
                                                  .ConfigureAwait(false);

                isOnline = container?.State.Running == true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }

        embed.AddField("Status", isOnline ? "Online" : "Offline");

        await context.Interaction
                     .ModifyOriginalResponseAsync(msg =>
                                                  {
                                                      msg.Content = null;
                                                      msg.Embed = embed.Build();
                                                  })
                     .ConfigureAwait(false);
    }

    /// <summary>
    /// Post the server logs
    /// </summary>
    /// <param name="context">Context</param>
    /// <param name="server">Selected server</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    public async Task PostLogs(IInteractionContext context, Server server)
    {
        await context.Interaction
                     .RespondAsync("Getting server logs...")
                     .ConfigureAwait(false);

        var serverConfiguration = await GetServerConfiguration(context, server).ConfigureAwait(false);

        if (serverConfiguration == null)
        {
            return;
        }

        string stdout = null;
        string stderr = null;

        try
        {
            using (var dockerClient = new DockerClientConfiguration(new Uri(Configuration.Current.DockerEndpoint)).CreateClient())
            {
                var container = await dockerClient.Containers
                                                  .InspectContainerAsync(serverConfiguration.Container)
                                                  .ConfigureAwait(false);

                if (container != null)
                {
                    var parameters = new ContainerLogsParameters
                                     {
                                         ShowStderr = true,
                                         ShowStdout = true
                                     };

                    var logs = await dockerClient.Containers
                                                 .GetContainerLogsAsync(container.ID, false, parameters)
                                                 .ConfigureAwait(false);

                    (stdout, stderr) = await logs.ReadOutputToEndAsync(CancellationToken.None)
                                                 .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }

        await context.Interaction
                     .ModifyOriginalResponseAsync(msg =>
                                                  {
                                                      if (string.IsNullOrWhiteSpace(stdout)
                                                          && string.IsNullOrWhiteSpace(stderr))
                                                      {
                                                          msg.Content = "No logs available.";
                                                      }
                                                      else
                                                      {
                                                          msg.Content = "\u200b";

                                                          var attachments = new List<FileAttachment>();

                                                          if (string.IsNullOrWhiteSpace(stdout) == false)
                                                          {
                                                              attachments.Add(new FileAttachment(new MemoryStream(Encoding.UTF8.GetBytes(stdout)), "stdout.txt"));
                                                          }

                                                          if (string.IsNullOrWhiteSpace(stderr) == false)
                                                          {
                                                              attachments.Add(new FileAttachment(new MemoryStream(Encoding.UTF8.GetBytes(stderr)), "stderr.txt"));
                                                          }

                                                          msg.Attachments = attachments;
                                                      }
                                                  })
                     .ConfigureAwait(false);
    }

    /// <summary>
    /// Restarts the server
    /// </summary>
    /// <param name="context">Context</param>
    /// <param name="server">Selected server</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    public async Task RestartServer(IInteractionContext context, Server server)
    {
        await context.Interaction
                     .RespondAsync("Getting server status...")
                     .ConfigureAwait(false);

        var serverConfiguration = await GetServerConfiguration(context, server).ConfigureAwait(false);

        if (serverConfiguration == null)
        {
            return;
        }

        try
        {
            using (var dockerClient = new DockerClientConfiguration(new Uri(Configuration.Current.DockerEndpoint)).CreateClient())
            {
                var container = await dockerClient.Containers
                                                  .InspectContainerAsync(serverConfiguration.Container)
                                                  .ConfigureAwait(false);

                if (container.State.Running)
                {
                    await context.Interaction
                                 .ModifyOriginalResponseAsync(msg => msg.Content = "Stopping server...")
                                 .ConfigureAwait(false);

                    try
                    {
                        await dockerClient.Containers
                                          .StopContainerAsync(container.ID,
                                                              new ContainerStopParameters
                                                              {
                                                                  WaitBeforeKillSeconds = 5
                                                              })
                                          .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex}");
                    }
                }

                container = await dockerClient.Containers
                                              .InspectContainerAsync(serverConfiguration.Container)
                                              .ConfigureAwait(false);

                if (container.State.Running)
                {
                    await context.Interaction
                                 .ModifyOriginalResponseAsync(msg => msg.Content = "Failed to stop server. Killing server process...")
                                 .ConfigureAwait(false);

                    try
                    {
                        await dockerClient.Containers
                                          .KillContainerAsync(container.ID,
                                                              new ContainerKillParameters
                                                              {
                                                                  Signal = "SIGKILL"
                                                              })
                                          .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex}");
                    }
                }

                container = await dockerClient.Containers
                                              .InspectContainerAsync(serverConfiguration.Container)
                                              .ConfigureAwait(false);

                if (container.State.Running)
                {
                    await context.Interaction
                                 .ModifyOriginalResponseAsync(msg => msg.Content = "Server is still running. Aborting restart.")
                                 .ConfigureAwait(false);
                }
                else
                {
                    await context.Interaction
                                 .ModifyOriginalResponseAsync(msg => msg.Content = "Starting server...")
                                 .ConfigureAwait(false);
                    try
                    {
                        await dockerClient.Containers
                                          .StartContainerAsync(container.ID, new ContainerStartParameters())
                                          .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex}");
                    }

                    container = await dockerClient.Containers
                                                  .InspectContainerAsync(serverConfiguration.Container)
                                                  .ConfigureAwait(false);

                    if (container.State.Running == false)
                    {
                        await context.Interaction
                                      .ModifyOriginalResponseAsync(msg => msg.Content = "Failed to start server. Retrying...")
                                      .ConfigureAwait(false);
                        try
                        {
                            await dockerClient.Containers
                                              .StartContainerAsync(container.ID, new ContainerStartParameters())
                                              .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex}");
                        }
                    }

                    if (container.State.Running)
                    {
                        await context.Interaction
                                     .ModifyOriginalResponseAsync(msg => msg.Content = "Server restarted successfully.")
                                     .ConfigureAwait(false);
                    }
                    else
                    {
                        await context.Interaction
                                     .ModifyOriginalResponseAsync(msg => msg.Content = "Server failed to start.")
                                     .ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }

    #endregion // Methods
}