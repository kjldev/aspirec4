using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

public sealed class AspireC4DiagramOptionsTests
{
	[Test]
	public async Task Constructor_DefaultOptions_AutoIconsEnabledIsTrue()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.AutoIconsEnabled).IsTrue();
	}

	[Test]
	public async Task Constructor_DefaultOptions_ExcludedResourceTypes_ContainsParameterResource()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.ExcludedResourceTypes).Contains(typeof(ParameterResource));
	}

	[Test]
	public async Task Constructor_DefaultOptions_ExcludedResourceTypes_HasExactlyOneEntry()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.ExcludedResourceTypes.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Constructor_DefaultOptions_CheckLatestImageVersionIsTrue()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.CheckLatestImageVersion).IsTrue();
	}

	[Test]
	public async Task CopyTo_ScalarProperty_CopiesValueToTarget()
	{
		// Arrange
		var source = CreateSut();
		source.Title = "My Title";
		source.FormatGeneratedFile = false;
		var target = CreateSut();

		// Act
		source.CopyTo(target);

		// Assert
		await Assert.That(target.Title).IsEqualTo("My Title");
		await Assert.That(target.FormatGeneratedFile).IsFalse();
	}

	[Test]
	public async Task CopyTo_NullableProperty_CopiesNullToTarget()
	{
		// Arrange — target has a non-null value (as if bound from config); source has null.
		var source = CreateSut(); // Title defaults to null
		var target = CreateSut();
		target.Title = "Config Title";

		// Act
		source.CopyTo(target);

		// Assert — null from source overrides non-null in target (code wins over config)
		await Assert.That(target.Title).IsNull();
	}

	[Test]
	public async Task CopyTo_CollectionProperty_ReplacesTargetContents()
	{
		// Arrange
		var source = CreateSut();
		source.AdditionalDSLFiles.Add("/path/to/file.c4");
		var target = CreateSut();

		// Act
		source.CopyTo(target);

		// Assert
		await Assert.That(target.AdditionalDSLFiles).Contains("/path/to/file.c4");
		await Assert.That(target.AdditionalDSLFiles.Count).IsEqualTo(1);
	}

	[Test]
	public async Task CopyTo_CollectionProperty_DoesNotShareReference()
	{
		// Arrange
		var source = CreateSut();
		source.AdditionalDSLFiles.Add("/initial.c4");
		var target = CreateSut();
		source.CopyTo(target);

		// Act — mutate source after copy
		source.AdditionalDSLFiles.Add("/added-after.c4");

		// Assert — target is unaffected
		await Assert.That(target.AdditionalDSLFiles.Count).IsEqualTo(1);
	}

	[Test]
	public async Task CopyTo_ExcludedResourceTypes_ReplacesTargetContents()
	{
		// Arrange
		var source = CreateSut();
		source.ExcludedResourceTypes.Clear();
		var target = CreateSut(); // default has typeof(ParameterResource)

		// Act
		source.CopyTo(target);

		// Assert — explicit clear from source overrides default in target
		await Assert.That(target.ExcludedResourceTypes).IsEmpty();
	}

	[Test]
	public async Task CopyTo_IconResolvers_ReplacesTargetContentsInPlace()
	{
		// Arrange
		var source = CreateSut();
		source.IconResolvers.Add(_ => "tech:redis");
		var target = CreateSut();
		target.IconResolvers.Add(_ => "tech:postgres"); // existing entry to be cleared

		// Act
		source.CopyTo(target);

		// Assert
		await Assert.That(target.IconResolvers.Count).IsEqualTo(1);
		await Assert.That(target.IconResolvers[0](null!)).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task CopyTo_DictionaryProperty_ReplacesTargetContents()
	{
		// Arrange
		var source = CreateSut();
		source.ImageAliases["@icons"] = "/path/to/icons";
		var target = CreateSut();

		// Act
		source.CopyTo(target);

		// Assert
		await Assert.That(target.ImageAliases.ContainsKey("@icons")).IsTrue();
		await Assert.That(target.ImageAliases["@icons"]).IsEqualTo("/path/to/icons");
	}

	[Test]
	public async Task CopyTo_DictionaryProperty_DoesNotShareReference()
	{
		// Arrange
		var source = CreateSut();
		source.StateTagMap["Running"] = "aspire-run-state-running";
		var target = CreateSut();
		source.CopyTo(target);

		// Act — mutate source after copy
		source.StateTagMap["Stopped"] = "aspire-run-state-stopped";

		// Assert — target is unaffected
		await Assert.That(target.StateTagMap.ContainsKey("Stopped")).IsFalse();
	}

	[Test]
	public async Task ApplyDelta_UnchangedScalarProperty_DoesNotOverrideTarget()
	{
		// Arrange — simulate config setting ViewTitle before IOptions.Value is resolved
		var baseline = CreateSut();
		var callbackResult = CreateSut(); // callback did not touch ViewTitle
		var target = CreateSut();
		target.ViewTitle = "Config Title"; // target already has a config-bound value

		// Act
		callbackResult.ApplyDelta(baseline, target);

		// Assert — config-bound value must be preserved since callback didn't change it
		await Assert.That(target.ViewTitle).IsEqualTo("Config Title");
	}

	[Test]
	public async Task ApplyDelta_ChangedScalarProperty_OverridesTarget()
	{
		// Arrange
		var baseline = CreateSut();
		var callbackResult = CreateSut();
		callbackResult.ViewTitle = "Callback Title"; // callback explicitly set this
		var target = CreateSut();
		target.ViewTitle = "Config Title";

		// Act
		callbackResult.ApplyDelta(baseline, target);

		// Assert — callback value wins
		await Assert.That(target.ViewTitle).IsEqualTo("Callback Title");
	}

	[Test]
	public async Task ApplyDelta_NullableChangedToNull_OverridesTarget()
	{
		// Arrange — DefaultViewId defaults to "index"; callback sets it to null
		var baseline = CreateSut();
		var callbackResult = CreateSut();
		callbackResult.DefaultViewId = null; // differs from baseline default "index"
		var target = CreateSut();
		target.DefaultViewId = "some-config-value";

		// Act
		callbackResult.ApplyDelta(baseline, target);

		// Assert — explicit null from callback wins
		await Assert.That(target.DefaultViewId).IsNull();
	}

	[Test]
	public async Task ApplyDelta_CollectionAddedByCallback_IsAppliedToTarget()
	{
		// Arrange
		var baseline = CreateSut();
		var callbackResult = CreateSut();
		callbackResult.AdditionalDSLFiles.Add("extra.c4"); // callback added an entry
		var target = CreateSut();

		// Act
		callbackResult.ApplyDelta(baseline, target);

		// Assert
		await Assert.That(target.AdditionalDSLFiles).Contains("extra.c4");
	}

	[Test]
	public async Task ApplyDelta_EmptyCollection_DoesNotOverrideTargetCollection()
	{
		// Arrange — callback did not add anything; target already has a config-driven entry
		var baseline = CreateSut();
		var callbackResult = CreateSut(); // callback left AdditionalDSLFiles empty
		var target = CreateSut();
		target.AdditionalDSLFiles.Add("config-added.c4");

		// Act
		callbackResult.ApplyDelta(baseline, target);

		// Assert — target's collection is preserved
		await Assert.That(target.AdditionalDSLFiles).Contains("config-added.c4");
	}

	[Test]
	public async Task ApplyDelta_ExcludedResourceTypesChangedByCallback_IsAppliedToTarget()
	{
		// Arrange — callback removes the default ParameterResource exclusion
		var baseline = CreateSut();
		var callbackResult = CreateSut();
		callbackResult.ExcludedResourceTypes.Clear(); // differs from baseline {ParameterResource}
		var target = CreateSut();

		// Act
		callbackResult.ApplyDelta(baseline, target);

		// Assert — callback's cleared set wins
		await Assert.That(target.ExcludedResourceTypes).IsEmpty();
	}

	static AspireC4DiagramOptions CreateSut() => new();
}
