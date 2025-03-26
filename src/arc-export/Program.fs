open System.IO
open ARCtrl
open ARCtrl.FileSystem
open Argu

open AssayRegistration
open PersonCleaning

try
    let args = CLIArgs.cliArgParser.ParseCommandLine()

    let arcPath = args.GetResult(CLIArgs.ARC_Directory)

    let outDir = 
        args.TryGetResult(CLIArgs.Out_Directory)
        |>Option.defaultValue arcPath

    if not <| Directory.Exists outDir then
        printfn "Creating output directory: %s" outDir
        Directory.CreateDirectory outDir |> ignore

    let arc = 
    
        try ARC.load arcPath with
        | err -> 
            printfn "Could not read investigation, writing empty arc json."
            let comment1 = Comment("Status","Could not parse ARC")
            let comment2 = Comment("ErrorMessage",$"Could not parse ARC:\n{err.Message}")
            let filePaths = ARCtrl.FileSystemHelper.getAllFilePathsAsync arcPath |> Async.RunSynchronously
            let fs = filePaths |> FileSystemTree.fromFilePaths |> FileSystem.create
            let inv = 
                ArcInvestigation(Helper.Identifier.createMissingIdentifier() , comments = ResizeArray [|comment1;comment2|])
            let arc = ARC(inv,fs = fs)
            arc

    arc.RegisterAssays()
    arc.CleanPersons()

    let outputFormats = args.GetResults(CLIArgs.Output_Format)
            
    if outputFormats |> List.contains CLIArgs.OutputFormat.ISA_Json || List.isEmpty outputFormats then
        Writers.write_isa_json outDir arc

    if outputFormats |> List.contains CLIArgs.OutputFormat.ROCrate_Metadata then
        Writers.write_ro_crate_metadata outDir arc

    if outputFormats |> List.contains CLIArgs.OutputFormat.Summary_Markdown then
        Writers.write_arc_summary_markdown outDir arc

with
    | :? ArguParseException as ex ->
        match ex.ErrorCode with
        | ErrorCode.HelpText  -> printfn "%s" (CLIArgs.cliArgParser.PrintUsage())
        | _ -> printfn "%s" ex.Message

    | ex ->
        printfn "Internal Error:"
        printfn "%s" ex.Message