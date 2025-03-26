module PersonCleaning


open ARCtrl

let personIsEmpty (p : Person) =
    p.Address = None
    && p.EMail = None
    && p.FirstName = None
    && p.LastName = None
    && p.MidInitials = None
    && p.Phone = None
    && p.Roles.Count = 0
    && p.Affiliation = None
    && p.Fax = None
    && p.ORCID = None

type ARC with

    member this.CleanPersons () =
        let isa = this.ISA |> Option.get
        let newContacts = isa.Contacts |> ARCtrl.Helper.ResizeArray.filter (fun p -> not (personIsEmpty p))
        isa.Contacts <- newContacts
        isa.Studies
        |> Seq.iter (fun s -> 
            let newContacts = s.Contacts |> ARCtrl.Helper.ResizeArray.filter (fun p -> not (personIsEmpty p))
            s.Contacts <- newContacts
        )
        isa.Assays
        |> Seq.iter (fun a -> 
            let newContacts = a.Performers |> ARCtrl.Helper.ResizeArray.filter (fun p -> not (personIsEmpty p))
            a.Performers <- newContacts
        )       
        