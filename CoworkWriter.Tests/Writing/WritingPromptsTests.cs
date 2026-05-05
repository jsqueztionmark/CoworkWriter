using CoworkWriter.Core.Writing;

namespace CoworkWriter.Tests.Writing;

public class WritingPromptsTests
{
    [Fact]
    public void Continue_ReturnsNonEmptyPrompt()
    {
        var prompt = WritingPrompts.Continue();
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void Edit_ContainsInstruction()
    {
        var prompt = WritingPrompts.Edit("make the dialogue snappier");
        Assert.Contains("make the dialogue snappier", prompt);
    }

    [Fact]
    public void Edit_EmptyInstruction_StillReturnsPrompt()
    {
        var prompt = WritingPrompts.Edit(string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void Brainstorm_ContainsTopic()
    {
        var prompt = WritingPrompts.Brainstorm("plot twists for chapter 5");
        Assert.Contains("plot twists for chapter 5", prompt);
    }

    [Fact]
    public void Summarize_ReturnsNonEmptyPrompt()
    {
        var prompt = WritingPrompts.Summarize();
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void Critique_ReturnsNonEmptyPrompt()
    {
        var prompt = WritingPrompts.Critique();
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Fact]
    public void AllPrompts_AreDistinct()
    {
        var prompts = new[]
        {
            WritingPrompts.Continue(),
            WritingPrompts.Edit("x"),
            WritingPrompts.Brainstorm("x"),
            WritingPrompts.Summarize(),
            WritingPrompts.Critique()
        };
        Assert.Equal(prompts.Length, prompts.Distinct().Count());
    }
}
