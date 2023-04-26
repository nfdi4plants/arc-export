module CLIArgs

open Argu
open System.IO

type CliArguments =
    | [<Mandatory>][<AltCommandLine("-p")>] ARC_Directory of path:string
    | [<AltCommandLine("-o")>] Out_Directory of path:string

    // to-do once we have a common schema, implement as optional flag to validate all generated json via schema
    //| [<AltCommandLine("-val")>] Validate 

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | ARC_Directory _ -> "Specify a directory that contains the arc to convert."
            | Out_Directory _ -> "Optional. Specify a output directory for the invenio metadata record."
            // to-do once we have a common schema, implement as optional flag to validate all generated json via schema
            //| Validate -> "Optional. Validate the output against the metadata record schema"

let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some System.ConsoleColor.Red)

let cliArgParser = ArgumentParser.Create<CliArguments>(programName = "arc-export", errorHandler = errorHandler)