module DataHubContext

open System
open System.Text.RegularExpressions
open ARCtrl.ROCrate
open ARCtrl.Json

[<Literal>]
let filename = "datahub-context.json"

// Read only known public CI metadata. In particular, CI_REPOSITORY_URL may contain
// a job token and must never be included by broad CI_* environment enumeration.
let private allowedVariables =
    [
        "CI_PROJECT_", [ "ID"; "NAME"; "TITLE"; "DESCRIPTION"; "URL"; "PATH"; "NAMESPACE"; "NAMESPACE_ID"; "ROOT_NAMESPACE"; "VISIBILITY"; "TOPICS"; "REPOSITORY_LANGUAGES"; "CLASSIFICATION_LABEL" ]
        "CI_SERVER_", [ "URL"; "NAME"; "HOST"; "PORT"; "PROTOCOL"; "FQDN"; "VERSION" ]
        "CI_COMMIT_", [ "SHA"; "SHORT_SHA"; "BEFORE_SHA"; "REF_NAME"; "REF_PROTECTED"; "BRANCH"; "TAG"; "TAG_MESSAGE"; "TIMESTAMP"; "TITLE"; "DESCRIPTION"; "MESSAGE"; "MESSAGE_IS_TRUNCATED"; "AUTHOR"; "USER_LOGIN" ]
        "GITLAB_USER_", [ "ID"; "LOGIN"; "NAME"; "EMAIL" ]
        "CI_PIPELINE_", [ "ID"; "IID"; "NAME"; "URL"; "SOURCE"; "CREATED_AT"; "SCHEDULE_DESCRIPTION" ]
        "CI_JOB_", [ "ID"; "NAME"; "GROUP_NAME"; "URL"; "STAGE"; "STATUS"; "STARTED_AT"; "MANUAL"; "IMAGE" ]
        "CI_MERGE_REQUEST_", [ "ID"; "IID"; "TITLE"; "DESCRIPTION"; "DESCRIPTION_IS_TRUNCATED"; "PROJECT_ID"; "PROJECT_PATH"; "PROJECT_URL"; "EVENT_TYPE"; "DIFF_BASE_SHA"; "REF_PATH"; "SOURCE_BRANCH_NAME"; "SOURCE_BRANCH_SHA"; "SOURCE_PROJECT_ID"; "SOURCE_PROJECT_PATH"; "SOURCE_PROJECT_URL"; "TARGET_BRANCH_NAME"; "TARGET_BRANCH_SHA"; "LABELS"; "ASSIGNEES"; "MILESTONE"; "APPROVED"; "DRAFT" ]
        "", [ "CI_DEFAULT_BRANCH"; "CI_RELEASE_DESCRIPTION" ]
    ]
    |> List.collect (fun (prefix, suffixes) -> suffixes |> List.map (fun suffix -> prefix + suffix))

let private schema term = "http://schema.org/" + term

let private createContext () =
    // An inline context keeps this document self-contained. Use the same IRIs as
    // ARCtrl's typed helpers, with a fresh context for every administrative graph.
    LDContext.fromMappingSeq [
        "Dataset", LDDataset.schemaType
        "CreativeWork", LDCreativeWork.schemaType
        "Person", LDPerson.schemaType
        "PropertyValue", LDPropertyValue.schemaType
        "name", LDCreativeWork.name
        "description", LDCreativeWork.description
        "identifier", LDCreativeWork.identifier
        "url", LDCreativeWork.url
        "dateCreated", LDCreativeWork.dateCreated
        "about", LDDataset.about
        "mentions", LDDataset.mentions
        "variableMeasured", LDDataset.variableMeasured
        "propertyID", LDPropertyValue.propertyID
        "value", LDPropertyValue.value
        "email", LDPerson.email
        "version", schema "version"
        "alternateName", schema "alternateName"
        "isPartOf", schema "isPartOf"
        "author", schema "author"
    ]

let private createCommitAuthor (rawAuthor : string) =
    // A full name is not necessarily a given/family-name pair. Only split the
    // conventional Git author format when its shape is unambiguous.
    let author = LDNode("#commit-author", ResizeArray [LDPerson.schemaType])
    let parsed = Regex.Match(rawAuthor, @"^\s*(?<name>[^<>\r\n]+?)\s*<(?<email>[^<>@\s]+@[^<>@\s]+)>\s*$")
    if parsed.Success && not (String.IsNullOrWhiteSpace(parsed.Groups.["name"].Value)) then
        author.SetProperty(LDCreativeWork.name, parsed.Groups.["name"].Value.Trim())
        LDPerson.setEmailAsString(author, parsed.Groups.["email"].Value)
    else
        author.SetProperty(LDCreativeWork.name, rawAuthor)
    author

