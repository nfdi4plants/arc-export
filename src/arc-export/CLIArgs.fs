module CLIArgs

open Argu
open System.IO

type OutputFormat =
    | ISA_Json
    | ROCrate_Metadata
    | ROCrate_Metadata_LFS
    | ROCrate_Bundle
    | Summary_Markdown

type CliArguments =
    | [<Mandatory>][<AltCommandLine("-p")>] ARC_Directory of path:string
    | [<Unique>][<AltCommandLine("-o")>] Out_Directory of path:string
    | [<AltCommandLine("-f")>] Output_Format of OutputFormat
    | [<Unique>] Include_LFS_Hashes

    // to-do once we have a common schema, implement as optional flag to validate all generated json via schema
    //| [<AltCommandLine("-val")>] Validate 

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | ARC_Directory _ -> "Specify a directory that contains the arc to convert."
            | Out_Directory _ -> "Optional. Specify an output directory. Defaults to the ARC directory."
            | Output_Format _ -> "Optional. Specify the output format. Default is ISA-JSON."
            | Include_LFS_Hashes -> "Include Git LFS hashes in the bundle. Requires -f rocrate-bundle."
            // to-do once we have a common schema, implement as optional flag to validate all generated json via schema
            //| Validate -> "Optional. Validate the output against the metadata record schema"

let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some System.ConsoleColor.Red)

let cliArgParser = ArgumentParser.Create<CliArguments>(programName = "arc-export", errorHandler = errorHandler)
