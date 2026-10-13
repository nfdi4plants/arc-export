module DataHubContextTests

open System
open System.Collections.Generic
open System.Text.Json
open Xunit
open ARCtrl.ROCrate
open ARCtrl.Json

let private build values =
    let environment = Map.ofList values
    DataHubContext.build (fun name -> Map.tryFind name environment)

let private roundTrip graph =
    graph |> DataHubContext.serialize |> LDGraph.fromROCrateJsonString

let private value (graph : LDGraph) (node : LDNode) property =
    node.TryGetPropertyAsSingleton("http://schema.org/" + property, ?context = graph.TryGetContext())
    |> Option.map (fun value -> Assert.IsType<string>(value))

let private nodes (graph : LDGraph) (node : LDNode) property =
    node.GetPropertyNodes("http://schema.org/" + property, graph = graph, ?context = graph.TryGetContext())

[<Fact>]
let ``empty environment emits an independent context document`` () =
    let graph = build [] |> roundTrip
    let root = graph.GetNode(DataHubContext.filename)
    Assert.True(root.HasType(LDDataset.schemaType, ?context = graph.TryGetContext()))
    Assert.Equal(Some "1", value graph root "version")
    Assert.Equal(2, graph.Nodes.Count)
    Assert.Equal("#repository", (nodes graph root "about").[0].Id)
    Assert.Empty(nodes graph root "variableMeasured")
    Assert.False(graph.ContainsNode("ro-crate-metadata.json"))
    let mention = root.TryGetPropertyAsSingleton(LDDataset.mentions, ?context = graph.TryGetContext()).Value
    Assert.Equal("ro-crate-metadata.json", Assert.IsType<LDRef>(mention).Id)

