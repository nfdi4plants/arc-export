module GitLFS

open System.Diagnostics
open System.Runtime.InteropServices
open System.IO

let [<Literal>] oidPattern = """(?<=oid )(?<HashType>\S+):(?<HashValue>\S+)"""
let [<Literal>] sizePattern = """(?<=size )(?<Size>\d+)"""
let [<Literal>] versionPattern = """(?<=version )(?<Version>\S+)"""

let tryMatch (pattern: string) (input: string) =
    let regex = System.Text.RegularExpressions.Regex(pattern)
    let result = regex.Match(input)
    if result.Success then
        Some result
    else
        None

type Hash = 
    | SHA256 of string

    //static member fromString (s: string) : Hash =
    //    if s.StartsWith("sha256:") then
    //        SHA256 (s.Substring(7))
    //    else
    //        failwith "Invalid hash format. Expected sha256: prefix."

type GitLFSObject = 
    { 
        Version: string
        Hash: Hash
        Size: int64
    }

    static member fromString (s: string) : GitLFSObject =
        let parts = s.Split ([| '\n' |], 3, System.StringSplitOptions.RemoveEmptyEntries)
        if parts.Length <> 3 then
            failwith "Invalid Git LFS object string format."
        
        let version = 
            parts
            |> Array.pick (fun (line : string) -> 
                tryMatch versionPattern line 
                |> Option.map (fun m -> m.Groups.["Version"].Value)               
            )
        let hash = 
            parts
            |> Array.pick (fun (line : string) -> 
                tryMatch oidPattern line
                |> Option.map (fun m -> 
                    let hashType = m.Groups.["HashType"].Value
                    let hashValue = m.Groups.["HashValue"].Value
                    if hashType = "sha256" then
                        Hash.SHA256 hashValue
                    else
                        failwithf "Unsupported hash type: %s" hashType
                )
            )
        let size = 
            parts
            |> Array.pick (fun (line : string) -> 
                tryMatch sizePattern line
                |> Option.map (fun m -> 
                    match System.Int64.TryParse(m.Groups.["Size"].Value) with
                    | true, size -> size
                    | _ -> failwith "Invalid size format."
                )
            )
        { Version = version; Hash = hash; Size = size }


let executeGitCommandWithResponse (repoDir : string) (command : string) =

    let procStartInfo = 
        ProcessStartInfo(
            WorkingDirectory = repoDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            FileName = "git",
            Arguments = command
        )
        
    let outputs = System.Collections.Generic.List<string>()
    let outputHandler (_sender:obj) (args:DataReceivedEventArgs) = 
        if (args.Data = null |> not) then
            outputs.Add(args.Data)
            printfn ($"GIT: {args.Data}")
        
    let errorHandler (_sender:obj) (args:DataReceivedEventArgs) =  
        if (args.Data = null |> not) then
            let msg = args.Data.ToLower()
            outputs.Add(args.Data)
            printfn ($"GIT: {args.Data}")
        
    let p = new Process(StartInfo = procStartInfo)

    p.OutputDataReceived.AddHandler(DataReceivedEventHandler outputHandler)
    p.ErrorDataReceived.AddHandler(DataReceivedEventHandler errorHandler)
    p.Start() |> ignore
    p.BeginOutputReadLine()
    p.BeginErrorReadLine()
    p.WaitForExit()
    outputs

/// Executes Git command.
let executeGitCommand (repoDir : string) (command : string) =
        
    executeGitCommandWithResponse repoDir command |> ignore

let executeGitLFSHashCommand (repoDir : string) (filePath : string) = 
    // or "git cat-file -p :assays/measurement1/dataset/proteomics_result.csv"
    let p = $"{filePath.Trim().Replace('\\','/')}"
    executeGitCommandWithResponse repoDir $"lfs pointer --file {p}"

let tryGetGitLFSObject (repoDir : string) (filePath : string) =
    let output = executeGitLFSHashCommand repoDir filePath
    if output.Count = 0 then
        None
    else
        try
            Some (GitLFSObject.fromString (String.concat "\n" output))
        with
        | ex -> 
            printfn "Error parsing Git LFS object: %s" ex.Message
            None