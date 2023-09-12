module ARCExtension

open FileSystemTreeExtension

open ARCtrl
open ARCtrl.FileSystem


//temporarily add implementations from https://github.com/nfdi4plants/ARCtrl/pull/189
type ARC with
    /// <summary>
    /// Returns the FileSystemTree of the ARC with only the registered files and folders included.
    /// </summary>
    /// <param name="IgnoreHidden">Wether or not to ignore hidden files and folders starting with '.'. If true, no hidden files are included in the result. (default: true)</param>
    member this.GetRegisteredPayload(?IgnoreHidden:bool) =

        let isaCopy = this.ISA |> Option.map (fun i -> i.Copy()) // not sure if needed, but let's be safe

        let registeredStudies =     
            isaCopy
            |> Option.map (fun isa -> isa.Studies.ToArray()) // to-do: isa.RegisteredStudies
            |> Option.defaultValue [||]
        
        let registeredAssays =     
            registeredStudies
            |> Array.map (fun s -> s.Assays.ToArray()) // to-do: s.RegisteredAssays
            |> Array.concat

        let includeRootFiles : Set<string> = 
            set [
                "isa.investigation.xlsx"
                "README.md"
            ]

        let includeStudyFiles = 
            registeredStudies
            |> Array.map (fun s -> 
                let studyFoldername = $"studies/{s.Identifier}"

                set [
                    yield $"{studyFoldername}/isa.study.xlsx"
                    yield $"{studyFoldername}/README.md"

                    //just allow any constructed path from cell values. there may be occasions where this includes wrong files, but its good enough for now.
                    for (kv) in s.Tables[0].Values do
                        yield kv.Value.AsFreeText // from arc root
                        yield $"{studyFoldername}/resources/{kv.Value.AsFreeText}" // from study root > resources
                        yield $"{studyFoldername}/protocols/{kv.Value.AsFreeText}" // from study root > protocols
                ]
            )
            |> Set.unionMany

        let includeAssayFiles = 
            registeredAssays
            |> Array.map (fun a -> 
                let assayFoldername = $"assays/{a.Identifier}"

                set [
                    yield $"{assayFoldername}/isa.assay.xlsx"
                    yield $"{assayFoldername}/README.md"

                    //just allow any constructed path from cell values. there may be occasions where this includes wrong files, but its good enough for now.
                    for (kv) in a.Tables[0].Values do
                        yield kv.Value.AsFreeText // from arc root
                        yield $"{assayFoldername}/dataset/{kv.Value.AsFreeText}" // from assay root > dataset
                        yield $"{assayFoldername}/protocols/{kv.Value.AsFreeText}" // from assay root > protocols
                ]
            )
            |> Set.unionMany


        let includeFiles = Set.unionMany [includeRootFiles; includeStudyFiles; includeAssayFiles]

        let ignoreHidden = defaultArg IgnoreHidden true
        let fsCopy = this.FileSystem.Copy() // not sure if needed, but let's be safe

        fsCopy.Tree
        |> FileSystemTree.toFilePaths()
        |> Array.filter (fun p -> 
            p.StartsWith("workflows") 
            || p.StartsWith("runs") 
            || includeFiles.Contains(p)
        )
        |> FileSystemTree.fromFilePaths
        |> fun tree -> if ignoreHidden then tree |> FileSystemTree.filterFiles (fun n -> not (n.StartsWith("."))) else Some tree
        |> Option.bind (fun tree -> if ignoreHidden then tree |> FileSystemTree.filterFolders (fun n -> not (n.StartsWith("."))) else Some tree)
        |> Option.defaultValue (FileSystemTree.fromFilePaths [||])

    /// <summary>
    /// Returns the FileSystemTree of the ARC with only and folders included that are considered additional payload.
    /// </summary>
    /// <param name="IgnoreHidden">Wether or not to ignore hidden files and folders starting with '.'. If true, no hidden files are included in the result. (default: true)</param>

    member this.GetAdditionalPayload(?IgnoreHidden:bool) =
        let ignoreHidden = defaultArg IgnoreHidden true
        let registeredPayload = 
            this.GetRegisteredPayload()
            |> FileSystemTree.toFilePaths()
            |> set

        this.FileSystem.Copy().Tree
        |> FileSystemTree.toFilePaths()
        |> Array.filter (fun p -> not (registeredPayload.Contains(p)))
        |> FileSystemTree.fromFilePaths
        |> fun tree -> if ignoreHidden then tree |> FileSystemTree.filterFiles (fun n -> not (n.StartsWith("."))) else Some tree
        |> Option.bind (fun tree -> if ignoreHidden then tree |> FileSystemTree.filterFolders (fun n -> not (n.StartsWith("."))) else Some tree)
        |> Option.defaultValue (FileSystemTree.fromFilePaths [||])