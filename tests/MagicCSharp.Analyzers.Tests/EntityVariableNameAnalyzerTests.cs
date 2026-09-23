namespace MagicCSharp.Analyzers.Tests;

public class EntityVariableNameAnalyzerTests
{
    [Fact]
    public async Task Variables_not_named_after_their_type_are_reported()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new EntityVariableNameAnalyzer(), """
                                                                                          namespace Subject;

                                                                                          public class LeaseEdit
                                                                                          {
                                                                                          }

                                                                                          public class LeaseWriter
                                                                                          {
                                                                                              public void Write(LeaseEdit[] leaseEdits)
                                                                                              {
                                                                                                  var edit = new LeaseEdit();
                                                                                                  foreach (var item in leaseEdits)
                                                                                                  {
                                                                                                  }
                                                                                              }
                                                                                          }
                                                                                          """);

        Assert.Equal([
            "MCS0019 Subject.cs:11 Variable 'edit' holds a 'LeaseEdit'; name it 'leaseEdit'",
            "MCS0019 Subject.cs:12 Variable 'item' holds a 'LeaseEdit'; name it 'leaseEdit'",
        ], diagnostics);
    }

    [Fact]
    public async Task A_leading_acronym_is_lowercased_as_one_word()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new EntityVariableNameAnalyzer(), """
                                                                                          namespace Subject;

                                                                                          public class MITSFile
                                                                                          {
                                                                                          }

                                                                                          public class FeedWriter
                                                                                          {
                                                                                              public void Write()
                                                                                              {
                                                                                                  var file = new MITSFile();
                                                                                                  var mitsFile = new MITSFile();
                                                                                              }
                                                                                          }
                                                                                          """);

        Assert.Equal(["MCS0019 Subject.cs:11 Variable 'file' holds a 'MITSFile'; name it 'mitsFile'"], diagnostics);
    }

    [Fact]
    public async Task Type_derived_qualified_and_third_party_variables_are_allowed()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new EntityVariableNameAnalyzer(), """
                                                                                          namespace Subject;

                                                                                          public class LeaseEdit
                                                                                          {
                                                                                          }

                                                                                          public class LeaseWriter
                                                                                          {
                                                                                              public void Write()
                                                                                              {
                                                                                                  var leaseEdit = new LeaseEdit();
                                                                                                  var previousLeaseEdit = new LeaseEdit();
                                                                                                  var widget = new ThirdParty.ExternalWidget();
                                                                                                  var names = new System.Collections.Generic.List<LeaseEdit>();
                                                                                              }
                                                                                          }
                                                                                          """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Types_from_a_sibling_assembly_are_first_party_too()
    {
        var diagnostics = await AnalyzerHarness.Analyze(new EntityVariableNameAnalyzer(), """
                                                                                          namespace Subject;

                                                                                          public class LeaseArchiver
                                                                                          {
                                                                                              public void Archive()
                                                                                              {
                                                                                                  var entry = new Acme.Libraries.Shared.LeaseArchiveEntry();
                                                                                              }
                                                                                          }
                                                                                          """);

        Assert.Equal(["MCS0019 Subject.cs:7 Variable 'entry' holds a 'LeaseArchiveEntry'; name it 'leaseArchiveEntry'"], diagnostics);
    }
}
