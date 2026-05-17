using System.ComponentModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Runtime bridge between the source-generator-produced module initializer and the
/// strict options configuration. Not intended for direct use by consumers.
/// </summary>
/// <remarks>
/// The AspireC4 source generator emits a <c>[ModuleInitializer]</c> in each app host that
/// carries a <c>[LikeC4Registry]</c> class. That initializer calls <see cref="Register"/>
/// to populate the allowed-value sets before <c>Main()</c> runs. <see cref="AddAspireC4"/>
/// then calls <see cref="Apply"/> in a <c>PostConfigure</c> to transfer those values into
/// <see cref="AspireC4StrictOptions"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LikeC4RegistryBridge
{
	static readonly List<Action<AspireC4StrictOptions>> s_configurators = [];

	/// <summary>
	/// Registers a strict-options configurator. Called exclusively by the source-generator-produced
	/// module initializer — do not call this method directly.
	/// </summary>
	public static void Register(Action<AspireC4StrictOptions> configurator) => s_configurators.Add(configurator);

	/// <summary>Applies all registered configurators to <paramref name="opts"/>.</summary>
	internal static void Apply(AspireC4StrictOptions opts)
	{
		foreach (var configurator in s_configurators)
			configurator(opts);
	}
}
