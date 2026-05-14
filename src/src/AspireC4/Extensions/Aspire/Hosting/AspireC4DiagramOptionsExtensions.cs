using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting;

/// <summary>
/// Fluent extension methods for configuring <see cref="AspireC4DiagramOptions"/>.
/// Allows chaining configuration in the <c>configure</c> callback of
/// <see cref="AspireC4DistributedApplicationBuilderExtensions.AddAspireC4"/>, e.g.:
/// <code>
/// builder.AddAspireC4(opts => opts
///     .WithTitle("My App")
///     .WithAutoIcons(false));
/// </code>
/// </summary>
public static class AspireC4DiagramOptionsExtensions
{
	/// <summary>Sets the LikeC4 view identifier emitted in the generated <c>.c4</c> file.</summary>
	/// <seealso cref="AspireC4DiagramOptions.GeneratedViewId"/>
	public static AspireC4DiagramOptions WithGeneratedViewId(this AspireC4DiagramOptions options, string? viewId)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.GeneratedViewId = viewId;
		return options;
	}

	/// <summary>Sets the LikeC4 view identifier used in the Aspire dashboard link URL.</summary>
	/// <seealso cref="AspireC4DiagramOptions.DefaultViewId"/>
	public static AspireC4DiagramOptions WithDefaultViewId(this AspireC4DiagramOptions options, string? viewId)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.DefaultViewId = viewId;
		return options;
	}

	/// <summary>Sets the title shown in the generated LikeC4 view.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ViewTitle"/>
	public static AspireC4DiagramOptions WithViewTitle(this AspireC4DiagramOptions options, string title)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		options.ViewTitle = title;
		return options;
	}

	/// <summary>Sets the description shown in the generated LikeC4 view.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ViewDescription"/>
	public static AspireC4DiagramOptions WithViewDescription(this AspireC4DiagramOptions options, string? description)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.ViewDescription = description;
		return options;
	}

	/// <summary>Sets the title shown in the generated LikeC4 hosting application.</summary>
	/// <seealso cref="AspireC4DiagramOptions.Title"/>
	public static AspireC4DiagramOptions WithTitle(this AspireC4DiagramOptions options, string? title)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (title is not null)
			ArgumentException.ThrowIfNullOrWhiteSpace(title);
		options.Title = title;
		return options;
	}

	/// <summary>Sets the output directory where the generated <c>.c4</c> file is written.</summary>
	/// <seealso cref="AspireC4DiagramOptions.OutputDirectory"/>
	public static AspireC4DiagramOptions WithOutputDirectory(
		this AspireC4DiagramOptions options,
		string outputDirectory
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
		options.OutputDirectory = outputDirectory;
		return options;
	}

	/// <summary>Sets the file name (without extension) for the generated <c>.c4</c> file.</summary>
	/// <seealso cref="AspireC4DiagramOptions.FileName"/>
	public static AspireC4DiagramOptions WithFileName(this AspireC4DiagramOptions options, string fileName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		options.FileName = fileName;
		return options;
	}

	/// <summary>Disables (or re-enables) the Hot Module Replacement channel.</summary>
	/// <seealso cref="AspireC4DiagramOptions.DisableHMR"/>
	public static AspireC4DiagramOptions WithHMRDisabled(this AspireC4DiagramOptions options, bool disabled = true)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.DisableHMR = disabled;
		return options;
	}

	/// <summary>Pins the <c>ghcr.io/likec4/likec4</c> container image to a specific tag.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ContainerImageTag"/>
	public static AspireC4DiagramOptions WithContainerImageTag(this AspireC4DiagramOptions options, string? tag)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.ContainerImageTag = tag;
		return options;
	}

	/// <summary>Enables or disables automatic icon inference for known resource types.</summary>
	/// <seealso cref="AspireC4DiagramOptions.AutoIconsEnabled"/>
	public static AspireC4DiagramOptions WithAutoIcons(this AspireC4DiagramOptions options, bool enabled = true)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.AutoIconsEnabled = enabled;
		return options;
	}

	/// <summary>
	/// Hides the LikeC4 server resource from the Aspire dashboard and surfaces the diagram
	/// URL as a link and command on every project resource row.
	/// </summary>
	/// <seealso cref="AspireC4DiagramOptions.HideFromDashboard"/>
	/// <seealso cref="AspireC4DiagramOptions.DashboardLinkDisplayName"/>
	public static AspireC4DiagramOptions WithHideFromDashboard(
		this AspireC4DiagramOptions options,
		string displayName = "Architecture Diagram"
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
		options.HideFromDashboard = true;
		options.DashboardLinkDisplayName = displayName;
		return options;
	}

	/// <summary>Sets the DSL syntax used to emit typed relationships.</summary>
	/// <seealso cref="AspireC4DiagramOptions.RelationshipKindSyntax"/>
	public static AspireC4DiagramOptions WithRelationshipKindSyntax(
		this AspireC4DiagramOptions options,
		LikeC4RelationshipKindSyntax syntax
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.RelationshipKindSyntax = syntax;
		return options;
	}

	/// <summary>Enables or disables automatic formatting of the generated <c>.c4</c> file.</summary>
	/// <seealso cref="AspireC4DiagramOptions.FormatGeneratedFile"/>
	public static AspireC4DiagramOptions WithFormatGeneratedFile(
		this AspireC4DiagramOptions options,
		bool format = true
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.FormatGeneratedFile = format;
		return options;
	}

	/// <summary>Enables or disables LikeC4 validation before startup.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ValidateBeforeStart"/>
	public static AspireC4DiagramOptions WithValidateBeforeStart(
		this AspireC4DiagramOptions options,
		bool validate = true
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.ValidateBeforeStart = validate;
		return options;
	}

	/// <summary>Adds a custom element kind specification to the <c>specification { }</c> block.</summary>
	/// <seealso cref="AspireC4DiagramOptions.ElementKindSpecs"/>
	public static AspireC4DiagramOptions WithElementKindSpec(
		this AspireC4DiagramOptions options,
		LikeC4ElementKindSpec spec
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(spec);
		options.ElementKindSpecs.Add(spec);
		return options;
	}

	/// <summary>Adds a custom relationship kind specification to the <c>specification { }</c> block.</summary>
	/// <seealso cref="AspireC4DiagramOptions.RelationshipKindSpecs"/>
	public static AspireC4DiagramOptions WithRelationshipKindSpec(
		this AspireC4DiagramOptions options,
		LikeC4RelationshipKindSpec spec
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(spec);
		options.RelationshipKindSpecs.Add(spec);
		return options;
	}

	/// <summary>Controls which Aspire runtime metadata is injected into generated LikeC4 elements.</summary>
	/// <seealso cref="AspireC4DiagramOptions.AutoIncludeAspireMetadata"/>
	public static AspireC4DiagramOptions WithAutoIncludeAspireMetadata(
		this AspireC4DiagramOptions options,
		AspireMetadataInclusion inclusion
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.AutoIncludeAspireMetadata = inclusion;
		return options;
	}

	/// <summary>Controls how invalid characters in LikeC4 metadata keys are handled.</summary>
	/// <seealso cref="AspireC4DiagramOptions.NormaliseMetadataBehaviour"/>
	public static AspireC4DiagramOptions WithNormaliseMetadataBehaviour(
		this AspireC4DiagramOptions options,
		NormaliseMetadataBehaviour behaviour
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.NormaliseMetadataBehaviour = behaviour;
		return options;
	}

	/// <summary>Disables automatic generation of <c>likec4.config.json</c> in the output directory.</summary>
	/// <seealso cref="AspireC4DiagramOptions.GenerateConfigFile"/>
	public static AspireC4DiagramOptions WithoutConfigFileGeneration(this AspireC4DiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.GenerateConfigFile = false;
		return options;
	}

	/// <summary>Enables or disables Aspire dashboard links on generated diagram elements.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IncludeAspireDashboardLinks"/>
	public static AspireC4DiagramOptions WithAspireDashboardLinks(
		this AspireC4DiagramOptions options,
		bool include = true
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.IncludeAspireDashboardLinks = include;
		return options;
	}

	/// <summary>Enables or disables embedding the Aspire browser token in dashboard links.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IncludeAspireTokenInDashboardLinks"/>
	public static AspireC4DiagramOptions WithAspireTokenInDashboardLinks(
		this AspireC4DiagramOptions options,
		bool include = true
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.IncludeAspireTokenInDashboardLinks = include;
		return options;
	}

	/// <summary>Enables or disables emitting default <c>aspire-run-state-*</c> style rules in the generated view.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IncludeDefaultStateStyles"/>
	public static AspireC4DiagramOptions WithDefaultStateStyles(
		this AspireC4DiagramOptions options,
		bool include = true
	)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.IncludeDefaultStateStyles = include;
		return options;
	}

	/// <summary>
	/// Overrides the tag applied to diagram elements for a specific Aspire resource state.
	/// The <paramref name="state"/> value should be one of the <see cref="KnownResourceStates"/> string constants.
	/// Set <paramref name="tag"/> to <see langword="null"/> to suppress tag assignment for that state.
	/// </summary>
	/// <seealso cref="AspireC4DiagramOptions.StateTagMap"/>
	public static AspireC4DiagramOptions WithStateTag(this AspireC4DiagramOptions options, string state, string? tag)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.StateTagMap[state] = tag;
		return options;
	}

	/// <summary>Adds a custom icon resolver evaluated before built-in icon inference.</summary>
	/// <seealso cref="AspireC4DiagramOptions.IconResolvers"/>
	public static AspireC4DiagramOptions WithIconResolver(this AspireC4DiagramOptions options, IconResolver resolver)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(resolver);
		options.IconResolvers.Add(resolver);
		return options;
	}

	/// <summary>
	/// Sets the strict validation mode. The provided <paramref name="mode"/> replaces the current mode.
	/// Use the bitwise OR operator to combine multiple flags:
	/// <c>AspireC4StrictMode.Tags | AspireC4StrictMode.Groups</c>.
	/// </summary>
	/// <seealso cref="AspireC4DiagramOptions.Strict"/>
	/// <seealso cref="AspireC4StrictMode"/>
	public static AspireC4DiagramOptions WithStrictMode(this AspireC4DiagramOptions options, AspireC4StrictMode mode)
	{
		ArgumentNullException.ThrowIfNull(options);
		options.Strict.Mode = mode;
		return options;
	}

	/// <summary>
	/// Adds a tag to the list of tags permitted under <see cref="AspireC4StrictMode.Tags"/>.
	/// A leading <c>#</c> is accepted and stripped automatically.
	/// </summary>
	/// <seealso cref="AspireC4StrictOptions.Tags"/>
	public static AspireC4DiagramOptions WithAllowedTag(this AspireC4DiagramOptions options, string tag)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(tag);
		options.Strict.Tags.Add(Helpers.NormaliseTag(tag));
		return options;
	}

	/// <summary>
	/// Adds a relationship kind identifier to the list of kinds permitted under
	/// <see cref="AspireC4StrictMode.RelationshipKinds"/>.
	/// </summary>
	/// <seealso cref="AspireC4StrictOptions.RelationshipKinds"/>
	public static AspireC4DiagramOptions WithAllowedRelationshipKind(this AspireC4DiagramOptions options, string kind)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(kind);
		options.Strict.RelationshipKinds.Add(kind);
		return options;
	}

	/// <summary>
	/// Adds a group name to the list of groups permitted under <see cref="AspireC4StrictMode.Groups"/>.
	/// </summary>
	/// <seealso cref="AspireC4StrictOptions.Groups"/>
	public static AspireC4DiagramOptions WithAllowedGroup(this AspireC4DiagramOptions options, string groupName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
		options.Strict.Groups.Add(groupName);
		return options;
	}

	/// <summary>
	/// Adds a metadata key to the list of keys permitted under <see cref="AspireC4StrictMode.MetadataKeys"/>.
	/// </summary>
	/// <seealso cref="AspireC4StrictOptions.MetadataKeys"/>
	public static AspireC4DiagramOptions WithAllowedMetadataKey(this AspireC4DiagramOptions options, string key)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		options.Strict.MetadataKeys.Add(key);
		return options;
	}
}
