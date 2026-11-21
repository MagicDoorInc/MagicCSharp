using MagicCSharp.Generators.Declarations;

namespace MagicCSharp.Generators.Tests;

public class UseCaseCallerPrefixesSettingTests
{
    [Fact]
    public void Read_ReturnsEachCommaSeparatedPrefix_FromTheEditorConfigMsBuildWrites()
    {
        // Arrange
        var buildProperties = GlobalConfigBuildProperties.Read(UseCaseCallerPrefixesSetting.PropertyName + " = Partner,Admin");

        // Act
        var callerPrefixes = UseCaseCallerPrefixesSetting.Read(OptionsFrom(buildProperties));

        // Assert
        Assert.Equal(["Partner", "Admin"], callerPrefixes);
    }

    [Fact]
    public void Read_TrimsPrefixesAndSkipsEmptyOnes()
    {
        // Arrange
        var buildProperties = GlobalConfigBuildProperties.Read(UseCaseCallerPrefixesSetting.PropertyName + " = Partner, ,Admin,");

        // Act
        var callerPrefixes = UseCaseCallerPrefixesSetting.Read(OptionsFrom(buildProperties));

        // Assert
        Assert.Equal(["Partner", "Admin"], callerPrefixes);
    }

    [Fact]
    public void Read_ReturnsNoPrefixes_WhenTheProjectSetsNone()
    {
        // Act
        var callerPrefixes = UseCaseCallerPrefixesSetting.Read(OptionsFrom(GlobalConfigBuildProperties.Read("RootNamespace = Acme.Leasing")));

        // Assert
        Assert.Empty(callerPrefixes);
    }

    private static TestAnalyzerConfigOptionsProvider OptionsFrom(IReadOnlyDictionary<string, string> buildProperties)
    {
        return new TestAnalyzerConfigOptionsProvider(buildProperties.ToDictionary(buildProperty => "build_property." + buildProperty.Key, buildProperty => buildProperty.Value));
    }
}
