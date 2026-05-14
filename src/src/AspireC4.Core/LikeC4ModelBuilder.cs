using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Builds a <see cref="LikeC4Model"/> from the Aspire application model resources.
/// </summary>
public static class LikeC4ModelBuilder
{
	// Aspire's well-known relationship type for wait-for dependencies — these are infrastructure
	// concerns and should not appear as architecture relationships in the diagram.
	const string WaitForRelationshipType = "WaitFor";

	/// <summary>Builds a <see cref="LikeC4Model"/> from a list of Aspire resources.</summary>
	/// <param name="resources">All resources in the Aspire app model.</param>
	/// <param name="resourceStates">
	/// Optional mapping of resource name → current <see cref="LikeC4ResourceState"/>.
	/// When provided, the corresponding diagram element is coloured to reflect the live state.
	/// </param>
	/// <param name="autoIconsEnabled">When <see langword="true"/>, infers icons from resource type/name.</param>
	/// <param name="aspireMetadataInclusion">Controls which Aspire runtime metadata is injected into elements.</param>
	/// <param name="normaliseMetadataBehaviour">Controls how invalid characters in metadata keys are handled.</param>
	/// <param name="iconResolvers">
	/// Optional list of custom icon resolvers evaluated before built-in auto-icon inference.
	/// Each resolver receives a <see cref="LikeC4IconResolverContext"/> and should return a non-null icon string
	/// to override the default, or <see langword="null"/> to defer to the next resolver or built-in inference.
	/// </param>
	/// <param name="includeDashboardLinks">
	/// When <see langword="true"/> and <paramref name="dashboardBaseUrl"/> is provided, adds links from each element
	/// to the Aspire dashboard console logs and structured logs pages for that resource.
	/// </param>
	/// <param name="dashboardBaseUrl">
	/// Base URL of the Aspire dashboard (e.g. <c>https://localhost:15086</c>). When provided and
	/// <paramref name="includeDashboardLinks"/> is <see langword="true"/>, dashboard deep-links are injected
	/// into each element's <see cref="LikeC4Element.Links"/> collection.
	/// </param>
	/// <param name="dashboardBrowserToken">
	/// The Aspire dashboard browser token (from <c>AppHost:BrowserToken</c> configuration). When provided,
	/// dashboard links use a <c>/login?t=…&amp;returnUrl=…</c> redirect URL so clicking the link in LikeC4
	/// authenticates the browser before navigating to the resource page.
	/// </param>
	/// <param name="resourceSnapshotUrls">
	/// Optional mapping of resource name to externally-accessible endpoint URLs captured from Aspire resource
	/// snapshots. When present for a resource, these URLs are used for endpoint links in preference to reading
	/// <see cref="Aspire.Hosting.ApplicationModel.EndpointAnnotation.AllocatedEndpoint"/> directly, ensuring
	/// the correct public port is used (the same source the Aspire dashboard uses).
	/// </param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1054:URI-like parameters should not be strings",
		Justification = "Dashboard base URL is composed via string concatenation consistent with LikeC4Link.Uri; wrapping as System.Uri would add unnecessary conversion overhead"
	)]
	public static LikeC4Model Build(
		IReadOnlyList<IResource> resources,
		IReadOnlyDictionary<string, LikeC4ResourceState>? resourceStates = null,
		bool autoIconsEnabled = true,
		AspireMetadataInclusion aspireMetadataInclusion = AspireMetadataInclusion.All,
		NormaliseMetadataBehaviour normaliseMetadataBehaviour = NormaliseMetadataBehaviour.Normalise,
		IReadOnlyList<LikeC4IconResolver>? iconResolvers = null,
		bool includeDashboardLinks = true,
		string? dashboardBaseUrl = null,
		string? dashboardBrowserToken = null,
		IReadOnlyDictionary<LikeC4ResourceState, string?>? stateTagMap = null,
		IReadOnlyDictionary<string, IReadOnlyList<(string Url, string Name)>>? resourceSnapshotUrls = null,
		AspireC4StrictOptions? strict = null
	)
	{
		ArgumentNullException.ThrowIfNull(resources);

		var visibleResources = BuildVisibleSet(resources);

		// Build a name → resource lookup so we can resolve "surrogate" resources.
		// When an Azure resource (e.g. AzureRedisCacheResource) is replaced by a local
		// container via RunAsContainer(), the original Azure resource is hidden and a
		// ContainerResource with the same name becomes the visible counterpart.
		// WithReference() still annotates with the hidden Azure resource, so we need
		// this lookup to resolve the visible surrogate by name.
		Dictionary<string, IResource> visibleByName = visibleResources
			.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

		// Hidden resources (e.g. AzurePostgresFlexibleServerResource) carry richer type info
		// than the visible surrogate (e.g. a generic ContainerResource). We pass the hidden
		// original to the element builder so the icon matcher can use its type name to select
		// the correct azure icon (e.g. azure:azure-database-postgre-sql-server).
		Dictionary<string, IResource> hiddenByName = resources
			.Where(r => !visibleResources.Contains(r))
			.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

#pragma warning disable IDE0028 // Simplify collection initialization
		List<LikeC4Element> elements = new(visibleResources.Count);
#pragma warning restore IDE0028 // Simplify collection initialization
		List<LikeC4Relationship> relationships = [];
		HashSet<(string Source, string Target)> visitedRelationships = [];

		foreach (var resource in visibleResources)
		{
			var state =
				resourceStates is not null && resourceStates.TryGetValue(resource.Name, out var s)
					? s
					: LikeC4ResourceState.Unknown;

			elements.Add(
				BuildElement(
					resource,
					state,
					autoIconsEnabled,
					aspireMetadataInclusion,
					normaliseMetadataBehaviour,
					hiddenByName.GetValueOrDefault(resource.Name),
					iconResolvers,
					includeDashboardLinks,
					dashboardBaseUrl,
					dashboardBrowserToken,
					stateTagMap,
					resourceSnapshotUrls?.GetValueOrDefault(resource.Name),
					strict
				)
			);
			CollectRelationships(
				resource,
				visibleResources,
				visibleByName,
				relationships,
				visitedRelationships,
				normaliseMetadataBehaviour,
				strict
			);
		}

		return new LikeC4Model { Elements = elements, Relationships = relationships };
	}

	/// <summary>
	/// Returns the names of all resources that would be included in the diagram
	/// (i.e. not excluded or hidden). Useful for filtering state-change notifications
	/// to only relevant resources.
	/// </summary>
	public static IReadOnlySet<string> GetVisibleResourceNames(IReadOnlyList<IResource> resources)
	{
		ArgumentNullException.ThrowIfNull(resources);

		return BuildVisibleSet(resources).Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	static HashSet<IResource> BuildVisibleSet(IReadOnlyList<IResource> resources)
	{
#pragma warning disable IDE0028 // Simplify collection initialization
		HashSet<IResource> visible = new(resources.Count);
#pragma warning restore IDE0028 // Simplify collection initialization

		foreach (var resource in resources)
		{
			if (ShouldExclude(resource))
			{
				continue;
			}

			visible.Add(resource);
		}

		return visible;
	}

	static bool ShouldExclude(IResource resource)
	{
		// Explicitly excluded by the user.
		if (resource.Annotations.OfType<ExcludeFromLikeC4Annotation>().Any())
		{
			return true;
		}

		// Hidden resources (e.g. internal Aspire infrastructure) should not appear.
		var snapshot = resource.Annotations.OfType<ResourceSnapshotAnnotation>().FirstOrDefault();
		return snapshot?.InitialSnapshot.IsHidden == true;
	}

	static LikeC4Element BuildElement(
		IResource resource,
		LikeC4ResourceState state,
		bool autoIconsEnabled,
		AspireMetadataInclusion aspireMetadataInclusion = AspireMetadataInclusion.All,
		NormaliseMetadataBehaviour normaliseMetadataBehaviour = NormaliseMetadataBehaviour.Normalise,
		IResource? hiddenOriginal = null,
		IReadOnlyList<LikeC4IconResolver>? iconResolvers = null,
		bool includeDashboardLinks = true,
		string? dashboardBaseUrl = null,
		string? dashboardBrowserToken = null,
		IReadOnlyDictionary<LikeC4ResourceState, string?>? stateTagMap = null,
		IReadOnlyList<(string Url, string Name)>? snapshotEndpointUrls = null,
		AspireC4StrictOptions? strict = null
	)
	{
		var details = resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().LastOrDefault();
		var inferredTechnology = InferTechnology(resource);

		var label = details?.Label ?? resource.Name;
		var technology = details?.Technology ?? inferredTechnology;
		var icon = ResolveIcon(resource, details, inferredTechnology, autoIconsEnabled, hiddenOriginal, iconResolvers);
		var kind = details?.Kind ?? InferKind(resource);
		var parentName = (resource as IResourceWithParent)?.Parent?.Name;
		var group = resource.Annotations.OfType<LikeC4GroupAnnotation>().LastOrDefault()?.GroupName;

		var userMetadata = NormaliseMetadataKeys(details?.Metadata ?? [], normaliseMetadataBehaviour);
		var userLinks = details?.Links ?? [];
		var (autoMetadata, autoLinks) = BuildAspireData(
			resource,
			aspireMetadataInclusion,
			userMetadata,
			userLinks,
			includeDashboardLinks,
			dashboardBaseUrl,
			dashboardBrowserToken,
			snapshotEndpointUrls
		);

		// Prepend the state tag (when configured) to the element's user-defined tags.
		IReadOnlyList<string> stateTags = [];
		if (stateTagMap is not null && stateTagMap.TryGetValue(state, out var stateTag) && stateTag is not null)
			stateTags = [LikeC4TagHelper.Normalize(stateTag)];

		var description = details?.Description;
		var summary = details?.Summary;

		// Strict-mode validation — runs after normalization so comparisons use the effective values.
		if (strict is not null && strict.Mode != AspireC4StrictMode.None)
		{
			var userTags = details?.Tags ?? [];
			EnforceStrictTags(strict, userTags, resource.Name);
			EnforceStrictGroup(strict, group, resource.Name);
			EnforceStrictMetadataKeys(strict, userMetadata, resource.Name, isRelationship: false);
		}

		return new()
		{
			Name = resource.Name,
			Label = label,
			Kind = kind,
			Technology = technology,
			Description = description,
			Summary = summary,
			Icon = icon,
			ParentName = parentName,
			State = state,
			Tags = [.. stateTags, .. (details?.Tags ?? [])],
			Links = [.. userLinks, .. autoLinks],
			Metadata = [.. userMetadata, .. autoMetadata],
			Group = group,
		};
	}

	/// <summary>
	/// Collects Aspire-derived metadata entries and links that are not already present
	/// in the user-provided sets. User-provided values always take precedence.
	/// </summary>
	static (IReadOnlyList<LikeC4Metadata> Metadata, IReadOnlyList<LikeC4Link> Links) BuildAspireData(
		IResource resource,
		AspireMetadataInclusion inclusion,
		IReadOnlyList<LikeC4Metadata> existingMetadata,
		IReadOnlyList<LikeC4Link> existingLinks,
		bool includeDashboardLinks = true,
		string? dashboardBaseUrl = null,
		string? dashboardBrowserToken = null,
		IReadOnlyList<(string Url, string Name)>? snapshotEndpointUrls = null
	)
	{
		List<LikeC4Metadata> metadata = [];
		List<LikeC4Link> links = [];

		if (inclusion.HasFlag(AspireMetadataInclusion.Metadata))
		{
			HashSet<string> existingKeys = existingMetadata
				.Select(m => m.Key)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			if (!existingKeys.Contains("aspire-name"))
			{
				metadata.Add(new LikeC4Metadata("aspire-name", resource.Name));
			}

			var resourceType = resource
				.Annotations.OfType<ResourceSnapshotAnnotation>()
				.LastOrDefault()
				?.InitialSnapshot.ResourceType;

			if (!string.IsNullOrEmpty(resourceType) && !existingKeys.Contains("aspire-type"))
			{
				metadata.Add(new LikeC4Metadata("aspire-type", resourceType));
			}
		}

		if (inclusion.HasFlag(AspireMetadataInclusion.Links))
		{
			HashSet<string> existingUris = existingLinks.Select(l => l.Uri).ToHashSet(StringComparer.OrdinalIgnoreCase);

			// Prefer snapshot URLs (same source as Aspire dashboard, correct public port).
			// Fall back to EndpointAnnotation when no snapshot data is available yet
			// (e.g. initial generation before resources start, or in unit tests).
			if (snapshotEndpointUrls is not null)
			{
				foreach (var (url, name) in snapshotEndpointUrls)
				{
					if (existingUris.Add(url))
					{
						links.Add(new LikeC4Link(url, $"Endpoint: {name}"));
					}
				}
			}
			else
			{
				foreach (var endpoint in resource.Annotations.OfType<EndpointAnnotation>())
				{
					if (endpoint.UriScheme is not "http" and not "https")
					{
						continue;
					}

					if (endpoint.AllocatedEndpoint is not { } ep || ep.Port <= 0)
					{
						continue;
					}

					var host = ep.Address is "0.0.0.0" ? "localhost" : ep.Address;
					var uri = $"{endpoint.UriScheme}://{host}:{ep.Port}";

					if (existingUris.Add(uri))
					{
						links.Add(new LikeC4Link(uri, $"Endpoint: {endpoint.Name}"));
					}
				}
			}

			if (includeDashboardLinks && dashboardBaseUrl is not null)
			{
				var consolePath = $"/consolelogs/resource/{Uri.EscapeDataString(resource.Name)}";
				var structuredPath = $"/structuredlogs/resource/{Uri.EscapeDataString(resource.Name)}";

				var consoleUrl = BuildDashboardLink(dashboardBaseUrl, consolePath, dashboardBrowserToken);
				var structuredUrl = BuildDashboardLink(dashboardBaseUrl, structuredPath, dashboardBrowserToken);

				if (existingUris.Add(consoleUrl))
				{
					links.Add(new LikeC4Link(consoleUrl, "Dashboard: Console Logs"));
				}

				if (existingUris.Add(structuredUrl))
				{
					links.Add(new LikeC4Link(structuredUrl, "Dashboard: Structured Logs"));
				}
			}
		}

		return (metadata, links);
	}

	static string BuildDashboardLink(string baseUrl, string path, string? browserToken)
	{
		if (browserToken is null)
			return $"{baseUrl}{path}";

		var encodedToken = Uri.EscapeDataString(browserToken);
		return $"{baseUrl}/login?t={encodedToken}&returnUrl={Uri.EscapeDataString(path)}";
	}

	/// <summary>
	/// Normalises a single metadata key according to the specified behaviour.
	/// Valid key characters are letters, digits, hyphens (<c>-</c>), and underscores (<c>_</c>).
	/// </summary>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="behaviour"/> is <see cref="NormaliseMetadataBehaviour.Throw"/>
	/// and <paramref name="key"/> contains invalid characters.
	/// </exception>
	static string NormaliseMetadataKey(string key, NormaliseMetadataBehaviour behaviour)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (behaviour == NormaliseMetadataBehaviour.Throw)
		{
			return !IsValidMetadataKey(key)
				? throw new ArgumentException(
					$"Metadata key '{key}' contains invalid characters. "
						+ "Valid characters are letters, digits, hyphens (-), and underscores (_).",
					nameof(key)
				)
				: key;
		}

		var normalised = string.Create(
			key.Length,
			key,
			static (span, src) =>
			{
				for (var i = 0; i < src.Length; i++)
				{
					span[i] = IsValidMetadataKeyChar(src[i]) ? src[i] : '_';
				}
			}
		);

		return behaviour == NormaliseMetadataBehaviour.NormaliseLowercase
#pragma warning disable CA1308 // ToLowerInvariant is intentional: metadata keys are normalised to lowercase for readability
			? normalised.ToLowerInvariant()
#pragma warning restore CA1308
			: normalised;
	}

	/// <summary>
	/// Returns a new dictionary with all keys normalised according to <paramref name="behaviour"/>.
	/// When two keys normalise to the same value, the first occurrence is kept.
	/// </summary>
	/// <summary>
	/// Returns a new list with all keys normalised according to <paramref name="behaviour"/>.
	/// When two keys normalise to the same value, the first occurrence is kept.
	/// </summary>
	static IReadOnlyList<LikeC4Metadata> NormaliseMetadataKeys(
		IReadOnlyList<LikeC4Metadata> metadata,
		NormaliseMetadataBehaviour behaviour
	)
	{
		if (metadata.Count == 0)
		{
			return metadata;
		}

		List<LikeC4Metadata> results = [];
		foreach (var (key, value) in metadata)
		{
			var normalised = NormaliseMetadataKey(key, behaviour);
			results.Add(new LikeC4Metadata(normalised, value));
		}

		return results;
	}

	static bool IsValidMetadataKey(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}

		foreach (var c in key)
		{
			if (!IsValidMetadataKeyChar(c))
			{
				return false;
			}
		}

		return true;
	}

	static bool IsValidMetadataKeyChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

	static string InferKind(IResource resource) =>
		resource switch
		{
			ProjectResource => LikeC4ElementKind.Component,
			ContainerResource => LikeC4ElementKind.Container,
			// IResourceWithConnectionString before ExecutableResource to catch DB-like resources
			IResourceWithConnectionString => LikeC4ElementKind.Database,
			ExecutableResource => LikeC4ElementKind.Executable,
			_ => LikeC4ElementKind.System,
		};

	static string? InferTechnology(IResource resource) =>
		resource switch
		{
			ProjectResource => ".NET",
			ContainerResource container => container
				.Annotations.OfType<ContainerImageAnnotation>()
				.LastOrDefault()
				?.Image,
			_ => null,
		};

	static string? ResolveIcon(
		IResource resource,
		LikeC4NodeDetailsAnnotation? details,
		string? inferredTechnology,
		bool autoIconsEnabled,
		IResource? hiddenOriginal = null,
		IReadOnlyList<LikeC4IconResolver>? iconResolvers = null
	)
	{
		if (!string.IsNullOrWhiteSpace(details?.Icon))
		{
			return details.Icon;
		}

		// Custom resolvers run before auto-inference; first non-null result wins.
		if (iconResolvers is { Count: > 0 })
		{
			LikeC4IconResolverContext ctx = new() { Resource = resource, HiddenOriginal = hiddenOriginal };
			foreach (var resolver in iconResolvers)
			{
				var resolved = resolver(ctx);
				if (resolved is not null)
				{
					return resolved;
				}
			}
		}

		return !(details?.AutoIconEnabled ?? autoIconsEnabled)
			? null
			: LikeC4IconMatcher.TryInferIcon([
				details?.Technology,
				inferredTechnology,
				// The hidden original (e.g. AzurePostgresFlexibleServerResource) carries richer
				// type information than the visible surrogate (e.g. a generic ContainerResource).
				// Short name first — it has fewer noise tokens than the fully-qualified name.
				hiddenOriginal?.GetType().Name,
				hiddenOriginal?.GetType().FullName,
				resource.Annotations.OfType<ResourceSnapshotAnnotation>().LastOrDefault()?.InitialSnapshot.ResourceType,
				resource.GetType().FullName,
				resource.GetType().Name,
				resource.Name,
			]);
	}

	static void CollectRelationships(
		IResource resource,
		HashSet<IResource> visibleResources,
		Dictionary<string, IResource> visibleByName,
		List<LikeC4Relationship> relationships,
		HashSet<(string, string)> visited,
		NormaliseMetadataBehaviour normaliseMetadataBehaviour,
		AspireC4StrictOptions? strict = null
	)
	{
		foreach (var annotation in resource.Annotations.OfType<ResourceRelationshipAnnotation>())
		{
			// Skip infrastructure-only wait-for dependencies.
			if (annotation.Type == WaitForRelationshipType)
			{
				continue;
			}

			// Resolve the effective target: use the annotated resource directly if it is
			// visible, otherwise look for a visible surrogate with the same name.
			// The surrogate pattern arises with Azure resources run via RunAsContainer():
			// the original Azure resource is hidden and replaced by a ContainerResource
			// with the same name, but WithReference() still annotates with the Azure resource.
			var effectiveTarget = visibleResources.Contains(annotation.Resource)
				? annotation.Resource
				: visibleByName.GetValueOrDefault(annotation.Resource.Name);

			if (effectiveTarget is null)
			{
				continue;
			}

			var key = (resource.Name, effectiveTarget.Name);
			if (!visited.Add(key))
			{
				continue;
			}

			// Look for a LikeC4-specific override. Keyed by target name; also checks the resolved
			// effective target for the Azure RunAsContainer() surrogate pattern.
			var details = resource
				.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>()
				.LastOrDefault(a =>
					string.Equals(a.TargetName, annotation.Resource.Name, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(a.TargetName, effectiveTarget.Name, StringComparison.OrdinalIgnoreCase)
				);

			// Fall back to the Aspire relationship type as label when not a generic 'Reference'
			// or infrastructure 'WaitFor' annotation and no explicit label was provided.
			var inferredLabel = annotation.Type is not ("Reference" or WaitForRelationshipType)
				? annotation.Type
				: null;

			var relationshipMetadata = NormaliseMetadataKeys(details?.Metadata ?? [], normaliseMetadataBehaviour);
			var relationshipTags = details?.Tags ?? [];
			var relationshipKind = details?.Kind;

			// Strict-mode validation for Aspire-backed relationships.
			if (strict is not null && strict.Mode != AspireC4StrictMode.None)
			{
				var label = $"{resource.Name} -> {effectiveTarget.Name}";
				EnforceStrictRelationshipKind(strict, relationshipKind, label);
				EnforceStrictTags(strict, relationshipTags, label);
				EnforceStrictMetadataKeys(strict, relationshipMetadata, label, isRelationship: true);
			}

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = resource.Name,
					TargetName = effectiveTarget.Name,
					Label = details?.Label ?? inferredLabel,
					Technology = details?.Technology,
					Description = details?.Description,
					Kind = relationshipKind,
					NavigateTo = details?.NavigateTo,
					Tags = relationshipTags,
					Links = details?.Links ?? [],
					Metadata = relationshipMetadata,
				}
			);
		}

		// Second pass: emit diagram-only relationships declared with WithLikeC4Reference that are
		// not backed by a ResourceRelationshipAnnotation (i.e. no WithReference was called).
		// The visited set from the first pass ensures we never duplicate a relationship.
		foreach (var details in resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>())
		{
			var effectiveTarget = visibleByName.GetValueOrDefault(details.TargetName);
			if (effectiveTarget is null)
			{
				continue;
			}

			var key = (resource.Name, effectiveTarget.Name);
			if (!visited.Add(key))
			{
				continue;
			}

			var diagramOnlyMetadata = NormaliseMetadataKeys(details.Metadata, normaliseMetadataBehaviour);

			// Strict-mode validation for diagram-only relationships.
			if (strict is not null && strict.Mode != AspireC4StrictMode.None)
			{
				var label = $"{resource.Name} -> {effectiveTarget.Name}";
				EnforceStrictRelationshipKind(strict, details.Kind, label);
				EnforceStrictTags(strict, details.Tags, label);
				EnforceStrictMetadataKeys(strict, diagramOnlyMetadata, label, isRelationship: true);
			}

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = resource.Name,
					TargetName = effectiveTarget.Name,
					Label = details.Label,
					Technology = details.Technology,
					Description = details.Description,
					Kind = details.Kind,
					NavigateTo = details.NavigateTo,
					Tags = details.Tags,
					Links = details.Links,
					Metadata = diagramOnlyMetadata,
				}
			);
		}
	}

	// ── Strict-mode enforcement helpers ──────────────────────────────────────

	static void EnforceStrictTags(AspireC4StrictOptions strict, IReadOnlyList<string> tags, string contextLabel)
	{
		if (!strict.Mode.HasFlag(AspireC4StrictMode.Tags) || tags.Count == 0)
			return;

		HashSet<string> allowed = new(strict.Tags, StringComparer.OrdinalIgnoreCase);
		foreach (var tag in tags)
		{
			if (!allowed.Contains(tag))
			{
				throw new InvalidOperationException(
					$"Tag '{tag}' on '{contextLabel}' is not in the allowed tags list. "
						+ $"Add it via {nameof(AspireC4DiagramOptionsExtensions.WithAllowedTag)}(\"{tag}\") "
						+ $"or add it to the '{AspireC4DiagramOptions.SectionName}:Strict:Tags' configuration."
				);
			}
		}
	}

	static void EnforceStrictRelationshipKind(AspireC4StrictOptions strict, string? kind, string contextLabel)
	{
		if (!strict.Mode.HasFlag(AspireC4StrictMode.RelationshipKinds) || kind is null)
			return;

		HashSet<string> allowed = new(strict.RelationshipKinds, StringComparer.OrdinalIgnoreCase);
		if (!allowed.Contains(kind))
		{
			throw new InvalidOperationException(
				$"Relationship kind '{kind}' on '{contextLabel}' is not in the allowed relationship kinds list. "
					+ $"Add it via {nameof(AspireC4DiagramOptionsExtensions.WithAllowedRelationshipKind)}(\"{kind}\") "
					+ $"or add it to the '{AspireC4DiagramOptions.SectionName}:Strict:RelationshipKinds' configuration."
			);
		}
	}

	static void EnforceStrictGroup(AspireC4StrictOptions strict, string? group, string contextLabel)
	{
		if (!strict.Mode.HasFlag(AspireC4StrictMode.Groups) || group is null)
			return;

		HashSet<string> allowed = new(strict.Groups, StringComparer.OrdinalIgnoreCase);
		if (!allowed.Contains(group))
		{
			throw new InvalidOperationException(
				$"Group '{group}' on element '{contextLabel}' is not in the allowed groups list. "
					+ $"Add it via {nameof(AspireC4DiagramOptionsExtensions.WithAllowedGroup)}(\"{group}\") "
					+ $"or add it to the '{AspireC4DiagramOptions.SectionName}:Strict:Groups' configuration."
			);
		}
	}

	static void EnforceStrictMetadataKeys(
		AspireC4StrictOptions strict,
		IReadOnlyList<LikeC4Metadata> metadata,
		string contextLabel,
		bool isRelationship
	)
	{
		if (!strict.Mode.HasFlag(AspireC4StrictMode.MetadataKeys) || metadata.Count == 0)
			return;

		HashSet<string> allowed = new(strict.MetadataKeys, StringComparer.OrdinalIgnoreCase);
		foreach (var (key, _) in metadata)
		{
			if (!allowed.Contains(key))
			{
				var subject = isRelationship ? $"relationship '{contextLabel}'" : $"element '{contextLabel}'";
				throw new InvalidOperationException(
					$"Metadata key '{key}' on {subject} is not in the allowed metadata keys list. "
						+ $"Add it via {nameof(AspireC4DiagramOptionsExtensions.WithAllowedMetadataKey)}(\"{key}\") "
						+ $"or add it to the '{AspireC4DiagramOptions.SectionName}:Strict:MetadataKeys' configuration."
				);
			}
		}
	}
}
