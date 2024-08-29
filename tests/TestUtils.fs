module TestUtils

open System
open System.IO
open type System.Environment
open Fake.Core

let runTool (tool: string) (args: string []) (dir:string) =
    CreateProcess.fromRawCommand tool args
    |> CreateProcess.withWorkingDirectory dir
    |> CreateProcess.redirectOutput
    |> Proc.run

let getCommitHash path = 
    let output = runTool "git" [|"rev-parse"; "HEAD"|] path
    output