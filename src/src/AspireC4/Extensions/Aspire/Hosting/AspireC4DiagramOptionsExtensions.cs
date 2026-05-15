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
	extension(AspireC4DiagramOptions options)
	{
		/// <summary>Sets the LikeC4 view identifier emitted in the generated <c>.c4</c> file.</summary>
		/// <seealso cref="AspireC4DiagramOptions.GeneratedViewId"/>
		public AspireC4DiagramOptions WithGeneratedViewId(string? viewId)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.GeneratedViewId = viewId;
			return options;
		}

		/// <summary>Sets the LikeC4 view identifier used in the Aspire dashboard link URL.</summary>
		/// <seealso cref="AspireC4DiagramOptions.DefaultViewId"/>
		public AspireC4DiagramOptions WithDefaultViewId(string? viewId)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.DefaultViewId = viewId;
			return options;
		}

		/// <summary>Sets the title shown in the generated LikeC4 view.</summary>
		/// <seealso cref="AspireC4DiagramOptions.ViewTitle"/>
		public AspireC4DiagramOptions WithViewTitle(string title)
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrWhiteSpace(title);
			options.ViewTitle = title;
			return options;
		}

		/// <summary>Sets the description shown in the generated LikeC4 view.</summary>
		/// <seealso cref="AspireC4DiagramOptions.ViewDescription"/>
		public AspireC4DiagramOptions WithViewDescription(string? description)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.ViewDescription = description;
			return options;
		}

		/// <summary>Sets the title shown in the generated LikeC4 hosting application.</summary>
		/// <seealso cref="AspireC4DiagramOptions.Title"/>
		public AspireC4DiagramOptions WithTitle(string? title)
		{
			ArgumentNullException.ThrowIfNull(options);
			if (title is not null)
				ArgumentException.ThrowIfNullOrWhiteSpace(title);
			options.Title = title;
			return options;
		}

		/// <summary>Sets the output directory where the generated <c>.c4</c> file is written.</summary>
		/// <seealso cref="AspireC4DiagramOptions.OutputDirectory"/>
		public AspireC4DiagramOptions WithOutputDirectory(string outputDirectory)
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
			options.OutputDirectory = outputDirectory;
			return options;
		}

		/// <summary>Sets the file name (without extension) for the generated <c>.c4</c> file.</summary>
		/// <seealso cref="AspireC4DiagramOptions.FileName"/>
		public AspireC4DiagramOptions WithFileName(string fileName)
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
			options.FileName = fileName;
			return options;
		}

		/// <summary>Disables (or re-enables) the Hot Module Replacement channel.</summary>
		/// <seealso cref="AspireC4DiagramOptions.DisableHMR"/>
		public AspireC4DiagramOptions WithHMRDisabled(bool disabled = true)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.DisableHMR = disabled;
			return options;
		}

		/// <summary>Pins the <c>ghcr.io/likec4/likec4</c> container image to a specific tag.</summary>
		/// <seealso cref="AspireC4DiagramOptions.ContainerImageTag"/>
		public AspireC4DiagramOptions WithContainerImageTag(string? tag)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.ContainerImageTag = tag;
			return options;
		}

		/// <summary>Enables or disables automatic icon inference for known resource types.</summary>
		/// <seealso cref="AspireC4DiagramOptions.AutoIconsEnabled"/>
		public AspireC4DiagramOptions WithAutoIcons(bool enabled = true)
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
		public AspireC4DiagramOptions WithHideFromDashboard(string displayName = "Architecture Diagram")
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
			options.HideFromDashboard = true;
			options.DashboardLinkDisplayName = displayName;
			return options;
		}

		/// <summary>Sets the DSL syntax used to emit typed relationships.</summary>
		/// <seealso cref="AspireC4DiagramOptions.RelationshipKindSyntax"/>
		public AspireC4DiagramOptions WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax syntax)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.RelationshipKindSyntax = syntax;
			return options;
		}

		/// <summary>Enables or disables automatic formatting of the generated <c>.c4</c> file.</summary>
		/// <seealso cref="AspireC4DiagramOptions.FormatGeneratedFile"/>
		public AspireC4DiagramOptions WithFormatGeneratedFile(bool format = true)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.FormatGeneratedFile = format;
			return options;
		}

		/// <summary>Enables or disables LikeC4 validation before startup.</summary>
		/// <seealso cref="AspireC4DiagramOptions.ValidateBeforeStart"/>
		public AspireC4DiagramOptions WithValidateBeforeStart(bool validate = true)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.ValidateBeforeStart = validate;
			return options;
		}

		/// <summary>Adds a custom element kind specification to the <c>specification { }</c> block.</summary>
		/// <seealso cref="AspireC4DiagramOptions.ElementKindSpecs"/>
		public AspireC4DiagramOptions WithElementKindSpec(LikeC4ElementKindSpec spec)
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentNullException.ThrowIfNull(spec);
			options.ElementKindSpecs.Add(spec);
			return options;
		}

		/// <summary>
		/// Adds a custom relationship kind specification to the <c>specification { }</c> block.
		/// </summary>
		/// <param name="spec">The relationship kind specification to add.</param>
		/// <param name="strict">
		/// Controls whether this kind is registered in the strict allowed-kinds list.
		/// <list type="bullet">
		///   <item><description><see langword="null"/> (default) — auto: adds to the allowed list if <see cref="AspireC4StrictMode.RelationshipKinds"/> is already enabled on the current options.</description></item>
		///   <item><description><see langword="true"/> — always add to the allowed list.</description></item>
		///   <item><description><see langword="false"/> — never add to the allowed list.</description></item>
		/// </list>
		/// </param>
		/// <seealso cref="AspireC4DiagramOptions.RelationshipKindSpecs"/>
		public AspireC4DiagramOptions WithRelationshipKindSpec(LikeC4RelationshipKindSpec spec, bool? strict = null)
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentNullException.ThrowIfNull(spec);
			options.RelationshipKindSpecs.Add(spec);
			if (strict ?? options.Strict.Mode.HasFlag(AspireC4StrictMode.RelationshipKinds))
				options.WithAllowedRelationshipKind(spec.Name);

			return options;
		}

		/// <summary>
		/// Adds a custom relationship kind identifier to the <c>specification { }</c> block.
		/// </summary>
		/// <param name="name">The kind identifier, e.g. <c>"async"</c> or <c>"grpc"</c>.</param>
		/// <param name="technology">Optional default technology label for all relationships of this kind (e.g. <c>"AMQP"</c>, <c>"gRPC"</c>).</param>
		/// <param name="strict">
		/// Controls whether this kind is registered in the strict allowed-kinds list.
		/// <list type="bullet">
		///   <item><description><see langword="null"/> (default) — auto: adds to the allowed list if <see cref="AspireC4StrictMode.RelationshipKinds"/> is already enabled on the current options.</description></item>
		///   <item><description><see langword="true"/> — always add to the allowed list.</description></item>
		///   <item><description><see langword="false"/> — never add to the allowed list.</description></item>
		/// </list>
		/// </param>
		/// <returns>The same <see cref="AspireC4DiagramOptions"/> for further configuration.</returns>
		public AspireC4DiagramOptions WithRelationshipKindSpec(
			string name,
			string? technology = null,
			bool? strict = null
		)
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrWhiteSpace(name);
			options.RelationshipKindSpecs.Add(new LikeC4RelationshipKindSpec(name, technology));
			if (strict ?? options.Strict.Mode.HasFlag(AspireC4StrictMode.RelationshipKinds))
				options.WithAllowedRelationshipKind(name);

			return options;
		}

		/// <summary>Controls which Aspire runtime metadata is injected into generated LikeC4 elements.</summary>
		/// <seealso cref="AspireC4DiagramOptions.AutoIncludeAspireMetadata"/>
		public AspireC4DiagramOptions WithAutoIncludeAspireMetadata(AspireMetadataInclusion inclusion)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.AutoIncludeAspireMetadata = inclusion;
			return options;
		}

		/// <summary>Controls how invalid characters in LikeC4 metadata keys are handled.</summary>
		/// <seealso cref="AspireC4DiagramOptions.NormaliseMetadataBehaviour"/>
		public AspireC4DiagramOptions WithNormaliseMetadataBehaviour(NormaliseMetadataBehaviour behaviour)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.NormaliseMetadataBehaviour = behaviour;
			return options;
		}

		/// <summary>Disables automatic generation of <c>likec4.config.json</c> in the output directory.</summary>
		/// <seealso cref="AspireC4DiagramOptions.GenerateConfigFile"/>
		public AspireC4DiagramOptions WithoutConfigFileGeneration()
		{
			ArgumentNullException.ThrowIfNull(options);
			options.GenerateConfigFile = false;
			return options;
		}

		/// <summary>Enables or disables Aspire dashboard links on generated diagram elements.</summary>
		/// <seealso cref="AspireC4DiagramOptions.IncludeAspireDashboardLinks"/>
		public AspireC4DiagramOptions WithAspireDashboardLinks(bool include = true)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.IncludeAspireDashboardLinks = include;
			return options;
		}

		/// <summary>Enables or disables embedding the Aspire browser token in dashboard links.</summary>
		/// <seealso cref="AspireC4DiagramOptions.IncludeAspireTokenInDashboardLinks"/>
		public AspireC4DiagramOptions WithAspireTokenInDashboardLinks(bool include = true)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.IncludeAspireTokenInDashboardLinks = include;
			return options;
		}

		/// <summary>Enables or disables emitting default <c>aspire-run-state-*</c> style rules in the generated view.</summary>
		/// <seealso cref="AspireC4DiagramOptions.IncludeDefaultStateStyles"/>
		public AspireC4DiagramOptions WithDefaultStateStyles(bool include = true)
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
		public AspireC4DiagramOptions WithStateTag(string state, string? tag)
		{
			ArgumentNullException.ThrowIfNull(options);
			options.StateTagMap[state] = tag;
			return options;
		}

		/// <summary>Adds a custom icon resolver evaluated before built-in icon inference.</summary>
		/// <seealso cref="AspireC4DiagramOptions.IconResolvers"/>
		public AspireC4DiagramOptions WithIconResolver(IconResolver resolver)
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
		public AspireC4DiagramOptions WithStrictMode(AspireC4StrictMode mode)
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
		public AspireC4DiagramOptions WithAllowedTag(string tag)
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
		public AspireC4DiagramOptions WithAllowedRelationshipKind(string kind)
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
		public AspireC4DiagramOptions WithAllowedGroup(string groupName)
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
		public AspireC4DiagramOptions WithAllowedMetadataKey(string key)
		{
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrWhiteSpace(key);
			options.Strict.MetadataKeys.Add(key);
			return options;
		}
	}
}
