using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Generates the content of a <c>likec4.config.json</c> file that configures a LikeC4 project.
/// </summary>
/// <remarks>
/// The config file is placed in the output directory alongside the generated model file.
/// Relative paths in the config are computed from the output directory, so the config is portable
/// as long as the relative structure between output and referenced folders is preserved.
/// </remarks>
public static class LikeC4ConfigGenerator
{
	static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

	/// <summary>
	/// Generates the content of a <c>likec4.config.json</c> file.
	/// </summary>
	/// <param name="name">
	/// The LikeC4 project name (becomes the <c>name</c> field; must be a unique identifier within the workspace).
	/// </param>
	/// <param name="title">
	/// The display title shown in generated diagrams (becomes the <c>title</c> field).
	/// </param>
	/// <param name="includePaths">
	/// Paths to include, relative to the location of the config file.
	/// Forwarded to the <c>include.paths</c> array. Can be empty.
	/// </param>
	/// <param name="imageAliases">
	/// Key/value pairs where each key is an image alias (must start with <c>@</c>) and the value
	/// is a path relative to the config file. Forwarded to the <c>imageAliases</c> object. Can be empty.
	/// </param>
	/// <returns>A formatted JSON string suitable for writing to a <c>likec4.config.json</c> file.</returns>
	public static string Generate(
		string name,
		string title,
		IEnumerable<string> includePaths,
		IReadOnlyDictionary<string, string> imageAliases
	)
	{
		ArgumentNullException.ThrowIfNull(imageAliases);
		var root = new JsonObject
		{
			["$schema"] = "https://likec4.dev/schemas/config.json",
			["name"] = name,
			["title"] = title,
		};

		var paths = includePaths.ToList();
		if (paths.Count > 0)
		{
			var pathArray = new JsonArray();
			foreach (var p in paths)
				pathArray.Add(JsonValue.Create(p));

			root["include"] = new JsonObject { ["paths"] = pathArray };
		}

		if (imageAliases.Count > 0)
		{
			var aliasesObject = new JsonObject();
			foreach (var (key, relativePath) in imageAliases)
				aliasesObject[key] = relativePath;

			root["imageAliases"] = aliasesObject;
		}

		return root.ToJsonString(WriteOptions);
	}
}
