open System.IO
open ARCtrl
open ARCtrl.FileSystem
open Argu

open AssayRegistration
open PersonCleaning

try

    printfn "Start arc-export"
    let args = CLIArgs.cliArgParser.ParseCommandLine()

    let arcPath = args.GetResult(CLIArgs.ARC_Directory)

    let outDir = 
        args.TryGetResult(CLIArgs.Out_Directory)
        |>Option.defaultValue arcPath

    if not <| Directory.Exists outDir then
        printfn "Creating output directory: %s" outDir
        Directory.CreateDirectory outDir |> ignore

    // We created this function specifically for sorting the paths before creating the ARC
    // Different sorting were causing errors in testing between different OS instances (local vs CI)
    let loadARC arcPath = 
        let paths = FileSystemHelper.getAllFilePathsAsync arcPath |> Async.RunSynchronously |> Array.sort
        let arc = ARC.fromFilePaths paths

        let contracts = arc.GetReadContracts()
             
        let fulFilledContracts = 
            contracts 
            |> ARCtrl.Contract.fullFillContractBatchAsync arcPath |> Async.RunSynchronously

        match fulFilledContracts with
        | Ok c -> 
            arc.SetISAFromContracts(c)
            arc
        | Error e -> failwithf "Could not load ARC from %s: %O" arcPath e

    printfn "Loading ARC from %s" arcPath
    let arc = 
    
        try loadARC arcPath with
        | err -> 
            printfn "Could not read investigation, writing empty arc json."
            let comment1 = Comment("Status","Could not parse ARC")
            let comment2 = Comment("ErrorMessage",$"Could not parse ARC:\n{err.Message}")
            let filePaths = ARCtrl.FileSystemHelper.getAllFilePathsAsync arcPath |> Async.RunSynchronously
            let fs = filePaths |> FileSystemTree.fromFilePaths |> FileSystem.create
            let inv = 
                ArcInvestigation(Helper.Identifier.createMissingIdentifier() , comments = ResizeArray [|comment1;comment2|])
            let arc = ARC.fromArcInvestigation(inv,fs = fs)
            arc

    arc.RegisterAssays()
    arc.CleanPersons()

    printfn "Exporting ARC content to %s" outDir

    let outputFormats = args.GetResults(CLIArgs.Output_Format)
            
    if outputFormats |> List.contains CLIArgs.OutputFormat.ISA_Json || List.isEmpty outputFormats then
        Writers.write_isa_json outDir arc

    if outputFormats |> List.contains CLIArgs.OutputFormat.ROCrate_Metadata then
        Writers.write_ro_crate_metadata outDir arc

    if outputFormats |> List.contains CLIArgs.OutputFormat.ROCrate_Metadata_LFS then
        Writers.write_ro_crate_metadata_LFSHashes arcPath outDir arc

    if outputFormats |> List.contains CLIArgs.OutputFormat.Summary_Markdown then
        Writers.write_arc_summary_markdown outDir arc

    printfn "Finished arc-export"

with
    | :? ArguParseException as ex ->
        match ex.ErrorCode with
        | ErrorCode.HelpText  -> printfn "%s" (CLIArgs.cliArgParser.PrintUsage())
        | _ -> printfn "%s" ex.Message

    | ex ->
        printfn "Internal Error:"
        printfn "%s" ex.Message