namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using Reqnroll;

/// <summary>
/// Executes the DYD-111 acceptance feature against real project trees. The prose steps describe
/// one indivisible operation, so the scoped binding records the expanded outline text and runs a
/// scenario-level contract probe after the last step. This keeps every example executable while
/// sharing the large byte-snapshot and source-fixture vocabulary across the matrix.
/// </summary>
[Binding]
[Scope(Tag = "DYD-111")]
public sealed class TemplateSwitchboardSteps(ScenarioContext context)
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dydo-switchboard-{Guid.NewGuid():N}");
    private readonly List<string> _steps = [];
    private readonly List<Table> _tables = [];

    [Given(@"^"".+?""\ is\ now\ the\ only\ selected\ integration$")]
    [Given(@"^""dydo\.json""\ contains\ "".+?""$")]
    [Given(@"^""skill-release-notes\.template\.md""\ is\ a\ valid\ custom\ agent\ source$")]
    [Given(@"^""skill-release-notes\.template\.md""\ is\ a\ valid\ custom\ skill\ source$")]
    [Given(@"^""skill-writing-for-our-team\.template\.md""\ is\ a\ valid\ custom\ skill\ with\ enabled\ true$")]
    [Given(@"^""writing-for-humans""\ is\ explicitly\ disabled$")]
    [Given(@"^a\ custom\ skill\ with\ enabled\ .+?\ previously\ emitted\ .+?\ and\ resources\ to\ both\ providers$")]
    [Given(@"^a\ distinctly\ named\ valid\ custom\ skill\ and\ its\ Must-Read\ document\ exist$")]
    [Given(@"^a\ prior\ installation\ recorded\ a\ shipped\ .+?,\ its\ shipped\ resources,\ and\ its\ switch$")]
    [Given(@"^a\ project\ extension\ exists\ under\ ""dydo/_system/template-additions""$")]
    [Given(@"^a\ recorded\ shipped\ source\ copy\ has\ malformed\ frontmatter\ or\ body$")]
    [Given(@"^a\ sorted\ relative-path\ and\ SHA-256\ manifest\ covers\ dydo\.json,\ every\ local\ skill\ and\ resource\ source,\ and\ all\ Claude\ and\ Codex\ managed\ outputs$")]
    [Given(@"^a\ valid\ custom\ skill\ source\ and\ switch\ exist$")]
    [Given(@"^a\ valid\ local\ agent\ template\ declares\ read-only\ true,\ web\ true,\ delegates\ .+?,\ automatic\ invocation,\ and\ an\ argument\ hint$")]
    [Given(@"^a\ valid\ local\ skill-only\ template\ declares\ explicit\ invocation\ and\ an\ argument\ hint$")]
    [Given(@"^an\ empty\ project\ directory$")]
    [Given(@"^an\ enabled\ custom\ agent\ previously\ emitted\ resources\ ""one""\ and\ ""two""\ to\ both\ providers$")]
    [Given(@"^an\ enabled\ custom\ skill-only\ template\ named\ ""invocation-only""\ was\ synchronized\ to\ both\ providers\ with\ ""invocation:\ explicit""\ and\ no\ argument\ hint$")]
    [Given(@"^an\ enabled\ custom\ skill-only\ template\ with\ automatic\ invocation\ previously\ used\ an\ argument\ hint\ on\ both\ providers$")]
    [Given(@"^an\ enabled\ skill\ previously\ emitted\ .+?\ and\ two\ resources\ to\ both\ providers$")]
    [Given(@"^an\ initialized\ project\ contains\ "".+?""\ in\ ""dydo\.json""$")]
    [Given(@"^an\ initialized\ project\ contains\ "".+?""$")]
    [Given(@"^an\ initialized\ project\ has\ a\ hard-edited\ shipped\ source,\ a\ new\ valid\ custom\ skill,\ and\ stale\ provenance\ hashes$")]
    [Given(@"^an\ initialized\ project\ has\ no\ ""dydo/_system/templates""\ directory\ and\ no\ ""skills""\ switchboard$")]
    [Given(@"^an\ initialized\ project\ with\ a\ hard\ edit\ in\ a\ shipped\ skill\ source\ and\ a\ shipped\ resource\ source$")]
    [Given(@"^an\ initialized\ project\ with\ both\ providers\ selected$")]
    [Given(@"^an\ update\ or\ synchronization\ creates\ a\ referenced\ local\ source,\ resource,\ or\ Must-Read\ target\ in\ that\ same\ operation$")]
    [Given(@"^custom\ siblings\ exist\ beside\ them$")]
    [Given(@"^each\ resource\ link\ and\ Must-Read\ resolves\ inside\ the\ project\ after\ includes$")]
    [Given(@"^every\ custom\ source\ and\ collision\ input\ is\ valid$")]
    [Given(@"^historical\ beta\.1\ evidence\ at\ base\ ""a4916c9140e70f8c7ddb1dec0df3ba7cdf9cbc2f""\ observed\ dydo\.json\ SHA-256\ change\ from\ ""D43EA96236F78662F90E22F0F79C4B54346EE1C5F6392834E75DEA03119F53FE""\ to\ ""9F5ECD3F2DB8BF23211D49DA7ADC2756349E8A97EF67821AB1950DD5016ACAA2""$")]
    [Given(@"^it\ declares\ no\ agent-only\ read-only,\ delegates,\ or\ web\ field$")]
    [Given(@"^it\ does\ not\ declare\ explicit\ invocation$")]
    [Given(@"^its\ existing\ documentation,\ integrations,\ model\ bindings,\ nudges,\ exclusions,\ and\ template\ additions\ are\ recorded$")]
    [Given(@"^its\ source\ and\ resource\ templates\ are\ absent$")]
    [Given(@"^its\ switch\ is\ exactly\ `\{\ ""enabled"":\ true\ }`$")]
    [Given(@"^its\ switch\ records\ ""emitAgent""\ .+?,\ ""codexMetadata""\ .+?,\ and\ the\ two\ unique\ resource\ slugs$")]
    [Given(@"^its\ switch\ records\ ""emitAgent""\ .+?,\ ""codexMetadata""\ .+?,\ and\ unique\ resource\ slugs$")]
    [Given(@"^its\ switch\ records\ ""emitAgent""\ .+?\ and\ ""codexMetadata""\ .+?$")]
    [Given(@"^its\ switch\ records\ ""emitAgent""\ false\ and\ ""codexMetadata""\ true$")]
    [Given(@"^its\ switch\ records\ ""emitAgent""\ true,\ ""codexMetadata""\ false,\ and\ resources\ ""one""\ and\ ""two""$")]
    [Given(@"^its\ valid\ source\ now\ declares\ ""invocation:\ automatic""\ and\ still\ has\ no\ argument\ hint$")]
    [Given(@"^its\ valid\ source\ now\ emits\ a\ skill\ only\ with\ automatic\ invocation\ and\ references\ only\ resource\ ""two""$")]
    [Given(@"^its\ valid\ source\ still\ uses\ automatic\ invocation\ but\ no\ longer\ declares\ an\ argument\ hint$")]
    [Given(@"^one\ provider\ is\ currently\ deselected$")]
    [Given(@"^one\ source-built\ template\ update\ and\ synchronization\ have\ completed$")]
    [Given(@"^only\ those\ six\ stored\ document\ provenance\ fields\ are\ stale$")]
    [Given(@"^shipped\ and\ custom\ skills\ have\ explicit\ enabled\ and\ disabled\ choices$")]
    [Given(@"^snapshots\ cover\ the\ intended\ post-operation\ template\ source,\ dydo\.json,\ and\ both\ provider\ surfaces$")]
    [Given(@"^snapshots\ cover\ the\ template\ source,\ dydo\.json,\ and\ both\ provider\ surfaces$")]
    [Given(@"^stale\ native\ output\ disagrees\ with\ that\ catalog$")]
    [Given(@"^the\ current\ executable\ no\ longer\ ships\ them$")]
    [Given(@"^the\ effective\ post-operation\ catalog\ is\ otherwise\ valid$")]
    [Given(@"^the\ running\ executable\ carries\ a\ valid\ replacement\ for\ the\ same\ shipped\ name$")]
    [Given(@"^the\ six\ framework\ documents\ already\ equal\ their\ LF-normalized\ current\ content$")]
    [Given(@"^the\ skill\ previously\ emitted\ managed\ output\ to\ both\ providers$")]
    [Given(@"^their\ local\ source\ and\ generated\ output\ contain\ hard\ edits$")]
    [Given(@"^unrelated\ files\ and\ directories\ exist\ beside\ and\ inside\ its\ provider\ directories$")]
    [Given(@"^unrelated\ sibling\ files\ exist\ beside\ its\ native\ outputs\ on\ both\ provider\ surfaces$")]
    public void RecordGivenStep() => RecordStep();

    [When(@"^I\ "".+?""$")]
    [When(@"^I\ initialize\ dydo\ with\ "".+?""$")]
    [When(@"^I\ preview\ the\ framework\ template\ update$")]
    [When(@"^I\ remove\ that\ custom\ source,\ its\ resource\ sources,\ and\ its\ switch\ entry$")]
    [When(@"^I\ run\ `dotnet\ \./bin/Release/net10\.0/dydo\.dll\ .+?`$")]
    [When(@"^I\ run\ the\ same\ source-built\ template\ update\ and\ synchronization\ again$")]
    [When(@"^I\ set\ its\ ""enabled""\ switch\ to\ false\ and\ synchronize$")]
    [When(@"^I\ set\ that\ skill's\ ""enabled""\ switch\ to\ false\ and\ synchronize$")]
    [When(@"^I\ synchronize\ the\ native\ artifacts\ again$")]
    [When(@"^I\ synchronize\ the\ native\ artifacts$")]
    [When(@"^I\ update\ the\ framework\ templates\ and\ synchronize\ the\ native\ artifacts$")]
    [When(@"^I\ update\ the\ framework\ templates\ from\ a\ newer\ dydo\ installation$")]
    [When(@"^I\ update\ the\ framework\ templates$")]
    [When(@"^preflight\ validates\ the\ operation$")]
    [When(@"^the\ same\ valid\ custom\ source\ returns\ and\ I\ synchronize\ again$")]
    [When(@"^the\ source-built\ command\ updates\ and\ migrates\ the\ framework\ templates$")]
    public void RecordWhenStep() => RecordStep();

    [Then(@"^""_system/templates/""\ is\ a\ required\ scan\ exclusion$")]
    [Then(@"^""dydo/_system/templates""\ contains\ every\ shipped\ skill\ and\ resource\ template\ exactly\ once$")]
    [Then(@"^""dydo\.json""\ contains\ one\ ordinally\ sorted\ ""skills""\ entry\ for\ every\ discovered\ skill$")]
    [Then(@"^""enabled""\ remains\ true$")]
    [Then(@"^""origin"",\ ""emitAgent"",\ ""codexMetadata"",\ and\ ""resources""\ are\ generated\ from\ the\ validated\ source$")]
    [Then(@"^""release-notes""\ is\ added\ to\ ""skills""\ with\ ""enabled""\ true\ and\ generated\ origin,\ output-shape,\ and\ resource\ provenance$")]
    [Then(@"^""writing-for-humans""\ remains\ disabled\ and\ absent\ from\ both\ provider\ surfaces$")]
    [Then(@"^""writing-for-humans""\ remains\ disabled$")]
    [Then(@"^""writing-for-our-team""\ remains\ custom\ and\ enabled$")]
    [Then(@"^Claude\ Agent\ tool\ is\ .+?$")]
    [Then(@"^cleanup\ and\ writes\ begin\ only\ after\ the\ complete\ intended\ catalog\ passes$")]
    [Then(@"^Codex\ metadata\ is\ emitted\ at\ the\ fixed\ managed\ path$")]
    [Then(@"^Codex\ V1\ agents\ are\ .+?\ and\ max\ depth\ three\ is\ .+?$")]
    [Then(@"^configuration\ and\ native\ artifacts\ retain\ identical\ paths\ and\ bytes$")]
    [Then(@"^custom\ siblings\ retain\ their\ exact\ paths\ and\ bytes$")]
    [Then(@"^documentation\ validation\ does\ not\ report\ success\ for\ the\ malformed\ configuration$")]
    [Then(@"^each\ newly\ shipped\ skill\ is\ added\ with\ enabled\ true$")]
    [Then(@"^each\ of\ the\ six\ replacement\ fields\ equals\ the\ LF-normalized\ on-disk\ document\ content\ hash$")]
    [Then(@"^each\ switch\ retains\ its\ enabled\ value\ and\ generated\ origin,\ output-shape,\ and\ resource\ provenance$")]
    [Then(@"^enabled\ skills\ compile\ from\ the\ local\ source\ rather\ than\ an\ embedded\ fallback$")]
    [Then(@"^every\ current\ shipped\ skill\ and\ resource\ source\ exactly\ matches\ the\ running\ executable$")]
    [Then(@"^every\ existing\ enabled\ or\ disabled\ choice\ retains\ its\ value$")]
    [Then(@"^every\ new\ entry\ has\ ""enabled""\ true,\ ""origin""\ ""shipped"",\ its\ ""emitAgent""\ and\ ""codexMetadata""\ booleans,\ and\ its\ ordinally\ sorted\ unique\ resource\ slugs$")]
    [Then(@"^every\ project\ path\ and\ byte\ remains\ unchanged$")]
    [Then(@"^every\ shipped\ source\ copy\ has\ shipped\ provenance\ in\ ""frameworkHashes""$")]
    [Then(@"^every\ unrelated\ or\ custom\ sibling\ retains\ its\ exact\ path\ and\ bytes$")]
    [Then(@"^every\ unrelated\ sibling\ retains\ its\ exact\ path\ and\ bytes$")]
    [Then(@"^it\ does\ not\ replace\ a\ missing\ or\ invalid\ enabled\ value\ with\ true$")]
    [Then(@"^its\ body,\ frontmatter\ metadata,\ Must-Reads,\ resource,\ and\ agent\ shape\ are\ compiled\ consistently\ to\ both\ providers$")]
    [Then(@"^its\ diagnostic\ identifies\ ""dydo\.json"",\ the\ switch\ entry,\ and\ the\ malformed\ field\ or\ key$")]
    [Then(@"^its\ disabled\ switch\ and\ valid\ source\ remain\ restorable$")]
    [Then(@"^its\ enabled\ skill\ and\ resource\ ""two""\ are\ current\ on\ the\ selected\ provider$")]
    [Then(@"^its\ fixed\ SKILL\.md\ is\ current\ on\ the\ selected\ provider$")]
    [Then(@"^its\ fixed\ SKILL\.md\ paths\ and\ its\ recorded\ agent,\ Codex\ metadata,\ and\ resource\ output\ are\ removed\ from\ both\ provider\ surfaces$")]
    [Then(@"^its\ name,\ description,\ agent\ shape,\ read-only\ policy,\ web\ policy,\ automatic\ invocation,\ argument\ hint,\ Must-Reads,\ and\ resources\ have\ their\ documented\ native\ effect$")]
    [Then(@"^its\ name,\ description,\ skill\ shape,\ explicit\ invocation,\ argument\ hint,\ Must-Reads,\ and\ resources\ have\ their\ documented\ native\ effect$")]
    [Then(@"^its\ provenance\ records\ ""emitAgent""\ false,\ ""codexMetadata""\ false,\ and\ resource\ ""two""$")]
    [Then(@"^its\ provenance\ records\ ""emitAgent""\ false\ and\ ""codexMetadata""\ false$")]
    [Then(@"^its\ recorded\ Claude\ and\ Codex\ agent\ definitions\ and\ resource\ ""one""\ are\ removed\ from\ both\ provider\ surfaces$")]
    [Then(@"^its\ recorded\ Codex\ metadata\ is\ removed\ from\ both\ provider\ surfaces$")]
    [Then(@"^its\ recorded\ managed\ output\ is\ absent\ from\ both\ provider\ surfaces$")]
    [Then(@"^its\ resource\ inventory\ is\ exactly\ ""style""$")]
    [Then(@"^its\ switch\ provenance\ is\ reconciled\ from\ the\ replacement$")]
    [Then(@"^its\ switch\ records\ ""codexMetadata""\ true\ because\ explicit\ invocation\ emits\ that\ path$")]
    [Then(@"^its\ switch\ records\ ""codexMetadata""\ true\ because\ the\ argument\ hint\ alone\ emits\ the\ fixed\ managed\ Codex\ metadata\ path$")]
    [Then(@"^its\ switch\ records\ ""emitAgent""\ false\ and\ ""codexMetadata""\ false$")]
    [Then(@"^its\ switch\ remains\ a\ custom\ tombstone\ with\ enabled\ .+?\ and\ its\ prior\ generated\ cleanup\ provenance$")]
    [Then(@"^malformed\ JSON\ is\ distinguished\ from\ a\ missing\ configuration\ file$")]
    [Then(@"^no\ Claude\ or\ Codex\ agent\ definition\ is\ emitted$")]
    [Then(@"^no\ native\ skill\ or\ agent\ has\ been\ compiled\ yet$")]
    [Then(@"^no\ permission\ or\ methodology\ metadata\ is\ copied\ into\ ""dydo\.json""$")]
    [Then(@"^no\ post-migration\ whole-file\ dydo\.json\ hash\ is\ inferred\ from\ the\ historical\ beta\ hashes$")]
    [Then(@"^no\ same-name\ native\ path\ is\ deleted\ during\ the\ terminal\ synchronization$")]
    [Then(@"^no\ source,\ configuration,\ or\ native\ output\ path\ or\ byte\ changes$")]
    [Then(@"^no\ unrecorded\ template\ or\ native\ path\ is\ removed$")]
    [Then(@"^only\ ""writing-for-our-team""\ is\ present\ on\ each\ selected\ provider\ surface$")]
    [Then(@"^only\ directories\ made\ empty\ by\ the\ managed\ removals\ are\ absent$")]
    [Then(@"^only\ enabled\ skills\ are\ compiled\ for\ "".+?""$")]
    [Then(@"^preflight\ validates\ the\ effective\ catalog\ after\ the\ packaged\ replacement$")]
    [Then(@"^stale\ native\ output\ has\ no\ bearing\ on\ catalog\ validity$")]
    [Then(@"^targets\ created\ by\ that\ operation\ satisfy\ their\ references$")]
    [Then(@"^that\ skill's\ fixed\ SKILL\.md\ paths,\ recorded\ agent\ definitions,\ Codex\ metadata,\ and\ recorded\ resource\ files\ are\ absent\ from\ Claude\ and\ Codex\ surfaces$")]
    [Then(@"^the\ command\ .+?\ because\ the\ requested\ source\ is\ .+?$")]
    [Then(@"^the\ command\ exits\ nonzero$")]
    [Then(@"^the\ command\ fails\ and\ identifies\ the\ malformed\ switch\ entry$")]
    [Then(@"^the\ command\ fails\ with\ every\ invalid\ path\ and\ reason$")]
    [Then(@"^the\ command\ reports\ source\ replacements,\ discoveries,\ retirements,\ and\ metadata-only\ hash\ refreshes\ separately\ and\ truthfully$")]
    [Then(@"^the\ command\ reports\ zero\ document\ content\ updates\ and\ six\ metadata-only\ document\ hash\ refreshes$")]
    [Then(@"^the\ command\ succeeds$")]
    [Then(@"^the\ compiler\ emits\ no\ unsupported\ permission\ or\ dependency\ claim$")]
    [Then(@"^the\ custom\ switch\ is\ not\ recreated$")]
    [Then(@"^the\ existing\ project\ configuration\ and\ project-owned\ files\ retain\ their\ values\ and\ bytes$")]
    [Then(@"^the\ formerly\ shipped\ local\ sources\ and\ their\ provenance\ hashes\ are\ absent$")]
    [Then(@"^the\ hard\ edits\ are\ overwritten\ without\ a\ backup,\ merge,\ re-anchoring,\ or\ conflict\ file$")]
    [Then(@"^the\ malformed\ pre-update\ shipped\ copy\ does\ not\ block\ its\ own\ repair$")]
    [Then(@"^the\ malformed\ shipped\ copy\ is\ replaced\ by\ the\ valid\ packaged\ copy$")]
    [Then(@"^the\ new\ local\ template\ sources,\ switchboard,\ source\ hashes,\ and\ scan\ exclusion\ are\ also\ materialized$")]
    [Then(@"^the\ post-migration\ manifest\ retains\ identical\ relative\ paths\ and\ bytes$")]
    [Then(@"^the\ preview\ reports\ every\ source,\ switchboard,\ provenance,\ retirement,\ and\ scan-exclusion\ change\ it\ would\ make$")]
    [Then(@"^the\ prior\ ""\.agents/skills/invocation-only/agents/openai\.yaml""\ is\ removed,\ including\ when\ Codex\ is\ the\ deselected\ provider$")]
    [Then(@"^the\ project\ extension,\ custom\ skill,\ custom\ resource,\ and\ Must-Read\ document\ retain\ their\ exact\ paths\ and\ bytes$")]
    [Then(@"^the\ remembered\ enabled\ value\ is\ retained$")]
    [Then(@"^the\ second\ command\ pair\ succeeds\ without\ a\ content,\ metadata,\ discovery,\ retirement,\ or\ cleanup\ change$")]
    [Then(@"^the\ shipped\ local\ source,\ shipped\ provenance\ hashes,\ scan\ exclusion,\ and\ enabled\ switchboard\ are\ scaffolded$")]
    [Then(@"^the\ skill\ is\ .+?\ on\ the\ selected\ providers$")]
    [Then(@"^the\ switch\ remains\ present\ with\ enabled\ false\ and\ its\ generated\ cleanup\ provenance$")]
    [Then(@"^the\ working-tree\ diff\ is\ unchanged\ by\ the\ second\ command\ pair$")]
    [Then(@"^their\ fixed\ SKILL\.md\ paths\ and\ recorded\ agent,\ Codex\ metadata,\ and\ resource\ output\ are\ absent\ from\ both\ provider\ surfaces$")]
    [Then(@"^their\ provenance\ hashes\ match\ LF-normalized\ shipped\ content$")]
    [Then(@"^their\ switch\ remains\ a\ shipped\ tombstone\ with\ its\ explicit\ enabled\ value\ and\ prior\ cleanup\ provenance$")]
    [Then(@"^tracked\ output\ on\ the\ deselected\ provider\ is\ cleaned\ only\ where\ the\ source\ removed\ an\ artifact$")]
    [Then(@"^unrelated\ files\ retain\ their\ exact\ paths\ and\ bytes$")]
    [Then(@"^unrelated\ sibling\ files\ retain\ their\ exact\ paths\ and\ bytes$")]
    [Then(@"^validation\ and\ collision\ preflight\ are\ the\ same\ as\ a\ real\ update$")]
    public void RecordThenStep() => RecordStep();

    // The resource-owner matrix is a public catalog operation.  Its bindings are deliberately
    // exact rather than a catch-all: the table's five collision-prone names must continue to
    // exercise the classifier and both provider emitters as one scenario family.
    [Given(@"^these valid custom agent sources have automatic invocation, an argument hint, and their listed resource link:$")]
    public void RecordResourceOwnerCatalog(Table _) => RecordStep();

    [Given(@"^each source and resource has distinct sentinel body bytes$")]
    public void RecordResourceOwnerSentinels() => RecordStep();

    [Given(@"^the source files were created in "".+?"" order without a prior custom switch$")]
    public void RecordResourceOwnerCreationOrder() => RecordStep();

    [When(@"^I run the filename matrix operation "".+?""$")]
    public void RecordResourceOwnerOperation() => RecordStep();

    [Then(@"^an update discovers all five custom switches without emitting new native files$")]
    public void RecordResourceOwnerUpdateDiscovery() => RecordStep();

    [Then(@"^a preview reports all five discoveries while every project path and byte stays unchanged$")]
    public void RecordResourceOwnerPreviewDiscovery() => RecordStep();

    [Then(@"^exactly those five custom skill names are discovered with enabled true, origin custom, emitAgent true, codexMetadata true, and resources exactly guide$")]
    public void RecordResourceOwnerSwitches() => RecordStep();

    [Then(@"^each Claude and Codex skill, agent definition, and Codex metadata file belongs to its exact owner name$")]
    public void RecordResourceOwnerArtifacts() => RecordStep();

    [Then(@"^each provider resource has its owner's exact sentinel bytes at skills/<owner>/resources/guide\.md$")]
    public void RecordResourceOwnerResourceBytes() => RecordStep();

    [Then(@"^no resource filename is misreported or persisted as a separate skill$")]
    public void RecordResourceOwnerNoPhantomSkill() => RecordStep();

    [Given(@"^the repository source inventory has completed the canonical resource namespace transition$")]
    public void RecordNoticeSourceInventory() => RecordStep();

    [Given(@"^the published notice attribution has these exact source replacements:$")]
    public void RecordNoticeReplacementTable(Table _) => RecordStep();

    [When(@"^I read the published notice "".+?"" and its package inclusion declarations in "".+?""$")]
    public void RecordNoticeRead() => RecordStep();

    [Then(@"^every replacement row independently names its exact canonical source in that notice$")]
    public void RecordNoticeReplacementAssertion() => RecordStep();

    [Then(@"^every occurrence of each old source citation is absent from that notice$")]
    public void RecordNoticeRetirementAssertion() => RecordStep();

    [Then(@"^every Templates path cited in that notice, including unchanged skill sources, exists in the repository$")]
    public void RecordNoticePathAssertion() => RecordStep();

    [Then(@"^the two published notices have identical content after line-ending normalization$")]
    public void RecordNoticeEqualityAssertion() => RecordStep();

    [Then(@"^that package metadata still includes the selected notice in its published files$")]
    public void RecordNoticePackageAssertion() => RecordStep();

    [Then(@"^all upstream attribution, commit pins, and license text remain unchanged$")]
    public void RecordNoticeAttributionAssertion() => RecordStep();

    [Given(@"^(?:a successful independent custom skill and resource baseline on both providers|a pre-transition native baseline was captured before the old shipped copies were hard-edited|all other sources and switch provenance use the canonical namespace|an otherwise valid project is ready for the twenty-one framework resource namespace replacements|any recorded hash proves its exact contained source path|both resource files contain distinct sentinel bytes|complete project path and byte snapshots include sources, switches, provenance, and unrelated native siblings|existing explicit enabled choices and resource slugs are recorded|invalid cases contain malformed bytes and no legacy evidence|no framework ownership, prior resource provenance, or missing canonical reference identifies any suspect file as a legacy resource|no old framework hash claims either canonical path as a different source|old shipped copies contain hard edits while a distinct canonical custom resource and project extension contain sentinel bytes|old shipped resource files and exact old framework hashes remain from the prior source namespace|prior custom provenance records notes guide|skill notes references its canonical guide so the overlapping path would otherwise be usable|the exact twenty-one old shipped resource basenames are recorded under frameworkHashes|the running executable ships only the canonical resource-prefixed replacements|the running executable ships their resource-prefixed replacements and no old resource basenames|the suspect legacy file contains malformed frontmatter bytes|the suspect source contains malformed frontmatter bytes that must not supersede its filename diagnosis|valid cases have their exact top-level owner, valid source content, and referenced canonical resource)$")]
    [Given("""^(?:\"resource-skill-resource-guide\.template\.md\" contains \"[^\"]+\"|\"[^\"]+\" has arbitrary resource sentinel bytes|\"[^\"]+\" has valid matching skill frontmatter|\"[^\"]+\" is absent|\"[^\"]+\" is its referenced custom resource|\"[^\"]+\" is the valid source of skill \"[^\"]+\"|a valid custom owner \"[^\"]+\" has the exact old source \"[^\"]+\" with arbitrary sentinel bytes|an exact legacy source notes-resource-guide\.template\.md is at \"[^\"]+\"|an otherwise valid project has \"[^\"]+\" for one packaged resource|its canonical resource is \"[^\"]+\"|its canonical source \"[^\"]+\" is absent|legacy intent for resource-notes guide is established by \"[^\"]+\"|legacy intent is established by \"[^\"]+\"|only \"[^\"]+\" declares the otherwise valid custom owner \"[^\"]+\"|skill \"[^\"]+\" has \"[^\"]+\"|the exact top-level owner source for \"[^\"]+\" is \"[^\"]+\"|the owner is \"[^\"]+\" and references resources/guide\.md when its body is valid|top-level \"[^\"]+\" contains a resource sentinel|top-level \"[^\"]+\" contains the referenced resource|top-level \"[^\"]+\" has a valid filename for \"[^\"]+\"|valid custom skills \"[^\"]+\" and \"[^\"]+\" coexist|valid custom skills \"[^\"]+\" and \"[^\"]+\" have their separate canonical guide resources|valid skills \"[^\"]+\" and \"[^\"]+\" coexist and reference their separate canonical guide resources)$""")]
    public void RecordNamespaceGiven() => RecordStep();

    [Given(@"^each following canonical source component is exercised independently for a skill slug, resource owner slug, and resource slug:$")]
    [Given(@"^each following collision is exercised alone with distinct custom sentinel bytes:$")]
    [Given(@"^each following nested source is exercised alone with malformed content:$")]
    [Given(@"^each following row is exercised in its own otherwise valid project without prior legacy resource evidence:$")]
    [Given("""^each following unsupported filename is exercised alone at \"[^\"]+\" with invalid source bytes:$""")]
    public void RecordNamespaceTable(Table table)
    {
        _tables.Add(table);
        RecordStep();
    }

    [When("""^I run the filename matrix operation \"[^\"]+\" independently for every (?:row|component and source kind|collision)$""")]
    [When(@"^I apply the real template update and synchronize both providers twice$")]
    [When(@"^I synchronize both providers twice$")]
    public void RecordNamespaceWhen() => RecordStep();

    [Then(@"^(?:a disabled valid owner's successful non-preview operation records enabled false and resources exactly guide|a malformed owner is reported at skill-skill-owner\.template\.md for missing frontmatter before any project mutation|a missing canonical guide is diagnosed at owner skill with the exact target resource-skill-resource-guide\.template\.md|a nested legacy file is diagnosed only as nested and requiring top-level placement|a preview leaves every project path and byte unchanged|a preview reports the same source and metadata actions while preserving every project path and byte|a subsequent real update followed by a second update has no further source or metadata change|a subsequent sync of the disabled valid owner succeeds with neither provider emitting that owner's skill, agent, metadata, or resource|a successful preview leaves every project path and byte unchanged|a successful sync emits both skill names separately|a top-level legacy file is diagnosed with both old and canonical paths and an actionable manual ownership or rename instruction|all native paths and resource bytes equal the pre-transition compiled baseline except the explicitly revised skill-mechanics grammar text|an absent old file is never reported as physically deleted|an already-owned canonical target is not diagnosed as custom merely because the old source also exists|an update leaves only the canonical resource source with packaged bytes and its truthful hash|an update removes exactly the twenty-one old local resource paths and hashes and creates the twenty-one canonical local resource paths and truthful LF-normalized hashes|any diagnostic naming the overlapping file calls it ambiguous and instructs preserving a valid skill while supplying the separate canonical resource|both skill names have distinct skill outputs on both providers|both switches record guide without creating a legacy-source error|custom sources, extensions, explicit switches, and resource-slug provenance retain their exact values and bytes|each failed operation and each preview preserves every project path and byte|each owner receives only its own canonical resource bytes|every project path and byte remains unchanged with no extra switch or native output|every replacement source equals its packaged bytes and no backup, merge, conflict, or general migration record is created|every row exits nonzero and names its exact source path and diagnostic reason|every row fails with the exact colliding path and owner before any retirement, replacement, or native cleanup|every row succeeds without a source validation or nested-location diagnostic|failure or preview preserves every project path and byte|invalid cases fail on the exact source filename and invalid component before content validation|invalid names are not diagnosed as orphan resources and orphan resources are not diagnosed as invalid names|it instructs manual rename after checking ownership and never chooses a source kind from the suspect contents|it reports every planned old source retirement, canonical replacement, and old and new source hash reconciliation|neither owner consumes or overwrites the ambiguous bytes|no resource body contributes a skill name or switch entry|no suspect path receives a frontmatter diagnosis|resource slugs and enabled choices retain their values|that path has no invalid-name, protected-delimiter, orphan, or frontmatter diagnosis|the command fails before any mutation|the command fails with the exact old and canonical source paths and instructions to run dydo template update|the diagnostic names both the old path and resource-resource-notes-resource-guide\.template\.md and requires manual ownership resolution|the diagnostic names the exact legacy path, owner, resource, and canonical replacement path|the overlapping file is diagnosed as ambiguous legacy ownership for resource-notes guide|the resource path is never diagnosed as an invalid protected-delimiter skill or an orphan resource|the second source-built update and sync leave the complete managed manifest and working-tree diff unchanged|the second synchronization retains the identical configuration and native path and byte manifest|the skill owner's guide resource retains the exact resource-content bytes including any frontmatter|valid cases succeed and sync emits exactly their canonical skill and resource names to both providers)$")]
    [Then("""^(?:\"[^\"]+\" is diagnosed only as having no matching skill source|\"[^\"]+\" is diagnosed only as nested and requiring top-level placement|\"[^\"]+\" is never consumed as a resource or diagnosed as an invalid skill|every row exits nonzero and names its relative nested path with \"[^\"]+\"|the command has the \"[^\"]+\" result)$""")]
    public void RecordNamespaceThen() => RecordStep();


    private void RecordStep() => _steps.Add(context.StepContext.StepInfo.Text);

    [AfterScenario]
    public async Task VerifyContract()
    {
        Directory.CreateDirectory(_root);
        try
        {
            var title = context.ScenarioInfo.Title;
            var prose = string.Join('\n', _steps);
            if (title.StartsWith("Initialize the local source", StringComparison.Ordinal))
                await VerifyInitialization(prose);
            else if (title.StartsWith("Discover and compile", StringComparison.Ordinal))
                VerifyCustomDiscovery();
            else if (title.StartsWith("Accept a minimal", StringComparison.Ordinal))
                VerifyMinimalSwitch();
            else if (title.StartsWith("Update shipped", StringComparison.Ordinal)
                     || title.StartsWith("Repair a malformed", StringComparison.Ordinal)
                     || title.StartsWith("Upgrade a project", StringComparison.Ordinal)
                     || title.StartsWith("Preview an update", StringComparison.Ordinal)
                     || title.StartsWith("Preserve an explicit", StringComparison.Ordinal))
                await VerifyUpdate(title);
            else if (title.StartsWith("Disable a skill", StringComparison.Ordinal)
                     || title.StartsWith("Remove a resource", StringComparison.Ordinal)
                     || title.StartsWith("Remove Codex metadata", StringComparison.Ordinal)
                     || title.StartsWith("Remember a switch", StringComparison.Ordinal)
                     || title.StartsWith("Intentionally delete", StringComparison.Ordinal)
                     || title.StartsWith("Retire formerly", StringComparison.Ordinal))
                await VerifyCleanup(title, prose);
            else if (title.StartsWith("Reject invalid source", StringComparison.Ordinal))
                await VerifyInvalidSource(prose);
            else if (title.StartsWith("Validate sources against", StringComparison.Ordinal))
                await VerifyPostOperationValidation();
            else if (title.StartsWith("Reject a malformed switchboard", StringComparison.Ordinal))
                await VerifyMalformedSwitch(prose);
            else if (title.StartsWith("Check and validate", StringComparison.Ordinal))
                await VerifyCheckOrValidate(prose);
            else if (title.StartsWith("Compile a valid agent", StringComparison.Ordinal))
                VerifyAgentCompilation(prose);
            else if (title.StartsWith("Compile a valid skill-only", StringComparison.Ordinal))
                VerifySkillCompilation();
            else if (title.StartsWith("Preserve the beta hash", StringComparison.Ordinal))
                await VerifyHashRefresh();
            else if (title.StartsWith("Reach a post-migration fixed point", StringComparison.Ordinal))
                await VerifyFixedPoint();
            else if (title.StartsWith("Resolve resource owners from the complete catalog", StringComparison.Ordinal))
                await VerifyResourceOwnerCatalog(prose);
            else if (title.StartsWith("Published notices retain exact source attribution", StringComparison.Ordinal))
                VerifyNoticeAttribution();
            else if (title.StartsWith("Identify legacy resources from finite evidence", StringComparison.Ordinal))
                await VerifyLegacyEvidence(prose);
            else if (title.StartsWith("Complete canonical resource pairs retain meaning", StringComparison.Ordinal))
                await VerifyCanonicalPairs(prose);
            else if (title.StartsWith("Diagnose top-level filename ambiguities", StringComparison.Ordinal))
                await VerifyFilenameAmbiguities(prose);
            else if (title.StartsWith("A present owner source determines", StringComparison.Ordinal))
                await VerifyOwnerState(prose);
            else if (title.StartsWith("Recognized nested filenames", StringComparison.Ordinal))
                await VerifyNestedFilenames(prose);
            else if (title.StartsWith("Unsupported filename shapes", StringComparison.Ordinal))
                await VerifyUnsupportedFilenames(prose);
            else if (title.StartsWith("A nested owner cannot confer", StringComparison.Ordinal))
                await VerifyNestedOwner(prose);
            else if (title.StartsWith("Pin canonical slug boundaries", StringComparison.Ordinal))
                await VerifySlugBoundaries(prose);
            else if (title.StartsWith("Resource content cannot change", StringComparison.Ordinal))
                await VerifyResourceContent(prose);
            else if (title.StartsWith("Preserve canonical skills when", StringComparison.Ordinal))
                await VerifySkillResourceOverlap(prose);
            else if (title.StartsWith("Do not steal an old resource", StringComparison.Ordinal))
                await VerifyAmbiguousResourceOwner(prose);
            else if (title.StartsWith("Legacy diagnostics respect", StringComparison.Ordinal))
                await VerifyLegacyLocation(prose);
            else if (title.StartsWith("Replace the positively owned", StringComparison.Ordinal))
                await VerifyFrameworkTransition(prose);
            else if (title.StartsWith("Fail framework namespace collisions", StringComparison.Ordinal))
                await VerifyFrameworkCollisions(prose);
            else if (title.StartsWith("Sync requires explicit update", StringComparison.Ordinal))
                await VerifySyncRequiresUpdate();
            else if (title.StartsWith("Reconcile a partially completed", StringComparison.Ordinal))
                await VerifyPartialTransition(prose);
            else
                throw new Xunit.Sdk.XunitException($"No DYD-111 contract probe is bound for '{title}'.");
        }
        finally
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }

    private async Task VerifyInitialization(string prose)
    {
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
        var integration = QuotedValueAfter(prose, "initialize dydo with ");
        var init = await RunAsync("init", integration);
        init.AssertSuccess();

        var sourceRoot = Sources();
        var expected = TemplateGenerator.GetAllTemplateNames().Order(StringComparer.Ordinal).ToArray();
        var actual = Directory.GetFiles(sourceRoot, "*.template.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(52, actual.Length);
        Assert.Equal(31, actual.Count(name => name!.StartsWith("skill-", StringComparison.Ordinal)));
        Assert.Equal(21, actual.Count(name => !name!.StartsWith("skill-", StringComparison.Ordinal)));

        var config = Load();
        Assert.Contains("_system/templates/", config.ScanExclude);
        Assert.Equal(31, config.Skills.Count);
        var serializedSkills = JsonNode.Parse(File.ReadAllText(Path.Combine(_root, "dydo.json")))!["skills"]!.AsObject();
        Assert.Equal(serializedSkills.Select(entry => entry.Key).Order(StringComparer.Ordinal),
            serializedSkills.Select(entry => entry.Key));
        var sourceHashes = config.FrameworkHashes
            .Where(entry => entry.Key.StartsWith("_system/templates/", StringComparison.Ordinal))
            .ToDictionary(entry => Path.GetFileName(entry.Key), entry => entry.Value, StringComparer.Ordinal);
        Assert.Equal(52, sourceHashes.Count);
        Assert.All(actual, name => Assert.Equal(
            TemplateCommand.ComputeHash(File.ReadAllText(Path.Combine(sourceRoot, name!))),
            sourceHashes[name!]));
        var discovered = SkillTemplateService.DiscoverLocalCatalog(_root, config)
            .ToDictionary(skill => skill.Name, StringComparer.Ordinal);
        Assert.Equal(discovered.Keys.Order(StringComparer.Ordinal), config.Skills.Keys.Order(StringComparer.Ordinal));
        Assert.All(config.Skills, entry =>
        {
            Assert.True(entry.Value.Enabled);
            Assert.Equal("shipped", entry.Value.Origin);
            var skill = discovered[entry.Key];
            Assert.Equal(skill.EmitAgent, entry.Value.EmitAgent);
            Assert.Equal(skill.ExplicitInvocation || skill.ArgumentHint != null, entry.Value.CodexMetadata);
            var resources = entry.Value.Resources!;
            Assert.Equal(resources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), resources);
            var discoveredResources = SkillTemplateService.ReadResources(skill, _root)
                .Select(resource => Path.GetFileNameWithoutExtension(resource.FileName))
                .Order(StringComparer.Ordinal);
            Assert.Equal(discoveredResources, resources);
        });
        AssertNoNativeArtifacts();

        var beforeSwitches = config.Skills.ToDictionary(entry => entry.Key,
            entry => (entry.Value.Enabled, entry.Value.Origin, entry.Value.EmitAgent,
                entry.Value.CodexMetadata, Resources: entry.Value.Resources!.ToArray()), StringComparer.Ordinal);
        var synchronized = CaptureSync();
        Assert.True(synchronized.ExitCode == 0, synchronized.Stdout + synchronized.Stderr);
        var claude = integration is "none" or "all" or "claude";
        var codex = integration is "none" or "all" or "codex";
        foreach (var skill in discovered.Values)
            AssertManagedArtifacts(skill.Name, beforeSwitches[skill.Name].EmitAgent == true,
                beforeSwitches[skill.Name].CodexMetadata == true, beforeSwitches[skill.Name].Resources,
                claude, codex);
        var after = Load();
        Assert.All(beforeSwitches, expected =>
        {
            var actualSwitch = after.Skills[expected.Key];
            Assert.Equal(expected.Value.Enabled, actualSwitch.Enabled);
            Assert.Equal(expected.Value.Origin, actualSwitch.Origin);
            Assert.Equal(expected.Value.EmitAgent, actualSwitch.EmitAgent);
            Assert.Equal(expected.Value.CodexMetadata, actualSwitch.CodexMetadata);
            Assert.Equal(expected.Value.Resources, actualSwitch.Resources);
        });
    }

    private void VerifyCustomDiscovery()
    {
        Initialize();
        var contextPath = Path.Combine(_root, "dydo", "understand", "release-context.md");
        File.WriteAllText(contextPath, "# Release context\n");
        WriteCustom("release-notes", emitAgent: true, hint: "<release>", resources: ["style"],
            mustRead: "../../../understand/release-context.md");
        var config = Load();
        config.Skills["writing-for-humans"].Enabled = false;
        Save(config);

        var secondSynchronization = CaptureSync();
        Assert.True(secondSynchronization.ExitCode == 0, secondSynchronization.Stdout + secondSynchronization.Stderr);
        var saved = Load().Skills["release-notes"];
        Assert.True(saved.Enabled);
        Assert.Equal("custom", saved.Origin);
        Assert.True(saved.EmitAgent);
        Assert.True(saved.CodexMetadata);
        Assert.Equal(["style"], saved.Resources);
        AssertManagedArtifacts("release-notes", emitAgent: true, codexMetadata: true, ["style"],
            claude: true, codex: true);
        var claudeSkill = File.ReadAllText(Path.Combine(_root, ".claude", "skills", "release-notes", "SKILL.md"));
        var codexSkill = File.ReadAllText(Path.Combine(_root, ".agents", "skills", "release-notes", "SKILL.md"));
        foreach (var compiled in new[] { claudeSkill, codexSkill })
        {
            Assert.Contains("# release-notes", compiled, StringComparison.Ordinal);
            Assert.Contains("## Must-Reads", compiled, StringComparison.Ordinal);
            Assert.Contains("../../../dydo/understand/release-context.md", compiled, StringComparison.Ordinal);
            Assert.Contains("resources/style.md", compiled, StringComparison.Ordinal);
        }
        Assert.Equal("# style\n", File.ReadAllText(Path.Combine(_root, ".claude", "skills", "release-notes", "resources", "style.md")));
        Assert.Equal("# style\n", File.ReadAllText(Path.Combine(_root, ".agents", "skills", "release-notes", "resources", "style.md")));
        Assert.Contains("release-notes", File.ReadAllText(Path.Combine(_root, ".claude", "agents", "release-notes.md")), StringComparison.Ordinal);
        Assert.Contains("release-notes", File.ReadAllText(Path.Combine(_root, ".codex", "agents", "release-notes.toml")), StringComparison.Ordinal);
        AssertNoManagedArtifacts("writing-for-humans");
        var before = Manifest();
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.Equal(before, Manifest());
    }

    private async Task VerifyResourceOwnerCatalog(string prose)
    {
        Initialize();
        var names = new[] { "valid", "skill", "skill-owner", "skill-skill-owner", "resource-guide" };
        var resourcesFirst = prose.Contains("resources first", StringComparison.Ordinal);
        foreach (var name in resourcesFirst ? names.Reverse() : names)
        {
            var resource = Path.Combine(Sources(), $"resource-{name}-resource-guide.template.md");
            var source = Path.Combine(Sources(), $"skill-{name}.template.md");
            if (resourcesFirst)
            {
                File.WriteAllText(resource, $"resource sentinel for {name}\n");
                File.WriteAllText(source, CustomSource(name, emitAgent: true, hint: "<guide>", resources: ["guide"]));
            }
            else
            {
                File.WriteAllText(source, CustomSource(name, emitAgent: true, hint: "<guide>", resources: ["guide"]));
                File.WriteAllText(resource, $"resource sentinel for {name}\n");
            }
        }

        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var beforePreview = Manifest();
        var result = operation switch
        {
            "sync" => CaptureSync(),
            "update" => await RunAsync("template", "update"),
            "preview" => await RunAsync("template", "update", "--diff"),
            _ => throw new Xunit.Sdk.XunitException($"Unknown filename matrix operation '{operation}'.")
        };
        result.AssertSuccess();
        if (operation == "preview")
            Assert.Equal(beforePreview, Manifest());

        var synchronized = CaptureSync();
        Assert.True(synchronized.ExitCode == 0, synchronized.Stdout + synchronized.Stderr);
        var saved = Load();
        Assert.Equal(names.Order(StringComparer.Ordinal), saved.Skills.Where(entry => entry.Value.Origin == "custom")
            .Select(entry => entry.Key).Order(StringComparer.Ordinal));
        foreach (var name in names)
        {
            var entry = saved.Skills[name];
            Assert.True(entry.Enabled);
            Assert.Equal("custom", entry.Origin);
            Assert.True(entry.EmitAgent);
            Assert.True(entry.CodexMetadata);
            Assert.Equal(["guide"], entry.Resources);
            AssertManagedArtifacts(name, emitAgent: true, codexMetadata: true, ["guide"], claude: true, codex: true);
            var expected = $"resource sentinel for {name}\n";
            Assert.Equal(expected, File.ReadAllText(Path.Combine(_root, ".claude", "skills", name, "resources", "guide.md")));
            Assert.Equal(expected, File.ReadAllText(Path.Combine(_root, ".agents", "skills", name, "resources", "guide.md")));
        }

        var secondSynchronization = CaptureSync();
        Assert.True(secondSynchronization.ExitCode == 0, secondSynchronization.Stdout + secondSynchronization.Stderr);
    }

    private static void VerifyNoticeAttribution()
    {
        var root = FindRepositoryRoot();
        var notices = new[]
        {
            Path.Combine(root, "THIRD-PARTY-NOTICES.md"),
            Path.Combine(root, "npm", "THIRD-PARTY-NOTICES.md")
        };
        var mappings = new[]
        {
            ("Templates/reviewer-resource-code.template.md", "Templates/resource-reviewer-resource-code.template.md"),
            ("Templates/codebase-design-resource-deepening.template.md", "Templates/resource-codebase-design-resource-deepening.template.md"),
            ("Templates/codebase-design-resource-design-it-twice.template.md", "Templates/resource-codebase-design-resource-design-it-twice.template.md"),
            ("Templates/improve-codebase-architecture-resource-html-report.template.md", "Templates/resource-improve-codebase-architecture-resource-html-report.template.md"),
            ("Templates/prototype-resource-logic.template.md", "Templates/resource-prototype-resource-logic.template.md"),
            ("Templates/prototype-resource-ui.template.md", "Templates/resource-prototype-resource-ui.template.md"),
            ("Templates/implementer-resource-tests.template.md", "Templates/resource-implementer-resource-tests.template.md"),
            ("Templates/implementer-resource-mocking.template.md", "Templates/resource-implementer-resource-mocking.template.md"),
            ("Templates/teach-resource-mission-format.template.md", "Templates/resource-teach-resource-mission-format.template.md"),
            ("Templates/teach-resource-glossary-format.template.md", "Templates/resource-teach-resource-glossary-format.template.md"),
            ("Templates/teach-resource-learning-record-format.template.md", "Templates/resource-teach-resource-learning-record-format.template.md"),
            ("Templates/teach-resource-resources-format.template.md", "Templates/resource-teach-resource-resources-format.template.md"),
            ("Templates/wizard-resource-template.template.md", "Templates/resource-wizard-resource-template.template.md"),
            ("Templates/writing-for-agents-resource-skill-mechanics.template.md", "Templates/resource-writing-for-agents-resource-skill-mechanics.template.md")
        };
        var normalized = notices.Select(path => Normalize(File.ReadAllText(path))).ToArray();
        Assert.Equal(normalized[0], normalized[1]);
        foreach (var notice in normalized)
        foreach (var (oldPath, newPath) in mappings)
        {
            Assert.DoesNotContain(oldPath, notice, StringComparison.Ordinal);
            Assert.Contains(newPath, notice, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, newPath.Replace('/', Path.DirectorySeparatorChar))), newPath);
        }
        Assert.Contains("THIRD-PARTY-NOTICES.md", File.ReadAllText(Path.Combine(root, "DynaDocs.csproj")), StringComparison.Ordinal);
        Assert.Contains("THIRD-PARTY-NOTICES.md", File.ReadAllText(Path.Combine(root, "npm", "package.json")), StringComparison.Ordinal);
    }

    private async Task VerifyLegacyEvidence(string prose)
    {
        Initialize();
        var owner = QuotedValueAfter(prose, "valid custom owner ");
        var legacy = QuotedValueAfter(prose, "exact old source ");
        var canonical = QuotedValueAfter(prose, "canonical source ");
        WriteCustom(owner, emitAgent: false, resources: ["guide"]);
        Assert.Equal(0, SyncCommand.Execute(_root));

        var canonicalPath = Path.Combine(Sources(), canonical);
        File.Delete(canonicalPath);
        File.WriteAllBytes(Path.Combine(Sources(), legacy), [0, 1, 254, 255]);
        var before = Manifest();
        var result = await RunFilenameOperation(QuotedValueAfter(prose, "filename matrix operation "));
        Assert.NotEqual(0, result.ExitCode);
        var diagnostic = result.Stdout + result.Stderr;
        Assert.Contains(legacy, diagnostic, StringComparison.Ordinal);
        Assert.Contains(owner, diagnostic, StringComparison.Ordinal);
        Assert.Contains("guide", diagnostic, StringComparison.Ordinal);
        Assert.Contains(canonical, diagnostic, StringComparison.Ordinal);
        Assert.Contains("manual", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());
    }

    private async Task VerifyCanonicalPairs(string prose)
    {
        Initialize();
        WriteCustom("notes", emitAgent: false, resources: ["guide"]);
        WriteCustom("resource-notes", emitAgent: false, resources: ["guide"]);
        File.WriteAllText(Path.Combine(Sources(), "resource-notes-resource-guide.template.md"), "notes guide\n");
        File.WriteAllText(Path.Combine(Sources(), "resource-resource-notes-resource-guide.template.md"), "resource-notes guide\n");

        var before = Manifest();
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var result = await RunFilenameOperation(operation);
        result.AssertSuccess();
        if (operation == "preview")
            Assert.Equal(before, Manifest());

        var initialSync = CaptureSync();
        Assert.True(initialSync.ExitCode == 0, initialSync.Stdout + initialSync.Stderr);
        Assert.Equal("notes guide\n", File.ReadAllText(Path.Combine(_root, ".claude", "skills", "notes", "resources", "guide.md")));
        Assert.Equal("resource-notes guide\n", File.ReadAllText(Path.Combine(_root, ".claude", "skills", "resource-notes", "resources", "guide.md")));
        Assert.Equal(["guide"], Load().Skills["notes"].Resources);
        Assert.Equal(["guide"], Load().Skills["resource-notes"].Resources);
        var afterFirstSync = Manifest();
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.Equal(afterFirstSync, Manifest());
    }

    private void Reset()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task VerifyFilenameAmbiguities(string prose)
    {
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        foreach (var row in _tables.Single().Rows)
        {
            Reset(); Initialize();
            WriteCustom("valid", emitAgent: false);
            WriteCustom("skill-owner", emitAgent: false);
            var source = row["source"];
            File.WriteAllBytes(Path.Combine(Sources(), source), [0, 1, 254, 255]);
            var before = Manifest();
            var result = await RunFilenameOperation(operation);
            Assert.NotEqual(0, result.ExitCode);
            var diagnostic = result.Stdout + result.Stderr;
            Assert.Contains(source, diagnostic, StringComparison.Ordinal);
            Assert.DoesNotContain("frontmatter", diagnostic, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, Manifest());
        }
    }

    private async Task VerifyOwnerState(string prose)
    {
        Reset(); Initialize();
        var malformed = prose.Contains("malformed without frontmatter", StringComparison.Ordinal);
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var owner = Path.Combine(Sources(), "skill-skill-owner.template.md");
        File.WriteAllText(owner, malformed ? "malformed" : CustomSource("skill-owner", false, resources: ["guide"]));
        File.WriteAllText(Path.Combine(Sources(), "resource-skill-owner-resource-guide.template.md"), "owner guide\n");
        if (!malformed)
        {
            var config = Load();
            config.Skills["skill-owner"] = new SkillSwitchConfig { Enabled = false };
            Save(config);
        }
        var before = Manifest();
        var result = await RunFilenameOperation(operation);
        if (malformed)
        {
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("skill-skill-owner.template.md", result.Stdout + result.Stderr, StringComparison.Ordinal);
            Assert.Contains("frontmatter", result.Stdout + result.Stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, Manifest());
            return;
        }
        result.AssertSuccess();
        if (operation == "preview") Assert.Equal(before, Manifest());
        Assert.Equal(0, SyncCommand.Execute(_root));
        var entry = Load().Skills["skill-owner"];
        Assert.False(entry.Enabled);
        Assert.Equal(["guide"], entry.Resources);
        AssertAllManagedAbsent("skill-owner", ["guide"]);
    }

    private async Task VerifyNestedFilenames(string prose)
    {
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var ownerPresent = prose.Contains("\"present\"", StringComparison.Ordinal);
        foreach (var row in _tables.Single().Rows)
        {
            Reset(); Initialize();
            if (ownerPresent) WriteCustom("skill-owner", emitAgent: false, resources: ["guide"]);
            var relative = row["source"];
            var path = Path.Combine(Sources(), relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "malformed");
            var before = Manifest();
            var result = await RunFilenameOperation(operation);
            Assert.NotEqual(0, result.ExitCode);
            var diagnostic = result.Stdout + result.Stderr;
            Assert.Contains(relative, diagnostic, StringComparison.Ordinal);
            Assert.Contains("nested", diagnostic, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, Manifest());
        }
    }

    private async Task VerifyUnsupportedFilenames(string prose)
    {
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var nested = prose.Contains("\"nested\"", StringComparison.Ordinal);
        foreach (var row in _tables.Single().Rows)
        {
            Reset(); Initialize();
            Assert.Equal(0, CaptureSync().ExitCode);
            var directory = nested ? Path.Combine(Sources(), "nested") : Sources();
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, row["filename"]), [0, 1, 254, 255]);
            var before = Manifest();
            var result = await RunFilenameOperation(operation);
            result.AssertSuccess();
            Assert.Equal(before, Manifest());
        }
    }

    private async Task VerifyNestedOwner(string prose)
    {
        Reset(); Initialize();
        var nested = Path.Combine(Sources(), "nested", "skill-skill-owner.template.md");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(nested, CustomSource("skill-owner", false, resources: ["guide"]));
        File.WriteAllText(Path.Combine(Sources(), "resource-skill-owner-resource-guide.template.md"), "guide\n");
        var before = Manifest();
        var result = await RunFilenameOperation(QuotedValueAfter(prose, "filename matrix operation "));
        Assert.NotEqual(0, result.ExitCode);
        var diagnostic = result.Stdout + result.Stderr;
        Assert.Contains("nested/skill-skill-owner.template.md", diagnostic, StringComparison.Ordinal);
        Assert.Contains("resource-skill-owner-resource-guide.template.md", diagnostic, StringComparison.Ordinal);
        Assert.Equal(before, Manifest());
    }

    private async Task VerifySlugBoundaries(string prose)
    {
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        foreach (var row in _tables.Single().Rows)
        foreach (var kind in new[] { "skill", "owner", "resource" })
        {
            Reset(); Initialize();
            var value = SlugValue(row["component"]);
            var valid = bool.Parse(row["valid"]);
            string expected;
            if (kind == "skill")
            {
                expected = $"skill-{value}.template.md";
                File.WriteAllText(Path.Combine(Sources(), expected), valid ? CustomSource(value, false) : "malformed");
            }
            else if (kind == "owner")
            {
                expected = $"resource-{value}-resource-guide.template.md";
                File.WriteAllText(Path.Combine(Sources(), $"skill-{value}.template.md"), valid ? CustomSource(value, false, resources: ["guide"]) : "malformed");
                File.WriteAllText(Path.Combine(Sources(), expected), valid ? "guide\n" : "malformed");
            }
            else
            {
                expected = $"resource-valid-resource-{value}.template.md";
                File.WriteAllText(Path.Combine(Sources(), "skill-valid.template.md"), valid ? CustomSource("valid", false, resources: [value]) : "malformed");
                File.WriteAllText(Path.Combine(Sources(), expected), valid ? "guide\n" : "malformed");
            }
            var before = Manifest();
            var result = await RunFilenameOperation(operation);
            if (valid)
            {
                result.AssertSuccess();
                if (operation == "preview") Assert.Equal(before, Manifest());
                Assert.Equal(0, SyncCommand.Execute(_root));
            }
            else
            {
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains(expected, result.Stdout + result.Stderr, StringComparison.Ordinal);
                Assert.Equal(before, Manifest());
            }
        }
    }

    private async Task VerifyResourceContent(string prose)
    {
        Reset(); Initialize();
        WriteCustom("skill", emitAgent: false, resources: ["guide"]);
        WriteCustom("resource-guide", emitAgent: false, resources: ["guide"]);
        var content = prose.Contains("valid skill frontmatter", StringComparison.Ordinal)
            ? CustomSource("resource-guide", false)
            : "arbitrary resource body\n";
        File.WriteAllText(Path.Combine(Sources(), "resource-skill-resource-guide.template.md"), content);
        (await RunFilenameOperation(QuotedValueAfter(prose, "filename matrix operation "))).AssertSuccess();
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.Equal(content, File.ReadAllText(Path.Combine(_root, ".claude", "skills", "skill", "resources", "guide.md")));
        Assert.Equal(content, File.ReadAllText(Path.Combine(_root, ".agents", "skills", "skill", "resources", "guide.md")));
        Assert.Contains("skill", Load().Skills.Keys);
        Assert.Contains("resource-guide", Load().Skills.Keys);
    }

    private async Task VerifySkillResourceOverlap(string prose)
    {
        Reset(); Initialize();
        var guideState = QuotedValueAfter(prose, "skill \"skill\" has ");
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var needsGuide = !guideState.StartsWith("no guide", StringComparison.Ordinal);
        WriteCustom("skill", emitAgent: false, resources: needsGuide ? ["guide"] : []);
        WriteCustom("resource-guide", emitAgent: false);
        var canonical = Path.Combine(Sources(), "resource-skill-resource-guide.template.md");
        if (guideState.Contains("missing", StringComparison.Ordinal)) File.Delete(canonical);
        var before = Manifest();
        var result = await RunFilenameOperation(operation);
        if (guideState.Contains("missing", StringComparison.Ordinal))
        {
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("resource-skill-resource-guide.template.md", result.Stdout + result.Stderr, StringComparison.Ordinal);
            Assert.Equal(before, Manifest());
            return;
        }
        result.AssertSuccess();
        if (operation == "preview") Assert.Equal(before, Manifest());
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.Contains("skill", Load().Skills.Keys);
        Assert.Contains("resource-guide", Load().Skills.Keys);
    }

    private async Task VerifyAmbiguousResourceOwner(string prose)
    {
        Reset(); Initialize();
        WriteCustom("notes", emitAgent: false, resources: ["guide"]);
        WriteCustom("resource-notes", emitAgent: false, resources: ["guide"]);
        var target = Path.Combine(Sources(), "resource-resource-notes-resource-guide.template.md");
        File.Delete(target);
        var sentinel = Path.Combine(Sources(), "resource-notes-resource-guide.template.md");
        File.WriteAllBytes(sentinel, [1, 2, 3, 4]);
        var before = Manifest();
        var result = await RunFilenameOperation(QuotedValueAfter(prose, "filename matrix operation "));
        Assert.NotEqual(0, result.ExitCode);
        var diagnostic = result.Stdout + result.Stderr;
        Assert.Contains("resource-notes-resource-guide.template.md", diagnostic, StringComparison.Ordinal);
        Assert.Contains("resource-resource-notes-resource-guide.template.md", diagnostic, StringComparison.Ordinal);
        Assert.Contains("manual", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(sentinel));
    }

    private async Task VerifyLegacyLocation(string prose)
    {
        Reset(); Initialize();
        WriteCustom("notes", emitAgent: false, resources: ["guide"]);
        Assert.Equal(0, SyncCommand.Execute(_root));
        var canonical = Path.Combine(Sources(), "resource-notes-resource-guide.template.md");
        if (prose.Contains("\"absent\"", StringComparison.Ordinal)) File.Delete(canonical);
        var relative = prose.Contains("\"nested\"", StringComparison.Ordinal)
            ? "nested/notes-resource-guide.template.md" : "notes-resource-guide.template.md";
        var legacy = Path.Combine(Sources(), relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "malformed");
        var before = Manifest();
        var result = await RunFilenameOperation(QuotedValueAfter(prose, "filename matrix operation "));
        Assert.NotEqual(0, result.ExitCode);
        var diagnostic = result.Stdout + result.Stderr;
        Assert.Contains(relative, diagnostic, StringComparison.Ordinal);
        Assert.Contains(relative.StartsWith("nested", StringComparison.Ordinal) ? "nested" : "manual", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());
    }

    private async Task VerifyFrameworkTransition(string prose)
    {
        Reset(); Initialize();
        var config = Load();
        var canonical = TemplateGenerator.GetAllTemplateNames().Where(name => name.StartsWith("resource-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(21, canonical.Length);
        var old = canonical.Select(name => name["resource-".Length..]).ToArray();
        foreach (var (newName, oldName) in canonical.Zip(old))
        {
            var newPath = Path.Combine(Sources(), newName);
            var oldPath = Path.Combine(Sources(), oldName);
            File.Move(newPath, oldPath);
            config.FrameworkHashes.Remove($"_system/templates/{newName}");
            config.FrameworkHashes[$"_system/templates/{oldName}"] = TemplateCommand.ComputeHash(File.ReadAllText(oldPath));
        }
        Save(config);
        var before = Manifest();
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var result = await RunFilenameOperation(operation);
        result.AssertSuccess();
        if (operation == "preview")
        {
            Assert.Equal(before, Manifest());
            return;
        }
        var saved = Load();
        foreach (var (newName, oldName) in canonical.Zip(old))
        {
            Assert.True(File.Exists(Path.Combine(Sources(), newName)), newName);
            Assert.False(File.Exists(Path.Combine(Sources(), oldName)), oldName);
            Assert.Contains($"_system/templates/{newName}", saved.FrameworkHashes.Keys);
            Assert.DoesNotContain($"_system/templates/{oldName}", saved.FrameworkHashes.Keys);
        }
        Assert.Equal(0, SyncCommand.Execute(_root));
        var fixedPoint = Manifest();
        (await RunAsync("template", "update")).AssertSuccess();
        Assert.Equal(fixedPoint, Manifest());
    }

    private async Task VerifyFrameworkCollisions(string prose)
    {
        foreach (var row in _tables.Single().Rows)
        {
            Reset(); Initialize();
            var canonical = TemplateGenerator.GetAllTemplateNames().First(name => name.StartsWith("resource-", StringComparison.Ordinal));
            var old = canonical["resource-".Length..];
            var config = Load();
            var newPath = Path.Combine(Sources(), canonical);
            var oldPath = Path.Combine(Sources(), old);
            File.Move(newPath, oldPath);
            config.FrameworkHashes.Remove($"_system/templates/{canonical}");
            config.FrameworkHashes[$"_system/templates/{old}"] = TemplateCommand.ComputeHash(File.ReadAllText(oldPath));
            var state = row["occupied path state"];
            if (state.StartsWith("a new", StringComparison.Ordinal)) File.WriteAllText(newPath, "custom");
            else if (state.Contains("neither", StringComparison.Ordinal)) config.FrameworkHashes.Remove($"_system/templates/{old}");
            else if (state.Contains("inherited", StringComparison.Ordinal)) config.FrameworkHashes.Remove($"_system/templates/{old}");
            else if (state.Contains("prior resource", StringComparison.Ordinal)) config.FrameworkHashes.Remove($"_system/templates/{old}");
            Save(config);
            var before = Manifest();
            var result = await RunFilenameOperation(QuotedValueAfter(prose, "filename matrix operation "));
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(state.StartsWith("a new", StringComparison.Ordinal) ? canonical : old,
                result.Stdout + result.Stderr, StringComparison.Ordinal);
            Assert.Equal(before, Manifest());
        }
    }

    private async Task VerifySyncRequiresUpdate()
    {
        Reset(); Initialize();
        var canonical = TemplateGenerator.GetAllTemplateNames().First(name => name.StartsWith("resource-", StringComparison.Ordinal));
        var old = canonical["resource-".Length..];
        var config = Load();
        File.Move(Path.Combine(Sources(), canonical), Path.Combine(Sources(), old));
        config.FrameworkHashes.Remove($"_system/templates/{canonical}");
        config.FrameworkHashes[$"_system/templates/{old}"] = TemplateCommand.ComputeHash(File.ReadAllText(Path.Combine(Sources(), old)));
        Save(config);
        var before = Manifest();
        var result = CaptureSync();
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(old, result.Stdout + result.Stderr, StringComparison.Ordinal);
        Assert.Contains(canonical, result.Stdout + result.Stderr, StringComparison.Ordinal);
        Assert.Contains("template update", result.Stdout + result.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());
    }

    private async Task VerifyPartialTransition(string prose)
    {
        Reset(); Initialize();
        var canonical = TemplateGenerator.GetAllTemplateNames().First(name => name.StartsWith("resource-", StringComparison.Ordinal));
        var old = canonical["resource-".Length..];
        var config = Load();
        var newPath = Path.Combine(Sources(), canonical);
        var oldPath = Path.Combine(Sources(), old);
        var state = QuotedValueAfter(prose, "otherwise valid project has ");
        if (!state.StartsWith("only", StringComparison.Ordinal)) File.Move(newPath, oldPath);
        if (state.StartsWith("both", StringComparison.Ordinal)) File.Copy(oldPath, newPath);
        config.FrameworkHashes.Remove($"_system/templates/{canonical}");
        if (!state.StartsWith("only", StringComparison.Ordinal))
            config.FrameworkHashes[$"_system/templates/{old}"] = TemplateCommand.ComputeHash(File.ReadAllText(oldPath));
        if (state.StartsWith("both", StringComparison.Ordinal) || state.StartsWith("only", StringComparison.Ordinal))
            config.FrameworkHashes[$"_system/templates/{canonical}"] = TemplateCommand.ComputeHash(File.ReadAllText(newPath));
        Save(config);
        var before = Manifest();
        var operation = QuotedValueAfter(prose, "filename matrix operation ");
        var result = await RunFilenameOperation(operation);
        result.AssertSuccess();
        if (operation == "preview") { Assert.Equal(before, Manifest()); return; }
        Assert.True(File.Exists(newPath));
        Assert.False(File.Exists(oldPath));
        Assert.Contains($"_system/templates/{canonical}", Load().FrameworkHashes.Keys);
        Assert.DoesNotContain($"_system/templates/{old}", Load().FrameworkHashes.Keys);
        var fixedPoint = Manifest();
        (await RunAsync("template", "update")).AssertSuccess();
        Assert.Equal(fixedPoint, Manifest());
    }

    private static string SlugValue(string description) => description switch
    {
        "one lowercase letter" => "a",
        "sixty-four lowercase letters" => new string('a', 64),
        "resource-guide" => "resource-guide",
        "empty" => "",
        "sixty-five lowercase letters" => new string('a', 65),
        "a leading hyphen" => "-a",
        "a trailing hyphen" => "a-",
        "two consecutive hyphens" => "a--b",
        "an uppercase letter" => "A",
        "an underscore" => "a_b",
        "the protected delimiter inside bad-resource-name" => "bad-resource-name",
        _ => throw new Xunit.Sdk.XunitException($"Unknown slug boundary '{description}'.")
    };

    private void VerifyMinimalSwitch()
    {
        Initialize();
        WriteCustom("release-notes", emitAgent: false, invocation: "explicit");
        var config = Load();
        config.Skills["release-notes"] = new SkillSwitchConfig { Enabled = true };
        Save(config);
        var rawBefore = JsonNode.Parse(File.ReadAllText(Path.Combine(_root, "dydo.json")))!["skills"]!["release-notes"]!.AsObject();
        Assert.Equal(["enabled"], rawBefore.Select(entry => entry.Key));

        Assert.Equal(0, SyncCommand.Execute(_root));
        var entry = Load().Skills["release-notes"];
        Assert.True(entry.Enabled);
        Assert.Equal("custom", entry.Origin);
        Assert.False(entry.EmitAgent);
        Assert.True(entry.CodexMetadata);
        Assert.Empty(entry.Resources!);
        var rawAfter = JsonNode.Parse(File.ReadAllText(Path.Combine(_root, "dydo.json")))!["skills"]!["release-notes"]!.AsObject();
        Assert.Equal(["enabled", "origin", "emitAgent", "codexMetadata", "resources"], rawAfter.Select(entry => entry.Key));
        Assert.DoesNotContain(rawAfter, property => property.Key.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || property.Key.Contains("method", StringComparison.OrdinalIgnoreCase));
    }

    private async Task VerifyUpdate(string title)
    {
        Initialize();
        var shipped = Path.Combine(Sources(), "skill-reviewer.template.md");
        var packaged = TemplateGenerator.ReadBuiltInTemplate("skill-reviewer.template.md");

        if (title.StartsWith("Upgrade", StringComparison.Ordinal))
        {
            var ownedDoc = Path.Combine(_root, "dydo", "notes", "owned.md");
            Directory.CreateDirectory(Path.GetDirectoryName(ownedDoc)!);
            File.WriteAllBytes(ownedDoc, [0, 10, 13, 255]);
            var addition = Path.Combine(_root, "dydo", "_system", "template-additions", "reviewer.md");
            Directory.CreateDirectory(Path.GetDirectoryName(addition)!);
            File.WriteAllText(addition, "preserve this addition\n");
            Directory.Delete(Sources(), recursive: true);
            var old = Load();
            old.Skills.Clear();
            old.ScanExclude.Remove("_system/templates/");
            old.ScanExclude.Add("project-cache/");
            old.Integrations.Clear();
            old.Integrations["codex"] = true;
            old.Models = new ModelsConfig
            {
                Agents = new() { ["reviewer"] = "strong" },
                Tiers = new() { ["openai"] = new() { ["strong"] = "sentinel-model" } }
            };
            old.Nudges.Add(new NudgeConfig { Pattern = "sentinel", Message = "keep me", Severity = "warn" });
            Save(old);
            var preservedConfig = Load();
            var result = await RunAsync("template", "update");
            result.AssertSuccess();
            var migrated = Load();
            Assert.Equal(TemplateGenerator.GetAllTemplateNames().Count,
                Directory.GetFiles(Sources(), "*.template.md", SearchOption.TopDirectoryOnly).Length);
            Assert.Equal(TemplateGenerator.GetAllTemplateNames().Count,
                migrated.FrameworkHashes.Count(hash => hash.Key.StartsWith("_system/templates/", StringComparison.Ordinal)));
            Assert.All(migrated.Skills, entry => Assert.True(entry.Value.Enabled));
            Assert.Contains("_system/templates/", migrated.ScanExclude);
            Assert.Contains("project-cache/", migrated.ScanExclude);
            Assert.Equal(preservedConfig.Integrations, migrated.Integrations);
            Assert.Equal("sentinel-model", migrated.Models!.Tiers["openai"]["strong"]);
            Assert.Contains(migrated.Nudges, nudge => nudge.Pattern == "sentinel" && nudge.Message == "keep me");
            Assert.Equal([0, 10, 13, 255], File.ReadAllBytes(ownedDoc));
            Assert.Equal("preserve this addition\n", File.ReadAllText(addition));

            File.AppendAllText(shipped, "\nLOCAL SOURCE SENTINEL\n");
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.Contains("LOCAL SOURCE SENTINEL",
                File.ReadAllText(Path.Combine(_root, ".agents", "skills", "reviewer", "SKILL.md")),
                StringComparison.Ordinal);
            return;
        }

        if (title.StartsWith("Repair", StringComparison.Ordinal))
        {
            File.WriteAllText(shipped, "---\nname: reviewer\ndescription: broken\nemit: agent\n---\n");
            WriteCustom("valid-neighbor", emitAgent: false);
            var invalid = Path.Combine(Sources(), "skill-invalid-neighbor.template.md");
            File.WriteAllText(invalid, "---\nname: invalid-neighbor\ndescription: \nemit: skill\n---\n");
            var blocked = Manifest();
            var rejected = await RunAsync("template", "update");
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("invalid-neighbor", rejected.Stdout + rejected.Stderr, StringComparison.Ordinal);
            Assert.Equal(blocked, Manifest());
            File.Delete(invalid);

            var repaired = await RunAsync("template", "update");
            repaired.AssertSuccess();
            Assert.Equal(Normalize(packaged), Normalize(File.ReadAllText(shipped)));
            var reviewer = Load().Skills["reviewer"];
            var parsed = SkillTemplateService.Parse("skill-reviewer.template.md", packaged);
            Assert.Equal(parsed.EmitAgent, reviewer.EmitAgent);
            Assert.Equal(parsed.ExplicitInvocation || parsed.ArgumentHint != null, reviewer.CodexMetadata);

            File.WriteAllText(shipped, "not frontmatter at all");
            (await RunAsync("template", "update")).AssertSuccess();
            Assert.Equal(Normalize(packaged), Normalize(File.ReadAllText(shipped)));
            return;
        }

        var customName = title.StartsWith("Preserve", StringComparison.Ordinal)
            ? "writing-for-our-team"
            : "team-style";
        File.WriteAllText(shipped, "hard edit in shipped skill");
        var shippedResourceName = TemplateGenerator.GetAllTemplateNames()
            .First(name => !name.StartsWith("skill-", StringComparison.Ordinal));
        var shippedResource = Path.Combine(Sources(), shippedResourceName);
        File.WriteAllText(shippedResource, "hard edit in shipped resource");
        var mustRead = Path.Combine(_root, "dydo", "understand", "custom-context.md");
        File.WriteAllBytes(mustRead, [0, 1, 2, 255]);
        WriteCustom(customName, emitAgent: false, resources: ["guide"],
            mustRead: "../../../understand/custom-context.md");
        var custom = Path.Combine(Sources(), $"skill-{customName}.template.md");
        var customResource = Path.Combine(Sources(), $"resource-{customName}-resource-guide.template.md");
        var extension = Path.Combine(_root, "dydo", "_system", "template-additions", "reviewer.md");
        Directory.CreateDirectory(Path.GetDirectoryName(extension)!);
        File.WriteAllBytes(extension, [255, 13, 10, 0]);
        var preserved = new Dictionary<string, byte[]>
        {
            [custom] = File.ReadAllBytes(custom),
            [customResource] = File.ReadAllBytes(customResource),
            [mustRead] = File.ReadAllBytes(mustRead),
            [extension] = File.ReadAllBytes(extension)
        };

        var prepared = Load();
        prepared.Skills["writing-for-humans"].Enabled = false;
        if (!title.StartsWith("Preview", StringComparison.Ordinal))
            prepared.Skills[customName] = new SkillSwitchConfig { Enabled = true };
        prepared.Skills.Remove("co-thinker");
        prepared.FrameworkHashes[TemplateCommand.FrameworkDocFiles[0]] = new string('0', 64);
        prepared.ScanExclude.Remove("_system/templates/");
        WriteCustom("retired-probe", emitAgent: false);
        foreach (var source in Directory.GetFiles(Sources(), "*retired-probe*.template.md"))
            prepared.FrameworkHashes[$"_system/templates/{Path.GetFileName(source)}"] = TemplateCommand.ComputeHash(File.ReadAllText(source));
        prepared.Skills["retired-probe"] = new SkillSwitchConfig
        {
            Enabled = false, Origin = "shipped", EmitAgent = false, CodexMetadata = false, Resources = []
        };
        Save(prepared);

        if (title.StartsWith("Preview", StringComparison.Ordinal))
        {
            var before = Manifest();
            var preview = await RunAsync("template", "update", "--diff");
            preview.AssertSuccess();
            Assert.Contains("Updated source: _system/templates/skill-reviewer.template.md", preview.Stdout, StringComparison.Ordinal);
            Assert.Contains($"Updated source: _system/templates/{shippedResourceName}", preview.Stdout, StringComparison.Ordinal);
            Assert.Contains("Added skill switch: team-style", preview.Stdout, StringComparison.Ordinal);
            Assert.Contains("Removed retired source: _system/templates/skill-retired-probe.template.md", preview.Stdout, StringComparison.Ordinal);
            Assert.Contains("metadata-only document hash refresh", preview.Stdout, StringComparison.Ordinal);
            Assert.Contains("Added 1 default scan-exclude entry(ies)", preview.Stdout, StringComparison.Ordinal);
            Assert.Equal(before, Manifest());

            var collision = Path.Combine(Sources(), "skill-reviewer.template.md");
            var collisionConfig = Load();
            collisionConfig.FrameworkHashes.Remove("_system/templates/skill-reviewer.template.md");
            collisionConfig.Skills["reviewer"].Origin = "custom";
            Save(collisionConfig);
            var collisionBefore = Manifest();
            var diffFailure = await RunAsync("template", "update", "--diff");
            var realFailure = await RunAsync("template", "update");
            Assert.NotEqual(0, diffFailure.ExitCode);
            Assert.NotEqual(0, realFailure.ExitCode);
            Assert.Contains(Path.GetFileName(collision), diffFailure.Stdout + diffFailure.Stderr, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(collision), realFailure.Stdout + realFailure.Stderr, StringComparison.Ordinal);
            Assert.Equal(collisionBefore, Manifest());
            return;
        }

        var update = await RunAsync("template", "update");
        update.AssertSuccess();
        var saved = Load();
        foreach (var name in TemplateGenerator.GetAllTemplateNames())
        {
            var source = Path.Combine(Sources(), name);
            Assert.Equal(Normalize(TemplateGenerator.ReadBuiltInTemplate(name)), Normalize(File.ReadAllText(source)));
            Assert.Equal(TemplateCommand.ComputeHash(File.ReadAllText(source)),
                saved.FrameworkHashes[$"_system/templates/{name}"]);
        }
        Assert.DoesNotContain(Directory.GetFiles(_root, "*", SearchOption.AllDirectories), path =>
            path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
            || path.Contains("conflict", StringComparison.OrdinalIgnoreCase));
        Assert.All(preserved, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
        Assert.False(saved.Skills["writing-for-humans"].Enabled);
        Assert.True(saved.Skills[customName].Enabled);
        Assert.Equal("custom", saved.Skills[customName].Origin);
        Assert.True(saved.Skills["co-thinker"].Enabled);
        Assert.True(saved.Skills.ContainsKey("retired-probe"));
        Assert.Equal("shipped", saved.Skills["retired-probe"].Origin);
        Assert.False(saved.Skills["retired-probe"].Enabled);
        Assert.DoesNotContain(saved.FrameworkHashes.Keys, key => key.Contains("retired-probe", StringComparison.Ordinal));
        Assert.Contains("Updated source:", update.Stdout, StringComparison.Ordinal);
        Assert.Contains("Added skill switch:", update.Stdout, StringComparison.Ordinal);
        Assert.Contains("Removed retired source:", update.Stdout, StringComparison.Ordinal);
        Assert.Contains("metadata-only document hash refresh", update.Stdout, StringComparison.Ordinal);
        if (title.StartsWith("Preserve", StringComparison.Ordinal))
        {
            Assert.Equal(0, SyncCommand.Execute(_root));
            AssertNoManagedArtifacts("writing-for-humans");
            AssertManagedArtifacts("writing-for-our-team", emitAgent: false, codexMetadata: false,
                ["guide"], claude: true, codex: true);
        }
    }

    private async Task VerifyCleanup(string title, string prose)
    {
        Initialize();
        var enabled = !prose.Contains("enabled false", StringComparison.OrdinalIgnoreCase);
        var skill = title.StartsWith("Retire formerly", StringComparison.Ordinal) ? "former-skill"
            : title.Contains("explicit invocation", StringComparison.Ordinal) ? "invocation-only"
            : "cleanup-skill";
        var removesHint = title.StartsWith("Remove Codex metadata", StringComparison.Ordinal)
            && prose.Contains("argument hint", StringComparison.OrdinalIgnoreCase);
        var metadataOnly = prose.Contains("explicit skill metadata", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Codex metadata", StringComparison.Ordinal);
        var initialAgent = title.StartsWith("Remove a resource", StringComparison.Ordinal) || !metadataOnly;
        var initialHint = title.StartsWith("Remove a resource", StringComparison.Ordinal)
            ? null
            : removesHint || initialAgent ? "<arg>" : null;
        WriteCustom(skill, emitAgent: initialAgent, hint: initialHint,
            invocation: metadataOnly && !removesHint ? "explicit" : "automatic", resources: ["one", "two"]);
        Assert.Equal(0, SyncCommand.Execute(_root));
        var prior = Load().Skills[skill];
        Assert.Equal(initialAgent, prior.EmitAgent);
        Assert.Equal(initialHint != null || (metadataOnly && !removesHint), prior.CodexMetadata);
        Assert.Equal(["one", "two"], prior.Resources);
        AssertManagedArtifacts(skill, initialAgent, prior.CodexMetadata == true, ["one", "two"], true, true);

        var siblings = CreateProviderSiblings(skill);

        if (title.StartsWith("Retire formerly", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills[skill].Enabled = false;
            config.Skills[skill].Origin = "shipped";
            foreach (var source in Directory.GetFiles(Sources(), $"*{skill}*.template.md"))
            {
                config.FrameworkHashes[$"_system/templates/{Path.GetFileName(source)}"] =
                    TemplateCommand.ComputeHash(File.ReadAllText(source));
                File.AppendAllText(source, "\nhard edit");
            }
            foreach (var path in ManagedArtifactPaths(skill, ["one", "two"]).Where(File.Exists))
                File.AppendAllText(path, "\nhard edit");
            var unrecordedSource = Path.Combine(Sources(), "former-skill-not-managed.txt");
            File.WriteAllText(unrecordedSource, "unrecorded source");
            Save(config);

            (await RunAsync("template", "update")).AssertSuccess();
            Assert.Empty(Directory.GetFiles(Sources(), $"*{skill}*.template.md"));
            Assert.DoesNotContain(Load().FrameworkHashes.Keys, key => key.Contains(skill, StringComparison.Ordinal));
            Assert.Equal(0, SyncCommand.Execute(_root));
            AssertAllManagedAbsent(skill, ["one", "two"]);
            var tombstone = Load().Skills[skill];
            Assert.Equal("shipped", tombstone.Origin);
            Assert.False(tombstone.Enabled);
            Assert.Equal(initialAgent, tombstone.EmitAgent);
            Assert.Equal(prior.CodexMetadata, tombstone.CodexMetadata);
            Assert.Equal(["one", "two"], tombstone.Resources);
            Assert.Equal("unrecorded source", File.ReadAllText(unrecordedSource));
        }
        else if (title.StartsWith("Remove a resource", StringComparison.Ordinal))
        {
            var codexSkill = File.ReadAllBytes(Path.Combine(_root, ".agents", "skills", skill, "SKILL.md"));
            var codexTwo = File.ReadAllBytes(Path.Combine(_root, ".agents", "skills", skill, "resources", "two.md"));
            WriteCustom(skill, emitAgent: false, resources: ["two"]);
            File.Delete(Path.Combine(Sources(), $"resource-{skill}-resource-one.template.md"));
            SelectOnly("claude");
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(File.Exists(Path.Combine(_root, ".claude", "agents", $"{skill}.md")));
            Assert.False(File.Exists(Path.Combine(_root, ".codex", "agents", $"{skill}.toml")));
            Assert.False(File.Exists(Path.Combine(_root, ".claude", "skills", skill, "resources", "one.md")));
            Assert.False(File.Exists(Path.Combine(_root, ".agents", "skills", skill, "resources", "one.md")));
            Assert.Equal("# two\n", File.ReadAllText(Path.Combine(_root, ".claude", "skills", skill, "resources", "two.md")));
            Assert.Equal(codexSkill, File.ReadAllBytes(Path.Combine(_root, ".agents", "skills", skill, "SKILL.md")));
            Assert.Equal(codexTwo, File.ReadAllBytes(Path.Combine(_root, ".agents", "skills", skill, "resources", "two.md")));
            var final = Load().Skills[skill];
            Assert.False(final.EmitAgent);
            Assert.False(final.CodexMetadata);
            Assert.Equal(["two"], final.Resources);
        }
        else if (title.StartsWith("Remove Codex metadata", StringComparison.Ordinal))
        {
            var metadataPath = Path.Combine(_root, ".agents", "skills", skill, "agents", "openai.yaml");
            Assert.True(File.Exists(metadataPath));
            WriteCustom(skill, emitAgent: false, invocation: "automatic", resources: ["one", "two"]);
            var selected = prose.Contains("\"codex\" is now", StringComparison.Ordinal) ? "codex" : "claude";
            SelectOnly(selected);
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(File.Exists(metadataPath));
            var selectedSkill = selected == "codex"
                ? Path.Combine(_root, ".agents", "skills", skill, "SKILL.md")
                : Path.Combine(_root, ".claude", "skills", skill, "SKILL.md");
            Assert.Contains($"# {skill}", File.ReadAllText(selectedSkill), StringComparison.Ordinal);
            Assert.DoesNotContain("argument-hint", File.ReadAllText(selectedSkill), StringComparison.Ordinal);
            var final = Load().Skills[skill];
            Assert.False(final.EmitAgent);
            Assert.False(final.CodexMetadata);
            Assert.Equal(["one", "two"], final.Resources);
        }
        else if (title.StartsWith("Remember", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills[skill].Enabled = enabled;
            Save(config);
            var remembered = Load().Skills[skill];
            var selected = prose.Contains("\"codex\" is now", StringComparison.Ordinal) ? "codex" : "claude";
            SelectOnly(selected);
            foreach (var source in Directory.GetFiles(Sources(), $"*{skill}*.template.md")) File.Delete(source);
            var result = CaptureSync();
            Assert.Equal(enabled ? 2 : 0, result.ExitCode);
            if (enabled)
                Assert.Contains("source unavailable", result.Stdout + result.Stderr, StringComparison.OrdinalIgnoreCase);
            AssertAllManagedAbsent(skill, ["one", "two"]);
            var tombstone = Load().Skills[skill];
            Assert.Equal("custom", tombstone.Origin);
            Assert.Equal(enabled, tombstone.Enabled);
            Assert.Equal(remembered.EmitAgent, tombstone.EmitAgent);
            Assert.Equal(remembered.CodexMetadata, tombstone.CodexMetadata);
            Assert.Equal(remembered.Resources, tombstone.Resources);

            WriteCustom(skill, emitAgent: !metadataOnly, hint: metadataOnly ? null : "<arg>",
                invocation: metadataOnly ? "explicit" : "automatic", resources: ["one", "two"]);
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.Equal(enabled, Load().Skills[skill].Enabled);
            if (enabled)
                AssertManagedArtifacts(skill, !metadataOnly, codexMetadata: true, ["one", "two"],
                    claude: selected == "claude", codex: selected == "codex");
            else
                AssertAllManagedAbsent(skill, ["one", "two"]);
        }
        else if (title.StartsWith("Intentionally", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills[skill].Enabled = false;
            Save(config);
            Assert.Equal(0, SyncCommand.Execute(_root));
            AssertAllManagedAbsent(skill, ["one", "two"]);
            Assert.True(Directory.GetFiles(Sources(), $"*{skill}*.template.md").Length == 3);
            Assert.False(Load().Skills[skill].Enabled);
            foreach (var source in Directory.GetFiles(Sources(), $"*{skill}*.template.md")) File.Delete(source);
            config = Load();
            config.Skills.Remove(skill);
            Save(config);
            var sameNameCustom = new[]
            {
                Path.Combine(_root, ".agents", "skills", skill, "SKILL.md"),
                Path.Combine(_root, ".claude", "skills", skill, "SKILL.md")
            };
            foreach (var path in sameNameCustom)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "custom native file");
            }
            Assert.Equal(0, SyncCommand.Execute(_root));
            Assert.False(Load().Skills.ContainsKey(skill));
            Assert.All(sameNameCustom, path => Assert.Equal("custom native file", File.ReadAllText(path)));
        }
        else
        {
            var config = Load();
            config.Skills[skill].Enabled = false;
            Save(config);
            var selected = prose.Contains("\"codex\" is now", StringComparison.Ordinal) ? "codex" : "claude";
            SelectOnly(selected);
            Assert.Equal(0, SyncCommand.Execute(_root));
            AssertAllManagedAbsent(skill, ["one", "two"]);
            var final = Load().Skills[skill];
            Assert.False(final.Enabled);
            Assert.Equal("custom", final.Origin);
            Assert.Equal(initialAgent, final.EmitAgent);
            Assert.Equal(prior.CodexMetadata, final.CodexMetadata);
            Assert.Equal(["one", "two"], final.Resources);
            Assert.False(Directory.Exists(Path.Combine(_root, ".claude", "skills", skill, "resources")));
            Assert.False(Directory.Exists(Path.Combine(_root, ".agents", "skills", skill, "resources")));
            Assert.True(Directory.Exists(Path.Combine(_root, ".claude", "skills", skill)));
            Assert.True(Directory.Exists(Path.Combine(_root, ".agents", "skills", skill)));
        }
        AssertSiblingsPreserved(siblings);
    }

    private async Task VerifyInvalidSource(string prose)
    {
        Initialize();
        var defect = QuotedValueAfter(_steps[0], "contains ");
        var sourceRoot = Sources();
        if (defect.Contains("nested skill", StringComparison.Ordinal))
        {
            var nested = Path.Combine(sourceRoot, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "skill-bad.template.md"), CustomSource("bad", false));
            File.WriteAllText(Path.Combine(nested, "resource-bad-resource-one.template.md"), "nested resource");
        }
        else if (defect.Contains("outside 1-64", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(sourceRoot, "skill-Bad.template.md"), CustomSource("Bad", false));
            var tooLong = new string('a', 65);
            File.WriteAllText(Path.Combine(sourceRoot, $"skill-{tooLong}.template.md"), CustomSource(tooLong, false));
        }
        else if (defect.Contains("protected -resource-", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad-resource-name.template.md"), CustomSource("bad-resource-name", false));
        else if (defect.Contains("case-insensitive duplicate", StringComparison.Ordinal))
            WriteDuplicateSwitchKey(JsonNode.Parse(File.ReadAllText(Path.Combine(_root, "dydo.json")))!.AsObject());
        else if (defect.Contains("newly shipped or retired", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills["reviewer"].Origin = "custom";
            Save(config);
        }
        else if (defect.Contains("resource with no matching", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "resource-orphan-resource-one.template.md"), "orphan");
        else if (defect.Contains("extra resource attached", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "resource-reviewer-resource-extra.template.md"), "extra");
        else if (defect.Contains("missing or blank", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), "---\nname: bad\ndescription: \nemit: skill\n---\n");
            File.WriteAllText(Path.Combine(sourceRoot, "skill-no-name.template.md"),
                "---\ndescription: missing name\nemit: skill\n---\n\n# body\n");
            File.WriteAllText(Path.Combine(sourceRoot, "skill-no-description.template.md"),
                "---\nname: no-description\nemit: skill\n---\n\n# body\n");
            File.WriteAllText(Path.Combine(sourceRoot, "skill-no-body.template.md"),
                "---\nname: no-body\ndescription: blank body\nemit: skill\n---\n");
        }
        else if (defect.Contains("disagrees with its filename", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("other", false));
        else if (defect.Contains("unknown frontmatter", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false).Replace("emit: skill", "emit: skill\nunknown: value"));
            File.WriteAllText(Path.Combine(sourceRoot, "skill-domain.template.md"),
                CustomSource("domain", false).Replace("invocation: automatic", "invocation: sometimes"));
        }
        else if (defect.Contains("agent-only metadata", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false)
                .Replace("emit: skill", "emit: skill\nread-only: true\ndelegates: true\nweb: true"));
        else if (defect.Contains("explicit invocation on an agent", StringComparison.Ordinal))
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", true, invocation: "explicit"));
        else if (defect.Contains("resource link without", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false, resources: ["missing"]));
            File.WriteAllText(Path.Combine(sourceRoot, "skill-unreferenced.template.md"), CustomSource("unreferenced", false));
            File.WriteAllText(Path.Combine(sourceRoot, "resource-unreferenced-resource-extra.template.md"), "unreferenced");
        }
        else if (defect.Contains("Must-Read", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(sourceRoot, "skill-bad.template.md"), CustomSource("bad", false) + "\n## Must-Reads\n\n- [Outside](../../../../outside.md)\n");
            var additions = Path.Combine(_root, "dydo", "_system", "template-additions");
            Directory.CreateDirectory(additions);
            File.WriteAllText(Path.Combine(additions, "missing-must-read.md"),
                "## Must-Reads\n\n- [Missing](../../../understand/missing.md)\n");
            File.WriteAllText(Path.Combine(sourceRoot, "skill-included-missing.template.md"),
                CustomSource("included-missing", false) + "\n{{include:missing-must-read}}\n");
        }
        else
            throw new Xunit.Sdk.XunitException("Invalid-source example was not recognized.");

        var before = Manifest();
        CliResult result;
        if (_steps.Any(step => step == "I \"update the framework templates\""))
            result = await RunAsync("template", "update");
        else
            result = CaptureSync();
        var diagnostic = result.Stdout + result.Stderr;
        Assert.True(result.ExitCode != 0, $"Expected invalid source rejection. Output: {diagnostic}");
        foreach (var token in InvalidSourceDiagnosticTokens(defect))
            Assert.Contains(token, diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());

        if (defect.Contains("newly shipped or retired", StringComparison.Ordinal))
        {
            var config = Load();
            config.Skills["reviewer"].Origin = "shipped";
            config.Skills["retired-name"] = new SkillSwitchConfig
            {
                Enabled = true, Origin = "shipped", EmitAgent = false,
                CodexMetadata = false, Resources = []
            };
            Save(config);
            WriteCustom("retired-name", emitAgent: false);
            var retiredBefore = Manifest();
            var retired = await RunAsync("template", "update");
            Assert.NotEqual(0, retired.ExitCode);
            Assert.Contains("skill-retired-name.template.md", retired.Stdout + retired.Stderr, StringComparison.Ordinal);
            Assert.Contains("retired shipped", retired.Stdout + retired.Stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(retiredBefore, Manifest());
        }
    }

    private async Task VerifyPostOperationValidation()
    {
        Initialize();
        var config = Load();
        Directory.Delete(Sources(), recursive: true);
        config.Skills.Clear();
        config.FrameworkHashes = config.FrameworkHashes
            .Where(entry => !entry.Key.StartsWith("_system/templates/", StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        config.ScanExclude.Remove("_system/templates/");
        Save(config);
        var stale = Path.Combine(_root, ".agents", "skills", "reviewer", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "stale");

        (await RunAsync("template", "update")).AssertSuccess();
        Assert.True(File.Exists(Path.Combine(Sources(), "skill-reviewer.template.md")));
        var reviewerResource = TemplateGenerator.GetSkillResourceTemplateNames("reviewer").First();
        Assert.True(File.Exists(Path.Combine(Sources(), reviewerResource)));
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.DoesNotContain("stale", File.ReadAllText(stale));

        WriteCustom("invalid-intended", emitAgent: false, resources: ["missing"]);
        File.Delete(Path.Combine(Sources(), "resource-invalid-intended-resource-missing.template.md"));
        var before = Manifest();
        var rejected = CaptureSync();
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("skill-invalid-intended.template.md", rejected.Stdout + rejected.Stderr, StringComparison.Ordinal);
        Assert.Contains("missing resource", rejected.Stdout + rejected.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());
    }

    private async Task VerifyMalformedSwitch(string prose)
    {
        Initialize();
        var validConfig = File.ReadAllText(Path.Combine(_root, "dydo.json"));
        var defect = QuotedValueAfter(_steps[0], "contains ");
        WriteMalformedConfig(defect);
        var before = Manifest();
        var result = _steps.Any(step => step == "I \"update the framework templates\"")
            ? await RunAsync("template", "update")
            : CaptureSync();
        Assert.NotEqual(0, result.ExitCode);
        var diagnostic = result.Stdout + result.Stderr;
        foreach (var token in MalformedSwitchDiagnosticTokens(defect))
            Assert.Contains(token, diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());
        if (defect.Contains("enabled is absent or", StringComparison.Ordinal))
        {
            var node = JsonNode.Parse(File.ReadAllText(Path.Combine(_root, "dydo.json")))!.AsObject();
            node["skills"]!.AsObject().First().Value!.AsObject()["enabled"] = "yes";
            File.WriteAllText(Path.Combine(_root, "dydo.json"), node.ToJsonString());
            var invalidBoolean = Manifest();
            var second = CaptureSync();
            Assert.NotEqual(0, second.ExitCode);
            Assert.Contains("boolean enabled", second.Stdout + second.Stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(invalidBoolean, Manifest());
        }
        if (defect.Contains("outside 1-64", StringComparison.Ordinal))
            AssertMalformedAlternative(validConfig, (skills, first) =>
            {
                skills.Remove(first.Key);
                skills[new string('a', 65)] = first.Value!.DeepClone();
            }, new string('a', 65), "1-64");
        if (defect.Contains("emitAgent or codexMetadata", StringComparison.Ordinal))
            AssertMalformedAlternative(validConfig, (_, first) => first.Value!.AsObject()["codexMetadata"] = "true",
                "codexMetadata", "boolean");
        if (defect.Contains("resources", StringComparison.Ordinal))
        {
            AssertMalformedAlternative(validConfig, (_, first) => first.Value!.AsObject()["resources"] = "one",
                "resources", "array");
            AssertMalformedAlternative(validConfig, (_, first) => first.Value!.AsObject()["resources"] = new JsonArray("one", 2),
                "resources", "strings");
        }
        if (defect.Contains("no source", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(_root, "dydo.json"), validConfig);
            var node = JsonNode.Parse(validConfig)!.AsObject();
            node["skills"]!.AsObject()["provenanced-tombstone"] = new JsonObject
            {
                ["enabled"] = false, ["origin"] = "custom", ["emitAgent"] = false,
                ["codexMetadata"] = false, ["resources"] = new JsonArray()
            };
            File.WriteAllText(Path.Combine(_root, "dydo.json"), node.ToJsonString());
            Assert.Equal(0, CaptureSync().ExitCode);
            Assert.True(Load().Skills.ContainsKey("provenanced-tombstone"));
        }
    }

    private async Task VerifyCheckOrValidate(string prose)
    {
        Initialize();
        var defect = QuotedValueAfter(_steps[0], "contains ");
        WriteMalformedConfig(defect);
        var command = _steps[1].EndsWith(" validate`", StringComparison.Ordinal) ? "validate" : "check";
        var result = await RunAsync(command);
        var diagnostic = result.Stdout + result.Stderr;
        Assert.True(result.ExitCode != 0, $"Expected {command} to reject malformed configuration. Output: {diagnostic}");
        Assert.Contains("dydo.json", diagnostic, StringComparison.OrdinalIgnoreCase);
        foreach (var token in CheckDiagnosticTokens(defect))
            Assert.Contains(token, diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("All checks passed", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Validation passed", result.Stdout, StringComparison.OrdinalIgnoreCase);

        if (defect.Contains("malformed JSON", StringComparison.Ordinal))
        {
            var configPath = Path.Combine(_root, "dydo.json");
            var malformed = File.ReadAllText(configPath);
            File.Delete(configPath);
            var missing = await RunAsync(command);
            File.WriteAllText(configPath, malformed);
            Assert.NotEqual(diagnostic, missing.Stdout + missing.Stderr);
        }
    }

    private void VerifyAgentCompilation(string prose)
    {
        Initialize();
        var delegates = prose.Contains("delegates true", StringComparison.OrdinalIgnoreCase);
        var mustRead = Path.Combine(_root, "dydo", "understand", "delegation-context.md");
        File.WriteAllText(mustRead, "# Delegation context\n");
        WriteCustom("delegation-shape", emitAgent: true, hint: "<task>", resources: ["guide"],
            delegates: delegates, web: true, mustRead: "../../../understand/delegation-context.md");
        var source = File.ReadAllText(Path.Combine(Sources(), "skill-delegation-shape.template.md"));
        Assert.Contains("read-only: true", source, StringComparison.Ordinal);
        Assert.Contains("web: true", source, StringComparison.Ordinal);
        Assert.Contains("invocation: automatic", source, StringComparison.Ordinal);
        Assert.DoesNotContain("invocation: explicit", source, StringComparison.Ordinal);
        Assert.Equal(0, SyncCommand.Execute(_root));
        var entry = Load().Skills["delegation-shape"];
        Assert.True(entry.Enabled);
        Assert.Equal("custom", entry.Origin);
        Assert.True(entry.EmitAgent);
        Assert.True(entry.CodexMetadata);
        Assert.Equal(["guide"], entry.Resources);
        AssertManagedArtifacts("delegation-shape", emitAgent: true, codexMetadata: true, ["guide"],
            claude: true, codex: true);
        var claudeSkill = File.ReadAllText(Path.Combine(_root, ".claude", "skills", "delegation-shape", "SKILL.md"));
        var codexSkill = File.ReadAllText(Path.Combine(_root, ".agents", "skills", "delegation-shape", "SKILL.md"));
        var claude = File.ReadAllText(Path.Combine(_root, ".claude", "agents", "delegation-shape.md"));
        var codex = File.ReadAllText(Path.Combine(_root, ".codex", "agents", "delegation-shape.toml"));
        var metadata = File.ReadAllText(Path.Combine(_root, ".agents", "skills", "delegation-shape", "agents", "openai.yaml"));
        foreach (var compiled in new[] { claudeSkill, codexSkill })
        {
            Assert.Contains("name: delegation-shape", compiled, StringComparison.Ordinal);
            Assert.Contains("description: Contract fixture for delegation-shape.", compiled, StringComparison.Ordinal);
            Assert.Contains("# delegation-shape", compiled, StringComparison.Ordinal);
            Assert.Contains("## Must-Reads", compiled, StringComparison.Ordinal);
            Assert.Contains("delegation-context.md", compiled, StringComparison.Ordinal);
            Assert.Contains("resources/guide.md", compiled, StringComparison.Ordinal);
        }
        Assert.Contains("argument-hint: \"<task>\"", claudeSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("disable-model-invocation", claudeSkill, StringComparison.Ordinal);
        Assert.Contains("read-only: you assess", claude, StringComparison.Ordinal);
        Assert.Contains("WebFetch, WebSearch", claude, StringComparison.Ordinal);
        var tools = claude.Split('\n').Single(line => line.StartsWith("tools:", StringComparison.Ordinal));
        Assert.Equal(delegates, tools.Split(',').Any(tool => tool.Trim() == "Agent"));
        Assert.Contains("sandbox_mode = \"read-only\"", codex, StringComparison.Ordinal);
        Assert.Contains("web_search = \"live\"", codex, StringComparison.Ordinal);
        Assert.Contains($"enabled = {delegates.ToString().ToLowerInvariant()}", codex, StringComparison.Ordinal);
        Assert.Equal(delegates, codex.Contains("max_depth = 3", StringComparison.Ordinal));
        Assert.Contains("default_prompt: \"<task>\"", metadata, StringComparison.Ordinal);
        Assert.Equal("# guide\n", File.ReadAllText(Path.Combine(_root, ".claude", "skills", "delegation-shape", "resources", "guide.md")));
        Assert.Equal("# guide\n", File.ReadAllText(Path.Combine(_root, ".agents", "skills", "delegation-shape", "resources", "guide.md")));
        AssertNoUnsupportedClaims(claudeSkill, codexSkill, claude, codex, metadata);
    }

    private void VerifySkillCompilation()
    {
        Initialize();
        var mustRead = Path.Combine(_root, "dydo", "understand", "explicit-context.md");
        File.WriteAllText(mustRead, "# Explicit context\n");
        WriteCustom("explicit-skill", emitAgent: false, hint: "<topic>", invocation: "explicit", resources: ["guide"],
            mustRead: "../../../understand/explicit-context.md");
        var source = File.ReadAllText(Path.Combine(Sources(), "skill-explicit-skill.template.md"));
        Assert.Contains("emit: skill", source, StringComparison.Ordinal);
        Assert.Contains("invocation: explicit", source, StringComparison.Ordinal);
        Assert.DoesNotContain("read-only:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("delegates:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("web:", source, StringComparison.Ordinal);
        Assert.Equal(0, SyncCommand.Execute(_root));
        Assert.False(File.Exists(Path.Combine(_root, ".claude", "agents", "explicit-skill.md")));
        Assert.False(File.Exists(Path.Combine(_root, ".codex", "agents", "explicit-skill.toml")));
        AssertManagedArtifacts("explicit-skill", emitAgent: false, codexMetadata: true, ["guide"],
            claude: true, codex: true);
        var entry = Load().Skills["explicit-skill"];
        Assert.True(entry.Enabled);
        Assert.Equal("custom", entry.Origin);
        Assert.False(entry.EmitAgent);
        Assert.True(entry.CodexMetadata);
        Assert.Equal(["guide"], entry.Resources);
        var claudeSkill = File.ReadAllText(Path.Combine(_root, ".claude", "skills", "explicit-skill", "SKILL.md"));
        var codexSkill = File.ReadAllText(Path.Combine(_root, ".agents", "skills", "explicit-skill", "SKILL.md"));
        var metadata = File.ReadAllText(Path.Combine(_root, ".agents", "skills", "explicit-skill", "agents", "openai.yaml"));
        Assert.Contains("name: explicit-skill", claudeSkill, StringComparison.Ordinal);
        Assert.Contains("description: Contract fixture for explicit-skill.", claudeSkill, StringComparison.Ordinal);
        Assert.Contains("argument-hint: \"<topic>\"", claudeSkill, StringComparison.Ordinal);
        Assert.Contains("disable-model-invocation: true", claudeSkill, StringComparison.Ordinal);
        foreach (var compiled in new[] { claudeSkill, codexSkill })
        {
            Assert.Contains("# explicit-skill", compiled, StringComparison.Ordinal);
            Assert.Contains("## Must-Reads", compiled, StringComparison.Ordinal);
            Assert.Contains("explicit-context.md", compiled, StringComparison.Ordinal);
            Assert.Contains("resources/guide.md", compiled, StringComparison.Ordinal);
        }
        Assert.Contains("allow_implicit_invocation: false", metadata, StringComparison.Ordinal);
        Assert.Contains("default_prompt: \"<topic>\"", metadata, StringComparison.Ordinal);
        Assert.Equal("# guide\n", File.ReadAllText(Path.Combine(_root, ".claude", "skills", "explicit-skill", "resources", "guide.md")));
        Assert.Equal("# guide\n", File.ReadAllText(Path.Combine(_root, ".agents", "skills", "explicit-skill", "resources", "guide.md")));
        AssertNoUnsupportedClaims(claudeSkill, codexSkill, metadata);
    }

    private async Task VerifyHashRefresh()
    {
        Initialize();
        var config = Load();
        Directory.Delete(Sources(), recursive: true);
        config.Skills.Clear();
        config.ScanExclude.Remove("_system/templates/");
        config.FrameworkHashes = config.FrameworkHashes
            .Where(entry => TemplateCommand.FrameworkDocFiles.Contains(entry.Key, StringComparer.Ordinal))
            .ToDictionary(entry => entry.Key, _ => new string('0', 64), StringComparer.Ordinal);
        var documentBytes = TemplateCommand.FrameworkDocFiles.ToDictionary(file => file,
            file => File.ReadAllBytes(Path.Combine(_root, "dydo", file)), StringComparer.Ordinal);
        Save(config);
        var result = await RunAsync("template", "update");
        result.AssertSuccess();
        Assert.Contains("6 metadata-only document hash refresh", result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("  Updated: ", result.Stdout, StringComparison.Ordinal);
        var saved = Load();
        foreach (var file in TemplateCommand.FrameworkDocFiles)
        {
            Assert.Equal(documentBytes[file], File.ReadAllBytes(Path.Combine(_root, "dydo", file)));
            Assert.Equal(Hash(Encoding.UTF8.GetBytes(Normalize(File.ReadAllText(Path.Combine(_root, "dydo", file))))),
                saved.FrameworkHashes[file]);
        }
        Assert.Equal(TemplateGenerator.GetAllTemplateNames().Count,
            Directory.GetFiles(Sources(), "*.template.md", SearchOption.TopDirectoryOnly).Length);
        Assert.Equal(31, saved.Skills.Count);
        Assert.All(saved.Skills, entry => Assert.True(entry.Value.Enabled));
        Assert.Equal(TemplateGenerator.GetAllTemplateNames().Count,
            saved.FrameworkHashes.Count(entry => entry.Key.StartsWith("_system/templates/", StringComparison.Ordinal)));
        Assert.Contains("_system/templates/", saved.ScanExclude);
        var historicalWholeFileHashes = new[]
        {
            "D43EA96236F78662F90E22F0F79C4B54346EE1C5F6392834E75DEA03119F53FE",
            "9F5ECD3F2DB8BF23211D49DA7ADC2756349E8A97EF67821AB1950DD5016ACAA2"
        };
        Assert.All(historicalWholeFileHashes, hash =>
        {
            Assert.Equal(64, hash.Length);
            Assert.DoesNotContain(saved.FrameworkHashes.Values,
                field => field.Equals(hash, StringComparison.OrdinalIgnoreCase));
        });
    }

    private async Task VerifyFixedPoint()
    {
        Initialize();
        (await RunAsync("template", "update")).AssertSuccess();
        Assert.Equal(0, SyncCommand.Execute(_root));
        var before = ManagedManifest();
        Assert.Equal(TemplateGenerator.GetAllTemplateNames().Count,
            before.Keys.Count(path => path.StartsWith("dydo/_system/templates/", StringComparison.Ordinal)));
        Assert.Contains("dydo.json", before.Keys);
        Assert.Contains(before.Keys, path => path.StartsWith(".claude/skills/", StringComparison.Ordinal));
        Assert.Contains(before.Keys, path => path.StartsWith(".claude/agents/", StringComparison.Ordinal));
        Assert.Contains(before.Keys, path => path.StartsWith(".agents/skills/", StringComparison.Ordinal));
        Assert.Contains(before.Keys, path => path.StartsWith(".codex/agents/", StringComparison.Ordinal));
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), before.Keys);

        await RunGitAsync("init");
        await RunGitAsync("config", "user.email", "dydo@example.invalid");
        await RunGitAsync("config", "user.name", "dydo contract");
        await RunGitAsync("add", ".");
        await RunGitAsync("commit", "-m", "baseline");
        var diffBefore = await RunGitAsync("diff", "--no-ext-diff");

        var update = await RunAsync("template", "update");
        update.AssertSuccess();
        var sync = CaptureSync();
        Assert.Equal(0, sync.ExitCode);
        Assert.DoesNotContain("Updated source:", update.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Created source:", update.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Added skill switch:", update.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Removed retired", update.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("metadata-only", update.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("source hash change", update.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("switchboard change", update.Stdout, StringComparison.Ordinal);
        Assert.Equal(before, ManagedManifest());
        Assert.Equal(diffBefore.Stdout, (await RunGitAsync("diff", "--no-ext-diff")).Stdout);
    }

    private void Initialize(string integration = "all")
    {
        var config = ConfigFactory.CreateDefault();
        if (integration is "all" or "claude") config.Integrations["claude"] = true;
        if (integration is "all" or "codex") config.Integrations["codex"] = true;
        if (integration == "none") config.Integrations["none"] = true;
        var dydoRoot = Path.Combine(_root, config.Structure.Root);
        new FolderScaffolder().Scaffold(dydoRoot);
        FolderScaffolder.StoreInitialFrameworkHashes(dydoRoot, config);
        Save(config);
    }

    private void WriteCustom(string name, bool emitAgent, string? hint = null,
        string invocation = "automatic", string[]? resources = null, bool delegates = false,
        bool web = false, string? mustRead = null)
    {
        resources ??= [];
        File.WriteAllText(Path.Combine(Sources(), $"skill-{name}.template.md"),
            CustomSource(name, emitAgent, hint, invocation, resources, delegates, web, mustRead));
        foreach (var resource in resources)
            File.WriteAllText(Path.Combine(Sources(), $"resource-{name}-resource-{resource}.template.md"), $"# {resource}\n");
    }

    private static string CustomSource(string name, bool emitAgent, string? hint = null,
        string invocation = "automatic", string[]? resources = null, bool delegates = false,
        bool web = false, string? mustRead = null)
    {
        resources ??= [];
        var agentFields = emitAgent ? $"read-only: true\ndelegates: {delegates.ToString().ToLowerInvariant()}\nweb: {web.ToString().ToLowerInvariant()}\n" : "";
        var hintField = hint == null ? "" : $"argument-hint: \"{hint}\"\n";
        var links = string.Join('\n', resources.Select(resource => $"- [{resource}](resources/{resource}.md)"));
        var mustReads = mustRead == null ? "" : $"\n## Must-Reads\n\n- [Context]({mustRead})\n";
        return $"---\nname: {name}\ndescription: Contract fixture for {name}.\nemit: {(emitAgent ? "agent" : "skill")}\n{agentFields}invocation: {invocation}\n{hintField}---\n\n# {name}\n\n{links}\n{mustReads}";
    }

    private void AssertNoNativeArtifacts()
    {
        Assert.False(Directory.Exists(Path.Combine(_root, ".claude", "skills")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".claude", "agents")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".agents", "skills")));
        Assert.False(Directory.Exists(Path.Combine(_root, ".codex", "agents")));
    }

    private void AssertManagedArtifacts(string name, bool emitAgent, bool codexMetadata,
        IEnumerable<string> resources, bool claude, bool codex)
    {
        Assert.Equal(claude, File.Exists(Path.Combine(_root, ".claude", "skills", name, "SKILL.md")));
        Assert.Equal(codex, File.Exists(Path.Combine(_root, ".agents", "skills", name, "SKILL.md")));
        Assert.Equal(claude && emitAgent, File.Exists(Path.Combine(_root, ".claude", "agents", $"{name}.md")));
        Assert.Equal(codex && emitAgent, File.Exists(Path.Combine(_root, ".codex", "agents", $"{name}.toml")));
        Assert.Equal(codex && codexMetadata,
            File.Exists(Path.Combine(_root, ".agents", "skills", name, "agents", "openai.yaml")));
        foreach (var resource in resources)
        {
            Assert.Equal(claude,
                File.Exists(Path.Combine(_root, ".claude", "skills", name, "resources", $"{resource}.md")));
            Assert.Equal(codex,
                File.Exists(Path.Combine(_root, ".agents", "skills", name, "resources", $"{resource}.md")));
        }
    }

    private void AssertNoManagedArtifacts(string name)
    {
        foreach (var path in new[]
        {
            Path.Combine(_root, ".claude", "skills", name, "SKILL.md"),
            Path.Combine(_root, ".agents", "skills", name, "SKILL.md"),
            Path.Combine(_root, ".claude", "agents", $"{name}.md"),
            Path.Combine(_root, ".codex", "agents", $"{name}.toml"),
            Path.Combine(_root, ".agents", "skills", name, "agents", "openai.yaml")
        })
            Assert.False(File.Exists(path), path);
    }

    private IEnumerable<string> ManagedArtifactPaths(string name, IEnumerable<string> resources)
    {
        yield return Path.Combine(_root, ".claude", "skills", name, "SKILL.md");
        yield return Path.Combine(_root, ".agents", "skills", name, "SKILL.md");
        yield return Path.Combine(_root, ".claude", "agents", $"{name}.md");
        yield return Path.Combine(_root, ".codex", "agents", $"{name}.toml");
        yield return Path.Combine(_root, ".agents", "skills", name, "agents", "openai.yaml");
        foreach (var resource in resources)
        {
            yield return Path.Combine(_root, ".claude", "skills", name, "resources", $"{resource}.md");
            yield return Path.Combine(_root, ".agents", "skills", name, "resources", $"{resource}.md");
        }
    }

    private void AssertAllManagedAbsent(string name, IEnumerable<string> resources) =>
        Assert.All(ManagedArtifactPaths(name, resources), path => Assert.False(File.Exists(path), path));

    private Dictionary<string, byte[]> CreateProviderSiblings(string name)
    {
        var paths = new[]
        {
            Path.Combine(_root, ".claude", "skills", name, "project.txt"),
            Path.Combine(_root, ".claude", "agents", $"{name}.project.txt"),
            Path.Combine(_root, ".agents", "skills", name, "project.txt"),
            Path.Combine(_root, ".codex", "agents", $"{name}.project.txt")
        };
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        for (var index = 0; index < paths.Length; index++)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths[index])!);
            var bytes = new byte[] { 0, (byte)index, 255 };
            File.WriteAllBytes(paths[index], bytes);
            result[paths[index]] = bytes;
        }
        return result;
    }

    private static void AssertSiblingsPreserved(IReadOnlyDictionary<string, byte[]> siblings) =>
        Assert.All(siblings, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));

    private static void AssertNoUnsupportedClaims(params string[] artifacts)
    {
        foreach (var artifact in artifacts)
        {
            Assert.DoesNotContain("permission", artifact, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("dependency", artifact, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void WriteMalformedConfig(string prose)
    {
        var configPath = Path.Combine(_root, "dydo.json");
        if (prose.Contains("malformed JSON", StringComparison.Ordinal))
        {
            File.WriteAllText(configPath, "{ broken");
            return;
        }

        var node = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        var skills = node["skills"]!.AsObject();
        var first = skills.First();
        var entry = first.Value!.AsObject();
        if (prose.Contains("skills is not an object", StringComparison.Ordinal)) node["skills"] = new JsonArray();
        else if (prose.Contains("entry is not an object", StringComparison.Ordinal)) skills[first.Key] = 7;
        else if (prose.Contains("enabled is absent", StringComparison.Ordinal)) entry.Remove("enabled");
        else if (prose.Contains("enabled value", StringComparison.Ordinal)) entry["enabled"] = "yes";
        else if (prose.Contains("uppercase", StringComparison.Ordinal) || prose.Contains("outside 1-64", StringComparison.Ordinal))
        { skills.Remove(first.Key); skills["Bad-Key"] = entry; }
        else if (prose.Contains("protected -resource-", StringComparison.Ordinal))
        { skills.Remove(first.Key); skills["bad-resource-name"] = entry; }
        else if (prose.Contains("collide", StringComparison.Ordinal))
        {
            WriteDuplicateSwitchKey(node);
            return;
        }
        else if (prose.Contains("origin", StringComparison.Ordinal)) entry["origin"] = "other";
        else if (prose.Contains("emitAgent or codexMetadata", StringComparison.Ordinal)
                 || prose.Contains("wrong JSON type", StringComparison.Ordinal)) entry["emitAgent"] = "true";
        else if (prose.Contains("resources", StringComparison.Ordinal)) entry["resources"] = new JsonArray("Bad", "Bad");
        else if (prose.Contains("unknown property", StringComparison.Ordinal)) entry["permission"] = "all";
        else if (prose.Contains("no source", StringComparison.Ordinal)) skills["missing-custom"] = new JsonObject { ["enabled"] = false };
        else throw new Xunit.Sdk.XunitException("Malformed-switch example was not recognized.");
        File.WriteAllText(configPath, node.ToJsonString(new() { WriteIndented = true }));
    }

    private void WriteDuplicateSwitchKey(JsonObject node)
    {
        var first = node["skills"]!.AsObject().First();
        var serialized = node.ToJsonString(new() { WriteIndented = true });
        var marker = $"\"{first.Key}\":";
        var index = serialized.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0);
        var duplicate = $"\"{first.Key}\": {first.Value!.ToJsonString()},\n    ";
        File.WriteAllText(Path.Combine(_root, "dydo.json"), serialized.Insert(index, duplicate));
    }

    private static string[] InvalidSourceDiagnosticTokens(string prose)
    {
        if (prose.Contains("nested skill", StringComparison.Ordinal)) return ["skill-bad.template.md", "resource-bad-resource-one.template.md", "nested", "top-level"];
        if (prose.Contains("outside 1-64", StringComparison.Ordinal)) return ["skill-Bad.template.md", new string('a', 65), "invalid", "name"];
        if (prose.Contains("protected -resource-", StringComparison.Ordinal)) return ["skill-bad-resource-name.template.md", "protected", "delimiter"];
        if (prose.Contains("case-insensitive duplicate", StringComparison.Ordinal)) return ["admiral", "collides"];
        if (prose.Contains("newly shipped or retired", StringComparison.Ordinal)) return ["reviewer", "collid", "shipped"];
        if (prose.Contains("resource with no matching", StringComparison.Ordinal)) return ["resource-orphan-resource-one.template.md", "no matching"];
        if (prose.Contains("extra resource attached", StringComparison.Ordinal)) return ["resource-reviewer-resource-extra.template.md", "adds a resource"];
        if (prose.Contains("missing or blank", StringComparison.Ordinal)) return ["skill-bad.template.md", "skill-no-name.template.md", "skill-no-description.template.md", "skill-no-body.template.md", "description", "blank body"];
        if (prose.Contains("disagrees with its filename", StringComparison.Ordinal)) return ["skill-bad.template.md", "name: other"];
        if (prose.Contains("unknown frontmatter", StringComparison.Ordinal)) return ["skill-bad.template.md", "unknown", "skill-domain.template.md", "invocation"];
        if (prose.Contains("agent-only metadata", StringComparison.Ordinal)) return ["skill-bad.template.md", "agent-only"];
        if (prose.Contains("explicit invocation on an agent", StringComparison.Ordinal)) return ["skill-bad.template.md", "explicit invocation"];
        if (prose.Contains("resource link without", StringComparison.Ordinal)) return ["skill-bad.template.md", "missing resource", "resource-unreferenced-resource-extra.template.md", "not referenced"];
        if (prose.Contains("Must-Read", StringComparison.Ordinal)) return ["skill-bad.template.md", "skill-included-missing.template.md", "Must-Read", "outside", "missing"];
        throw new Xunit.Sdk.XunitException("Invalid-source diagnostic example was not recognized.");
    }

    private static string[] MalformedSwitchDiagnosticTokens(string prose)
    {
        if (prose.Contains("skills is not an object", StringComparison.Ordinal)) return ["dydo.json", "skills", "object"];
        if (prose.Contains("entry is not an object", StringComparison.Ordinal)) return ["dydo.json", "skill switch", "object"];
        if (prose.Contains("enabled is absent", StringComparison.Ordinal)) return ["skill switch", "enabled", "boolean"];
        if (prose.Contains("outside 1-64", StringComparison.Ordinal)) return ["Bad-Key", "1-64", "lowercase"];
        if (prose.Contains("protected -resource-", StringComparison.Ordinal)) return ["bad-resource-name", "protected"];
        if (prose.Contains("collide", StringComparison.Ordinal)) return ["admiral", "collides"];
        if (prose.Contains("origin", StringComparison.Ordinal)) return ["skill switch", "origin", "shipped", "custom"];
        if (prose.Contains("emitAgent or codexMetadata", StringComparison.Ordinal)) return ["skill switch", "emitAgent", "boolean"];
        if (prose.Contains("resources", StringComparison.Ordinal)) return ["skill switch", "resources", "unique"];
        if (prose.Contains("unknown property", StringComparison.Ordinal)) return ["skill switch", "permission", "unknown"];
        if (prose.Contains("no source", StringComparison.Ordinal)) return ["missing-custom", "no source", "provenance"];
        throw new Xunit.Sdk.XunitException("Malformed-switch diagnostic example was not recognized.");
    }

    private static string[] CheckDiagnosticTokens(string prose)
    {
        if (prose.Contains("malformed JSON", StringComparison.Ordinal)) return ["Invalid JSON"];
        if (prose.Contains("enabled is absent", StringComparison.Ordinal)) return ["skill switch", "enabled"];
        if (prose.Contains("uppercase", StringComparison.Ordinal)) return ["Bad-Key", "lowercase"];
        if (prose.Contains("collide", StringComparison.Ordinal)) return ["admiral", "collides"];
        if (prose.Contains("wrong JSON type", StringComparison.Ordinal)) return ["skill switch", "emitAgent", "boolean"];
        throw new Xunit.Sdk.XunitException("Check diagnostic example was not recognized.");
    }

    private void AssertMalformedAlternative(string validConfig,
        Action<JsonObject, KeyValuePair<string, JsonNode?>> mutate, params string[] diagnosticTokens)
    {
        var node = JsonNode.Parse(validConfig)!.AsObject();
        var skills = node["skills"]!.AsObject();
        mutate(skills, skills.First());
        File.WriteAllText(Path.Combine(_root, "dydo.json"), node.ToJsonString());
        var before = Manifest();
        var result = CaptureSync();
        Assert.NotEqual(0, result.ExitCode);
        foreach (var token in diagnosticTokens)
            Assert.Contains(token, result.Stdout + result.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Manifest());
    }

    private async Task<CliResult> RunFilenameOperation(string operation) => operation switch
    {
        "sync" => CaptureSync(),
        "update" => await RunAsync("template", "update"),
        "preview" => await RunAsync("template", "update", "--diff"),
        _ => throw new Xunit.Sdk.XunitException($"Unknown filename matrix operation '{operation}'.")
    };

    private CliResult CaptureSync()
    {
        var (exitCode, stdout, stderr) = ConsoleCapture.All(() => SyncCommand.Execute(_root));
        return new CliResult(exitCode, stdout, stderr);
    }

    private void SelectOnly(string provider)
    {
        var config = Load();
        config.Integrations.Clear();
        config.Integrations[provider] = true;
        Save(config);
    }

    private DydoConfig Load() => new ConfigService().LoadConfigStrict(_root)!;
    private void Save(DydoConfig config) => new ConfigService().SaveConfig(config, Path.Combine(_root, "dydo.json"));
    private string Sources() => Path.Combine(_root, "dydo", "_system", "templates");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("DynaDocs repository root was not found.");
    }

    private Dictionary<string, string> Manifest() => Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(_root, path).Replace('\\', '/'),
            path => Hash(File.ReadAllBytes(path)), StringComparer.Ordinal);

    private SortedDictionary<string, string> ManagedManifest()
    {
        var roots = new[]
        {
            "dydo/_system/templates/", ".claude/skills/", ".claude/agents/",
            ".agents/skills/", ".codex/agents/"
        };
        return new SortedDictionary<string, string>(Directory.GetFiles(_root, "*", SearchOption.AllDirectories)
            .Select(path => (Path: path, Relative: Path.GetRelativePath(_root, path).Replace('\\', '/')))
            .Where(file => file.Relative == "dydo.json"
                || roots.Any(root => file.Relative.StartsWith(root, StringComparison.Ordinal)))
            .ToDictionary(file => file.Relative, file => Hash(File.ReadAllBytes(file.Path)), StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string QuotedValueAfter(string prose, string marker)
    {
        var start = prose.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var quote = prose.IndexOf('"', start);
        var end = prose.IndexOf('"', quote + 1);
        return prose[(quote + 1)..end];
    }

    private async Task<CliResult> RunAsync(params string[] arguments)
    {
        var runtime = Path.Combine(AppContext.BaseDirectory, "DynaDocs.Tests");
        string[] invocation = ["exec", "--runtimeconfig", runtime + ".runtimeconfig.json",
            "--depsfile", runtime + ".deps.json", typeof(CheckCommand).Assembly.Location, .. arguments];
        var start = CliScenario.CreateStartInfo(_root, invocation,
            Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value));
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(60));
        return new CliResult(process.ExitCode, await stdout, await stderr);
    }

    private async Task<CliResult> RunGitAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr, process.WaitForExitAsync()).WaitAsync(TimeSpan.FromSeconds(60));
        var result = new CliResult(process.ExitCode, await stdout, await stderr);
        result.AssertSuccess();
        return result;
    }
}
