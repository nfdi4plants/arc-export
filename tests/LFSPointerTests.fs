module LFSPointerTests

open GitLFS
open Xunit

let missingLFSOutput = """GIT: open assays/MassHunter_targets/dataset/./assays/MassHunter_targets/dataset/QuantReports/22-0005_exp001.batch_a.bin/190614_QuantReport_ISTD_DB.xlsx: The system cannot find the path specified.
Error parsing Git LFS object: Invalid Git LFS object string format.
No Git LFS object found for assays/MassHunter_targets/dataset/./assays/MassHunter_targets/dataset/QuantReports/22-0005_exp001.batch_a.bin/190614_QuantReport_ISTD_DB.xlsx"""

let correctLFSOutput = """GIT: Git LFS pointer for assays/RNASeq/dataset/DB_097_CAMMD_CAGATC_L001_R1_001.fastq.gz
GIT: version https://git-lfs.github.com/spec/v1
GIT: oid sha256:53accc2afcca23e16f97ba977e3414f902ffcf9685adda86db93825d7d07bbd7
GIT: size 135
GIT:"""

let correctLFSObject = 
    { Version = "https://git-lfs.github.com/spec/v1"
      Hash = SHA256 "53accc2afcca23e16f97ba977e3414f902ffcf9685adda86db93825d7d07bbd7"
      Size = 135L }

let fixtureLFSFilePath = "assays/measurement1/dataset/proteomics_result.csv"

let fixtureLFSObject = 
    { Version = "https://git-lfs.github.com/spec/v1"
      Hash = SHA256 "01bb750bd981905d7065d48943567869570597745c4478bde4f7dbee16be8e3d"
      Size = 95710L }

[<Fact>]
let ``Git LFS JSON deserialization normalizes null files to an empty array`` () =
    let result = GitLFS.deserializeGitLfsJson """{"files":null}"""

    Assert.NotNull(result.files)
    Assert.Empty(result.files)


[<Fact>]
let ``Can correctly parse lfs pointer result`` () = 
    Assert.Equal(
        Some correctLFSObject,        
        GitLFS.GitLFSObject.tryFromString correctLFSOutput
    )

[<Fact>]
let ``Returns None for wrong lfs pointer result`` () = 
    Assert.Equal(
        None,
        GitLFS.GitLFSObject.tryFromString missingLFSOutput      
    )

[<Fact>]
let ``Can correctly retreive and parse lfs pointer`` () =
    Assert.Equal(
        Some fixtureLFSObject,
        GitLFS.tryGetGitLFSObject "fixtures/ArcPrototype" fixtureLFSFilePath
    )

[<Fact>]
let ``tryParseLsFilesLine parses a checked-out (present) file`` () =
    Assert.Equal(
        Some "assays/measurement1/dataset/proteomics_result.csv",
        GitLFS.tryParseLsFilesLine "01bb750bd9 * assays/measurement1/dataset/proteomics_result.csv"
    )

[<Fact>]
let ``tryParseLsFilesLine parses a pointer-only file`` () =
    Assert.Equal(
        Some "assays/measurement1/dataset/sample1.raw",
        GitLFS.tryParseLsFilesLine "abcdef0123 - assays/measurement1/dataset/sample1.raw"
    )

[<Fact>]
let ``tryParseLsFilesLine keeps path containing a separator-like substring`` () =
    // The oid never contains spaces, so the first " * " / " - " is always the separator.
    Assert.Equal(
        Some "data/raw - copy.txt",
        GitLFS.tryParseLsFilesLine "abcdef0123 * data/raw - copy.txt"
    )

[<Fact>]
let ``tryParseLsFilesLine returns None for a malformed line`` () =
    Assert.Equal(
        None,
        GitLFS.tryParseLsFilesLine "garbage"
    )

[<Fact>]
let ``tryGetLfsTrackedFiles returns exactly the LFS-tracked files`` () =
    // `git lfs ls-files` needs a working git repo, so this points at the source
    // submodule (the copied fixture in bin/ has a broken submodule .git pointer).
    match GitLFS.tryGetLfsTrackedFiles "../../../fixtures/ArcPrototype" with
    | None -> Assert.Fail("Expected `git lfs ls-files` to succeed for the fixture")
    | Some files ->
        // The one file that is genuinely LFS-tracked must be reported.
        Assert.Contains("assays/measurement1/dataset/proteomics_result.csv", files)
        // Regression: files that are NOT LFS-tracked must not appear. `git lfs
        // pointer --file` would otherwise hash them and produce bogus entries.
        Assert.DoesNotContain("assays/measurement1/dataset/sample1.raw", files)
        Assert.DoesNotContain("workflows/FixedScript/workflow.cwl", files)
        Assert.Equal(1, files.Count)
