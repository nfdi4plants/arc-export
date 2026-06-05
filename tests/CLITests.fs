module CLITests

open System
open System.IO
open Xunit

open TestUtils

[<Fact>]
let ``Can execute compiled tool`` () =
    let res = runTool "arc-export" [|"--help"|] "."
    Assert.Equal(0, res.ExitCode)

[<Fact>]
let ``lfs ro-crate metadata only hashes LFS-tracked files`` () =
    // Regression test: only files actually tracked by Git LFS may receive a sha256.
    // The LFS writer shells out to git, which needs a real repo - the fixture copied
    // into bin/ has a broken submodule .git pointer, so run against the source
    // submodule (the same path GitSubmoduleTests/TestObjects use).
    let outDir = "./ArcPrototype_lfs"
    DirectoryInfo(outDir).Create() |> ignore
    try
        let res = runTool "arc-export" [|"-p"; "../../../fixtures/ArcPrototype"; "-f"; "rocrate-metadata-lfs"; "-o"; outDir|] "."
        Assert.Equal(0, res.ExitCode)
        let actual =
            File.ReadAllText(Path.Combine(outDir, "arc-ro-crate-metadata.json"))
            |> fun f -> f.ReplaceLineEndings("\n")
            |> ROCrateDates.undateString
        let expected = ROCrateDates.undateString ReferenceObjects.ArcPrototype.arc_ro_crate_metadata_lfs
        Assert.Equal(expected, actual)
    finally
        Directory.Delete(outDir, true)


type ARCPrototypeFixture() = inherit ARCTestFixture("ArcPrototype")

type ArcPrototype() =

    let tool_fixture = new ARCPrototypeFixture()

    interface IClassFixture<ARCPrototypeFixture>

    member this.Fixture with get() = tool_fixture

    [<Fact>]
    member this.``isa json is correct`` () =
        Assert.Equal(0,this.Fixture.ISAJsonProcessResult.ExitCode)
        match this.Fixture.ISAJson with
        | Ok isa -> Assert.Equal(ReferenceObjects.ArcPrototype.isa_json, isa)
        | Error e -> Assert.True(false, e)
        
    [<Fact>]
    member this.``summary markdown is correct`` () =
        Assert.Equal(0,this.Fixture.ArcSummaryProcessResult.ExitCode)
        match this.Fixture.ArcSummary with
        | Ok s -> Assert.Equal(ReferenceObjects.ArcPrototype.arc_summary, s)
        | Error e -> Assert.True(false, e)
        
    [<Fact>]
    member this.``ro-crate metadata is correct`` () =
        Assert.Equal(0,this.Fixture.ROCrateMetadataProcessResult.ExitCode)
        match this.Fixture.ROCrateMetadata with
        | Ok roc -> 
            let actual = ROCrateDates.undateString roc
            let expected = ROCrateDates.undateString ReferenceObjects.ArcPrototype.arc_ro_crate_metadata
            // Commented out until this actually creates additional value by ignoring sorting of elements in lists
            //let equals = TestUtils.jsonStringEquals expected actual
            //Assert.True(equals)
            Assert.Equal(expected, actual)
        | Error e -> Assert.True(false, e)


type UnregisteredAssayFixture() = inherit ARCTestFixture("ARC-Export-TestFixture")

type UnregisteredAssay() =

    let tool_fixture = new UnregisteredAssayFixture()

    interface IClassFixture<UnregisteredAssayFixture>

    member this.Fixture with get() = tool_fixture

    [<Fact>]
    member this.``isa json is correct`` () =
        Assert.Equal(0,this.Fixture.ISAJsonProcessResult.ExitCode)
        match this.Fixture.ISAJson with
        | Ok isa -> Assert.Equal(ReferenceObjects.UnregisteredAssay.isa_json, isa)
        | Error e -> Assert.True(false, e)
        
    [<Fact>]
    member this.``summary markdown is correct`` () =
        Assert.Equal(0,this.Fixture.ArcSummaryProcessResult.ExitCode)
        match this.Fixture.ArcSummary with
        | Ok s -> Assert.Equal(ReferenceObjects.UnregisteredAssay.arc_summary, s)
        | Error e -> Assert.True(false, e)
        
    [<Fact>]
    member this.``ro-crate metadata is correct`` () =
        Assert.Equal(0,this.Fixture.ROCrateMetadataProcessResult.ExitCode)
        match this.Fixture.ROCrateMetadata with
        | Ok roc -> 
            let actual = (ROCrateDates.undateString roc)
            let expected = ROCrateDates.undateString ReferenceObjects.UnregisteredAssay.arc_ro_crate_metadata
            // Commented out until this actually creates additional value by ignoring sorting of elements in lists
            //let equals = TestUtils.jsonStringEquals expected actual
            //Assert.True(equals)
            Assert.Equal(expected, actual)
        | Error e -> Assert.True(false, e)