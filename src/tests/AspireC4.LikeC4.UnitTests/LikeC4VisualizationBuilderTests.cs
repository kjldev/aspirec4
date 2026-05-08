using Aspire.Hosting;
using Aspire.Hosting.LikeC4;

namespace Aspire.Hosting.LikeC4;

/// <summary>
/// Unit tests for the local CLI command builder logic and the <see cref="LikeC4ServerResource"/> constants.
/// </summary>
public sealed class LikeC4VisualizationBuilderTests
{
    // --- BuildLocalCliCommand ---

    [Test]
    public async Task BuildLocalCliCommand_Npx_UsesNpxWithLikeC4Args()
    {
        var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
            LikeC4LocalCliRuntime.Npx, "/tmp/likec4", 5173);

        await Assert.That(command).IsEqualTo("npx");
        await Assert.That(args).IsEquivalentTo(new[] { "likec4", "serve", "/tmp/likec4", "--port", "5173" });
    }

    [Test]
    public async Task BuildLocalCliCommand_Pnpm_UsesPnpmExec()
    {
        var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
            LikeC4LocalCliRuntime.Pnpm, "/tmp/likec4", 5173);

        await Assert.That(command).IsEqualTo("pnpm");
        await Assert.That(args).IsEquivalentTo(new[] { "exec", "likec4", "serve", "/tmp/likec4", "--port", "5173" });
    }

    [Test]
    public async Task BuildLocalCliCommand_Yarn_UsesYarnDlx()
    {
        var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
            LikeC4LocalCliRuntime.Yarn, "/tmp/likec4", 5173);

        await Assert.That(command).IsEqualTo("yarn");
        await Assert.That(args).IsEquivalentTo(new[] { "dlx", "likec4", "serve", "/tmp/likec4", "--port", "5173" });
    }

    [Test]
    public async Task BuildLocalCliCommand_Bun_UsesBunx()
    {
        var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
            LikeC4LocalCliRuntime.Bun, "/tmp/likec4", 5173);

        await Assert.That(command).IsEqualTo("bunx");
        await Assert.That(args).IsEquivalentTo(new[] { "likec4", "serve", "/tmp/likec4", "--port", "5173" });
    }

    [Test]
    public async Task BuildLocalCliCommand_Auto_Throws()
    {
        // Auto is resolved before reaching BuildLocalCliCommand; passing it directly is an error.
        await Assert.That(() =>
            LikeC4VisualizationBuilder.BuildLocalCliCommand(LikeC4LocalCliRuntime.Auto, "/tmp", 5173))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task BuildLocalCliCommand_IncludesCorrectPort()
    {
        var (_, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
            LikeC4LocalCliRuntime.Npx, "/output", 9090);

        await Assert.That(args).Contains("9090");
    }

    // --- LikeC4ServerResource constants ---

    [Test]
    public async Task LikeC4ServerResource_HasExpectedDefaults()
    {
        await Assert.That(LikeC4ServerResource.DefaultRegistry).IsEqualTo("ghcr.io");
        await Assert.That(LikeC4ServerResource.DefaultImage).IsEqualTo("likec4/likec4");
        await Assert.That(LikeC4ServerResource.DefaultTag).IsEqualTo("latest");
        await Assert.That(LikeC4ServerResource.DefaultContainerPort).IsEqualTo(5173);
        await Assert.That(LikeC4ServerResource.WorkspacePath).IsEqualTo("/data");
    }

    // --- LikeC4LocalServerResource constants ---

    [Test]
    public async Task LikeC4LocalServerResource_HasExpectedDefaults()
    {
        await Assert.That(LikeC4LocalServerResource.DefaultPort).IsEqualTo(5173);
    }
}
