using CoworkWriter.Core;
using CoworkWriter.Core.Scrivener;
using Spectre.Console;

LoadEnvFile();

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    AnsiConsole.MarkupLine("[red]Error:[/] ANTHROPIC_API_KEY not found. Add it to a .env file.");
    return 1;
}

var model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL");
var config = model is not null ? new AppConfig(apiKey, model) : new AppConfig(apiKey);
var service = new AnthropicService(config);
var contextBuilder = new ContextBuilder();

ScrivenerProject? loadedProject = null;
var selectedIds = new HashSet<string>();

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
              [bold]/load <path>[/]    — load a .scriv project
              [bold]/list[/]           — display the binder tree
              [bold]/select <id>[/]    — toggle a document in/out of context
              [bold]/context[/]        — show currently loaded context
              [bold]/clear[/]          — clear conversation history
              [bold]/help[/]           — show this message
              [bold]/quit[/]           — exit
            """);
        continue;
    }

    if (input.StartsWith("/load ", StringComparison.OrdinalIgnoreCase))
    {
        var path = input[6..].Trim();
        var result = ScrivenerParser.Parse(path);
        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.Error!)}");
        }
        else
        {
            loadedProject = result.Project;
            selectedIds.Clear();
            service.SystemPrompt = null;
            AnsiConsole.MarkupLine($"[green]Loaded:[/] {Markup.Escape(path)}\n");
        }
        continue;
    }

    if (input.Equals("/list", StringComparison.OrdinalIgnoreCase))
    {
        if (loadedProject is null)
        {
            AnsiConsole.MarkupLine("[yellow]No project loaded. Use /load <path>.[/]\n");
        }
        else
        {
            var tree = new Tree("[bold]Binder[/]");
            AddBinderItems(tree, loadedProject.Binder, selectedIds);
            AnsiConsole.Write(tree);
            Console.WriteLine();
        }
        continue;
    }

    if (input.StartsWith("/select ", StringComparison.OrdinalIgnoreCase))
    {
        if (loadedProject is null)
        {
            AnsiConsole.MarkupLine("[yellow]No project loaded. Use /load <path>.[/]\n");
            continue;
        }
        var id = input[8..].Trim();
        var item = loadedProject.AllItems().FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            AnsiConsole.MarkupLine($"[red]Unknown ID:[/] {Markup.Escape(id)}\n");
        }
        else if (selectedIds.Remove(id))
        {
            AnsiConsole.MarkupLine($"[grey]Deselected:[/] {Markup.Escape(item.Title)}\n");
            UpdateSystemPrompt(service, contextBuilder, loadedProject, selectedIds);
        }
        else
        {
            selectedIds.Add(id);
            AnsiConsole.MarkupLine($"[green]Selected:[/] {Markup.Escape(item.Title)}\n");
            UpdateSystemPrompt(service, contextBuilder, loadedProject, selectedIds);
        }
        continue;
    }

    if (input.Equals("/context", StringComparison.OrdinalIgnoreCase))
    {
        if (loadedProject is null)
        {
            AnsiConsole.MarkupLine("[yellow]No project loaded.[/]\n");
        }
        else if (selectedIds.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No documents selected.[/]\n");
        }
        else
        {
            var ctx = contextBuilder.Build(loadedProject, selectedIds);
            AnsiConsole.MarkupLine($"[bold]Active context[/] ({ctx.Length:N0} chars):\n");
            AnsiConsole.WriteLine(ctx.Length > 2000 ? ctx[..2000] + "\n[...preview truncated]" : ctx);
            Console.WriteLine();
        }
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

static void UpdateSystemPrompt(
    AnthropicService service,
    ContextBuilder builder,
    ScrivenerProject project,
    HashSet<string> selectedIds)
{
    var ctx = builder.Build(project, selectedIds);
    service.SystemPrompt = ctx.Length > 0
        ? $"You are a writing assistant. The following manuscript excerpts are your context:\n\n{ctx}"
        : null;
}

static void AddBinderItems(IHasTreeNodes node, IEnumerable<CoworkWriter.Core.Scrivener.BinderItem> items, HashSet<string> selectedIds)
{
    foreach (var item in items)
    {
        var marker = selectedIds.Contains(item.Id) ? "[green]✓[/] " : "  ";
        var label = $"{marker}[grey]{Markup.Escape(item.Id[..Math.Min(8, item.Id.Length)])}[/] {Markup.Escape(item.Title)}";
        var child = node.AddNode(label);
        if (item.Children.Count > 0)
            AddBinderItems(child, item.Children, selectedIds);
    }
}

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
                if (!string.IsNullOrEmpty(key) && Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
            break;
        }
        dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
    }
}
