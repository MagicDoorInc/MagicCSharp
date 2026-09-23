using MagicCSharp.Cli.Commands;
using MagicCSharp.Cli.Infrastructure;

namespace MagicCSharp.Cli.Tests;

public class AiFilesTests
{
    [Fact]
    public void The_tool_carries_CLAUDE_md_the_index_and_the_project_file()
    {
        // The hidden .ai-knowledge folder is embedded file by file; this fails if the csproj stops finding it.
        var paths = AiFiles.Bundled("Acme").Select(aiFile => aiFile.Path).ToList();

        Assert.Contains("CLAUDE.md", paths);
        Assert.Contains(".ai-knowledge/INDEX.md", paths);
        Assert.Contains(AiFiles.ProjectFile, paths);
    }

    [Fact]
    public void Init_writes_the_guides_with_the_prefix_filled_in()
    {
        using var temporaryRepository = new TemporaryRepository();

        InitCommand.WriteRepository(temporaryRepository.Root, new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3" });

        var claudeMd = File.ReadAllText(Path.Combine(temporaryRepository.Root, "CLAUDE.md"));
        Assert.Contains("dotnet build Acme.All.slnx", claudeMd);
        Assert.DoesNotContain("{{ prefix }}", claudeMd);
        Assert.True(File.Exists(Path.Combine(temporaryRepository.Root, ".ai-knowledge", "INDEX.md")));
    }

    [Fact]
    public void No_ai_knowledge_leaves_the_guides_out()
    {
        using var temporaryRepository = new TemporaryRepository();

        InitCommand.WriteRepository(temporaryRepository.Root, new InitCommand.Settings { Prefix = "Acme", PackageVersion = "1.2.3", ShouldSkipAiKnowledge = true });

        Assert.False(File.Exists(Path.Combine(temporaryRepository.Root, "CLAUDE.md")));
        Assert.False(Directory.Exists(Path.Combine(temporaryRepository.Root, ".ai-knowledge")));
    }

    [Fact]
    public void Update_brings_an_edited_guide_back_to_the_shipped_version()
    {
        using var temporaryRepository = new TemporaryRepository();
        AiFiles.WriteMissing(temporaryRepository.Root, "Acme");
        var indexPath = Path.Combine(temporaryRepository.Root, ".ai-knowledge", "INDEX.md");
        var shippedIndex = File.ReadAllText(indexPath);
        File.WriteAllText(indexPath, "an older version");

        var changedCount = AiFiles.Update(temporaryRepository.Root, "Acme");

        Assert.Equal(1, changedCount);
        Assert.Equal(shippedIndex, File.ReadAllText(indexPath));
    }

    [Fact]
    public void Update_restores_a_deleted_guide()
    {
        using var temporaryRepository = new TemporaryRepository();
        AiFiles.WriteMissing(temporaryRepository.Root, "Acme");
        File.Delete(Path.Combine(temporaryRepository.Root, ".ai-knowledge", "coding-style.md"));

        AiFiles.Update(temporaryRepository.Root, "Acme");

        Assert.True(File.Exists(Path.Combine(temporaryRepository.Root, ".ai-knowledge", "coding-style.md")));
    }

    [Fact]
    public void Update_never_touches_the_repositorys_project_file()
    {
        using var temporaryRepository = new TemporaryRepository();
        AiFiles.WriteMissing(temporaryRepository.Root, "Acme");
        var projectPath = Path.Combine(temporaryRepository.Root, AiFiles.ProjectFile);
        File.WriteAllText(projectPath, "# Our services");

        var changedCount = AiFiles.Update(temporaryRepository.Root, "Acme");

        Assert.Equal(0, changedCount);
        Assert.Equal("# Our services", File.ReadAllText(projectPath));
    }

    [Fact]
    public void Update_never_touches_a_guide_the_repository_added()
    {
        using var temporaryRepository = new TemporaryRepository();
        AiFiles.WriteMissing(temporaryRepository.Root, "Acme");
        var ownGuidePath = Path.Combine(temporaryRepository.Root, ".ai-knowledge", "payments.md");
        File.WriteAllText(ownGuidePath, "# How we take payments");

        AiFiles.Update(temporaryRepository.Root, "Acme");

        Assert.Equal("# How we take payments", File.ReadAllText(ownGuidePath));
    }
}
