namespace MagicCSharp.Generators.Tests;

public class DefaultValueReaderTests
{
    [Fact]
    public void ReadExpression_KeepsTheLongSuffix_OfALongDefault()
    {
        // Act
        var generatedText = InterfaceOfUseCaseWithParameter("long chargeId = 5");

        // Assert
        Assert.Contains("Execute(long chargeId = 5L);", generatedText);
    }

    [Fact]
    public void ReadExpression_KeepsTheUnsignedLongSuffix_OfAUlongDefault()
    {
        // Act
        var generatedText = InterfaceOfUseCaseWithParameter("ulong chargeId = 5");

        // Assert
        Assert.Contains("Execute(ulong chargeId = 5UL);", generatedText);
    }

    [Fact]
    public void ReadExpression_KeepsTheUnsignedSuffix_OfAUintDefault()
    {
        // Act
        var generatedText = InterfaceOfUseCaseWithParameter("uint chargeId = 5");

        // Assert
        Assert.Contains("Execute(uint chargeId = 5U);", generatedText);
    }

    [Fact]
    public void ReadExpression_KeepsTheFloatSuffix_OfAFloatDefault()
    {
        // Act
        var generatedText = InterfaceOfUseCaseWithParameter("float lateFeeRate = 1.5f");

        // Assert
        Assert.Contains("Execute(float lateFeeRate = 1.5f);", generatedText);
    }

    [Fact]
    public void ReadExpression_CastsTheNumber_WhenAnEnumDefaultIsNoMember()
    {
        // Act
        var generatedText = InterfaceOfUseCaseWithParameter("ChargeStatus status = (ChargeStatus)7");

        // Assert
        Assert.Contains("Execute(global::Acme.Leasing.Charges.ChargeStatus status = (global::Acme.Leasing.Charges.ChargeStatus)7);", generatedText);
    }

    private static string InterfaceOfUseCaseWithParameter(string parameter)
    {
        var source = $$"""
                       using System.Threading.Tasks;
                       using MagicCSharp.UseCases;

                       namespace Acme.Leasing.Charges;

                       public enum ChargeStatus
                       {
                           Open,
                           Paid,
                       }

                       [GenerateInterface]
                       public partial class PayChargeUseCase
                       {
                           public Task Execute({{parameter}})
                           {
                               return Task.CompletedTask;
                           }
                       }
                       """;
        return GeneratorHarness.RunClean(source).GeneratedText("Acme.Leasing.Charges.PayChargeUseCase.Interface.g.cs");
    }
}
