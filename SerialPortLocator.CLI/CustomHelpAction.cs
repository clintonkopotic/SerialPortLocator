namespace SerialPortLocator.CLI;

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.IO;

/// <summary>
/// Used to override the default help action so that the
/// <see cref="VersionOption"/> is also displayed, if it is available. Use the
/// <see langword="static"/> <see langword="method"/>
/// <see cref="UpdateWith(RootCommand)"/> to override.
/// </summary>
internal class CustomHelpAction : SynchronousCommandLineAction
{
    /// <summary>
    /// The default <see cref="string"/> for indenting prior to the
    /// <see cref="VersionOption.Action"/> being displayed.
    /// </summary>
    public const string DefaultIndent = "  ";

    /// <summary>
    /// The default <see cref="string"/> for the version section title.
    /// </summary>
    public const string DefaultVersionSectionTitle = "Version";

    private readonly HelpAction _helpAction;

    private readonly string _indent;

    private readonly VersionOption? _versionOption;

    private readonly string _versionSectionTitle;

    private CustomHelpAction(HelpAction helpAction,
        VersionOption? versionOption = null,
        string versionSectionTitle = DefaultVersionSectionTitle,
        string indent = DefaultIndent)
    {
        _helpAction = helpAction;
        _versionOption = versionOption;
        _versionSectionTitle = versionSectionTitle;
        _indent = indent;
    }

    /// <inheritdoc/>
    public override int Invoke(ParseResult parseResult)
    {
        TextWriter output = parseResult.InvocationConfiguration.Output;

        // The exit code for the process.
        int exitCode = 0;

        // Only display the Version option if it and its Action are available.
        if (_versionOption is not null
            && (_versionOption.Action is SynchronousCommandLineAction
                || _versionOption.Action is AsynchronousCommandLineAction))
        {
            if (!string.IsNullOrWhiteSpace(_versionSectionTitle))
            {
                output.WriteLine($"{_versionSectionTitle}:");
            }

            output.Write(_indent);

            if (_versionOption.Action
                is SynchronousCommandLineAction versionAction)
            {
                exitCode = versionAction.Invoke(parseResult);
            }
            else if (_versionOption.Action
                is AsynchronousCommandLineAction versionActionAsync)
            {
                exitCode = versionActionAsync.InvokeAsync(parseResult)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
            }

            output.WriteLine();

            // If the exit code is non-zero then return, as this indicates an
            // anomaly.
            if (exitCode != 0)
            {
                return exitCode;
            }
        }

        // Display the built-in help message.
        exitCode = _helpAction.Invoke(parseResult);

        return exitCode;
    }

    /// <summary>
    /// Overrides the default <see cref="HelpOption.Action"/> of the
    /// <paramref name="rootCommand"/> with <see langword="this"/>.
    /// </summary>
    /// <param name="rootCommand"></param>
    /// <param name="versionSectionTitle">The <see cref="string"/> to display at
    /// the start of the version section, followed by a <c>':'</c>
    /// character and end-of-line. If it is empty or consists only of
    /// white-space characters, then nothing is written.</param>
    /// <param name="indent">The <see cref="string"/> to display before the
    /// <see cref="VersionOption.Action"/> is displayed.</param>
    public static void UpdateWith(RootCommand rootCommand,
        string versionSectionTitle = DefaultVersionSectionTitle,
        string indent = DefaultIndent)
    {
        HelpOption? helpOption = null;
        VersionOption? versionOption = null;

        // Locate the Help and Version options and store.
        foreach (var option in rootCommand.Options)
        {
            if (option is HelpOption defaultHelpOption)
            {
                helpOption = defaultHelpOption;
            }
            else if (option is VersionOption defaultVersionOption)
            {
                versionOption = defaultVersionOption;
            }

            // Stop looking if both options are found.
            if (helpOption is not null && versionOption is not null)
            {
                break;
            }
        }

        // Only override the Help option's action if there already is one.
        if (helpOption is not null
            && helpOption.Action is HelpAction helpAction)
        {
            helpOption.Action = new CustomHelpAction(helpAction,
                versionOption, versionSectionTitle, indent);
        }
    }
}
