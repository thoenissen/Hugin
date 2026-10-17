using System;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Interactions;

using Hugin.Data;

namespace Hugin.Discord.TypeConverters;

/// <summary>
/// Type converter for selecting a configured server by name
/// </summary>
public class ServerTypeConverter : TypeConverter<Server>
{
    #region Constants

    /// <summary>
    /// Maximum number of choices supported by Discord
    /// </summary>
    private const int MaxChoices = 25;

    #endregion // Constants

    #region TypeConverter

    /// <inheritdoc/>
    public override ApplicationCommandOptionType GetDiscordType() => ApplicationCommandOptionType.String;

    /// <inheritdoc/>
    public override Task<TypeConverterResult> ReadAsync(IInteractionContext context, IApplicationCommandInteractionDataOption option, IServiceProvider services)
    {
        var name = option.Value as string;
        var server = Configuration.Current.Servers.Find(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(server != null
                                   ? TypeConverterResult.FromSuccess(server)
                                   : TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, $"Unknown server: {name}"));
    }

    /// <inheritdoc/>
    public override void Write(ApplicationCommandOptionProperties properties, IParameterInfo parameter)
    {
        properties.Choices = Configuration.Current
                                          .Servers
                                          .Select(s => s.Name)
                                          .Distinct(StringComparer.OrdinalIgnoreCase)
                                          .Take(MaxChoices)
                                          .Select(name => new ApplicationCommandOptionChoiceProperties
                                                          {
                                                              Name = name,
                                                              Value = name
                                                          })
                                          .ToList();
    }

    #endregion // TypeConverter
}