[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
[<InlineData(3)>]
let ``zero one and multiple values survive ARCtrl compaction and roundtrip`` count =
    let input =
        [ "CI_PROJECT_ID", "007"
          "CI_PROJECT_DESCRIPTION", "  Ünicode \"quote\"\\path\nline\t🌱  "
          "CI_JOB_MANUAL", "false" ]
        |> List.take count
    let graph = build input |> roundTrip
    let root = graph.GetNode(DataHubContext.filename)
    let properties = nodes graph root "variableMeasured"
    Assert.Equal(count, properties.Count)
    for name, expected in input do
        let property = properties |> Seq.find (fun node -> value graph node "name" = Some name)
        Assert.True(property.HasType(LDPropertyValue.schemaType, ?context = graph.TryGetContext()))
        Assert.Equal(Some name, value graph property "propertyID")
        Assert.Equal(Some expected, value graph property "value")
    // Compact again to catch singleton/array handling regressions in graph output.
    let again = graph |> roundTrip
    Assert.Equal(count, (nodes again (again.GetNode(DataHubContext.filename)) "variableMeasured").Count)

[<Fact>]
let ``allowlist captures all specified families and never reads credential variables`` () =
    let requested = HashSet<string>(StringComparer.Ordinal)
    let graph =
        DataHubContext.build (fun name ->
            Assert.True(requested.Add(name), $"Read {name} more than once instead of using the export snapshot.")
            Some ("value of " + name))
        |> roundTrip
    let expected =
        [
            "CI_PROJECT_ID"; "CI_PROJECT_NAME"; "CI_PROJECT_TITLE"; "CI_PROJECT_DESCRIPTION"; "CI_PROJECT_URL"
            "CI_PROJECT_PATH"; "CI_PROJECT_NAMESPACE"; "CI_PROJECT_NAMESPACE_ID"; "CI_PROJECT_ROOT_NAMESPACE"
            "CI_PROJECT_VISIBILITY"; "CI_PROJECT_TOPICS"; "CI_PROJECT_REPOSITORY_LANGUAGES"; "CI_PROJECT_CLASSIFICATION_LABEL"
            "CI_SERVER_URL"; "CI_SERVER_NAME"; "CI_SERVER_HOST"; "CI_SERVER_PORT"; "CI_SERVER_PROTOCOL"; "CI_SERVER_FQDN"; "CI_SERVER_VERSION"
            "CI_COMMIT_SHA"; "CI_COMMIT_SHORT_SHA"; "CI_COMMIT_BEFORE_SHA"; "CI_COMMIT_REF_NAME"; "CI_COMMIT_REF_PROTECTED"
            "CI_COMMIT_BRANCH"; "CI_COMMIT_TAG"; "CI_COMMIT_TAG_MESSAGE"; "CI_COMMIT_TIMESTAMP"; "CI_COMMIT_TITLE"
            "CI_COMMIT_DESCRIPTION"; "CI_COMMIT_MESSAGE"; "CI_COMMIT_MESSAGE_IS_TRUNCATED"; "CI_COMMIT_AUTHOR"; "CI_COMMIT_USER_LOGIN"
            "GITLAB_USER_ID"; "GITLAB_USER_LOGIN"; "GITLAB_USER_NAME"; "GITLAB_USER_EMAIL"
            "CI_PIPELINE_ID"; "CI_PIPELINE_IID"; "CI_PIPELINE_NAME"; "CI_PIPELINE_URL"; "CI_PIPELINE_SOURCE"
            "CI_PIPELINE_CREATED_AT"; "CI_PIPELINE_SCHEDULE_DESCRIPTION"
            "CI_JOB_ID"; "CI_JOB_NAME"; "CI_JOB_GROUP_NAME"; "CI_JOB_URL"; "CI_JOB_STAGE"; "CI_JOB_STATUS"; "CI_JOB_STARTED_AT"; "CI_JOB_MANUAL"; "CI_JOB_IMAGE"
            "CI_MERGE_REQUEST_ID"; "CI_MERGE_REQUEST_IID"; "CI_MERGE_REQUEST_TITLE"; "CI_MERGE_REQUEST_DESCRIPTION"
            "CI_MERGE_REQUEST_DESCRIPTION_IS_TRUNCATED"; "CI_MERGE_REQUEST_PROJECT_ID"; "CI_MERGE_REQUEST_PROJECT_PATH"
            "CI_MERGE_REQUEST_PROJECT_URL"; "CI_MERGE_REQUEST_EVENT_TYPE"; "CI_MERGE_REQUEST_DIFF_BASE_SHA"
            "CI_MERGE_REQUEST_REF_PATH"; "CI_MERGE_REQUEST_SOURCE_BRANCH_NAME"; "CI_MERGE_REQUEST_SOURCE_BRANCH_SHA"
            "CI_MERGE_REQUEST_SOURCE_PROJECT_ID"; "CI_MERGE_REQUEST_SOURCE_PROJECT_PATH"; "CI_MERGE_REQUEST_SOURCE_PROJECT_URL"
            "CI_MERGE_REQUEST_TARGET_BRANCH_NAME"; "CI_MERGE_REQUEST_TARGET_BRANCH_SHA"; "CI_MERGE_REQUEST_LABELS"
            "CI_MERGE_REQUEST_ASSIGNEES"; "CI_MERGE_REQUEST_MILESTONE"; "CI_MERGE_REQUEST_APPROVED"; "CI_MERGE_REQUEST_DRAFT"
            "CI_DEFAULT_BRANCH"; "CI_RELEASE_DESCRIPTION"
        ]
    Assert.True(requested.SetEquals(expected), "Read exactly the documented metadata allowlist.")
    let properties = nodes graph (graph.GetNode(DataHubContext.filename)) "variableMeasured"
    Assert.Equal(expected.Length, properties.Count)
    for name in expected do
        let property = properties |> Seq.find (fun node -> value graph node "propertyID" = Some name)
        Assert.Equal(Some ("value of " + name), value graph property "value")
    for excluded in [ "CI_REPOSITORY_URL"; "CI_JOB_TOKEN"; "CI_DEPLOY_PASSWORD"; "PRIVATE_TOKEN"; "CUSTOM_VARIABLE" ] do
        Assert.DoesNotContain(excluded, requested)

[<Fact>]
let ``blank and unlisted values are excluded without trimming present values`` () =
    let graph =
        build [ "CI_PROJECT_ID", null
                "CI_PROJECT_NAME", " \t\r\n"
                "CI_COMMIT_AUTHOR", ""
                "GITLAB_USER_NAME", "   "
                "CI_PROJECT_DESCRIPTION", " keep these spaces "
                "CI_REPOSITORY_URL", "https://gitlab-ci-token:secret@example.org/repository.git"
                "CI_JOB_TOKEN", "secret"
                "CUSTOM_VARIABLE", "secret" ]
        |> roundTrip
    let root = graph.GetNode(DataHubContext.filename)
    let property = Assert.Single(nodes graph root "variableMeasured")
    Assert.Equal(Some " keep these spaces ", value graph property "value")
    Assert.False(graph.ContainsNode("#revision"))
    Assert.False(graph.ContainsNode("#commit-author"))
    Assert.False(graph.ContainsNode("#triggering-user"))
    Assert.DoesNotContain("secret", graph.ToROCrateJsonString())

[<Fact>]
let ``repository revision and separate people retain their provenance`` () =
    let author = "Ada Author <author@example.org>"
    let timestamp = "2026-10-13T10:11:12+02:00"
    let graph =
        build [ "CI_PROJECT_URL", "https://datahub.example.org/team/arc"
                "CI_PROJECT_ID", "42"
                "CI_PROJECT_TITLE", "ARC title"
                "CI_PROJECT_NAME", "arc"
                "CI_PROJECT_DESCRIPTION", "Repository description"
                "CI_COMMIT_SHA", "0123456789abcdef"
                "CI_COMMIT_TITLE", "Add samples"
                "CI_COMMIT_TIMESTAMP", timestamp
                "CI_COMMIT_AUTHOR", author
                "GITLAB_USER_ID", "23"
                "GITLAB_USER_LOGIN", "triggerer"
                "GITLAB_USER_NAME", "Pat Pipeline"
                "GITLAB_USER_EMAIL", "triggerer@example.org" ]
        |> roundTrip
    let root = graph.GetNode(DataHubContext.filename)
    let repository = Assert.Single(nodes graph root "about")
    Assert.True(repository.HasType(LDCreativeWork.schemaType, ?context = graph.TryGetContext()))
    Assert.Equal("https://datahub.example.org/team/arc", repository.Id)
    Assert.Equal(Some repository.Id, value graph repository "url")
    Assert.Equal(Some "42", value graph repository "identifier")
    Assert.Equal(Some "ARC title", value graph repository "name")
    Assert.Equal(Some "arc", value graph repository "alternateName")
    Assert.Equal(Some "Repository description", value graph repository "description")
    let revision = graph.GetNode("#revision")
    Assert.Equal(Some "0123456789abcdef", value graph revision "identifier")
    Assert.Equal(Some "Add samples", value graph revision "name")
    Assert.Equal(Some timestamp, value graph revision "dateCreated")
    Assert.Equal(repository.Id, (Assert.Single(nodes graph revision "isPartOf")).Id)
    let commitAuthor = Assert.Single(nodes graph revision "author")
    let mentioned = nodes graph root "mentions"
    Assert.Contains(mentioned, fun node -> node.Id = revision.Id)
    let triggeringUser = mentioned |> Seq.filter (fun node -> node.HasType(LDPerson.schemaType, ?context = graph.TryGetContext())) |> Assert.Single
    Assert.NotEqual<string>(commitAuthor.Id, triggeringUser.Id)
    Assert.True(commitAuthor.HasType(LDPerson.schemaType, ?context = graph.TryGetContext()))
    Assert.True(triggeringUser.HasType(LDPerson.schemaType, ?context = graph.TryGetContext()))
    Assert.Equal(Some "Ada Author", value graph commitAuthor "name")
    Assert.Equal(Some "author@example.org", value graph commitAuthor "email")
    Assert.Equal(Some author, value graph (graph.GetNode("#env-CI_COMMIT_AUTHOR")) "value")
    Assert.Equal(Some "23", value graph triggeringUser "identifier")
    Assert.Equal(Some "triggerer", value graph triggeringUser "alternateName")
    Assert.Equal(Some "Pat Pipeline", value graph triggeringUser "name")
    Assert.Equal(Some "triggerer@example.org", value graph triggeringUser "email")
    Assert.Equal(Some "User who started the pipeline.", value graph triggeringUser "description")

[<Theory>]
[<InlineData("true", "User who started this manual job.")>]
[<InlineData("false", "User who started the pipeline.")>]
let ``triggering user describes pipeline or manual job starter`` manual expected =
    let graph = build [ "CI_JOB_MANUAL", manual; "GITLAB_USER_LOGIN", "starter" ] |> roundTrip
    Assert.Equal(Some expected, value graph (graph.GetNode("#triggering-user")) "description")
    Assert.False(graph.ContainsNode("#commit-author"))
    Assert.False(graph.ContainsNode("#revision"))

[<Theory>]
[<InlineData("Ada Author")>]
[<InlineData("Ada Author <unparseable>")>]
[<InlineData("Ada <author@example.org> <other@example.org>")>]
let ``unstructured commit author remains intact without guessing an email`` author =
    let graph = build [ "CI_COMMIT_AUTHOR", author ] |> roundTrip
    let person = graph.GetNode("#commit-author")
    Assert.Equal(Some author, value graph person "name")
    Assert.Equal(None, value graph person "email")

[<Fact>]
let ``project name provides the title fallback in a partial environment`` () =
    let graph = build [ "CI_PROJECT_NAME", "arc"; "CI_COMMIT_USER_LOGIN", "committer" ] |> roundTrip
    Assert.Equal(Some "arc", value graph (graph.GetNode("#repository")) "name")
    Assert.False(graph.ContainsNode("#revision"))
    Assert.False(graph.ContainsNode("#commit-author"))
    Assert.False(graph.ContainsNode("#triggering-user"))

[<Fact>]
let ``inline vocabulary resolves every emitted type and property`` () =
    let graph = DataHubContext.build (fun name -> Some name)
    use document = JsonDocument.Parse(DataHubContext.serialize graph)
    Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("@context").ValueKind)
    let context = graph.TryGetContext().Value
    for node in graph.Nodes do
        for schemaType in node.SchemaType do
            Assert.Equal(Some ("http://schema.org/" + schemaType), context.TryResolveTerm(schemaType))
        for property in node.GetDynamicPropertyHelpers() do
            Assert.Equal(Some ("http://schema.org/" + property.Name), context.TryResolveTerm(property.Name))

