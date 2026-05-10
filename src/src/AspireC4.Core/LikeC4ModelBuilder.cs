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
	public static LikeC4Model Build(
		IReadOnlyList<IResource> resources,
		IReadOnlyDictionary<string, LikeC4ResourceState>? resourceStates = null,
		bool autoIconsEnabled = true,
		AspireMetadataInclusion aspireMetadataInclusion = AspireMetadataInclusion.All,
		NormaliseMetadataBehaviour normaliseMetadataBehaviour = NormaliseMetadataBehaviour.Normalise
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
		var visibleByName = visibleResources
			.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

		var elements = new List<LikeC4Element>(visibleResources.Count);
		var relationships = new List<LikeC4Relationship>();
		var visitedRelationships = new HashSet<(string Source, string Target)>();

		foreach (var resource in visibleResources)
		{
			var state =
				resourceStates is not null && resourceStates.TryGetValue(resource.Name, out var s)
					? s
					: LikeC4ResourceState.Unknown;

			elements.Add(
				BuildElement(resource, state, autoIconsEnabled, aspireMetadataInclusion, normaliseMetadataBehaviour)
			);
			CollectRelationships(
				resource,
				visibleResources,
				visibleByName,
				relationships,
				visitedRelationships,
				normaliseMetadataBehaviour
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
		var visible = new HashSet<IResource>(resources.Count);

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
		NormaliseMetadataBehaviour normaliseMetadataBehaviour = NormaliseMetadataBehaviour.Normalise
	)
	{
		var details = resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().LastOrDefault();
		var inferredTechnology = InferTechnology(resource);

		var label = details?.Label ?? resource.Name;
		var technology = details?.Technology ?? inferredTechnology;
		var description = details?.Description;
		var summary = details?.Summary;
		var icon = ResolveIcon(resource, details, inferredTechnology, autoIconsEnabled);
		var kind = details?.Kind ?? InferKind(resource);
		var parentName = (resource as IResourceWithParent)?.Parent?.Name;
		var group = resource.Annotations.OfType<LikeC4GroupAnnotation>().LastOrDefault()?.GroupName;

		var userMetadata = new List<LikeC4Metadata>();
		var seenMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var m in details?.Metadata ?? [])
		{
			var normalisedKey = NormaliseMetadataKey(m.Key, normaliseMetadataBehaviour);
			if (seenMetadataKeys.Add(normalisedKey))
			{
				userMetadata.Add(new LikeC4Metadata(normalisedKey, m.Value));
			}
		}

		var userLinks = details?.Links ?? [];
		var (autoMetadata, autoLinks) = BuildAspireData(resource, aspireMetadataInclusion, userMetadata, userLinks);

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
			Tags = details?.Tags ?? [],
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
		IReadOnlyList<LikeC4Link> existingLinks
	)
	{
		var metadata = new List<LikeC4Metadata>();
		var links = new List<LikeC4Link>();

		if (inclusion.HasFlag(AspireMetadataInclusion.Metadata))
		{
			var existingKeys = existingMetadata.Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
			var existingUris = existingLinks.Select(l => l.Uri).ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var endpoint in resource.Annotations.OfType<EndpointAnnotation>())
			{
				if (endpoint.UriScheme != "http" && endpoint.UriScheme != "https")
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

		return (metadata, links);
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
			if (!IsValidMetadataKey(key))
			{
				throw new ArgumentException(
					$"Metadata key '{key}' contains invalid characters. "
						+ "Valid characters are letters, digits, hyphens (-), and underscores (_).",
					nameof(key)
				);
			}

			return key;
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
	static IReadOnlyDictionary<string, string> NormaliseMetadataKeys(
		IReadOnlyDictionary<string, string> metadata,
		NormaliseMetadataBehaviour behaviour
	)
	{
		if (metadata.Count == 0)
		{
			return metadata;
		}

		var result = new Dictionary<string, string>(metadata.Count, StringComparer.OrdinalIgnoreCase);
		foreach (var (key, value) in metadata)
		{
			result.TryAdd(NormaliseMetadataKey(key, behaviour), value);
		}

		return result;
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
		bool autoIconsEnabled
	)
	{
		if (!string.IsNullOrWhiteSpace(details?.Icon))
		{
			return details.Icon;
		}

		if (!(details?.AutoIconEnabled ?? autoIconsEnabled))
		{
			return null;
		}

		return LikeC4IconMatcher.TryInferIcon([
			details?.Technology,
			inferredTechnology,
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
		NormaliseMetadataBehaviour normaliseMetadataBehaviour
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

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = resource.Name,
					TargetName = effectiveTarget.Name,
					Label = details?.Label ?? inferredLabel,
					Technology = details?.Technology,
					Description = details?.Description,
					Kind = details?.Kind,
					Tags = details?.Tags ?? [],
					Links = details?.Links ?? [],
					Metadata = NormaliseMetadataKeys(
						details?.Metadata ?? new Dictionary<string, string>(),
						normaliseMetadataBehaviour
					),
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

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = resource.Name,
					TargetName = effectiveTarget.Name,
					Label = details.Label,
					Technology = details.Technology,
					Description = details.Description,
					Kind = details.Kind,
					Tags = details.Tags,
					Links = details.Links,
					Metadata = NormaliseMetadataKeys(details.Metadata, normaliseMetadataBehaviour),
				}
			);
		}
	}
}
