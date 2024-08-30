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
let ``Compiled tool returns correct isa json for ArcPrototype`` () =
    let res = runTool "arc-export" [|"-p"; "./fixtures/ArcPrototype"; "-f"; "isa-json"; "-o"; "."|] "."
    Assert.Equal(0, res.ExitCode)
    let file = File.ReadAllText "./arc-isa.json" |> fun f -> f.ReplaceLineEndings("\n")
    Assert.Equal(ReferenceObjects.ArcPrototype.isa_json, file)

[<Fact>]
let ``Compiled tool returns correct arc summary for ArcPrototype`` () =
    let res = runTool "arc-export" [|"-p"; "./fixtures/ArcPrototype"; "-f"; "summary-markdown"; "-o"; "."|] "."
    Assert.Equal(0, res.ExitCode)
    let file = File.ReadAllText "./arc-summary.md" |> fun f -> f.ReplaceLineEndings("\n")
    Assert.Equal(ReferenceObjects.ArcPrototype.arc_summary, file)

[<Fact>]
let ``Compiled tool returns correct ro crate metadata json for ArcPrototype`` () =
    let res = runTool "arc-export" [|"-p"; "./fixtures/ArcPrototype"; "-f"; "rocrate-metadata"; "-o"; "."|] "."
    Assert.Equal(0, res.ExitCode)
    let file = File.ReadAllText "./arc-ro-crate-metadata.json" |> fun f -> f.ReplaceLineEndings("\n")
    Assert.Equal(ReferenceObjects.ArcPrototype.arc_ro_crate_metadata, file)