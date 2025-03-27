module TestUtils

open System
open System.IO
open type System.Environment
open Fake.Core

module ROCrateDates = 
    
    let datePublishedRegex = "datePublished\": \"[^\\\"]*\""

    let dateModifiedRegex = "dateModified\": \"[^\\\"]*\""

    let replaceRegex (regex: string) (replacement: string) (input: string) =
        let r = new System.Text.RegularExpressions.Regex(regex)
        r.Replace(input, replacement)

    let undateString (s : string) = 
        replaceRegex datePublishedRegex "datePublished\": \"\"" s
        |> replaceRegex dateModifiedRegex "dateModified\": \"\""

let jsonStringEquals (expected: string) (actual: string) =
    let e1 = System.Text.Json.JsonDocument.Parse(expected).RootElement
    let e2 = System.Text.Json.JsonDocument.Parse(actual).RootElement
    System.Text.Json.JsonElement.DeepEquals(e1,e2)

let runTool (tool: string) (args: string []) (dir:string) =
    CreateProcess.fromRawCommand tool args
    |> CreateProcess.withWorkingDirectory dir
    |> CreateProcess.redirectOutput
    |> Proc.run

let getCommitHash path = 
    let output = runTool "git" [|"rev-parse"; "HEAD"|] path
    output

// 
type ARCTestFixture(arcName : string) =

    let _ = DirectoryInfo($"./{arcName}").Create()

    let resultIsaJson = runTool "arc-export" [|"-p"; $"./fixtures/{arcName}"; "-f"; "isa-json"; "-o"; $"./{arcName}"|] "."
    let resultSummaryMarkdown = runTool "arc-export" [|"-p"; $"./fixtures/{arcName}"; "-f"; "summary-markdown"; "-o"; $"./{arcName}"|] "."
    let resultROCrateMetadata = runTool "arc-export" [|"-p"; $"./fixtures/{arcName}"; "-f"; "rocrate-metadata"; "-o"; $"./{arcName}"|] "."

    let isaJson = 
        try
            Ok (File.ReadAllText $"./{arcName}/arc-isa.json" |> fun f -> f.ReplaceLineEndings("\n"))
        with e as ex -> 
            Error(ex.Message)

    let arcSummary = 
        try
            Ok (File.ReadAllText $"./{arcName}/arc-summary.md" |> fun f -> f.ReplaceLineEndings("\n"))
        with e as ex -> 
            Error(ex.Message)

    let roCrateMetadata =
        try 
            Ok (File.ReadAllText $"./{arcName}/arc-ro-crate-metadata.json" |> fun f -> f.ReplaceLineEndings("\n"))
        with e as ex ->
            Error(ex.Message)

    interface IDisposable with
        override this.Dispose() =
            Directory.Delete($"./{arcName}", true)

    member this.ISAJsonProcessResult = resultIsaJson
    member this.ArcSummaryProcessResult = resultSummaryMarkdown
    member this.ROCrateMetadataProcessResult = resultROCrateMetadata

    member this.ISAJson = isaJson
    member this.ArcSummary = arcSummary
    member this.ROCrateMetadata = roCrateMetadata