[<Fact>]
let ``context graphs share no nodes or contexts with other exports`` () =
    let scientificNode = LDDataset.create("./", name = "Scientific dataset")
    let scientific = scientificNode.Flatten()
    scientific.SetContext(Context.initV1_1())
    scientific.Compact_InPlace()
    let before = scientific.ToROCrateJsonString(2)
    let first = build [ "CI_PROJECT_ID", "1" ]
    let second = build [ "CI_PROJECT_ID", "2" ]
    Assert.False(Object.ReferenceEquals(first.TryGetContext().Value, second.TryGetContext().Value))
    Assert.False(Object.ReferenceEquals(first.GetNode(DataHubContext.filename), second.GetNode(DataHubContext.filename)))
    first.TryGetContext().Value.AddMapping("unexpected", "http://example.org/unexpected")
    first.GetNode("#repository").SetProperty(LDCreativeWork.name, "Changed")
    first |> DataHubContext.serialize |> ignore
    Assert.Equal(None, second.TryGetContext().Value.TryResolveTerm("unexpected"))
    Assert.Equal(None, value second (second.GetNode("#repository")) "name")
    Assert.Equal(Some "2", value second (second.GetNode("#env-CI_PROJECT_ID")) "value")
    Assert.Equal(before, scientific.ToROCrateJsonString(2))
