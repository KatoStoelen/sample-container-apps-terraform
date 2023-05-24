private void Dbg(
        RawString message,
        ConsoleColor? color = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, backgroundColor, LogLevel.Debug);

private void Dbg(
        FormattableString message,
        ConsoleColor? color = null,
        ConsoleColor? interpolationColor = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, interpolationColor, backgroundColor, LogLevel.Debug);

private void Verb(
        RawString message,
        ConsoleColor? color = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, backgroundColor, LogLevel.Verbose);

private void Verb(
        FormattableString message,
        ConsoleColor? color = null,
        ConsoleColor? interpolationColor = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, interpolationColor, backgroundColor, LogLevel.Verbose);

private void Info(
        RawString message,
        ConsoleColor? color = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, backgroundColor, LogLevel.Information);

private void Info(
        FormattableString message,
        ConsoleColor? color = null,
        ConsoleColor? interpolationColor = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, interpolationColor, backgroundColor, LogLevel.Information);

private void Warn(
        RawString message,
        ConsoleColor? color = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, backgroundColor, LogLevel.Warning);

private void Warn(
        FormattableString message,
        ConsoleColor? color = null,
        ConsoleColor? interpolationColor = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, interpolationColor, backgroundColor, LogLevel.Warning);

private void Err(
        RawString message,
        ConsoleColor? color = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, backgroundColor, LogLevel.Error);

private void Err(
        FormattableString message,
        ConsoleColor? color = null,
        ConsoleColor? interpolationColor = null,
        ConsoleColor? backgroundColor = null) =>
    Write(message, color, interpolationColor, backgroundColor, LogLevel.Error);

private void Write(
    RawString message,
    ConsoleColor? color,
    ConsoleColor? backgroundColor,
    LogLevel logLevel)
{
    var (defaultForeground, defaultBackground, _) = GetDefaultColors(logLevel);

    Context.Log.Write(
        Context.Log.Verbosity,
        logLevel,
        Colorize(
            message.Value,
            color ?? defaultForeground,
            backgroundColor ?? defaultBackground,
            shouldReset: true));
}

private void Write(
    FormattableString message,
    ConsoleColor? color,
    ConsoleColor? interpolationColor,
    ConsoleColor? backgroundColor,
    LogLevel logLevel)
{
    if (message.ArgumentCount == 0)
    {
        Write(message.ToString(), color, backgroundColor, logLevel);
    }
    else
    {
        var (defaultForeground, defaultBackground, defaultInterpolation) = GetDefaultColors(logLevel);

        var substrings = message.Format.Split(
            Enumerable
                .Range(0, message.ArgumentCount)
                .Select(i => $"{{{i}}}")
                .ToArray(),
            StringSplitOptions.None);

        var outputText = string.Empty;

        for (var i = 0; i < substrings.Length; i++)
        {
            outputText += Colorize(
                substrings[i],
                color ?? defaultForeground,
                backgroundColor ?? defaultBackground,
                shouldReset: i == substrings.Length - 1);

            if (i < message.ArgumentCount)
            {
                outputText += Colorize(
                    message.GetArgument(i)?.ToString() ?? "[null]",
                    interpolationColor ?? defaultInterpolation,
                    backgroundColor ?? defaultBackground,
                    shouldReset: false);
            }
        }

        Context.Log.Write(
            Context.Log.Verbosity,
            logLevel,
            outputText);
    }
}

private string Colorize(
    string text,
    ConsoleColor color,
    ConsoleColor? backgroundColor,
    bool shouldReset)
{
    var builder = new StringBuilder();

    if (text.Length > 0)
    {
        builder.Append($"\u001b[{GetAnsiForegroundColor(color)}");

        if (backgroundColor.HasValue)
        {
            builder.Append($";{GetAnsiBackgroundColor(backgroundColor.Value)}");
        }

        builder.Append($"m{text}");
    }

    if (shouldReset)
    {
        builder.Append("\u001b[0m");
    }

    return builder.ToString();
}

private int GetAnsiForegroundColor(ConsoleColor color) =>
    color switch
    {
        ConsoleColor.Black => 30,
        ConsoleColor.DarkRed => 31,
        ConsoleColor.DarkGreen => 32,
        ConsoleColor.DarkYellow => 33,
        ConsoleColor.DarkBlue => 34,
        ConsoleColor.DarkMagenta => 35,
        ConsoleColor.DarkCyan => 36,
        ConsoleColor.Gray => 37,
        ConsoleColor.DarkGray => 90,
        ConsoleColor.Red => 91,
        ConsoleColor.Green => 92,
        ConsoleColor.Yellow => 93,
        ConsoleColor.Blue => 94,
        ConsoleColor.Magenta => 95,
        ConsoleColor.Cyan => 96,
        ConsoleColor.White => 97,
        _ => throw new ArgumentOutOfRangeException(nameof(color), $"Unknown console color: {color}"),
    };

