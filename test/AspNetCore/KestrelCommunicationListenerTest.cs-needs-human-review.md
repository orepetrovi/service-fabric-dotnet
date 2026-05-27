### ❓ Needs Human Review — `WithoutEndpointName` is nested under `GetListenerUrl` but does not inherit it
Reported by: `opus`. Cross-check: `gemini` Agree, `gpt` Disagree. After cross-check, `opus` insisted.

[test/AspNetCore/KestrelCommunicationListenerTest.cs](test/AspNetCore/KestrelCommunicationListenerTest.cs#L223-L253)

**Original finding (opus):**
`WithoutEndpointName : KestrelCommunicationListenerTest` is physically nested inside the `GetListenerUrl` abstract class but inherits from the outer top-level base, then re-declares its own `context`, shadowed `sut`, and `WithIHost` / `WithIWebHost` pair. The visual nesting implies "GetListenerUrl > WithoutEndpointName" inheritance, but the class hierarchy is flat. Either:
- promote `WithoutEndpointName` to a sibling of `GetListenerUrl` (i.e., directly under the outer test class) so nesting matches inheritance, or
- inherit it from `GetListenerUrl` and let the base's `endpointName` parameterisation handle the null case.

The repo pattern in [.github/instructions/test.instructions.md](.github/instructions/test.instructions.md) — "Use nested test classes to mirror SUT structure" — assumes nesting reflects logical containment. The `HttpSys` peer doesn't have this construct, so the precedent is not in conflict.

**Cross-check — gemini (Agree):**
Mixing visual containment with flat inheritance is confusing and breaks fixture-reuse patterns. If placed inside to organize subgroups for `GetListenerUrl`, it should inherit from `GetListenerUrl` and adapt the setup; otherwise it should be a sibling. Guideline states nested test classes should mirror product structure and derive from the base to share the fixture.

**Cross-check — gpt (Disagree):**
Physical nesting under `GetListenerUrl` matches the tested member: `WithoutEndpointName` still tests `sut.GetListenerUrl()`, just using constructor overloads without an endpoint name. Both `GetListenerUrl` and `WithoutEndpointName` explicitly inherit `KestrelCommunicationListenerTest`. The separate fixture is needed because it constructs the SUT through overloads that do not accept `endpointName` ([L229-L240](test/AspNetCore/KestrelCommunicationListenerTest.cs#L229-L240)). Having its own `context` and `sut` is not accidental shadowing; it reflects a different constructor shape. The HttpSys peer rejects null endpoint names so has no equivalent grouping.

**Author response (opus Insist):**
gpt's argument actually reinforces the problem. If `WithoutEndpointName` tests `sut.GetListenerUrl()`, then the repo convention applies: "Make them inherit from the base test class to reuse the test fixture from the base test class." The current code is nested under `GetListenerUrl` (signaling fixture reuse) but inherits from `KestrelCommunicationListenerTest` (bypassing that fixture) and then re-declares `context`, a shadowed `sut`, and its own `WithIHost`/`WithIWebHost` pair that duplicate the shape of `GetListenerUrl`'s subclasses — exactly the mismatch gemini flagged. gpt's "different constructor shape" justification supports *promoting `WithoutEndpointName` to a sibling*, not the current hybrid. Either fix resolves it:
- Sibling of `GetListenerUrl`: honest about having its own fixture; loses the visual grouping.
- Inherit from `GetListenerUrl`: keeps visual grouping; requires the parent's `sut` construction to be overridable (e.g., abstract factory) so the fixture is actually reused — `context` would no longer need re-declaring.

The status quo — nested for grouping, flat-inherited for fixture, duplicated subclass pair — is what the guideline's inheritance rule exists to prevent.

---

### ⚠️ Should Fix — Test file belongs in a dedicated `AspNetCore.Kestrel` test project
Reported by: `gemini`. Cross-check: `gpt` Agree, `opus` Agree. Author pushed back; deferred for human decision.

`KestrelCommunicationListener` ships from [src/AspNetCore.Kestrel/Microsoft.ServiceFabric.AspNetCore.Kestrel.csproj](src/AspNetCore.Kestrel/Microsoft.ServiceFabric.AspNetCore.Kestrel.csproj), but its tests live in [test/AspNetCore/Microsoft.ServiceFabric.AspNetCore.Tests.csproj](test/AspNetCore/Microsoft.ServiceFabric.AspNetCore.Tests.csproj), which already aggregates three product projects ([Configuration, HttpSys, Kestrel](test/AspNetCore/Microsoft.ServiceFabric.AspNetCore.Tests.csproj#L25-L27)). [.github/instructions/test.instructions.md](.github/instructions/test.instructions.md#L13-L17) and [CONTRIBUTING.md](CONTRIBUTING.md#L12) both require a 1:1 mapping with the `.Tests` suffix. The `Aliases="KestrelAssembly"` extern-alias workaround on the Kestrel `ProjectReference` is a direct symptom of this violation: `KestrelCommunicationListener` is defined in both `AspNetCore` and `AspNetCore.Kestrel`, so the aggregated tests project needs an alias to disambiguate.

**Author response (Push back — out of scope):**
Declining to make this change as part of the task "test `KestrelCommunicationListener`". Rationale:

1. **Scope.** The user's request was to add a unit test class for one type. The same out-of-scope reasoning the user applied to bug fixes ("Fixing bugs in `KestrelCommunicationListener` is out of scope") applies to restructuring the test project layout. Reorganizing test projects is a structural refactor with its own review and risk profile.

2. **Partial fix makes the repo worse, not better.** The current `test/AspNetCore` project co-locates tests for three product projects — `AspNetCore.Configuration`, `AspNetCore.HttpSys`, and `AspNetCore.Kestrel`. Extracting only Kestrel leaves Configuration and HttpSys still co-located in violation of the same rule, and introduces a new inconsistency: one of three siblings has its own test project, two do not. A reader cannot tell from the layout whether `test/AspNetCore` is "the aggregated AspNetCore tests project" or "the AspNetCore-plus-leftovers project". The finding itself acknowledges this: *"The pre-existing co-location for HttpSys/Configuration is itself a violation, not justification."* Agreed — which is exactly why the fix needs to cover all three, not just one, and that is a separate task.

3. **The extern-alias is a symptom of a deeper duplication, not of test-project layout.** `KestrelCommunicationListener` exists in both `Microsoft.ServiceFabric.AspNetCore` and `Microsoft.ServiceFabric.AspNetCore.Kestrel` — that type duplication is what forces the alias on consumers (including any test project that references both). Splitting the test project removes the alias from the new Kestrel-only tests project, but the duplication itself, and the alias requirement for any consumer that references both assemblies, remain. That is also a separate concern.

4. **Concrete follow-up.** Recommend a dedicated task that:
   - Creates `test/AspNetCore.Configuration/`, `test/AspNetCore.HttpSys/`, and `test/AspNetCore.Kestrel/` test projects mirroring their src counterparts 1:1.
   - Moves `ServiceFabricConfiguration*Test.cs`, `HttpSysCommunicationListenerTest*.cs`, and `KestrelCommunicationListenerTest*.cs` (with their `-needs-human-review.md` siblings) into the respective new projects.
   - Decides how to share `TestMocksRepository`, `KeyedCollectionImpl`, and the `Mocks/` folder across the four projects (shared project, linked files, or a small internal test-helper project).
   - Updates each `src/AspNetCore*/AssemblyInfo.cs` `InternalsVisibleTo` entry to point at its matching `.Tests` assembly.
   - Adds all three new projects to `code.slnx`.
   - Separately addresses the `KestrelCommunicationListener` type duplication between `AspNetCore` and `AspNetCore.Kestrel`, which is the actual source of the extern-alias requirement.

Human reviewer: please confirm scope before this is acted on.

---

### ⚠️ Should Fix — Inspector workaround TODO references an unfiled issue
Reported by: `gemini`, `opus`.

[test/AspNetCore/KestrelCommunicationListenerTest.cs#L42-L44](test/AspNetCore/KestrelCommunicationListenerTest.cs#L42-L44) and [test/AspNetCore/HttpSysCommunicationListenerTest.cs#L39-L41](test/AspNetCore/HttpSysCommunicationListenerTest.cs#L39-L41) both contain:

> // TODO: Inspector v0.9.0 sut.Constructor<TSig>() binds multiple overloads when delegate-typed parameters
> // only differ in generic arguments (relaxed signature matching). Track via olegsych/inspector once filed.

[.github/instructions/inspector.instructions.md](.github/instructions/inspector.instructions.md) requires workaround TODOs to link to a filed GitHub issue, and instructs the agent to ask the user to submit one to `olegsych/inspector`. The agent cannot file the upstream issue itself.

**Action required:**
1. File an issue on https://github.com/olegsych/inspector describing the behavior: in Inspector v0.9.0, `sut.Constructor<TSig>()` binds multiple overloads when delegate-typed parameters only differ in generic arguments (relaxed signature matching).
2. Replace `once filed` in both TODO comments with the actual issue URL.
3. Delete this section.

---

#### ❓ Needs Human Review — Style/analyzer diagnostics on the test file

Reported by **gpt**; cross-check **gemini Agree**, **opus Disagree** (Release build is clean with `TreatWarningsAsErrors=true` reporting 0 warnings; header borders match sibling files; `GetConstructor(...)!` suppressions appear required). **gpt re-evaluated: Insist** with narrowed scope.

- [test/AspNetCore/KestrelCommunicationListenerTest.cs#L1](test/AspNetCore/KestrelCommunicationListenerTest.cs#L1), [test/AspNetCore/KestrelCommunicationListenerTest.cs#L28](test/AspNetCore/KestrelCommunicationListenerTest.cs#L28), [test/AspNetCore/KestrelCommunicationListenerTest.cs#L44](test/AspNetCore/KestrelCommunicationListenerTest.cs#L44), [test/AspNetCore/KestrelCommunicationListenerTest.cs#L65](test/AspNetCore/KestrelCommunicationListenerTest.cs#L65), [test/AspNetCore/KestrelCommunicationListenerTest.cs#L237](test/AspNetCore/KestrelCommunicationListenerTest.cs#L237)

**gpt evidence (after re-evaluation):** Release `dotnet build` is clean across all TFMs; VS Code Problems is clean. However, `dotnet format ... style --verify-no-changes --include test/AspNetCore/KestrelCommunicationListenerTest.cs` exits 1 reporting: IDE0073 header mismatch (line 1), IDE0052 unread base `sut` (line 28), IDE0300 collection initialization simplification at the `GetConstructor(new[] { ... })` calls (e.g. line 44), IDE0370 unnecessary `!` suppression (e.g. line 65), IDE0200 lambda can be removed (line 237).

**gpt caveats:** (a) The unread `sut` should likely be intentionally suppressed, not removed, since the repo's test guidance says to create a readonly `sut` field at the top of the base test class even when not applicable to every test. (b) The header mismatch is relative to `.editorconfig`, but many sibling files use the same legacy border banner, suggesting a repo-wide convention/tooling mismatch rather than a file-specific cleanup item.

**opus evidence (still on record):** Sibling files like [test/AspNetCore/HttpSysCommunicationListenerTest.cs#L1](test/AspNetCore/HttpSysCommunicationListenerTest.cs#L1) and [test/AspNetCore/GenericHostCommunicationListenerTest.cs#L1](test/AspNetCore/GenericHostCommunicationListenerTest.cs#L1) use the same header style without triggering IDE0073 in build; the suppressions are required by the BCL's `ConstructorInfo?` return type.

**Why human review:** The diagnostics are real under `dotnet format style`, but they don't fail the build, and several reflect repo-wide convention vs. `.editorconfig` drift rather than file-specific issues. Decide which to clean up (likely IDE0300/IDE0200) vs. intentionally suppress (IDE0052 for `sut`) vs. defer as a repo-wide concern (IDE0073).
