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

    [Fact]
    public void Required_is_accepted_in_a_marked_Dal_whose_From_is_generated()
    {
        var violations = Validate(MarkedDal("public required long LeaseId { get; set; }", ownMethods: ""));

        Assert.Empty(violations);
    }

    [Fact]
    public void Required_is_reported_in_a_partial_Dal_without_GenerateDalMapping()
    {
        var violations = Validate(MarkedDal("public required long LeaseId { get; set; }", ownMethods: "", attribute: ""));

        var violation = Assert.Single(violations);
        Assert.Equal("LeaseId is 'required' and From() does not assign it", violation.Message);
    }

    [Fact]
    public void Required_is_accepted_in_a_Dal_marked_with_the_attributes_full_name()
    {
        var violations = Validate(MarkedDal("public required long LeaseId { get; set; }", ownMethods: "",
            attribute: "[Table(\"charges\"), GenerateDalMappingAttribute]"));

        Assert.Empty(violations);
    }

    [Fact]
    public void Required_is_reported_in_an_unmarked_partial_Dal_beside_a_marked_one_in_the_same_file()
    {
        var markedDal = MarkedDal("public required long LeaseId { get; set; }", ownMethods: "");
        var unmarkedDal = MarkedDal("public required long UnitId { get; set; }", ownMethods: "", attribute: "")
            .Replace("ChargeDal", "ChargeLineDal");

        var violations = Validate(markedDal + "\n\n" + unmarkedDal);

        var violation = Assert.Single(violations);
        Assert.Equal("UnitId is 'required' and From() does not assign it", violation.Message);
    }

    [Fact]
    public void Required_is_reported_in_a_marked_Dal_that_maps_by_hand_without_From()
    {
        var violations = Validate(MarkedDal(
            "public required long LeaseId { get; set; }",
            ownMethods: """
                        public override Charge ToEntity()
                        {
                            return new Charge { Id = Id, LeaseId = LeaseId };
                        }
                        """));

        var violation = Assert.Single(violations);
        Assert.Equal("LeaseId is 'required' and From() does not assign it", violation.Message);
    }

    [Fact]
    public void Required_is_accepted_in_a_marked_Dal_that_maps_by_hand_and_lists_IDalFromEdit()
    {
        var violations = Validate(MarkedDal(
            "public required long LeaseId { get; set; }",
            ownMethods: """
                        public override Charge ToEntity()
                        {
                            return new Charge { Id = Id, LeaseId = LeaseId };
                        }
                        """,
            baseList: "BaseIdDal<Charge, ChargeEdit>, IDalFromEdit<ChargeDal, ChargeEdit>"));

        Assert.Empty(violations);
    }

    [Fact]
    public void Required_is_reported_in_a_marked_Dal_whose_own_Apply_sets_it()
    {
        var violations = Validate(MarkedDal(
            "public required decimal Amount { get; set; }",
            ownMethods: """
                        public override void Apply(ChargeEdit edit)
                        {
                            Amount = edit.Amount;
                        }
                        """));

        var violation = Assert.Single(violations);
        Assert.Equal("Amount is 'required' and Apply() assigns it", violation.Message);
    }

    [Fact]
    public void Required_is_reported_in_a_marked_Dal_whose_own_From_leaves_it_out()
    {
        var violations = Validate(MarkedDal(
            "public required long LeaseId { get; set; }",
            ownMethods: """
                        public static ChargeDal From(ChargeEdit edit, long id)
                        {
                            var dal = new ChargeDal
                            {
                                Id = id,
                            };
                            return dal;
                        }
                        """));

        var violation = Assert.Single(violations);
        Assert.Equal("LeaseId is 'required' and From() does not assign it", violation.Message);
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

    /// <summary>
    ///     A DAL the generator maps: <c>partial</c> and <c>[GenerateDalMapping]</c> (unless another attribute line is
    ///     given), with only the methods it writes by hand.
    /// </summary>
    private static string MarkedDal(string property, string ownMethods, string baseList = "BaseIdDal<Charge, ChargeEdit>",
        string attribute = "[GenerateDalMapping]")
    {
        return $$"""
                 {{attribute}}
                 public partial class ChargeDal : {{baseList}}
                 {
                     [Required]
                     [Column("column")]
                     {{property}}

                     {{ownMethods}}
                 }
                 """;
    }
}
