using MagicCSharp.Cli.Commands;

namespace MagicCSharp.Cli.Tests;

public class DalValidationTests
{
    [Fact]
    public void Required_is_accepted_on_a_column_From_sets_once()
    {
        var violations = Validate(Dal(
            "public required long LeaseId { get; set; }",
            fromInitializer: "LeaseId = edit.LeaseId,",
            applyBody: "Amount = edit.Amount;"));

        Assert.Empty(violations);
    }

    [Fact]
    public void Required_is_reported_on_a_column_Apply_sets()
    {
        var violations = Validate(Dal(
            "public required decimal Amount { get; set; }",
            fromInitializer: "",
            applyBody: "Amount = edit.Amount;"));

        var violation = Assert.Single(violations);
        Assert.Equal("Amount is 'required' and Apply() assigns it", violation.Message);
    }

    [Fact]
    public void Required_is_reported_on_a_column_From_also_leaves_to_Apply()
    {
        var violations = Validate(Dal(
            "public required decimal Amount { get; set; }",
            fromInitializer: "Amount = edit.Amount,",
            applyBody: "Amount = edit.Amount;"));

        Assert.Single(violations);
    }

    [Fact]
    public void Required_is_reported_on_a_column_nothing_sets()
    {
        var violations = Validate(Dal(
            "public required long LeaseId { get; set; }",
            fromInitializer: "",
            applyBody: "Amount = edit.Amount;"));

        var violation = Assert.Single(violations);
        Assert.Equal("LeaseId is 'required' and From() does not assign it", violation.Message);
    }

    [Fact]
    public void A_non_nullable_column_without_Required_is_still_reported()
    {
        var violations = Validate(Dal(
            "public decimal Amount { get; set; }",
            fromInitializer: "",
            applyBody: "Amount = edit.Amount;",
            attribute: ""));

        var violation = Assert.Single(violations);
        Assert.Equal("DAL column is non-nullable but not marked [Required]", violation.Rule);
    }

    private static List<Violation> Validate(string source)
    {
        var sourceFile = new SourceFile { Path = "ChargeDal.cs", Lines = source.Split('\n') };
        return ValidateCommand.DalPropertiesAreRequired([sourceFile]).ToList();
    }

    private static string Dal(string property, string fromInitializer, string applyBody, string attribute = "[Required]")
    {
        return $$"""
                 public class ChargeDal : BaseIdDal<Charge, ChargeEdit>
                 {
                     {{attribute}}
                     [Column("column")]
                     {{property}}

                     public override void Apply(ChargeEdit edit)
                     {
                         {{applyBody}}
                     }

                     public static ChargeDal From(ChargeEdit edit, long id)
                     {
                         var dal = new ChargeDal
                         {
                             Id = id,
                             {{fromInitializer}}
                         };
                         dal.Apply(edit);
                         return dal;
                     }
                 }
                 """;
    }
}
