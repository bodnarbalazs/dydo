# DYD-96 portable-PDB document classification evidence (2026-09-09)

Specification-time fact enumeration for the SDK/generated PDB document amendment. Worktree `C:/Users/User/Desktop/Projects/DynaDocs/.worktrees/dyd96-adoption`, branch `codex/DYD-96-assurance-adoption`, base HEAD `0d3f0995f3eb315648987af685db061583629162`, .NET SDK 10.0.300, AltCover 9.0.102 from `.config/dotnet-tools.json`. No repository campaign, `run_tests.py` or test suite was run; every command below is a build, a producer invocation or a scratch probe outside the repository tree. Hashes are SHA-256, uppercase.

## Disproved obligation

Campaign `DynaDocs.Tests/coverage/results/assurance/native-20260908-01/owner/stderr.log` (ignored native evidence, untouched):

    Assembly identity failed for <isolated>\DynaDocs.Tests\bin\Debug\net10.0\DynaDocs.Tests.dll: PDB source outside inventory root: C:\Users\User\.nuget\packages\microsoft.net.test.sdk\18.0.1\build\net8.0\Microsoft.NET.Test.Sdk.Program.cs

## Commands

1. `dotnet build DynaDocs.sln -c Debug -p:RunAnalyzers=false -p:NuGetAudit=false -p:UseSharedCompilation=false` (cwd worktree; `DOTNET_CLI_USE_MSBUILD_SERVER=0`, `MSBUILDDISABLENODEREUSE=1`), exit 0.
2. `dotnet build DynaDocs.Tests/coverage/metrics/GateMetrics.csproj -c Debug -p:RunAnalyzers=false -p:NuGetAudit=false -p:UseSharedCompilation=false`, exit 0.
3. Throwaway enumerator (scratch only, never committed; C# on `System.Reflection.Metadata`, source SHA-256 `7A981E5F1D8BA57FE863078216C8AAA4C60B1D8D4784636B69320ECD681A22E5`) run once per assembly as `--assembly <dll> --root <worktree> --project <csproj>`, exit 0 for all three. It reads the portable PDB document table, every `MethodDebugInformation` row's non-hidden sequence points, the `EmbeddedSource` custom debug information (kind `0E8A571B-6926-466E-B4AD-8AB04611F5FE`), on-disk existence and, where the file exists, the SHA-1/SHA-256 of its bytes against the PDB hash, and classifies every document by the proposed assembly rule: an absolute URL under the root without an `obj` segment is `maintained`, under the root with an `obj` segment is `generated`, under a `packageFolders` entry of the owning project's `obj/project.assets.json` with `<package>/<version>` in `libraries` is `package` (`nuget:<package>/<version>/<path>`), anything else is a failing control.
4. `dotnet DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll --project <csproj> --root <worktree>` for `DynaDocs.csproj`, `DynaDocs.Tests/DynaDocs.Tests.csproj` and `DynaDocs.Tests/coverage/metrics/GateMetrics.csproj`, exit 0 for all three; `generated_files` compared against the PDB-level identities.
5. Four scratch fixture projects (section Fixture facts) built with `dotnet build Subject.csproj -c Debug --verbosity quiet -p:UseSharedCompilation=false`, each enumerated as in 3 and measured as in 4.
6. Two scratch AltCover probes with the production prepare flags (section AltCover probes). One `dotnet` process at a time throughout.

## Inputs

| Assembly | DLL | DLL SHA-256 | PDB SHA-256 | Owning project | `obj/project.assets.json` SHA-256 |
|---|---|---|---|---|---|
| `dydo` | `bin/Debug/net10.0/dydo.dll` | `8AE938D386EF2BFDD87542ECE09553761F1C57F99CCF85B899066F087220E915` | `258A938AFE4090D9FAD55C42D9977BA06448FEFD60EA8A2BC76552E685061E69` | `DynaDocs.csproj` | `FAF7910369450E5A03BCA7A56CB91EEFB60A4E9BB46847E6911A84802FC178F6` |
| `DynaDocs.Tests` | `DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.dll` | `A949DFD83B5AE89F51B996050AC1F58E9CC1CEB6D3D981EEC9DFD82FB4454F98` | `7A15E9CCFE5CD42B3618A3B534CB103D0FAB4E49E5D8DE6023E05960A892FB42` | `DynaDocs.Tests/DynaDocs.Tests.csproj` | `71888B89B0DA6D4B3504BE7B80C630B727FE272F81742E2351506B178F8DE142` |
| `GateMetrics` | `DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll` | `053BAAB41C169694F805DB67AFD9867898B3667434B0FB552DFC28438416BFA5` | `E9BEB5404D912D7A4A4A8E95CC76EFEDD8F50D8FD3659784B0ACD050DE134D28` | `DynaDocs.Tests/coverage/metrics/GateMetrics.csproj` | `C61A2D3DBB8CE95A6D6F4D62363343ACB793E7C1768AEBEDBDC51201E68D049A` |

`packageFolders` of all three locked assets: `C:\Users\User\.nuget\packages\` and `C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages`; `libraries` counts 9 (`dydo`), 28 (`DynaDocs.Tests`), 23 (`GateMetrics`). Every maintained or generated document URL is the worktree root joined with the identity (backslashes); the one package URL is given verbatim below.

## dydo

Document table rows: 124; reached by non-hidden sequence points: 105 (generated 29, maintained 76); not reached: 19. Checksum algorithm SHA-256 on every reached document; embedded source present on 29 reached documents; every reached document exists on disk and its bytes match the PDB checksum (105/105); language GUID `3f5162f8-07c6-11d3-9053-00c04fa302a1` (C#) throughout.
Methods with a debug-information row: without sequence points 623, hidden-only 0, all points maintained 846, all points generated 552, all points package 0, mixed origins 0, spanning more than one document 2.
Multi-document methods: token 100663860 `DynaDocs.Serialization.DydoConfigJsonContext::.cctor` over `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.g.cs`, `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.PropertyNames.g.cs`; token 100663887 `DynaDocs.Serialization.DydoDefaultJsonContext::.cctor` over `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.g.cs`, `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.PropertyNames.g.cs`.
Compilation level (`--project`): 91 `files`, 33 `generated_files`. Every generated or package PDB identity is in `generated_files` (29/29); maintained PDB identities not in `files`: none; `files` with no reached PDB document: 15 (`Models/LinkType.cs`, `Models/ViolationSeverity.cs`, `Rules/IRule.cs`, `Serialization/DydoJsonContext.cs`, `Services/AssemblyAttributes.cs`, `Services/IConfigService.cs`, `Services/IDocGraph.cs`, `Services/IDocScanner.cs`, `Services/IFolderScaffolder.cs`, `Services/IFrontmatterTypesService.cs`, `Services/IIndexGenerator.cs`, `Services/ILinkResolver.cs`, `Services/IMarkdownParser.cs`, `Services/IValidationService.cs`, `Utils/ExitCodes.cs`).

| Identity | Class | Points | Methods | PDB SHA-256 | Embedded | On disk |
|---|---|---|---|---|---|---|
| `Commands/CheckCommand.cs` | maintained | 91 | 16 | `7527FA99BB0240D1EABB5B2C93C2E595C56C5671811F295FE64079DFB8852CD8` | no | yes |
| `Commands/CheckConfigValidator.cs` | maintained | 12 | 1 | `71DCEDC4040DC39899F0BB535A673AAD11F745D9FF613B650E22ABB63E5CAECE` | no | yes |
| `Commands/CheckDocValidator.cs` | maintained | 52 | 5 | `6A2318B5D49E1CCA6B9A2314A78BA458097C435C64815223D9F499DFF2D16184` | no | yes |
| `Commands/CompleteCommand.cs` | maintained | 23 | 4 | `F5B985806B3C6E6B16C6B494186804EA79FA47382168AB5C52EE20A0DC5F30C3` | no | yes |
| `Commands/CompletionsCommand.cs` | maintained | 26 | 3 | `8B5094C75B7CD1D1F383F2D1715B4F1FDF9544F6B1DB15EEB8F1A1443C077399` | no | yes |
| `Commands/FixCommand.cs` | maintained | 138 | 16 | `0C032F527FCAAAA2A685B1C89CDD6193CF68E96300E53C12BE55C6F1AF54BA6D` | no | yes |
| `Commands/FixFileHandler.cs` | maintained | 90 | 5 | `92276EF42747125D6477350EBB8DF89B7D14D217CAF4C1D4816F738F41350DDE` | no | yes |
| `Commands/FixHubHandler.cs` | maintained | 99 | 12 | `07EA5476C2FB21034067D59DC6B15D6FD76145DAB8C2D893A478B5992AE84770` | no | yes |
| `Commands/GraphCommand.cs` | maintained | 117 | 11 | `63D388D3115301922C3802D4D7F1F0C515CA3D7BFEBF65BA50C9095CF18FCB9F` | no | yes |
| `Commands/GraphDisplayHandler.cs` | maintained | 91 | 13 | `8C2F21AFDD9D2785875CE584FFD4435B742872A6FA1359E12DC618B8373C24E4` | no | yes |
| `Commands/GuardCommand.cs` | maintained | 445 | 55 | `B2EA0D944AFA3D9A61E10A33B148F0DBCBA64E35E28800DBEDC704E9DCC543E7` | no | yes |
| `Commands/HelpCommand.cs` | maintained | 47 | 3 | `DD24275C563B9335F030EE45E707D7DDA8CA7CB316E1A2E0D1DCC6373EA1FA55` | no | yes |
| `Commands/IndexCommand.cs` | maintained | 56 | 6 | `DA6F3D294AC7D0785DA42A0670EA09FB6063CF6EFD0FC518CDAF64AD924EEA03` | no | yes |
| `Commands/InitCommand.cs` | maintained | 297 | 27 | `C9A90200A23ABA26AA34DD4C25FB495B6D6AE7C02F50087AB6E679AB46B5811B` | no | yes |
| `Commands/SyncCommand.cs` | maintained | 270 | 47 | `A53A51BD4B7E1FEA821CDF40599807DD4A1AAE24E1B3A3C1EBA58440134D4E0E` | no | yes |
| `Commands/TemplateCommand.cs` | maintained | 303 | 35 | `6870D0315378490F32277342224A5EB58E5973B4070BBDDC5DF4B07BBAB8E7D0` | no | yes |
| `Commands/ValidateCommand.cs` | maintained | 39 | 4 | `C7C7BB590A4BAE8999C9F09D7064E507761383F0620E4AC92D2E6EC0327A627C` | no | yes |
| `Models/DocFile.cs` | maintained | 24 | 23 | `D507A72BC19294BF1A219A87D0273FEEE083524F0D481B45C82EC99341FBB82D` | no | yes |
| `Models/DydoConfig.cs` | maintained | 20 | 15 | `A76E45F27563A275688C133D49EB9E9BE3758D9C7E443F18A627B4391E49269F` | no | yes |
| `Models/Frontmatter.cs` | maintained | 11 | 9 | `F7B4A39DB7504F88A244FD6BAA73C9F3D44D1E38E05EA5FBFFBDC0D83D5C3EAE` | no | yes |
| `Models/HookInput.cs` | maintained | 20 | 20 | `468C18970E9D65F2DF109D32B3E068152C87B1546C4E07DF0E509813AB572895` | no | yes |
| `Models/HookInputExtensions.cs` | maintained | 30 | 9 | `38E9E33F338836E9E399FC61B21046A0407A0825F4B4CF59A9DFEC3B97D7EEE0` | no | yes |
| `Models/LinkInfo.cs` | maintained | 14 | 14 | `20A76E8D7F9802C8F36EF611E01EF4CEC5FE6F5E198B80E16560053CD1FE4B9B` | no | yes |
| `Models/ModelsConfig.cs` | maintained | 6 | 5 | `7DCE5A7E33C9A944686C91411FB6B8CAA869385ED73E4D44B4C40FA4547CF3F1` | no | yes |
| `Models/NudgeConfig.cs` | maintained | 19 | 13 | `86C7146031A57D2D58E9662D617A83CF4095693CFC3035A42047F197E6F6CE35` | no | yes |
| `Models/SkillTemplate.cs` | maintained | 18 | 18 | `15D7D1690B20C00C0BB716FE06E74885B829FA4FE80D7C102CCF19705A32858C` | no | yes |
| `Models/StructureConfig.cs` | maintained | 3 | 3 | `12494633482CB995B5E5DC5D33D4A1B5B60A7880F34DBC9F0559171DB08E5988` | no | yes |
| `Models/ToolInputData.cs` | maintained | 16 | 16 | `1F332241FEDC2144B5C22F056F9062FF964CC158B4ED3E0CD80CEEA088FDA7C0` | no | yes |
| `Models/ValidationIssue.cs` | maintained | 6 | 6 | `63BF120D481E81B9482F2FB99FE522CBF91202AE31B880B14792CCE1E8823BAE` | no | yes |
| `Models/ValidationResult.cs` | maintained | 11 | 11 | `15A2756C29366842434F0F4119A0BF114FB3375C4D3D99174FD7319313218B66` | no | yes |
| `Models/Violation.cs` | maintained | 16 | 16 | `9DC7AAE6186782D506A83923CDFE58B08E5C8B3DF676C5DB83FDEF543B31809A` | no | yes |
| `Program.cs` | maintained | 22 | 2 | `ADBD9382DF0B557E6C7E74DD08FE4E28343F39EA2547E451DC8A84CA15FA4C0B` | no | yes |
| `Rules/BrokenLinksRule.cs` | maintained | 33 | 5 | `0D6AED12991DDC9704B429768A447662317A9523101CD56A2863501338966C21` | no | yes |
| `Rules/FolderMetaFilesRule.cs` | maintained | 29 | 8 | `0C436F6B295F1E3C1F2DAB75CEA8ADA1814570B40F1951700A1C59E343602B1A` | no | yes |
| `Rules/FrontmatterRule.cs` | maintained | 56 | 4 | `801E296DD25FBC2D9AE66C338E19B3751165168B4240A0E260185940DFCC88F3` | no | yes |
| `Rules/FutureFeatureRule.cs` | maintained | 98 | 20 | `FCE785A395214A68FE26BCDEA7E2EBD15EF26624A0182176E5E5BAFF2962B5F6` | no | yes |
| `Rules/HubFilesRule.cs` | maintained | 26 | 6 | `9D2D1B10FC3D8EDB6B07BD6FEC80FBDF470733CB61A3CE721CF1D664B7260208` | no | yes |
| `Rules/LegacyPmRecordRule.cs` | maintained | 47 | 9 | `D401D0C8F0A496B5DE9D8B06FDCBDC37BA14698AC651CB448EB38C9330D6645A` | no | yes |
| `Rules/NamingRule.cs` | maintained | 25 | 4 | `4C832B43E5BC889575DD6CD5861695B43A7120A2EFA0224FA892AB8C052DE703` | no | yes |
| `Rules/OffLimitsRule.cs` | maintained | 36 | 4 | `6AD1DE96FC19CAB88C012FA6EE2EA5E4EF347364FC9581C3C847649CDD38BCF9` | no | yes |
| `Rules/OrphanDocsRule.cs` | maintained | 83 | 11 | `22EE52D599CC9BCDDDCAEA71F8D1759C554C3E70DD2A8CDA560A92DD8D349C42` | no | yes |
| `Rules/RelativeLinksRule.cs` | maintained | 33 | 7 | `B813C311A3C7753557CB838B3150A07C71464DEDBEAA9A931BC47A367E0D8418` | no | yes |
| `Rules/RuleBase.cs` | maintained | 17 | 7 | `CF4725E78C77449F9CE693123E30D848F129D27B6BAD78C9A42F0BF7043BE8C7` | no | yes |
| `Rules/TitleRule.cs` | maintained | 10 | 3 | `1BD20D7E2FCCD4705099DF70E0FE595D94025E79AE6B711EE2181AB9EC5CCD7A` | no | yes |
| `Rules/UncustomizedDocsRule.cs` | maintained | 19 | 3 | `A56FFD14D2734ED37DD260B7A8A7E18351469C982E576772D2A76407C3D548B9` | no | yes |
| `Services/AnchorExtractor.cs` | maintained | 37 | 3 | `C5118F351620A6D3C3CCDAA1EB6611E7FB07C41AD24025F92A7B6A6BA6798407` | no | yes |
| `Services/BashCommandAnalyzer.cs` | maintained | 405 | 35 | `72B3FD3A6590BED1E40412AA4FDEAB8FE1DA4326A826F71AAE2C5A535DF0FCAE` | no | yes |
| `Services/CompletionProvider.cs` | maintained | 27 | 6 | `66FB6FD999E24D67C517FD627C4C03A2EC56A6E142242FEDC92F08066EE1C0A8` | no | yes |
| `Services/ConfigFactory.cs` | maintained | 42 | 9 | `01D3FB4361D5A80286D281D182FB6310D6DF325EF032F5F66946BA90A668A44A` | no | yes |
| `Services/ConfigFileLocator.cs` | maintained | 14 | 1 | `2FF1F18D4C0FE06C8DE00DFF48197BE19D17DAD0E71597DAF2C1C2F81474F1BB` | no | yes |
| `Services/ConfigService.cs` | maintained | 44 | 9 | `138D48A2211F8485A559B95373047967D64DFC50C74D26C5457522775272B96C` | no | yes |
| `Services/DocGraph.cs` | maintained | 83 | 9 | `30843C2203A770C83FAA99B588AF6C704E5C0929BB1298C415C91971BBB2E1CA` | no | yes |
| `Services/DocScanner.cs` | maintained | 65 | 6 | `34F72F2D2F3D2A162C7CC98105943C1F590CCCF03D3AC0102730CD2B1F27287F` | no | yes |
| `Services/FolderScaffolder.cs` | maintained | 87 | 15 | `F9F82EC97F8BE29AAA1E4AECA5C1327A755ED39C059C27AE5ED9B884D7AD8980` | no | yes |
| `Services/FrontmatterExtractor.cs` | maintained | 23 | 2 | `7E1DCE9C184D40AB8A6311BA2782EFD5E3A3240F009CD000340D5CE06C1669B5` | no | yes |
| `Services/FrontmatterTypesService.cs` | maintained | 39 | 3 | `755A8AAB847F4F5748B6A3E6F05A57C9B4996A9EDD518820AC476BA2A24C3CF5` | no | yes |
| `Services/HubGenerator.cs` | maintained | 279 | 32 | `1B998A0CA6DA3E53B4934B5218AADD6EA6FFBD13672A42F314AA9E19A4C6EA3A` | no | yes |
| `Services/IBashCommandAnalyzer.cs` | maintained | 20 | 18 | `C85E20E1DD9571EDD7E65FF6357AC3A2386C798CD208196B1B6A2C86386CE049` | no | yes |
| `Services/IOffLimitsService.cs` | maintained | 6 | 6 | `39D032760F30CC276CEB3F2591690B8C69B24F9C319C5152D0E32EEE85A263D3` | no | yes |
| `Services/IndexGenerator.cs` | maintained | 40 | 2 | `1DAB7556725939FA462B832599694FE9B27CEBADD080E6F56A801DFA7ADE43A9` | no | yes |
| `Services/LegacyPmManifestService.cs` | maintained | 93 | 12 | `5EC87E10B5DADD0F6B07CFE3EDE4041D707C721D2FB12F443CD7F9403676D8DF` | no | yes |
| `Services/LinkExtractor.cs` | maintained | 68 | 3 | `346E42E7945AB988F37AF7FD29DB44C12BFD5FD74B46F9991FEF6119D2C5A71C` | no | yes |
| `Services/LinkResolver.cs` | maintained | 34 | 6 | `E20F2001D62A1A8EAA3B5D3271724B536C9089F02905D50E2D84FAB6C8A988DE` | no | yes |
| `Services/MarkdownParser.cs` | maintained | 57 | 7 | `505E96C5C38D64465F9859B32699FC8AA50D68A160ACEE5D5A2F32327E13BCD0` | no | yes |
| `Services/OffLimitsService.cs` | maintained | 263 | 27 | `FFACB451DF7691F725D69E5C88692D48F09F479379DA030301A2449F341425A6` | no | yes |
| `Services/ShellCompletionInstaller.cs` | maintained | 121 | 6 | `625B4B4DD2E8D42843015564771F2216566BB61D5FA134FDE1E68FF895E04AE4` | no | yes |
| `Services/SkillTemplateService.cs` | maintained | 24 | 5 | `C68F86A8F46A399957D2EA951BBB2ED3EE71D9FF7D8EABF41AA3576D0ACD62BB` | no | yes |
| `Services/TemplateGenerator.cs` | maintained | 145 | 42 | `6714416B3FDFB5B00F41D9A874EC9DAD8BB597DDE149A9E4DA88DFA16A7E6D8B` | no | yes |
| `Services/ValidationService.cs` | maintained | 58 | 3 | `A2AF9C35A8E98F7F8533C2CD957B27D6B654884945352310CFAA8E408357662D` | no | yes |
| `Utils/ConsoleOutput.cs` | maintained | 70 | 11 | `3612ACECFB0F22DDD0ED1C2B60A612A67DB64293B15094EC9B719B7CB0790916` | no | yes |
| `Utils/FrontmatterParser.cs` | maintained | 77 | 7 | `033F8E205BE08E6AB1818B077FDE5ED99C0B8A60D374DB08FC4849DA69405026` | no | yes |
| `Utils/GlobMatcher.cs` | maintained | 14 | 4 | `0B26F72399E93FE6740E3772FE899336B8D1F443624FF4F0E2872E8E1D352359` | no | yes |
| `Utils/PathUtils.Discovery.cs` | maintained | 86 | 8 | `D5B2009825696C2AB8E4D6BA6FB084C3BEB12D738A5C9FD5791C5EEE53F7D85A` | no | yes |
| `Utils/PathUtils.cs` | maintained | 86 | 10 | `9DDF400B10D2F4CFE5AD019AD0E816CBA9217C458577952FA067F141C987D823` | no | yes |
| `Utils/RuleSkipPaths.cs` | maintained | 9 | 3 | `83D539FED5477B0F5F9CE2517918353147C443CEE802D4A390263FF43F211B90` | no | yes |
| `Utils/TitlePrettifier.cs` | maintained | 7 | 2 | `B77411FA3B1360C53692504A490C0ED56DBE193274D1207435F53FEE5C1115B4` | no | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.Boolean.g.cs` | generated | 9 | 2 | `5B52728B3E8D49C0BCE6F4EBA9F1E10CF9F82249B040A6EBD8FF1B63DCE089E6` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.DictionaryStringBoolean.g.cs` | generated | 27 | 4 | `673C53A214E21B067FF03BD291CEF9A11B6F3B8F406B7AFBCB74370C927ABFF1` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.DictionaryStringDictionaryStringString.g.cs` | generated | 28 | 4 | `CCEBEF89549D2EF249646A82866C764CBB495A2FEB15CA6D804392FC213A2033` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.DictionaryStringString.g.cs` | generated | 27 | 4 | `7D9ED401E0FD69DB458A77181DFA642D2CD017256C0E1E4C20C5B940902CE6E4` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.DydoConfig.g.cs` | generated | 84 | 28 | `3ED96D6DD228C2D38BE0EEC33B47F2FEAC1581FA415484698A26B490EB150B36` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.GetJsonTypeInfo.g.cs` | generated | 43 | 2 | `66492BDA710BBC8E831EBEFF493B96DE6583E93A0A960E0A6932F4891F5D7225` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.Int32.g.cs` | generated | 9 | 2 | `D1AB438160078A386BCD2436E1768371F082158E6336A0DC78BA2769E2A85DF4` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.ListNudgeConfig.g.cs` | generated | 26 | 4 | `2B1834A795FE5BAFCA5D44B992CEBE73A7BAD1F30BF642308CDC7CF95E3AD0F4` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.ListString.g.cs` | generated | 26 | 4 | `C29A828D09D2EB7222744FD89EF54F9B01D6FE59F11376FCCD42028C255DC534` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.ModelsConfig.g.cs` | generated | 44 | 13 | `E640236D81320C85A0A9D1F2B324ADB5717DC5E1F187F9CC92B6DBA9C1578A17` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.NudgeConfig.g.cs` | generated | 68 | 21 | `F05F1F897AD609A862011876D56DBF30786287A9A5A539B7D96378EC6F9A1A9F` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.PropertyNames.g.cs` | generated | 14 | 1 | `1E8EA5C2D4319F34EAC317B410EB79FBA5769B10FB2782939085C63B3DE0BCAD` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.String.g.cs` | generated | 9 | 2 | `49B0C30107C84E504E59938EBE6CC9762C90F9742E07A35AFB68370771EA9274` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.StructureConfig.g.cs` | generated | 34 | 10 | `1073A7305300A4FA9E475E6B997CF3AE6B26856158BE9B2D54F55799CC26B496` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoConfigJsonContext.g.cs` | generated | 46 | 8 | `75471083E4909EB24DA2106048BA81A2C820A9D358A52B92C5F7C1CB75429CCE` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.Boolean.g.cs` | generated | 9 | 2 | `F9C4C8A17E95AF67FED0CF880D277389162998574468F5FCDE0D8362DAB360EE` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.DictionaryStringString.g.cs` | generated | 27 | 4 | `7E935F778F8A2337F30449E5D3DF7B770E84421A65B2566C7E0D52B2912BAFFC` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.GetJsonTypeInfo.g.cs` | generated | 25 | 2 | `FEE945DBCC4490B6C13ECB55921FBF9CB1475B8F551F68AC2C7F3ED6E44F3BD4` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.HookInput.g.cs` | generated | 87 | 37 | `FB608F2548DE25FE4ECE899AE5FA74360844C2FC3B7ACF3D41981F13018A1CEA` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.NullableBoolean.g.cs` | generated | 10 | 2 | `AAE51A6D5FE6B5343AB9B8599C7A8AE44DC165B6A69E5052924A4AC449CFD40C` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.PropertyNames.g.cs` | generated | 18 | 1 | `D4F0B2471B00AC3C6C997514F18D0174DC01CC573F357A9A16CA5F43DB397511` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.String.g.cs` | generated | 9 | 2 | `79FC6162D8814B1E04D7FE2E340BC7BD71168264591B157E1850F2C0925E9676` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.ToolInputData.g.cs` | generated | 75 | 31 | `09101198939BEE25AB361FAC0E0B675B45423A47287977C4E32AB8E79C25C5B8` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/DydoDefaultJsonContext.g.cs` | generated | 46 | 8 | `074BDFDD7053E036B3C59F69776557F65FB80A06971270BC09043407F344DEE9` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/TypesJsonContext.GetJsonTypeInfo.g.cs` | generated | 13 | 2 | `68D30C4E1B761D6B8AAE21D6BD0BB015B3986611A37B83773607CDEA31DFE5B0` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/TypesJsonContext.String.g.cs` | generated | 9 | 2 | `6AD23474895D90F9C6B27F2EE96666CC5920BFD0BE6EB91652F37B97700A2A73` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/TypesJsonContext.StringArray.g.cs` | generated | 25 | 3 | `16DD18438EBA5AD0DF368C70393C4D51C386E0391EB6E651CB2E6DEAA1C760E3` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/TypesJsonContext.g.cs` | generated | 46 | 8 | `FAEA72BA05D2D7455958429D280525E463F7C3215C144382F9B8999A3D1188A1` | yes | yes |
| `obj/Debug/net10.0/generated/System.Text.RegularExpressions.Generator/System.Text.RegularExpressions.Generator.RegexGenerator/RegexGenerator.g.cs` | generated | 5977 | 341 | `92D03C3BF6DF6BB7E08B9385973471D25358DADC9B9F501F520441DB660322B7` | yes | yes |

Not reached by any non-hidden sequence point: `Models/LinkType.cs` (maintained), `Models/ViolationSeverity.cs` (maintained), `Rules/IRule.cs` (maintained), `Serialization/DydoJsonContext.cs` (maintained), `Services/AssemblyAttributes.cs` (maintained), `Services/IConfigService.cs` (maintained), `Services/IDocGraph.cs` (maintained), `Services/IDocScanner.cs` (maintained), `Services/IFolderScaffolder.cs` (maintained), `Services/IFrontmatterTypesService.cs` (maintained), `Services/IIndexGenerator.cs` (maintained), `Services/ILinkResolver.cs` (maintained), `Services/IMarkdownParser.cs` (maintained), `Services/IValidationService.cs` (maintained), `Utils/ExitCodes.cs` (maintained), `obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs` (generated), `obj/Debug/net10.0/DynaDocs.AssemblyInfo.cs` (generated), `obj/Debug/net10.0/DynaDocs.GlobalUsings.g.cs` (generated), `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/TypesJsonContext.PropertyNames.g.cs` (generated).

## DynaDocs.Tests

Document table rows: 114; reached by non-hidden sequence points: 109 (generated 7, maintained 101, package 1); not reached: 5. Checksum algorithm SHA-256 on every reached document; embedded source present on 14 reached documents; every reached document exists on disk and its bytes match the PDB checksum (109/109); language GUID `3f5162f8-07c6-11d3-9053-00c04fa302a1` (C#) throughout.
Methods with a debug-information row: without sequence points 1720, hidden-only 0, all points maintained 2122, all points generated 8, all points package 1, mixed origins 0, spanning more than one document 0.
Package-only methods: token 100663421 `.AutoGeneratedProgram::Main` in `nuget:microsoft.net.test.sdk/18.0.1/build/net8.0/Microsoft.NET.Test.Sdk.Program.cs`.
Compilation level (`--project`): 97 `files`, 11 `generated_files`. Every generated or package PDB identity is in `generated_files` (8/8); maintained PDB identities not in `files`: `DynaDocs.Tests/Features/assurance-adoption.feature`, `DynaDocs.Tests/Features/experimental-beta.feature`, `DynaDocs.Tests/Features/fresh-installation.feature`, `DynaDocs.Tests/Features/optional-summaries.feature`, `DynaDocs.Tests/Features/testing-facade.feature`, `DynaDocs.Tests/Features/workflow-retirement.feature`; `files` with no reached PDB document: 2 (`DynaDocs.Tests/AssemblyInfo.cs`, `DynaDocs.Tests/Integration/IntegrationTestCollection.cs`).

| Identity | Class | Points | Methods | PDB SHA-256 | Embedded | On disk |
|---|---|---|---|---|---|---|
| `DynaDocs.Tests/Commands/CheckDocValidatorTests.cs` | maintained | 73 | 14 | `3ABE721B357AF7A691E4FBC8B1AF3FAE96B5DDD2F2E4A44F2E6EBDC35FA5DF5B` | no | yes |
| `DynaDocs.Tests/Commands/ChiefOfStaffSyncTests.cs` | maintained | 44 | 7 | `C4CDCEBFD95E97BC20EB7E45203855F9EAAA45035CD4897553E84C7689D51E79` | no | yes |
| `DynaDocs.Tests/Commands/CommandDocConsistencyTests.cs` | maintained | 408 | 50 | `973C5A7A1DB295BD92822EF53A025F86113A92C2ED38E0AAC58D8A7B6BCCC5DE` | no | yes |
| `DynaDocs.Tests/Commands/CommandSmokeTests.cs` | maintained | 21 | 4 | `FF94044C72FFFACD5B9A6E88F8779D65A518E3AE6DCB9D4CD02843562A2305CF` | no | yes |
| `DynaDocs.Tests/Commands/CompleteCommandTests.cs` | maintained | 76 | 11 | `3660EE63D56616646FC85DC799208C43D3C58E74498E336E39697CB6BAFF509D` | no | yes |
| `DynaDocs.Tests/Commands/CompletionsCommandTests.cs` | maintained | 34 | 7 | `28052C6EB074A2155F5EBAFCFFC9D22F11588B2569BC2A437B10D9CA467CA485` | no | yes |
| `DynaDocs.Tests/Commands/FixFileHandlerTests.cs` | maintained | 118 | 15 | `762DA57541C7C8248EED0D85438D01CCD3059D75C7DD3BE0AB30A01B7013F6E4` | no | yes |
| `DynaDocs.Tests/Commands/FixHubHandlerTests.cs` | maintained | 60 | 7 | `7E44FC70BF3D54216F3FD7791326A94178F8A7672D721EC0098B3DF3383D97D0` | no | yes |
| `DynaDocs.Tests/Commands/GraphDisplayHandlerTests.cs` | maintained | 67 | 27 | `C43FFAF7408FEA0B09C6A0D5CE41D5C53857E690C4B56F2F452597886BC5289D` | no | yes |
| `DynaDocs.Tests/Commands/GuardCommandTests.cs` | maintained | 351 | 67 | `8D2E37B42E058FF9647D5489F26640B6BD3D771E70E900D6FBCCC6B1386DFBD3` | no | yes |
| `DynaDocs.Tests/Commands/HelpCommandTests.cs` | maintained | 53 | 9 | `2EB4BF1A7A5B20A3042F244F0C38B38C8B06CD10385C2478B73A2F3D400B0857` | no | yes |
| `DynaDocs.Tests/Commands/SyncCommandTests.cs` | maintained | 1088 | 185 | `796E7EEE66692054DCC31D0E348B79686EA1390FB8829F33CB340421CB5FB62B` | no | yes |
| `DynaDocs.Tests/Commands/ValidateCommandTests.cs` | maintained | 57 | 11 | `0CBBD27C7C4D7ED38A7E90073327B05F6D99C5DD8FBDD47CE8788CAAA691FE5A` | no | yes |
| `DynaDocs.Tests/Commands/WayfinderHarmonyTests.cs` | maintained | 62 | 10 | `46FE9164443A516F339CA95A63C1A5770265F853DA55EF89907299A210A36A21` | no | yes |
| `DynaDocs.Tests/ConsoleCapture.cs` | maintained | 96 | 6 | `A3A0EE84A5D6B7C5B1DDD521669053196368695A6F5B287CC5AE5128D5545A3C` | no | yes |
| `DynaDocs.Tests/ConsoleCaptureTests.cs` | maintained | 15 | 5 | `00CC8E80DF6786EC9AD791D68598FD62F2E2923B0495023B5382CBD84E2388B9` | no | yes |
| `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs` | maintained | 182 | 31 | `F842CE6BFE21ED990F67983A98714BD8BB242E05A9DC7FBD495659238E06A249` | no | yes |
| `DynaDocs.Tests/Features/assurance-adoption.feature` | maintained | 28 | 4 | `0503E8DB2BA78E53DB5056A2154F21AEE5088762A3CC7893FC21C083DC352337` | yes | yes |
| `DynaDocs.Tests/Features/experimental-beta.feature` | maintained | 26 | 2 | `C70A799287DD442E73DAE0E7067FDE2066696A028353A5EAFABD06AD2A53A683` | yes | yes |
| `DynaDocs.Tests/Features/fresh-installation.feature` | maintained | 26 | 1 | `0AA9F736C6ED145EDF4DD2224097EDE7C29F8CEA14732C21EAFFEB74E570FC6A` | yes | yes |
| `DynaDocs.Tests/Features/optional-summaries.feature` | maintained | 49 | 10 | `C305E924458CA37DCDF6A2D845F8D3B02797DE666628A9A493B7B84B7FDBE4C4` | yes | yes |
| `DynaDocs.Tests/Features/testing-facade.feature` | maintained | 180 | 24 | `844D99572A489C8E819B12C61DCBAEFD10D4C65DB88FF594B57D1B238E35C756` | yes | yes |
| `DynaDocs.Tests/Features/workflow-retirement.feature` | maintained | 40 | 3 | `F2D81D305B12D929DD07208D0211ACA1C147A085225A091A0917E6DFA4473661` | yes | yes |
| `DynaDocs.Tests/Integration/ChangelogStructureTests.cs` | maintained | 54 | 5 | `000E521F47DABB6DA0D08F573DF6C4AA4B9230971D51091E4C67F0435EF0AA59` | no | yes |
| `DynaDocs.Tests/Integration/CodexSyncArtifactsE2ETests.cs` | maintained | 173 | 16 | `0B5EA4E99AE27F7B431D374087ECBC0765A9ABF3CC0BBF7BDB91A9ACD00A92EE` | no | yes |
| `DynaDocs.Tests/Integration/DocumentationTests.cs` | maintained | 296 | 38 | `453DCA912DA0120A7BC3BFB57CDB4920476E0E8ACCD7440EE11336C7AA509440` | no | yes |
| `DynaDocs.Tests/Integration/EntryPointParityTests.cs` | maintained | 25 | 5 | `4CB2F1D3FAB4CA2734670610BA446539B7E33F4C9ACEAAC18352B83425383B57` | no | yes |
| `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs` | maintained | 319 | 26 | `215D91FE25CB227657E35E358D12E1A38E4DC44E94130DD72817726212D86987` | no | yes |
| `DynaDocs.Tests/Integration/GuardIntegrationTests.cs` | maintained | 224 | 34 | `2DB1906061BC2822E0A48FCFDF9019DE65B3CB773916B8DE61493F732FF10CF1` | no | yes |
| `DynaDocs.Tests/Integration/GuardSecurityTests.cs` | maintained | 376 | 90 | `93ABB49A979973A0531CBC998B7B2CF8919A65F66A50163FD184659371C1264F` | no | yes |
| `DynaDocs.Tests/Integration/GuardWorkerLaneTests.cs` | maintained | 99 | 20 | `955606A0E17DF0E2F72DB4BCF9BDA883CC00C0C588B9A141F7E9707F74134238` | no | yes |
| `DynaDocs.Tests/Integration/InitCheckIntegrationTests.cs` | maintained | 119 | 11 | `866F78110EA475A31D3F890BEE8A5D29B27F143FF773F7E70182987BE1A9A970` | no | yes |
| `DynaDocs.Tests/Integration/InitCommandTests.cs` | maintained | 410 | 57 | `641C91876C67C635D3E9ECB4D46526FFC6CD768FBB4A330F2CB16A66892C3F1B` | no | yes |
| `DynaDocs.Tests/Integration/IntegrationTestBase.cs` | maintained | 111 | 32 | `0423135F8851CF0B23AC559279B3DFEF0FF408594B60C8C7F9D31B844743E2E8` | no | yes |
| `DynaDocs.Tests/Integration/ProcessWorkflowTests.cs` | maintained | 6 | 1 | `FCA89350992A75928AD42EC3088E9ACF94CDE4F9606C9A05C12A828A640492B1` | no | yes |
| `DynaDocs.Tests/Integration/ScanExcludeInvariantsTests.cs` | maintained | 47 | 8 | `638D7C5A5B1DB6653AFC132D458B8D439F645026F3BE55A5AEEFF1A9E00233B7` | no | yes |
| `DynaDocs.Tests/Integration/TemplateCommandTests.cs` | maintained | 280 | 25 | `B43F0C153E91C75C6B6411AFBF146F84180B39BD0C6FADE289C8E0134B22543A` | no | yes |
| `DynaDocs.Tests/Integration/TemplateScaffoldingTests.cs` | maintained | 148 | 19 | `AD6A7CA75A66D7F9442EAA7EFB19E15D56EA53F309F3BA273868820BB1733507` | no | yes |
| `DynaDocs.Tests/Integration/UpstreamSkillSourceTests.cs` | maintained | 73 | 13 | `B7921B2E84D8D7BB2F8C4E098FFD5C68506B54E7CAF17C5B2B7559BA243308F7` | no | yes |
| `DynaDocs.Tests/Models/DocFileTests.cs` | maintained | 11 | 3 | `58ACF6BA138E9CD331B92995221933B65BEBD25B5D9B64B162C14024D8426062` | no | yes |
| `DynaDocs.Tests/Models/HookInputExtensionsTests.cs` | maintained | 55 | 14 | `6C816D08C65542628CC6256CB79C6C89117241CA9355B2820DEB8AA2FFE23DCE` | no | yes |
| `DynaDocs.Tests/Models/HookInputTests.cs` | maintained | 53 | 5 | `7FD1FEC383C3BDBAC2E2A58F1E375EB5B550BA81FEACA853BF5D07A69F87103D` | no | yes |
| `DynaDocs.Tests/Models/ToolInputDataTests.cs` | maintained | 51 | 5 | `1819EEE60DD4C29963AC723A88D91F8C0C5C1AF6593B24CF10090C886C68C096` | no | yes |
| `DynaDocs.Tests/Rules/BrokenLinksRuleTests.cs` | maintained | 127 | 23 | `6F2F322BFFFABC7D6834530431D1511BCD3E1B2466E1F6D65A9108713042B290` | no | yes |
| `DynaDocs.Tests/Rules/FolderMetaFilesRuleTests.cs` | maintained | 71 | 15 | `7DE113571B265AFFD94BDF2335961F3787C651C5A8C5EFA3491D37D113420CD5` | no | yes |
| `DynaDocs.Tests/Rules/FrontmatterRuleTests.cs` | maintained | 172 | 39 | `A64C914285D32C5E654D0B1635019E7F792C51A2495B5A399CE72761CAC85331` | no | yes |
| `DynaDocs.Tests/Rules/FutureFeatureRuleTests.cs` | maintained | 164 | 36 | `B08C174BC128458C53410EF021F4FF173B801DCAD5C0264F756FAF6CC6DC4F22` | no | yes |
| `DynaDocs.Tests/Rules/HubFilesRuleTests.cs` | maintained | 62 | 13 | `C286E8D8977D1E28CC12841485047368D078C968CC9A456E0C2ECB56B04B69AD` | no | yes |
| `DynaDocs.Tests/Rules/LegacyPmRecordRuleTests.cs` | maintained | 65 | 15 | `C265733B35A0E0E3F1B8590927CD8AC2F7048EA709AB707CAC050F76DAE1819B` | no | yes |
| `DynaDocs.Tests/Rules/NamingRuleTests.cs` | maintained | 111 | 22 | `0A349EA6E01196CF2E7A00F35A153FBA7F4EF98629E9DE9431D5373F4C5B56E8` | no | yes |
| `DynaDocs.Tests/Rules/OffLimitsRuleTests.cs` | maintained | 64 | 21 | `0DE5AD0887BBFC123D43A7AF8F4DBA3A24D3DE29417C73F18A8476CB56A54F5D` | no | yes |
| `DynaDocs.Tests/Rules/OrphanDocsRuleTests.cs` | maintained | 172 | 25 | `77C8940CCEBDD104A052EC3B2173722EEC5ABCB094EA476903CF9AE1BB39AB7E` | no | yes |
| `DynaDocs.Tests/Rules/RelativeLinksRuleTests.cs` | maintained | 53 | 11 | `C95243FBF9E11AD5880F282E2DAA4887E2923BBB89F6ACF04A2B8A389B830552` | no | yes |
| `DynaDocs.Tests/Rules/TitleRuleTests.cs` | maintained | 25 | 6 | `66C29A53176A34946B9E112B33187741B6AA5BD0F4C5465930E8FF1806464A66` | no | yes |
| `DynaDocs.Tests/Rules/UncustomizedDocsRuleTests.cs` | maintained | 36 | 8 | `ED07CF8DE62A62FEAE08C7E8614E65FE7FE2DF66B1DF3C3A14F754B2D200C025` | no | yes |
| `DynaDocs.Tests/RuntimeRegression/ParallelisationDisabledTests.cs` | maintained | 5 | 1 | `4F3AE461C74E6A66FE5B8075788EBFF9F97D13278393FB24897AA5F5B43AD234` | no | yes |
| `DynaDocs.Tests/Services/BashAnalysisResultTests.cs` | maintained | 47 | 8 | `5084244B5EAA5A914FB1E1159B9B62C3F3338E7208D16582AF4DB4B26C539225` | no | yes |
| `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs` | maintained | 427 | 143 | `6C8EEE53CBA600CB17053A21E811A0BCF461EEF78646FFC3C68692369344CC3A` | no | yes |
| `DynaDocs.Tests/Services/CompletionProviderTests.cs` | maintained | 18 | 3 | `70E4D41EFF72F9135C1C0E1FFDCD9E1D3F0CD37581E4BAA5A76C2EA27CE90727` | no | yes |
| `DynaDocs.Tests/Services/ConfigFactoryTests.cs` | maintained | 135 | 32 | `CF59968A69AEDD8C9E35FB0315674E8A8CD8BC4A3745BBB2227C38C0D8E6D91F` | no | yes |
| `DynaDocs.Tests/Services/ConfigServiceTests.cs` | maintained | 177 | 24 | `2BEA8361857017A8B6DD5FEACBB02F5DFC3B59CD26CC927513B7160ABDB28A2E` | no | yes |
| `DynaDocs.Tests/Services/DocGraphTests.cs` | maintained | 111 | 24 | `841C99987DD37CD3A72B01BB653EBED6C8086D37CBC88723522F40247DFD4415` | no | yes |
| `DynaDocs.Tests/Services/DocScannerTests.cs` | maintained | 98 | 35 | `B637E10157A777C998982CE3A5E0FA4CFE7180E7E71E44DB21FEF0844ABDBE23` | no | yes |
| `DynaDocs.Tests/Services/FolderScaffolderTests.cs` | maintained | 127 | 18 | `975232912CF3089F9300F2C97B990FCA871D861D781BAE7DD14980A325A38AAA` | no | yes |
| `DynaDocs.Tests/Services/FrontmatterTypesServiceTests.cs` | maintained | 82 | 13 | `8B4EE591B55D4EEC866FB80261645FD8D2B5A32413BC79438916CBBDED374064` | no | yes |
| `DynaDocs.Tests/Services/HubGeneratorTests.cs` | maintained | 105 | 25 | `2ACBBA9C232AD69727E032AD47F0FD0385BA048BFE233A1E506D79026174A37A` | no | yes |
| `DynaDocs.Tests/Services/IndexGeneratorTests.cs` | maintained | 34 | 5 | `98F0467A46254A175AE9BA9372398B95A3E07ED9A8C947A2A6D6C81F68C04929` | no | yes |
| `DynaDocs.Tests/Services/LegacyPmManifestServiceTests.cs` | maintained | 89 | 24 | `6E5EB86D4C8233D6AE00E77E1BB04161DD9983B9F1B80B931DBA3F1E0E226538` | no | yes |
| `DynaDocs.Tests/Services/LinkResolverTests.cs` | maintained | 175 | 29 | `9C2062CE335818929754DFD87C6FAF88DB6B51C68D19756A525251EAE3BD5254` | no | yes |
| `DynaDocs.Tests/Services/MarkdownParserTests.cs` | maintained | 138 | 23 | `5C034370003CEEDC38D792FF02AB8CE0265CA217C26A42241D90C885B4F4502E` | no | yes |
| `DynaDocs.Tests/Services/OffLimitsServiceTests.cs` | maintained | 344 | 53 | `25C580FC3ACBAAAEDCD548509069CCE1EFD87A0F529A0AEDD3A5447D31FC2488` | no | yes |
| `DynaDocs.Tests/Services/PathUtilsDiscoveryTests.cs` | maintained | 110 | 16 | `AB0F5FB871D2C3B598B7CAB9872571A858EE3DCCA0E53B78C170CA69CABAF97B` | no | yes |
| `DynaDocs.Tests/Services/PathUtilsTests.cs` | maintained | 61 | 12 | `FAFE8653181CD281975D4C22F0591169102B9D4FE9880D73165F97FDB8DD57E7` | no | yes |
| `DynaDocs.Tests/Services/PathUtilsWorktreeIsolationTests.cs` | maintained | 70 | 7 | `C61BA97B10208AD26CB3ABFB37C28C08A129DF87E639C7143A7ABF672F3EABD0` | no | yes |
| `DynaDocs.Tests/Services/ResolveIncludesTests.cs` | maintained | 114 | 19 | `D48BB120950CCC0EE8E47BDB65322C55A8265A20EDB92AB5505BA29B42BA83F4` | no | yes |
| `DynaDocs.Tests/Services/ShellCompletionInstallerTests.cs` | maintained | 452 | 46 | `860EA33655F3BC54C9D9205690EB749D83EB538B8AFDFDDE1D68175E5D4705A1` | no | yes |
| `DynaDocs.Tests/Services/SkillTemplateServiceTests.cs` | maintained | 165 | 34 | `4BA19687D9BF3EA42DFBF4C74109D8E5F0AE8C07F2CAD8D103A362E6705B5108` | no | yes |
| `DynaDocs.Tests/Services/TemplateGeneratorTests.cs` | maintained | 308 | 51 | `952655681909B036FCA63DCAA10B260EB1A15330C33EBC2B5F72D619E92C35B6` | no | yes |
| `DynaDocs.Tests/Services/TemplateUpdateTests.cs` | maintained | 100 | 15 | `50C9FA705856BDC025E923B16C91DDFBCB045AACC676F1C738AF960221A54496` | no | yes |
| `DynaDocs.Tests/Services/ValidationServiceTests.cs` | maintained | 92 | 29 | `3D5F32522E962FF1231564D8D80FCD126C1D1E56EAD1F9678A91827E519C6764` | no | yes |
| `DynaDocs.Tests/Steps/AssuranceAdoptionSteps.cs` | maintained | 40 | 13 | `176F60DDDA785816795A42AC218118C5407469DE9729C78C75AE4457C5E20076` | no | yes |
| `DynaDocs.Tests/Steps/CheckAssertionTests.cs` | maintained | 45 | 16 | `DE363F288043B8113BDA71B7F709D438CD6FC0F49DEE4B73A102CEF7BC3BE540` | no | yes |
| `DynaDocs.Tests/Steps/CliProcessProbe.cs` | maintained | 20 | 8 | `1E4E637DF07B5DD8A90B767A10CEF5028F69C1C778EBE0378026EF74B7AADF8C` | no | yes |
| `DynaDocs.Tests/Steps/CliResult.cs` | maintained | 9 | 9 | `E87E1B3AA380B7891606167E258B1F1029AF3BCE525DD6F4EE7A9718DD5F72A2` | no | yes |
| `DynaDocs.Tests/Steps/CliScenario.cs` | maintained | 62 | 11 | `116A8A35981ABEE5BB652027987106975267F49BA21836F616D4D28F204714AC` | no | yes |
| `DynaDocs.Tests/Steps/CliScenarioIsolationTests.cs` | maintained | 12 | 1 | `B96B20BD9D2FF5B7826405D2E1DA6E32F780E028817174941C5381C59C179BF2` | no | yes |
| `DynaDocs.Tests/Steps/CliScenarioProcessTests.cs` | maintained | 77 | 14 | `63B60361483AAE27839E929A17FD0C6F1BA8A381B4E6505EBD4BF967D980C7E6` | no | yes |
| `DynaDocs.Tests/Steps/ExperimentalBetaSteps.cs` | maintained | 85 | 17 | `9496450C991B02B5CCE006F42A0F7996F6B0E8721BCFD51913D97FDCC825B5A6` | no | yes |
| `DynaDocs.Tests/Steps/FreshInstallationAssertionTests.cs` | maintained | 106 | 20 | `4704196D448871A7C02D618E5FC58EE1D4C77BC7449743ED78FD2B0B1FEB18AC` | no | yes |
| `DynaDocs.Tests/Steps/FreshInstallationSteps.cs` | maintained | 117 | 29 | `AC34A0CBC2A6E93D5B5561E12C7741BDB422AB35C5510DA006A971E1C08FEE62` | no | yes |
| `DynaDocs.Tests/Steps/OptionalSummaryAssertionTests.cs` | maintained | 30 | 8 | `34F848003DA0E6EE8FE90E6B7E15B6E81D937492CA089A187416854F30B1D7F1` | no | yes |
| `DynaDocs.Tests/Steps/OptionalSummarySteps.cs` | maintained | 47 | 22 | `8FCB7E2BFEEB2FD366FA3C967CD349C70D7966389D22ED720F68FB7D7EA4C1A7` | no | yes |
| `DynaDocs.Tests/Steps/TestingFacadeSteps.cs` | maintained | 114 | 8 | `BC895EE9B06ED9D6BA04F397A66B88F4434D0CADF6ED7260AA8A9F7D586C6E95` | no | yes |
| `DynaDocs.Tests/Steps/WorkflowRetirementAssertionTests.cs` | maintained | 113 | 14 | `39C21B73CF0DEBE58FA31BA7AEEA56E0CF292C77A7CF1F73FC6C9BFA16C8F950` | no | yes |
| `DynaDocs.Tests/Steps/WorkflowRetirementSteps.cs` | maintained | 88 | 16 | `6CAF19BA42CDB30007E982441063ECE8233F25CF928291CAE82709168256B7E9` | no | yes |
| `DynaDocs.Tests/UndeletableFile.cs` | maintained | 15 | 2 | `9805E9C4AC38F1C7886EEE977263A24E783F00D11D44DE87FA22F7B65A2F51EF` | no | yes |
| `DynaDocs.Tests/Utils/FrontmatterParserTests.cs` | maintained | 197 | 39 | `C7BC6D98B405148019B75BB5CD6B3264C5E3EC5EBDCB6C24AB32FF70E3A9BD18` | no | yes |
| `DynaDocs.Tests/Utils/GlobMatcherTests.cs` | maintained | 34 | 10 | `65EA5FC95778CE7338DC7D30241D58BDB999782587EE822B01AFE83AD1BA53A7` | no | yes |
| `DynaDocs.Tests/Utils/RuleSkipPathsTests.cs` | maintained | 6 | 2 | `9A5DA5421ACFF6AA2EE3FAF6D0A3E2F258F83AC29272174782F418F9299CC358` | no | yes |
| `DynaDocs.Tests/Utils/TitlePrettifierTests.cs` | maintained | 1 | 1 | `D43E3D03B68EFF0B0A46BA3FBD1EC633AF8EDA8B0581C4ABE27AD05A32FA64D1` | no | yes |
| `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs` | maintained | 20 | 2 | `BCC187A2E4659C214EC3D8A6097E03BFD340BAFB12A78E579434EF485527C5A1` | no | yes |
| `DynaDocs.Tests/obj/Debug/net10.0/Features/assurance-adoption.feature.cs` | generated | 2 | 1 | `E1A02CCF3B50A5EABDAF8A1045EF59A54C816ABDB91379EDFFFCAEBEE6EEB643` | yes | yes |
| `DynaDocs.Tests/obj/Debug/net10.0/Features/experimental-beta.feature.cs` | generated | 2 | 1 | `F032368C9E51E3E7B84BD1A3D498030E3EA1DC6DBD524B678CF49F46082AA8E3` | yes | yes |
| `DynaDocs.Tests/obj/Debug/net10.0/Features/fresh-installation.feature.cs` | generated | 2 | 1 | `B75BF54BB7125B4005BD6AA9CE49F067ADF423303C5AED53E925C5BB8EA2687E` | yes | yes |
| `DynaDocs.Tests/obj/Debug/net10.0/Features/optional-summaries.feature.cs` | generated | 2 | 1 | `AA5264D902B1971FE2418EE0DBB5A780A05CBA530027C5A4947D3D6E64976876` | yes | yes |
| `DynaDocs.Tests/obj/Debug/net10.0/Features/testing-facade.feature.cs` | generated | 2 | 1 | `872BA08860A8AA900C4089308ABE96BED91071FE5D6D81E1B9EC406FEAE6A347` | yes | yes |
| `DynaDocs.Tests/obj/Debug/net10.0/Features/workflow-retirement.feature.cs` | generated | 2 | 1 | `CF4F2E5A2CED193754BB687340A478B09D821D7F0CFA16103B18CA94599FD7F6` | yes | yes |
| `DynaDocs.Tests/obj/Debug/net10.0/xUnit.AssemblyHooks.DynaDocs_Tests.cs` | generated | 8 | 2 | `FD932F77E271A479AB027C6032A3934271976D75B3200DF9184B1C012872988B` | yes | yes |
| `nuget:microsoft.net.test.sdk/18.0.1/build/net8.0/Microsoft.NET.Test.Sdk.Program.cs` | package | 2 | 1 | `D66F12F9433816A8C7DC1B2ACB9E1010F5418F5EEB51E80F1DF57AA1DCAB8D8A` | yes | yes |

Not reached by any non-hidden sequence point: `DynaDocs.Tests/AssemblyInfo.cs` (maintained), `DynaDocs.Tests/Integration/IntegrationTestCollection.cs` (maintained), `DynaDocs.Tests/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs` (generated), `DynaDocs.Tests/obj/Debug/net10.0/DynaDocs.Tests.AssemblyInfo.cs` (generated), `DynaDocs.Tests/obj/Debug/net10.0/DynaDocs.Tests.GlobalUsings.g.cs` (generated).

The package document URL is `C:\Users\User\.nuget\packages\microsoft.net.test.sdk\18.0.1\build\net8.0\Microsoft.NET.Test.Sdk.Program.cs`; its source is `[Microsoft.VisualStudio.TestPlatform.TestSDKAutoGeneratedCode] class AutoGeneratedProgram {static void Main(string[] args){}}`. The six `.feature` documents are reached only through the `#line` pragmas of the Reqnroll code-behind under `DynaDocs.Tests/obj/Debug/net10.0/Features/`; each scenario method's points lie wholly in its `.feature` document, so no method mixes origins.

## GateMetrics

Document table rows: 22; reached by non-hidden sequence points: 19 (maintained 19); not reached: 3. Checksum algorithm SHA-256 on every reached document; embedded source present on 0 reached documents; every reached document exists on disk and its bytes match the PDB checksum (19/19); language GUID `3f5162f8-07c6-11d3-9053-00c04fa302a1` (C#) throughout.
Methods with a debug-information row: without sequence points 222, hidden-only 0, all points maintained 267, all points generated 0, all points package 0, mixed origins 0, spanning more than one document 0.
Compilation level (`--project`): 19 `files`, 3 `generated_files`. Every generated or package PDB identity is in `generated_files` (0/0); maintained PDB identities not in `files`: none; `files` with no reached PDB document: 0.

| Identity | Class | Points | Methods | PDB SHA-256 | Embedded | On disk |
|---|---|---|---|---|---|---|
| `DynaDocs.Tests/coverage/metrics/AssemblyFacts.cs` | maintained | 18 | 18 | `5B2559CEE3C2427E1BD139DDB00D01FBFA421D9182948CE0F965BD560A394D82` | no | yes |
| `DynaDocs.Tests/coverage/metrics/AssemblyMetrics.cs` | maintained | 50 | 12 | `D2095F6C459D8374388EA947A1A7EEBE9F09806BDCC7A5C97BB935E82DD78677` | no | yes |
| `DynaDocs.Tests/coverage/metrics/BehaviorFacts.cs` | maintained | 10 | 10 | `C957C3D63396042D3D3217B308142880AD9603B3940EF735912B44B62B05AFB6` | no | yes |
| `DynaDocs.Tests/coverage/metrics/BehaviorFragment.cs` | maintained | 18 | 18 | `446D5A2AF98A67C262A173038003FFF6CF404DCB00E4A16A0768FD490D0259AC` | no | yes |
| `DynaDocs.Tests/coverage/metrics/CompiledMethod.cs` | maintained | 16 | 16 | `97198AB1719EA2B9012236C7B546C90888EEDA18B70C9EB189808F0BF3DB739D` | no | yes |
| `DynaDocs.Tests/coverage/metrics/ConstructorFacts.cs` | maintained | 12 | 12 | `34826CA0245E5C0900A6A9844E6FEA80215698196300A2D0CA36D634F6FCA000` | no | yes |
| `DynaDocs.Tests/coverage/metrics/FileFacts.cs` | maintained | 6 | 6 | `4C52CAF45A266CF7027F9F255B3E61DD9D05D0B6FBBA841BA480E0F6A2AED060` | no | yes |
| `DynaDocs.Tests/coverage/metrics/MethodIdentity.cs` | maintained | 48 | 9 | `52AE624651BB13AEFDD193CE855BD7315A5FD839B32902D4EC483BC974C6E47E` | no | yes |
| `DynaDocs.Tests/coverage/metrics/NamespaceDependencies.cs` | maintained | 40 | 10 | `2DB90E941CC30B1020EC12060260E7745847B6EEC18F1B4DF421B10B273B50AB` | no | yes |
| `DynaDocs.Tests/coverage/metrics/Program.cs` | maintained | 25 | 4 | `D07BAD4104652868E0DA39A9A587868CB9F62FE5D6DE10BEEA4F6914EAB77EC6` | no | yes |
| `DynaDocs.Tests/coverage/metrics/ProjectFacts.cs` | maintained | 16 | 16 | `A3714AD4ECBAD7E1E0EDF18E02D5C22FD572A6240A3C2106C1C3E297547C62DA` | no | yes |
| `DynaDocs.Tests/coverage/metrics/ProjectMetrics.cs` | maintained | 85 | 12 | `7C89EB128A9147B00708805E40CBEFF7D40C5CDA69B04F9ACA8EDC31790F214B` | no | yes |
| `DynaDocs.Tests/coverage/metrics/SemanticMethod.cs` | maintained | 14 | 14 | `BD27259FA50E0AA8B7A3C044D666D87B9D29DAD454CBA80081A430591130A271` | no | yes |
| `DynaDocs.Tests/coverage/metrics/SourceBehavior.cs` | maintained | 148 | 22 | `D4E6AE9CD935B5C18976AB4D957D35EF74D67946550CD1561B42BE741B0878D9` | no | yes |
| `DynaDocs.Tests/coverage/metrics/SourceMember.cs` | maintained | 30 | 30 | `66FD1BC61D286756C85C0FC6150B2D9810D33DD361225834FF77F7195E8D63F3` | no | yes |
| `DynaDocs.Tests/coverage/metrics/SourceMetrics.cs` | maintained | 109 | 20 | `5BDD1020272AB1666B8CA0B54AAF9603C290A07236AE8A9BFF89D2197B5B83C1` | no | yes |
| `DynaDocs.Tests/coverage/metrics/SourcePoint.cs` | maintained | 18 | 18 | `7EF61EBD39BCEE52FD140744667498631CE91B30DF25B98C792CA69B06A5227E` | no | yes |
| `DynaDocs.Tests/coverage/metrics/SourceToken.cs` | maintained | 14 | 14 | `85A933DA8E1D969E471B36A1D7958CCEFF8A9858BEE808EB0B96F6FFA03EA9A5` | no | yes |
| `DynaDocs.Tests/coverage/metrics/StructuralMethod.cs` | maintained | 6 | 6 | `1C1474013C8BC7371F71478535231372FAC8407056F6F43529A0AF96D779C1FC` | no | yes |

Not reached by any non-hidden sequence point: `DynaDocs.Tests/coverage/metrics/obj/Debug/net10.0/.NETCoreApp,Version=v10.0.AssemblyAttributes.cs` (generated), `DynaDocs.Tests/coverage/metrics/obj/Debug/net10.0/GateMetrics.AssemblyInfo.cs` (generated), `DynaDocs.Tests/coverage/metrics/obj/Debug/net10.0/GateMetrics.GlobalUsings.g.cs` (generated).

## Fixture facts

Scratch projects (`net10.0`, `NuGetAudit=false`, restored from the local package cache only), enumerated at both levels. `Source.cs` is `public class C { public int M() => 1; }` unless stated.

| Fixture | Shape | PDB-level reached documents (class) | Method mix | `--project generated_files` agrees |
|---|---|---|---|---|
| sdk | `<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />` | `Source.cs` (maintained, 1 points, on disk yes); `nuget:microsoft.net.test.sdk/18.0.1/build/net8.0/Microsoft.NET.Test.Sdk.Program.cs` (package, 2 points, on disk yes) | maintained 1, generated 0, package 1, mixed 0 | the same `nuget:` identity is listed |
| objitem | `<Compile Include="obj/Gen.cs" />` with `public static class Gen { public static int G() { return 2; } }`; `M() => Gen.G()` | `Source.cs` (maintained, 1 points, on disk yes); `obj/Gen.cs` (generated, 3 points, on disk yes) | maintained 1, generated 1, package 0, mixed 0 | `obj/Gen.cs` listed |
| sg-emit | `EmitCompilerGeneratedFiles=true`; `[JsonSerializable(typeof(int[]))] internal partial class Ctx : JsonSerializerContext { }` beside `C` | `Source.cs` (maintained, 1 points, on disk yes); `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.GetJsonTypeInfo.g.cs` (generated, 13 points, on disk yes); `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.Int32.g.cs` (generated, 9 points, on disk yes); `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.Int32Array.g.cs` (generated, 25 points, on disk yes); `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.g.cs` (generated, 46 points, on disk yes) | maintained 1, generated 15, package 0, mixed 0 | all four generator documents listed under `obj/Debug/net10.0/generated/` |
| sg-default | same as sg-emit without `EmitCompilerGeneratedFiles` | `Source.cs` (maintained, 1 points, on disk yes); `obj/Debug/net10.0/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.GetJsonTypeInfo.g.cs` (generated, 13 points, on disk no); `obj/Debug/net10.0/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.Int32.g.cs` (generated, 9 points, on disk no); `obj/Debug/net10.0/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.Int32Array.g.cs` (generated, 25 points, on disk no); `obj/Debug/net10.0/System.Text.Json.SourceGeneration/System.Text.Json.SourceGeneration.JsonSourceGenerator/Ctx.g.cs` (generated, 46 points, on disk no) | maintained 1, generated 15, package 0, mixed 0 | all four listed under `obj/Debug/net10.0/` (no `generated/` segment) |

Observations: the SDK fixture reproduces the exact disproving document and its package-only `Main` (token 100663299 there, 100663421 in `DynaDocs.Tests`); a source generator without `EmitCompilerGeneratedFiles` still writes absolute in-root `obj/...` URLs with embedded source and no file on disk, so classification cannot depend on existence; the compilation level (`ProjectMetrics`) emits byte-identical identities for every generated and package document in all four shapes.

## AltCover probes (production prepare flags)

Both probes ran `dotnet tool run altcover -- --inputDirectory=<fixture bin> --outputDirectory=<fixture instrumented> --report=<template> --reportFormat=OpenCover --eager --localSource --visibleBranches --showGenerated --assemblyFilter=^(?!Subject$).*` from the worktree (tool manifest only), exit 0.

1. `--localSource` with absent generated documents: prepare-only on `sg-default` (four generator documents absent on disk) and `sg-emit` (present). Both template reports list all 15 `Ctx`/`C` methods with their sequence points and all five `File` rows, none marked `skippedDueTo`; `fullPath` equals the PDB document URL verbatim. `--localSource` is therefore assembly-level and drops nothing whose document is absent.
2. `--showGenerated` visit-count marker (adjacent fact, outside this amendment; returned to the captain): fixture `gen-vc` (Exe, SDK reference, `[ModuleInitializer]` calling `Entry.Run()`), prepare then `runner --recorderDirectory=<instrumented> --workingDirectory=<instrumented> --executable=dotnet --outputFile=<collected> --summary=N -- Subject.dll`, exit 0, 21 visits. Collected rows:

| Method | Attributes | Executed | `SequencePoint vc` | `visited` |
|---|---|---|---|---|
| `GenType::Called()` | type `[GeneratedCode][CompilerGenerated]` | yes | 1,1,1 | true |
| `GenType::Never()` | type `[GeneratedCode][CompilerGenerated]` | no | -2,-2,-2 | true |
| `Plain::NeverAttributed()` | method `[GeneratedCode]` | no | -2,-2,-2 | true |
| `Plain::NeverPlain()` | none | no | 0,0,0 | false |
| `Plain/<>c::<Lambdas>b__2_0()` | compiler lambda, invoked | yes | 1 | true |
| `Plain/<>c::<Lambdas>b__2_1()` | compiler lambda, never invoked | no | -2 | true |
| `AutoGeneratedProgram::Main(System.String[])` | `[TestSDKAutoGeneratedCode]` | yes (entry point) | 1,1 | true |

Under the pinned production flags AltCover 9.0.102 reports unvisited sequence points of attribute-generated methods (`GeneratedCodeAttribute` on the method or its type, and `CompilerGeneratedAttribute` bodies such as never-invoked lambdas in maintained code) as `vc="-2"` with `visited="true"`, exactly as its help states (`--showGenerated: Mark generated code with a visit count of -2 (Automatic) for the Visualizer if unvisited`); the pinned physical and child probes did not use `--showGenerated` and saw `vc="0"` for the never-invoked lambda. `csharp_join.coverage_methods` rejects any negative `vc` (`Invalid native sequence value`). This contradiction between the accepted prepare command and the accepted non-negative `vc` rule is independent of document origin (it hits maintained-origin lambdas in `dydo` and generated-origin Reqnroll methods in `DynaDocs.Tests` alike) and is not settled by this amendment.

## Result

The root-relative rule is disproved by a legitimate, locked, reproducible package document; the amended rule classifies every reached document of the three required assemblies positively (`dydo` 76/29/0, `DynaDocs.Tests` 101/7/1, `GateMetrics` 19/0/0 maintained/generated/package) with no failing control triggered and no mixed-origin method, and both levels agree on every identity.
