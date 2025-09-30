module AssayRegistration

open ARCtrl


let getEntitiesOfTable (t : ArcTable) = 
    let cellsOfColOpt (col : CompositeColumn option) =
        match col with
        | Some c -> c.Cells
        | None -> ResizeArray()
    let tryNameOfCell (c : CompositeCell) =
        match c with
        | CompositeCell.FreeText "" -> None
        | CompositeCell.FreeText s -> Some s
        | CompositeCell.Data d -> 
            match d.Name with
            | Some "" -> None
            | Some s -> Some s
            | None -> None
        | _ -> None
    let inputs = 
        t.TryGetInputColumn() 
        |> cellsOfColOpt
        |> Seq.choose tryNameOfCell
    let outputs =
        t.TryGetOutputColumn() 
        |> cellsOfColOpt
        |> Seq.choose tryNameOfCell
    Seq.append inputs outputs

let getEntitiesOfTableSequence (tables : seq<ArcTable>) =
    tables
    |> Seq.collect getEntitiesOfTable
    |> Seq.distinct

let assayBelongsToStudy (assayEntities : string seq) (studyEntityset : string Set) =
    assayEntities
    |> Seq.exists (fun e -> Set.contains e studyEntityset)

let createStudyEntityMap (studies : ArcStudy seq) =
    let dict = System.Collections.Generic.Dictionary<string, ResizeArray<ArcStudy>>()
    studies
    |> Seq.iter (fun s -> 
        getEntitiesOfTableSequence s
        |> Seq.iter (fun e -> 
            if dict.ContainsKey(e) then
                dict.[e].Add(s)
            else
                dict.[e] <- ResizeArray [s]
        )   
    )
    dict

type ARC with

    member this.RegisterAssays () =
        let studyMap = 
            createStudyEntityMap this.Studies
        this.Assays
        |> Seq.iter (fun a -> 
            if a.StudiesRegisteredIn.Length = 0 then   
                let mutable wasRegistered = false
                let registeredStudes = System.Collections.Generic.HashSet<string>()
                getEntitiesOfTableSequence a
                |> Seq.iter (fun e -> 
                    if studyMap.ContainsKey(e) then
                        wasRegistered <- true
                        studyMap.[e]
                        |> Seq.iter (fun s ->
                            if not (registeredStudes.Contains(s.Identifier)) then
                                registeredStudes.Add(s.Identifier) |> ignore
                                s.RegisterAssay a.Identifier
                        )
                )
                if not wasRegistered then
                    let newStudy = ArcStudy($"PLACEHOLDER_STUDY_{a.Identifier}")
                    this.AddStudy newStudy
                    newStudy.RegisterAssay a.Identifier    
        )