using System;
using System.IO;
using System.Threading.Tasks;

using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;

using Hugin.Data;
using Hugin.Discord.CommandHandler;
using Hugin.Discord.SlashCommands;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

namespace Hugin;

/// <summary>
/// Main class
/// </summary>
public static class Program
{
    #region Fields

    /// <summary>
    /// Wait for program exit
    /// </summary>
    private static readonly TaskCompletionSource<bool> _waitForExitTaskSource = new();

    /// <summary>
    /// Discord client
    /// </summary>
    private static DiscordSocketClient _discordClient;

    /// <summary>
    /// Interaction service
    /// </summary>
    private static InteractionService _discordInteraction;

    /// <summary>
    /// Service provider
    /// </summary>
    private static ServiceProvider _serviceProvider;

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Main entry point
    /// </summary>
    /// <param name="args">Arguments</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        WriteLine("Starting Hugin...");

        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        try
        {
            var configurationFilePath = Environment.GetEnvironmentVariable("HUGIN_CONFIG_FILE_PATH");

            if (string.IsNullOrEmpty(configurationFilePath))
            {
                Console.WriteLine("HUGIN_CONFIG_FILE_PATH environment variable is not set.");

                return;
            }

            if (File.Exists(configurationFilePath) == false)
            {
                Console.WriteLine($"Configuration file not found: {configurationFilePath}");

                return;
            }

            var configuration = Configuration.Current = JsonConvert.DeserializeObject<Configuration>(await File.ReadAllTextAsync(configurationFilePath).ConfigureAwait(false));

            if (args.Length == 0)
            {
                WriteLine("Starting Discord bot...");

                await ExecuteDiscordBot(configuration).ConfigureAwait(false);
            }
            else if (args[0] == "installCommands")
            {
                WriteLine("Installing commands...");

                await InstallCommands(configuration).ConfigureAwait(false);
            }
            else
            {
                WriteLine($"Unknown command: {args[0]}");
            }
        }
        catch (Exception ex)
        {
            WriteLine("Unhandled exception", ex);
        }

        WriteLine("Hugin stopped");
    }

    /// <summary>
    /// Install commands
    /// </summary>
    /// <param name="configuration">Configuration</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task InstallCommands(Configuration configuration)
    {
        var discordClient = new DiscordRestClient();

        await discordClient.LoginAsync(TokenType.Bot, configuration.DiscordToken)
                           .ConfigureAwait(false);

        var discordInteraction = new InteractionService(discordClient);

        await discordInteraction.AddModuleAsync<ServerSlashCommands>(BuildServiceProvider()).ConfigureAwait(false);

        await discordInteraction.RegisterCommandsToGuildAsync(configuration.GuildId)
                                .ConfigureAwait(false);
    }

    /// <summary>
    /// Execute the Discord bot
    /// </summary>
    /// <param name="configuration">Configuration</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task ExecuteDiscordBot(Configuration configuration)
    {
        _serviceProvider = BuildServiceProvider();

        var discordConfiguration = new DiscordSocketConfig
                                   {
                                       LogLevel = LogSeverity.Info,
                                       MessageCacheSize = 100,
                                       GatewayIntents = GatewayIntents.GuildIntegrations
                                   };

        _discordClient = new DiscordSocketClient(discordConfiguration);
        _discordClient.InteractionCreated += OnInteractionCreated;
        _discordClient.Log += OnDiscordClientLog;
        _discordClient.Connected += OnConnected;
        _discordClient.Disconnected += OnDisconnected;

        var interactionConfiguration = new InteractionServiceConfig
                                       {
                                           LogLevel = LogSeverity.Info,
                                           DefaultRunMode = RunMode.Async,
                                           ThrowOnError = true
                                       };

        _discordInteraction = new InteractionService(_discordClient, interactionConfiguration);
        _discordInteraction.Log += OnDiscordInteractionServiceLog;

        await _discordInteraction.AddModuleAsync<ServerSlashCommands>(_serviceProvider)
                                 .ConfigureAwait(false);
        await _discordClient.LoginAsync(TokenType.Bot, configuration.DiscordToken)
                            .ConfigureAwait(false);
        await _discordClient.StartAsync()
                            .ConfigureAwait(false);
        await _waitForExitTaskSource.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Build the service provider
    /// </summary>
    /// <returns>Service provider</returns>
    private static ServiceProvider BuildServiceProvider()
    {
        var serviceCollection = new ServiceCollection().AddTransient<ServerCommandHandler>();

        return serviceCollection.BuildServiceProvider();
    }

    /// <summary>
    /// Called when the Discord client is connected
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static Task OnConnected()
    {
        WriteLine("Connected");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the Discord client is disconnected
    /// </summary>
    /// <param name="arg">Argument</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static Task OnDisconnected(Exception arg)
    {
        WriteLine("Disconnected", arg);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the Discord client logs a message
    /// </summary>
    /// <param name="arg">Argument</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static Task OnDiscordClientLog(LogMessage arg)
    {
        WriteLine(arg.ToString());

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the Discord interaction service logs a message
    /// </summary>
    /// <param name="arg">Argument</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static Task OnDiscordInteractionServiceLog(LogMessage arg)
    {
        WriteLine(arg.ToString());

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when an interaction is created
    /// </summary>
    /// <param name="arg">Interaction</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task OnInteractionCreated(SocketInteraction arg)
    {
        try
        {
            IMessageChannel channel = arg.Channel;

            if (channel == null
                && arg.ChannelId != null)
            {
                channel = await _discordClient.GetChannelAsync(arg.ChannelId.Value)
                                              .ConfigureAwait(false) as IMessageChannel;
            }

            await _discordInteraction.ExecuteCommandAsync(new InteractionContext(_discordClient, arg, channel), _serviceProvider)
                                     .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            WriteLine("Interaction failed", ex);
        }
    }

    /// <summary>
    /// Write a line to the console
    /// </summary>
    /// <param name="message">Message</param>
    /// <param name="ex">Exception</param>
    private static void WriteLine(string message, Exception ex = null)
    {
        Console.WriteLine(ex != null
                              ? $"[{DateTime.Now:g}] {message}{Environment.NewLine}  => {ex}"
                              : $"[{DateTime.Now:g}] {message}");
    }

    /// <summary>
    /// The cancel key was pressed
    /// </summary>
    /// <param name="sender">Sender</param>
    /// <param name="e">Arguments</param>
    private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = false;

        _waitForExitTaskSource.SetResult(true);
    }

    /// <summary>
    /// Occurs when the default application domain's parent process exits.
    /// </summary>
    /// <param name="sender">Sender</param>
    /// <param name="e">Argument</param>
    private static void OnProcessExit(object sender, EventArgs e)
    {
        _waitForExitTaskSource.SetResult(true);
    }

    #endregion // Methods
}