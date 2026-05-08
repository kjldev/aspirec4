namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Specifies which local JavaScript package manager CLI is used to run
/// <c>likec4 serve</c> when <see cref="ILikeC4VisualizationBuilder.WithLocalCli"/> is called.
/// </summary>
public enum LikeC4LocalCliRuntime
{
	/// <summary>
	/// Automatically detects the first available runtime on the system PATH,
	/// checking in order: npx → pnpm → yarn → bun.
	/// </summary>
	Auto,

	/// <summary>Uses <c>npx likec4 serve</c>.</summary>
	Npx,

	/// <summary>Uses <c>pnpm exec likec4 serve</c>.</summary>
	Pnpm,

	/// <summary>Uses <c>yarn dlx likec4 serve</c>.</summary>
	Yarn,

	/// <summary>Uses <c>bunx likec4 serve</c>.</summary>
	Bun,
}
