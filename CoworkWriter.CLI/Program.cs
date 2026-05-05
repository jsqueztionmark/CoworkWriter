using CoworkWriter.Core;
using Spectre.Console;

LoadEnvFile();

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    AnsiConsole.MarkupLine("[red]Error:[/] ANTHROPIC_API_KEY not found. Add it to a .env file.");
    return 1;
}

var config = new AppConfig(apiKey);
var service = new AnthropicService(config);

AnsiConsole.MarkupLine("[bold green]CoworkWriter[/] — Claude-powered writing assistant");
AnsiConsole.MarkupLine("Type [bold]/help[/] for commands or [bold]/quit[/] to exit.\n");

while (true)
{
    AnsiConsole.Markup("[grey]You>[/] ");
    var input = Console.ReadLine()?.Trim() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        break;

    if (input.Equals("/clear", StringComparison.OrdinalIgnoreCase))
    {
        service.ClearHistory();
        AnsiConsole.MarkupLine("[grey]Conversation cleared.[/]\n");
        continue;
    }

    if (input.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("""
            [bold]Commands:[/]
              [bold]/clear[/]  — clear conversation history
              [bold]/help[/]   — show this message
              [bold]/quit[/]   — exit
            """);
        continue;
    }

    AnsiConsole.Markup("[bold cyan]Claude>[/] ");

    try
    {
        await foreach (var chunk in service.StreamMessageAsync(input))
            Console.Write(chunk);

        Console.WriteLine("\n");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine();
        break;
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}\n");
    }
}

return 0;

static void LoadEnvFile()
{
    var dir = Directory.GetCurrentDirectory();
    while (!string.IsNullOrEmpty(dir))
    {
        var envPath = Path.Combine(dir, ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#') || !trimmed.Contains('='))
                    continue;
                var idx = trimmed.IndexOf('=');
                var key = trimmed[..idx].Trim();
                var value = trimmed[(idx + 1)..].Trim();
                if (!string.IsNullOrEmpty(key))
                    Environment.SetEnvironmentVariable(key, value);
            }
            break;
        }
        dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
    }
}
