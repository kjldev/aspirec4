using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	static AspireC4StrictOptions CreateStrictOptions(
		AspireC4StrictMode mode,
		IEnumerable<string>? tags = null,
		IEnumerable<string>? relationshipKinds = null,
		IEnumerable<string>? groups = null,
		IEnumerable<string>? metadataKeys = null
	) =>
		new()
		{
			Mode = mode,
			Tags = tags?.ToHashSet(StringComparer.Ordinal) ?? [],
			RelationshipKinds = relationshipKinds?.ToHashSet(StringComparer.Ordinal) ?? [],
			Groups = groups?.ToHashSet(StringComparer.Ordinal) ?? [],
			MetadataKeys = metadataKeys?.ToHashSet(StringComparer.Ordinal) ?? [],
		};

	static (ProjectResource Api, ContainerResource Db) CreateApiAndDbResources()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		return (api, db);
	}
}
