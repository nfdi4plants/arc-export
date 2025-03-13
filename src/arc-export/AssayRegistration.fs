module AssayRegistration

open ARCtrl

let assayBelongsToStudy (assay:ArcAssay) (study:ArcStudy) =
    let cellsOfColOpt (col : CompositeColumn option) =
        match col with
        | Some c -> c.Cells
        | None -> [||]
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
    let getEntitiesOfTable (t : ArcTable) = 
        let inputs = 
            t.TryGetInputColumn() 
            |> cellsOfColOpt
            |> Seq.choose tryNameOfCell
        let outputs =
            t.TryGetOutputColumn() 
            |> cellsOfColOpt
            |> Seq.choose tryNameOfCell
        Seq.append inputs outputs
    let getEntities (tables : seq<ArcTable>) =
        tables
        |> Seq.collect (getEntitiesOfTable >> Seq.distinct)
        |> Seq.distinct
    let assayEntities = getEntities assay.Tables
    let studyEntities = getEntities study.Tables
    Seq.exists (fun e -> Seq.contains e studyEntities) assayEntities

type ARC with

    member this.RegisterAssays () =
        let isa = this.ISA |> Option.get
        isa.Assays
        |> Seq.iter (fun a -> 
            if a.StudiesRegisteredIn.Length = 0 then               
                let mutable wasRegistered = false
                isa.Studies
                |> Seq.iter (fun s -> 
                    if assayBelongsToStudy a s then
                        s.RegisterAssay a.Identifier
                        wasRegistered <- true
                )
                if not wasRegistered then
                    let newStudy = ArcStudy($"PLACEHOLDER_STUDY_{a.Identifier}")
                    isa.AddStudy newStudy
                    newStudy.RegisterAssay a.Identifier    
        )