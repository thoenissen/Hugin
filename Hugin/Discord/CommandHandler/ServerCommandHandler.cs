using System;
using System.Collections.Generic;
using System.IO;
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
    /// Post the current status of the server
    /// </summary>
    /// <param name="context">Context</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    public async Task PostStatus(IInteractionContext context)
    {
        await context.Interaction
                     .RespondAsync("Getting server status...")
                     .ConfigureAwait(false);

        var serverConfiguration = Configuration.Current.Servers.Find(s => s.ChannelId == context.Channel.Id);

        if (serverConfiguration == null)
        {
            await context.Interaction
                         .ModifyOriginalResponseAsync(msg => msg.Content = "No server is assigned to this channel.")
                         .ConfigureAwait(false);

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
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    public async Task PostLogs(IInteractionContext context)
    {
        await context.Interaction
                     .RespondAsync("Getting server logs...")
                     .ConfigureAwait(false);

        var serverConfiguration = Configuration.Current.Servers.Find(s => s.ChannelId == context.Channel.Id);

        if (serverConfiguration == null)
        {
            await context.Interaction
                         .ModifyOriginalResponseAsync(msg => msg.Content = "No server is assigned to this channel.")
                         .ConfigureAwait(false);

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
    /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
    public async Task RestartServer(IInteractionContext context)
    {
        await context.Interaction
                     .RespondAsync("Getting server status...")
                     .ConfigureAwait(false);

        var serverConfiguration = Configuration.Current.Servers.Find(s => s.ChannelId == context.Channel.Id);

        if (serverConfiguration == null)
        {
            await context.Interaction
                         .ModifyOriginalResponseAsync(msg => msg.Content = "No server is assigned to this channel.")
                         .ConfigureAwait(false);

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