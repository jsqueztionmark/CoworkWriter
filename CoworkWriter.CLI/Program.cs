using CoworkWriter.Core;
using CoworkWriter.Core.Scrivener;
using CoworkWriter.Core.Writing;
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
var sessionStore = new SessionStore();

ScrivenerProject? loadedProject = null;
var selectedIds = new HashSet<string>();
var pinnedIds = new HashSet<string>();

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
            [bold]Project:[/]
              [bold]/load <path>[/]         — load a .scriv project (auto-loads session)
              [bold]/list[/]                — display the binder tree
              [bold]/select <id>[/]         — toggle a document in/out of context
              [bold]/pin <id>[/]            — toggle always-include (pinned docs load first)
              [bold]/context[/]             — preview the active context
              [bold]/save[/]                — save conversation to project session file

            [bold]Writing:[/]
              [bold]/continue[/]            — continue prose from the selected scene
              [bold]/edit <instruction>[/]   — apply a targeted edit to selected text
              [bold]/brainstorm <topic>[/]   — generate creative ideas
              [bold]/summarize[/]           — summarize selected documents
              [bold]/critique[/]            — get structural and prose feedback

            [bold]General:[/]
              [bold]/clear[/]               — clear conversation history
              [bold]/help[/]                — show this message
              [bold]/quit[/]                — exit
            """);
        continue;
    }

    // --- Project commands ---

    if (input.StartsWith("/load ", StringComparison.OrdinalIgnoreCase))
    {
        var path = input[6..].Trim().Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var result = ScrivenerParser.Parse(path);
        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(result.Error!)}");
        }
        else
        {
            loadedProject = result.Project;
            selectedIds.Clear();
            pinnedIds.Clear();
            service.ClearHistory();
            service.SystemPrompt = null;

            var sessionPath = SessionStore.SessionPath(path);
            if (File.Exists(sessionPath))
            {
                var loaded = sessionStore.Load(sessionPath);
                service.LoadHistory(loaded);
                AnsiConsole.MarkupLine($"[green]Loaded:[/] {Markup.Escape(path)} [grey](session restored, {loaded.Messages.Count} messages)[/]\n");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Loaded:[/] {Markup.Escape(path)}\n");
            }
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
            AddBinderItems(tree, loadedProject.Binder, selectedIds, pinnedIds);
            AnsiConsole.Write(tree);
            Console.WriteLine();
        }
        continue;
    }

    if (input.StartsWith("/select ", StringComparison.OrdinalIgnoreCase))
    {
        if (loadedProject is null) { AnsiConsole.MarkupLine("[yellow]No project loaded.[/]\n"); continue; }
        var id = input[8..].Trim();
        var item = loadedProject.AllItems().FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            AnsiConsole.MarkupLine($"[red]Unknown ID:[/] {Markup.Escape(id)}\n");
        }
        else if (selectedIds.Remove(id))
        {
            AnsiConsole.MarkupLine($"[grey]Deselected:[/] {Markup.Escape(item.Title)}\n");
            UpdateSystemPrompt(service, contextBuilder, loadedProject, selectedIds, pinnedIds);
        }
        else
        {
            selectedIds.Add(id);
            AnsiConsole.MarkupLine($"[green]Selected:[/] {Markup.Escape(item.Title)}\n");
            UpdateSystemPrompt(service, contextBuilder, loadedProject, selectedIds, pinnedIds);
        }
        continue;
    }

    if (input.StartsWith("/pin ", StringComparison.OrdinalIgnoreCase))
    {
        if (loadedProject is null) { AnsiConsole.MarkupLine("[yellow]No project loaded.[/]\n"); continue; }
        var id = input[5..].Trim();
        var item = loadedProject.AllItems().FirstOrDefault(i => i.Id == id);
        if (item is null)
        {
            AnsiConsole.MarkupLine($"[red]Unknown ID:[/] {Markup.Escape(id)}\n");
        }
        else if (pinnedIds.Remove(id))
        {
            AnsiConsole.MarkupLine($"[grey]Unpinned:[/] {Markup.Escape(item.Title)}\n");
            UpdateSystemPrompt(service, contextBuilder, loadedProject, selectedIds, pinnedIds);
        }
        else
        {
            pinnedIds.Add(id);
            AnsiConsole.MarkupLine($"[bold yellow]Pinned:[/] {Markup.Escape(item.Title)}\n");
            UpdateSystemPrompt(service, contextBuilder, loadedProject, selectedIds, pinnedIds);
        }
        continue;
    }

    if (input.Equals("/context", StringComparison.OrdinalIgnoreCase))
    {
        if (loadedProject is null)
        {
            AnsiConsole.MarkupLine("[yellow]No project loaded.[/]\n");
        }
        else if (selectedIds.Count == 0 && pinnedIds.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No documents selected or pinned.[/]\n");
        }
        else
        {
            var ctx = contextBuilder.Build(loadedProject, selectedIds, pinnedIds);
            AnsiConsole.MarkupLine($"[bold]Active context[/] ({ctx.Length:N0} chars):\n");
            AnsiConsole.WriteLine(ctx.Length > 2000 ? ctx[..2000] + "\n[...preview truncated]" : ctx);
            Console.WriteLine();
        }
        continue;
    }

    if (input.Equals("/save", StringComparison.OrdinalIgnoreCase))
    {
        if (loadedProject is null)
        {
            AnsiConsole.MarkupLine("[yellow]No project loaded.[/]\n");
        }
        else if (service.History.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nothing to save.[/]\n");
        }
        else
        {
            var sessionPath = SessionStore.SessionPath(loadedProject.FolderPath);
            sessionStore.Save(sessionPath, service.History);
            AnsiConsole.MarkupLine($"[green]Session saved.[/] ({service.History.Count} messages)\n");
        }
        continue;
    }

    // --- Writing commands ---

    if (input.Equals("/continue", StringComparison.OrdinalIgnoreCase))
    {
        if (!RequiresContext(loadedProject, selectedIds, pinnedIds)) continue;
        await SendWritingPrompt(service, WritingPrompts.Continue());
        continue;
    }

    if (input.StartsWith("/edit ", StringComparison.OrdinalIgnoreCase))
    {
        if (!RequiresContext(loadedProject, selectedIds, pinnedIds)) continue;
        var instruction = input[6..].Trim();
        if (string.IsNullOrWhiteSpace(instruction))
        {
            AnsiConsole.MarkupLine("[yellow]Usage: /edit <instruction>[/]\n");
            continue;
        }
        await SendWritingPrompt(service, WritingPrompts.Edit(instruction));
        continue;
    }

    if (input.StartsWith("/brainstorm ", StringComparison.OrdinalIgnoreCase))
    {
        var topic = input[12..].Trim();
        if (string.IsNullOrWhiteSpace(topic))
        {
            AnsiConsole.MarkupLine("[yellow]Usage: /brainstorm <topic>[/]\n");
            continue;
        }
        await SendWritingPrompt(service, WritingPrompts.Brainstorm(topic));
        continue;
    }

    if (input.Equals("/summarize", StringComparison.OrdinalIgnoreCase))
    {
        if (!RequiresContext(loadedProject, selectedIds, pinnedIds)) continue;
        await SendWritingPrompt(service, WritingPrompts.Summarize());
        continue;
    }

    if (input.Equals("/critique", StringComparison.OrdinalIgnoreCase))
    {
        if (!RequiresContext(loadedProject, selectedIds, pinnedIds)) continue;
        await SendWritingPrompt(service, WritingPrompts.Critique());
        continue;
    }

    // --- General chat ---

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

static bool RequiresContext(ScrivenerProject? project, HashSet<string> selected, HashSet<string> pinned)
{
    if (project is null)
    {
        AnsiConsole.MarkupLine("[yellow]No project loaded. Use /load <path>.[/]\n");
        return false;
    }
    if (selected.Count == 0 && pinned.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No documents selected. Use /select or /pin to add context.[/]\n");
        return false;
    }
    return true;
}

static async Task SendWritingPrompt(AnthropicService service, string prompt)
{
    AnsiConsole.Markup("[bold cyan]Claude>[/] ");
    try
    {
        await foreach (var chunk in service.StreamMessageAsync(prompt))
            Console.Write(chunk);
        Console.WriteLine("\n");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}\n");
    }
}

static void UpdateSystemPrompt(
    AnthropicService service,
    ContextBuilder builder,
    ScrivenerProject project,
    HashSet<string> selectedIds,
    HashSet<string> pinnedIds)
{
    var ctx = builder.Build(project, selectedIds, pinnedIds);
    service.SystemPrompt = ctx.Length > 0
        ? $"You are a writing assistant. The following manuscript excerpts are your context:\n\n{ctx}"
        : null;
}

static void AddBinderItems(
    IHasTreeNodes node,
    IEnumerable<BinderItem> items,
    HashSet<string> selectedIds,
    HashSet<string> pinnedIds)
{
    foreach (var item in items)
    {
        var marker = pinnedIds.Contains(item.Id) ? "[yellow]★[/] " :
                     selectedIds.Contains(item.Id) ? "[green]✓[/] " : "  ";
        var label = $"{marker}[grey]{Markup.Escape(item.Id[..Math.Min(8, item.Id.Length)])}[/] {Markup.Escape(item.Title)}";
        var child = node.AddNode(label);
        if (item.Children.Count > 0)
            AddBinderItems(child, item.Children, selectedIds, pinnedIds);
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
