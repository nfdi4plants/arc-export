module CLITests

open System
open System.IO
open Xunit

open TestUtils

[<Fact>]
let ``Can execute compiled tool`` () =
    let res = runTool "arc-export" [|"--help"|] "."
    Assert.Equal(0, res.ExitCode)


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
        | Ok roc -> Assert.Equal(ROCrateDates.undateString ReferenceObjects.ArcPrototype.arc_ro_crate_metadata, ROCrateDates.undateString roc)
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
        | Ok roc -> Assert.Equal(ROCrateDates.undateString ReferenceObjects.UnregisteredAssay.arc_ro_crate_metadata, ROCrateDates.undateString roc)
        | Error e -> Assert.True(false, e)