/// Build a standalone administrative JSON-LD graph from a snapshot of the
/// allowlisted environment values. Missing/blank values are omitted; other values
/// are retained verbatim in PropertyValue nodes, including numeric/boolean text.
let build (lookup : string -> string option) : LDGraph =
    let values =
        allowedVariables
        |> List.choose (fun name ->
            lookup name
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.map (fun value -> name, value))
        |> Map.ofList
    let get name = Map.tryFind name values
    let graph = LDGraph(context = createContext ())
    let contextNode = LDDataset.create(filename)
    contextNode.SetProperty(schema "version", "1")
    let mentions = ResizeArray [LDRef("ro-crate-metadata.json")]

    let repository =
        LDCreativeWork.create(
            get "CI_PROJECT_URL" |> Option.defaultValue "#repository",
            ?identifier = get "CI_PROJECT_ID",
            ?name = (get "CI_PROJECT_TITLE" |> Option.orElseWith (fun () -> get "CI_PROJECT_NAME")),
            ?description = get "CI_PROJECT_DESCRIPTION",
            ?url = get "CI_PROJECT_URL")
    match get "CI_PROJECT_TITLE", get "CI_PROJECT_NAME" with
    | Some title, Some name when title <> name -> repository.SetProperty(schema "alternateName", name)
    | _ -> ()
    graph.AddNode(repository)
    contextNode.SetProperty(LDDataset.about, LDRef(repository.Id))

    let commitAuthor = get "CI_COMMIT_AUTHOR" |> Option.map createCommitAuthor
    let hasRevision =
        [ "CI_COMMIT_SHA"; "CI_COMMIT_TITLE"; "CI_COMMIT_TIMESTAMP"; "CI_COMMIT_AUTHOR" ]
        |> List.exists (fun name -> values.ContainsKey(name))
    if hasRevision then
        let revision =
            LDCreativeWork.create("#revision", ?identifier = get "CI_COMMIT_SHA", ?name = get "CI_COMMIT_TITLE")
        // Preserve GitLab's timestamp string, including its original UTC offset.
        revision.SetOptionalProperty(LDCreativeWork.dateCreated, get "CI_COMMIT_TIMESTAMP")
        revision.SetProperty(schema "isPartOf", LDRef(repository.Id))
        commitAuthor |> Option.iter (fun author ->
            graph.AddNode(author)
            revision.SetProperty(schema "author", LDRef(author.Id)))
        graph.AddNode(revision)
        mentions.Add(LDRef(revision.Id))

    let hasTriggeringUser =
        [ "GITLAB_USER_ID"; "GITLAB_USER_LOGIN"; "GITLAB_USER_NAME"; "GITLAB_USER_EMAIL" ]
        |> List.exists (fun name -> values.ContainsKey(name))
    if hasTriggeringUser then
        let user = LDNode("#triggering-user", ResizeArray [LDPerson.schemaType])
        user.SetOptionalProperty(LDPerson.identifier, get "GITLAB_USER_ID")
        user.SetOptionalProperty(schema "alternateName", get "GITLAB_USER_LOGIN")
        user.SetOptionalProperty(LDCreativeWork.name, get "GITLAB_USER_NAME")
        get "GITLAB_USER_EMAIL" |> Option.iter (fun email -> LDPerson.setEmailAsString(user, email))
        let description =
            match get "CI_JOB_MANUAL" with
            | Some value when String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) -> "User who started this manual job."
            | _ -> "User who started the pipeline."
        user.SetProperty(LDCreativeWork.description, description)
        graph.AddNode(user)
        mentions.Add(LDRef(user.Id))

    let measurements = ResizeArray<LDRef>()
    for KeyValue(name, value) in values do
        let property = LDPropertyValue.create(name, value = value, propertyID = name, id = "#env-" + name)
        graph.AddNode(property)
        measurements.Add(LDRef(property.Id))
    if measurements.Count > 0 then
        contextNode.SetProperty(LDDataset.variableMeasured, measurements)
    contextNode.SetProperty(LDDataset.mentions, mentions)
    graph.AddNode(contextNode)
    graph

/// Compact and serialize with ARCtrl's JSON-LD implementation, as for the
/// scientific RO-Crate graph. This graph has no scientific crate descriptor.
let serialize (graph : LDGraph) =
    graph.Compact_InPlace()
    graph.ToROCrateJsonString(2)
