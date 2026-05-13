using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Validates generated LikeC4 DSL output using the real <c>likec4 validate</c> CLI
/// (<c>npx likec4 validate --json --no-layout</c>).  Each test generates a DSL string,
/// writes it to a temporary project directory, runs the validator and asserts that the
/// number of validation errors in the generated file is zero.
/// </summary>
public sealed class LikeC4DSLValidationTests
{
	static readonly AspireC4DiagramOptions DefaultOptions = new()
	{
		Title = "Test Architecture",
		OutputDirectory = "./likec4",
		FileName = "model.gen",
	};

	string _tempDir = string.Empty;
	string _dslFile = string.Empty;

	[Before(Test)]
	public void SetUp()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), $"aspirec4-dslval-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);
		// Minimal LikeC4 project config — `name` is required by the CLI.
		File.WriteAllText(Path.Combine(_tempDir, "likec4.config.json"), """{"name":"aspirec4-test"}""");
		_dslFile = Path.Combine(_tempDir, "model.c4");
	}

	[After(Test)]
	public void TearDown()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// -------------------------------------------------------------------------
	// Helper
	// -------------------------------------------------------------------------

	readonly record struct ValidationResult(bool Valid, int FilteredErrors, int TotalErrors, string RawOutput);

	async Task<ValidationResult> RunValidateAsync(
		string dsl,
		CancellationToken cancellationToken,
		(string FileName, string Content)[]? additionalFiles = null
	)
	{
		await File.WriteAllTextAsync(_dslFile, dsl);

		var filesToValidate = new List<string> { _dslFile };

		if (additionalFiles != null)
		{
			foreach (var (fileName, content) in additionalFiles)
			{
				var path = Path.Combine(_tempDir, fileName);
				await File.WriteAllTextAsync(path, content);
				filesToValidate.Add(path);
			}
		}

		var useDotFlag = await Helpers.IsDotAvailableAsync(cancellationToken) ? " --use-dot" : "";

		// npx on Windows is a .cmd wrapper, so it must be invoked through the shell.
		string shellFile,
			shellArgs;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var fileArgs = string.Join(" ", filesToValidate.Select(f => $"--file \"{f}\""));
			shellFile = "cmd.exe";
			shellArgs = $"/c npx --yes likec4 validate --json --no-layout{useDotFlag} {fileArgs} \"{_tempDir}\"";
		}
		else
		{
			var fileArgs = string.Join(" ", filesToValidate.Select(f => $"--file '{f}'"));
			shellFile = "/bin/sh";
			shellArgs = $"-c \"npx --yes likec4 validate --json --no-layout{useDotFlag} {fileArgs} '{_tempDir}'\"";
		}

		using var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = shellFile,
				Arguments = shellArgs,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = _tempDir,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdout = await process.StandardOutput.ReadToEndAsync();
		var stderr = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

		// Extract JSON object from stdout — npx may prefix with a download progress line.
		var jsonStart = stdout.IndexOf('{', StringComparison.Ordinal);
		var jsonEnd = stdout.LastIndexOf('}');
		if (jsonStart < 0 || jsonEnd < 0)
		{
			var rawOutput = $"stdout:\n{stdout}\nstderr:\n{stderr}";
			return new ValidationResult(Valid: false, FilteredErrors: -1, TotalErrors: -1, RawOutput: rawOutput);
		}

		var json = stdout[jsonStart..(jsonEnd + 1)];
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var valid = root.GetProperty("valid").GetBoolean();
		var stats = root.GetProperty("stats");
		var filteredErrors = stats.GetProperty("filteredErrors").GetInt32();
		var totalErrors = stats.GetProperty("totalErrors").GetInt32();
		var rawOut = $"stdout:\n{stdout}\nstderr:\n{stderr}";
		return new ValidationResult(
			Valid: valid,
			FilteredErrors: filteredErrors,
			TotalErrors: totalErrors,
			RawOutput: rawOut
		);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "dot availability check is best-effort; any failure means dot is unavailable"
	)]
	static bool IsDotAvailable()
	{
		try
		{
			using var proc = Process.Start(
				new ProcessStartInfo
				{
					FileName = "dot",
					Arguments = "-V",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);
			proc?.WaitForExit();
			return proc?.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	static void AssertNoValidationErrors(ValidationResult result, string dsl)
	{
		if (result.FilteredErrors != 0)
		{
			throw new InvalidOperationException(
				$"Expected 0 LikeC4 DSL validation errors but got {result.FilteredErrors}.\n\n"
					+ $"Validator output:\n{result.RawOutput}\n\n"
					+ $"Generated DSL:\n{dsl}"
			);
		}
	}

	// -------------------------------------------------------------------------
	// Tests
	// -------------------------------------------------------------------------

	[Test]
	public async Task ValidatedDsl_EmptyModel_ProducesNoErrors(CancellationToken cancellationToken)
	{
		var dsl = LikeC4DSLGenerator.Generate(LikeC4Model.Empty, DefaultOptions);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task ValidatedDsl_SingleElement_ProducesNoErrors(CancellationToken cancellationToken)
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	[MethodDataSource(nameof(AllResourceStates))]
	public async Task ValidatedDsl_EachResourceState_ProducesNoErrors(
		LikeC4ResourceState state,
		CancellationToken cancellationToken
	)
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "svc",
					Label = "Service",
					Kind = LikeC4ElementKind.Component,
					State = state,
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	public static IEnumerable<LikeC4ResourceState> AllResourceStates() => Enum.GetValues<LikeC4ResourceState>();

	[Test]
	public async Task ValidatedDsl_ElementWithMetadataTagsLinks_ProducesNoErrors(CancellationToken cancellationToken)
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
					Technology = "ASP.NET Core",
					Description = "Handles HTTP requests",
					Tags = ["backend", "critical"],
					Metadata = [new LikeC4Metadata("version", "1.0.0"), new LikeC4Metadata("owner", "platform-team")],
					Links = [new LikeC4Link("https://example.com/api", "Docs")],
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task ValidatedDsl_RelationshipWithKindAndLabel_ProducesNoErrors(CancellationToken cancellationToken)
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "frontend",
					Label = "Frontend",
					Kind = LikeC4ElementKind.Component,
				},
				new LikeC4Element
				{
					Name = "backend",
					Label = "Backend",
					Kind = LikeC4ElementKind.Component,
				},
			],
			Relationships =
			[
				new LikeC4Relationship
				{
					SourceName = "frontend",
					TargetName = "backend",
					Label = "calls",
					Kind = "async",
				},
			],
		};

		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task ValidatedDsl_NestedElements_ProducesNoErrors(CancellationToken cancellationToken)
	{
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "cloud",
					Label = "Cloud",
					Kind = LikeC4ElementKind.System,
				},
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
					ParentName = "cloud",
				},
				new LikeC4Element
				{
					Name = "db",
					Label = "Database",
					Kind = LikeC4ElementKind.Database,
					ParentName = "cloud",
				},
			],
			Relationships =
			[
				new LikeC4Relationship
				{
					SourceName = "api",
					TargetName = "db",
					Label = "reads",
				},
			],
		};

		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task ValidatedDsl_ElementKindSpecWithStyleAndNotation_ProducesNoErrors(
		CancellationToken cancellationToken
	)
	{
		var options = new AspireC4DiagramOptions
		{
			Title = "Styled Architecture",
			OutputDirectory = "./likec4",
			FileName = "model.gen",
			ElementKindSpecs =
			[
				new LikeC4ElementKindSpec(LikeC4ElementKind.Component)
				{
					Notation = "Service",
					Technology = "ASP.NET Core",
					Style = new LikeC4ElementKindStyle { Shape = "component", Color = "primary" },
				},
			],
		};

		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
				},
			],
			Relationships = [],
		};

		var dsl = LikeC4DSLGenerator.Generate(model, options);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task ValidatedDsl_MultipleElementsAllStatesSimultaneously_ProducesNoErrors(
		CancellationToken cancellationToken
	)
	{
		// Stress-test: all state variants co-existing in one diagram.
		var elements = Enum.GetValues<LikeC4ResourceState>()
			.Select(
				(state, idx) =>
					new LikeC4Element
					{
						Name = $"svc_{idx}",
						Label = $"Service {idx}",
						Kind = LikeC4ElementKind.Component,
						State = state,
					}
			)
			.ToList();

		var model = new LikeC4Model { Elements = elements, Relationships = [] };
		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);
		var result = await RunValidateAsync(dsl, cancellationToken);
		AssertNoValidationErrors(result, dsl);
		await Assert.That(result.FilteredErrors).IsEqualTo(0);
	}

	[Test]
	public async Task ValidatedDsl_WithAdditionalExtensionFile_ProducesNoErrors(CancellationToken cancellationToken)
	{
		// Arrange: a model with two elements and a relationship.
		var model = new LikeC4Model
		{
			Elements =
			[
				new LikeC4Element
				{
					Name = "api",
					Label = "API",
					Kind = LikeC4ElementKind.Component,
				},
				new LikeC4Element
				{
					Name = "db",
					Label = "Database",
					Kind = LikeC4ElementKind.Database,
				},
			],
			Relationships =
			[
				new LikeC4Relationship
				{
					SourceName = "api",
					TargetName = "db",
					Label = "reads",
				},
			],
		};

		var dsl = LikeC4DSLGenerator.Generate(model, DefaultOptions);

		// Act: validate the generated DSL alongside a hand-authored extension file that
		// extends the model with extra metadata and adds custom views.
		const string extensionDsl = """
			model {
			  extend api {
			    link https://example.com/api-docs 'API Docs'
			    metadata { team 'Backend' }
			  }
			}

			views {
			  view api_detail {
			    title 'API Detail'
			    include api
			    include -> api ->
			  }
			}
			""";

		var result = await RunValidateAsync(dsl, cancellationToken, [("extensions.c4", extensionDsl)]);

		AssertNoValidationErrors(result, $"main:\n{dsl}\nextension:\n{extensionDsl}");
		await Assert.That(result.TotalErrors).IsEqualTo(0);
	}
}
