module GitLFS

open System.Diagnostics
open System.Runtime.InteropServices
open System.IO

open System
open System.IO

/// This is a function to strictly check if a file is a git lfs pointer file. It was created due to
/// system out of memory issues with the tryfromString function
let isGitLfsPointerFile (filePath: string) =
    if not (File.Exists filePath) then
        printfn "file for Path does not exist: %s" filePath
        false
    else
        try
            // LFS pointer files are small (usually < 1 KB)
            let fileInfo = FileInfo(filePath)
            if fileInfo.Length > 2048L then
                false
            else
                let lines = File.ReadLines(filePath) |> Seq.truncate 3 |> Seq.toArray

                if lines.Length < 3 then
                    false
                else
                    let hasVersion =
                        lines.[0].StartsWith("version https://git-lfs.github.com/spec/")

                    let hasOid =
                        lines |> Array.exists (fun l -> l.StartsWith("oid sha256:"))

                    let hasSize =
                        lines |> Array.exists (fun l -> l.StartsWith("size "))

                    hasVersion && hasOid && hasSize
        with
        | _ -> false

open System.IO
open System.Text.Json

open System.Text.Json.Serialization


type GitLfsFile = { 
    name: string
    size: int64
    checkout: bool
    downloaded: bool
    [<JsonPropertyName("oid_type")>]
    oidType: string
    oid: string
    version: string 
}

type GitLfsJson = {
    files: GitLfsFile []
}

let deserializeGitLfsJson (json: string) =
    let options = JsonSerializerOptions()
    options.PropertyNameCaseInsensitive <- true

    JsonSerializer.Deserialize<GitLfsJson>(json, options)

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

    static member tryFromString (s: string) : GitLFSObject option =
        try 
            let parts = s.Split ([| "\n"; System.Environment.NewLine |], System.StringSplitOptions.RemoveEmptyEntries)
        
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
            Some { Version = version; Hash = hash; Size = size }
        with
        | ex -> 
            printfn "Error parsing Git LFS object: %s - (%s)" ex.Message s
            None

open System
open System.Diagnostics
open System.IO

type CommandResult =
    { ExitCode: int
      StdOut: string
      StdErr: string }

let runGit (repoDir: string) (args: string) : CommandResult =
    let psi =
        ProcessStartInfo(
            FileName = "git",
            Arguments = args,
            WorkingDirectory = repoDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        )

    use proc = new Process(StartInfo = psi)

    proc.Start() |> ignore

    // Read both streams fully BEFORE waiting
    let stdOut = proc.StandardOutput.ReadToEnd()
    let stdErr = proc.StandardError.ReadToEnd()

    proc.WaitForExit()

    { ExitCode = proc.ExitCode
      StdOut = stdOut
      StdErr = stdErr }
 
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
            // printfn ($"GIT: {args.Data}")
        
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
    printfn "%s" p
    executeGitCommandWithResponse repoDir $"lfs pointer --file=\"{p}\""

/// Gets the Git LFS object from a pointer file by simply reading it.
let tryGetGitLFSObjectFromPointerFile (repoDir : string) (filePath : string) =
    let fullPath = Path.Combine(repoDir, filePath)
    // check if file is pointer file
    if isGitLfsPointerFile fullPath |> not then
        
        None
    else
        File.ReadAllText fullPath
        |> GitLFSObject.tryFromString 

/// Gets the Git LFS object from the actual file by executing the git command.
let tryGetGitLFSObjectFromActualFile (repoDir : string) (filePath : string) =
    let output = executeGitLFSHashCommand repoDir filePath
    if output.Count = 0 then
        printf "Git LFS object not found for %s\n" filePath
        None
    else
        GitLFSObject.tryFromString (String.concat "\n" output)

let tryGetGitLFSObject (repoDir : string) (filePath : string) =
    // First try to get the LFS object from the pointer file
    let pointerObject = tryGetGitLFSObjectFromPointerFile repoDir filePath
    match pointerObject with
    | Some obj -> Some obj
    | None -> 
        // If that fails, try to get it from the actual file
        tryGetGitLFSObjectFromActualFile repoDir filePath

let tryCreateGitLfsJson (repoDir: string) =
    let output = 
        runGit repoDir "lfs ls-files -j"
    try
        deserializeGitLfsJson output.StdOut
        |> Some
    with
        | _ -> None

let normalizePathToGitPath (p: string) =
    p.Replace("\\", "/").Trim()
    |> fun p -> if p.StartsWith "./" then p.Substring(2) else p
    |> fun p -> p.Trim('/')

let tryGetPathFromGitLfsJson (p: string) (arr: GitLfsFile []) =
    arr
    |> Array.tryFind (fun lfs -> lfs.name = normalizePathToGitPath p) 