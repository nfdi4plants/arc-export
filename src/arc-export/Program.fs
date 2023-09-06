open System.IO
open ARCtrl
open ARCtrl.FileSystem
open ARCtrl.ISA
open ARCtrl.NET
open Argu
open ArcSummaryMarkdown
open ARCtrl.NET.Contract

let getAllFilePaths (directoryPath : string) =
    let directoryPath = System.IO.Path.GetFullPath(directoryPath)
    let rec allFiles dirs =
        if Seq.isEmpty dirs then Seq.empty else
            seq { yield! dirs |> Seq.collect Directory.EnumerateFiles
                  yield! dirs |> Seq.collect Directory.EnumerateDirectories |> allFiles }

    allFiles [directoryPath] 
    |> Seq.toArray
    |> Array.map System.IO.Path.GetFullPath
    |> Array.map (fun p -> p.Replace(directoryPath, "").Replace("\\","/"))

let loadARCCustom (arcPath : string) =
                
    //// EINFACH DIESE ZEIELE AUSTAUSCHEN
           
    let paths = getAllFilePaths arcPath
            
    let arc = ARC.fromFilePaths paths

    let contracts = arc.GetReadContracts()

    let fulFilledContracts = 
        contracts 
        |> Array.map (fulfillReadContract arcPath)

    arc.SetISAFromContracts(fulFilledContracts,true)
    arc

try
    let args = CLIArgs.cliArgParser.ParseCommandLine()

    let arcPath = args.GetResult(CLIArgs.ARC_Directory)

    let outPath = 
        args.TryGetResult(CLIArgs.Out_Directory)
        |>Option.defaultValue arcPath

    let jsonFile = Path.Combine(outPath,"arc.json")

    let mdfile = Path.Combine(outPath,"arc-summary.md")

    let inv, mdContent = 
        try 
            let arc = loadARCCustom arcPath
            let inv = arc.ISA |> Option.get

            getAllFilePaths arcPath |> Seq.iter (printfn "%s")

            inv,
            MARKDOWN_TEMPLATE
                .Replace("[[ARC_TITLE]]", inv.Title |> Option.defaultValue "Untitled ARC")
                .Replace("[[FILE_TREE]]", FileSystemTree.toMarkdownTOC arc.FileSystem.Tree)
        with 
        | err ->
            printfn "Could not read investigation, writing empty arc json."
            let comment1 = Comment.fromString "Status" "Could not parse ARC"
            let comment2 = Comment.fromString "ErrorMessage" $"Could not parse ARC:\n{err.Message}"
            ArcInvestigation(Identifier.createMissingIdentifier() , comments = [|comment1;comment2|]),
            ""
    

    File.WriteAllText(mdfile, mdContent)

    inv
    |> ARCtrl.ISA.Json.ArcInvestigation.toString
    |> fun json -> File.WriteAllText(jsonFile, json)

with
    | :? ArguParseException as ex ->
        match ex.ErrorCode with
        | ErrorCode.HelpText  -> printfn "%s" (CLIArgs.cliArgParser.PrintUsage())
        | _ -> printfn "%s" ex.Message

    | ex ->
        printfn "Internal Error:"
        printfn "%s" ex.Message