private int GetAnsiBackgroundColor(ConsoleColor color) =>
    color switch
    {
        ConsoleColor.Black => 40,
        ConsoleColor.DarkRed => 41,
        ConsoleColor.DarkGreen => 42,
        ConsoleColor.DarkYellow => 43,
        ConsoleColor.DarkBlue => 44,
        ConsoleColor.DarkMagenta => 45,
        ConsoleColor.DarkCyan => 46,
        ConsoleColor.Gray => 47,
        ConsoleColor.DarkGray => 100,
        ConsoleColor.Red => 101,
        ConsoleColor.Green => 102,
        ConsoleColor.Yellow => 103,
        ConsoleColor.Blue => 104,
        ConsoleColor.Magenta => 105,
        ConsoleColor.Cyan => 106,
        ConsoleColor.White => 107,
        _ => throw new ArgumentOutOfRangeException(nameof(color), $"Unknown console color: {color}"),
    };

private class RawString
{
    private RawString(string value)
    {
        Value = value;
    }

    public string Value { get; set; }

    public static implicit operator RawString(string value) => new(value);
    public static implicit operator RawString(FormattableString value) => new(value.ToString());
}

var _outputColorOverrides = new Dictionary<(LogLevel, BuildProvider?), (ConsoleColor?, ConsoleColor?, ConsoleColor?)>();

private void SetOutputColors(
    LogLevel logLevel,
    BuildProvider? buildProvider = null,
    ConsoleColor? foreground = null,
    ConsoleColor? background = null,
    ConsoleColor? interpolation = null)
{
    if (logLevel == LogLevel.Fatal)
        throw new ArgumentException("Setting colors for log level 'Fatal' is not supported", nameof(logLevel));

    _outputColorOverrides[(logLevel, buildProvider)] = (foreground, background, interpolation);
}

private (ConsoleColor Foreground, ConsoleColor? Background, ConsoleColor Interpolation) GetDefaultColors(LogLevel logLevel)
{
    var (defaultForeground, defaultBackground, defaultInterpolation) = (logLevel, BuildSystem.Provider) switch
    {
        (LogLevel.Debug, BuildProvider.TeamCity)   => (ConsoleColor.Black, (ConsoleColor?)null, ConsoleColor.DarkGray),
        (LogLevel.Debug, _)                        => (ConsoleColor.Gray, (ConsoleColor?)null, ConsoleColor.DarkGray),
        (LogLevel.Verbose, BuildProvider.TeamCity) => (ConsoleColor.DarkGray, (ConsoleColor?)null, ConsoleColor.Black),
        (LogLevel.Verbose, _)                      => (ConsoleColor.DarkGray, (ConsoleColor?)null, ConsoleColor.White),
        (LogLevel.Information, _)                  => (ConsoleColor.DarkCyan, (ConsoleColor?)null, ConsoleColor.DarkGreen),
        (LogLevel.Warning, BuildProvider.TeamCity) => (ConsoleColor.DarkYellow, (ConsoleColor?)null, ConsoleColor.DarkGreen),
        (LogLevel.Warning, _)                      => (ConsoleColor.Yellow, (ConsoleColor?)null, ConsoleColor.DarkGreen),
        (LogLevel.Error, _)                        => (ConsoleColor.White, ConsoleColor.DarkRed, ConsoleColor.Black),
        (_, _)                                     => (ConsoleColor.White, ConsoleColor.DarkMagenta, ConsoleColor.Black)
    };

    ConsoleColor? overrideForeground = null;
    ConsoleColor? overrideBackground = null;
    ConsoleColor? overrideInterpolation = null;

    if (_outputColorOverrides.ContainsKey((logLevel, BuildSystem.Provider)))
    {
        (overrideForeground, overrideBackground, overrideInterpolation) =
            _outputColorOverrides[(logLevel, BuildSystem.Provider)];
    }
    else if (_outputColorOverrides.ContainsKey((logLevel, null)))
    {
        (overrideForeground, overrideBackground, overrideInterpolation) =
            _outputColorOverrides[(logLevel, null)];
    }

    return (
        overrideForeground ?? defaultForeground,
        overrideBackground ?? defaultBackground,
        overrideInterpolation ?? defaultInterpolation
    );
}