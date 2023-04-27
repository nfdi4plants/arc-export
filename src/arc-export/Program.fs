open System.IO
open ISADotNet
open arcIO.NET
open Argu

try
    let args = CLIArgs.cliArgParser.ParseCommandLine()

    let arcPath = args.GetResult(CLIArgs.ARC_Directory)

    let outPath = 
        args.TryGetResult(CLIArgs.Out_Directory)
        |>Option.defaultValue arcPath

    let outFile = Path.Combine(outPath,"arc.json")

    try Investigation.fromArcFolder arcPath
    with
    | _ -> 
        printfn "Could not read full arc, try reading investigation."
        try Investigation.read arcPath
        with
        | err -> 
            printfn "Could not read investigation, writing empty arc json."
            let comment1 = Comment.fromString "Status" "Could not parse ARC"
            let comment2 = Comment.fromString "ErrorMessage" $"Could not parse ARC:\n{err.Message}"
            Investigation.create(Comments = [comment1;comment2])
    |> ISADotNet.Json.Investigation.toFile outFile

with
    | :? ArguParseException as ex ->
        match ex.ErrorCode with
        | ErrorCode.HelpText  -> printfn "%s" (CLIArgs.cliArgParser.PrintUsage())
        | _ -> printfn "%s" ex.Message

    | ex ->
        printfn "Internal Error:"
        printfn "%s" ex.Message