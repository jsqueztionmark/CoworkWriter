namespace CoworkWriter.Core.Writing;

public static class WritingPrompts
{
    public static string Continue() =>
        "Continue the prose from where the selected scene ends. Match the style, tone, and voice of the existing text exactly.";

    public static string Edit(string instruction) =>
        $"Apply the following edit to the selected text:\n\n{instruction}\n\nReturn only the revised text without commentary.";

    public static string Brainstorm(string topic) =>
        $"Brainstorm a range of creative ideas for the following:\n\n{topic}";

    public static string Summarize() =>
        "Summarize the selected documents concisely, capturing the key events, character development, and themes.";

    public static string Critique() =>
        "Provide detailed feedback on the selected text. Cover: narrative structure and pacing, prose clarity and style, dialogue authenticity, characterization, and specific areas for improvement.";
}
