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

---

### ❓ Needs Human Review — `GetListenerUrl*` tests placed inside `Constructor_*` classes

Reported by `gemini` (⚠️ Should Fix) and `opus` (❓ Needs Human Review).

Four `GetListenerUrlReturnsDefaultHttpUrl*` tests live inside the four `Constructor_*` nested classes rather than under the nested `GetListenerUrl` class:
- [test/AspNetCore/KestrelCommunicationListenerTest.cs](test/AspNetCore/KestrelCommunicationListenerTest.cs#L66-L73)
- [test/AspNetCore/KestrelCommunicationListenerTest.cs](test/AspNetCore/KestrelCommunicationListenerTest.cs#L95-L102)
- [test/AspNetCore/KestrelCommunicationListenerTest.cs](test/AspNetCore/KestrelCommunicationListenerTest.cs#L143-L151)
- [test/AspNetCore/KestrelCommunicationListenerTest.cs](test/AspNetCore/KestrelCommunicationListenerTest.cs#L192-L200)

**gemini:** Per `test.instructions.md` — *"Don't create a test class for the SUT constructor when it would duplicate tests for other members"*. The four duplicated tests should be removed and replaced with a single `GetListenerUrl.ReturnsDefaultHttpUrlWhenEndpointNameIsNull` test.

**opus:** Defensible — mirrors the SUT's duplicated `endpointName?.Length == 0 / this.endpointName = endpointName` block across the four constructor overloads ([src/AspNetCore.Kestrel/KestrelCommunicationListener.cs](src/AspNetCore.Kestrel/KestrelCommunicationListener.cs#L58-L86)) and pins per-overload regressions. But it conflicts with the rule that nested test classes are named after the target member, and a reader scanning `GetListenerUrl` for null-endpoint coverage would miss it. Alternative: parameterize a single `GetListenerUrl.ReturnsDefaultHttpUrlWhenEndpointNameIsNull` test via `[Theory]`/`[MemberData]` with a `Func<ServiceContext, AspNetCoreCommunicationListener>` factory for each overload — keeps per-overload coverage while placing the test under its target member.

**Decision needed:** keep the per-overload pinning as-is, or refactor into a parameterized test under `GetListenerUrl`.

**Iterator note:** This is part of a multi-round oscillation across reviewers on where null-endpoint `GetListenerUrl` coverage should live. Previous rounds pulled this coverage out of `GetListenerUrl`, into separate sibling classes, then collapsed it back into a single `GetListenerUrl` test, then required per-overload coverage (the current placement). Human judgment is requested to break the cycle.

---

## ⚠️ Should Fix — Missing coverage for the non-null `IHost` constructor's `endpointName` assignment

**Reported by GPT, agreed by Opus, Gemini abstained.**

The `GetListenerUrl` fixture at [test/AspNetCore/KestrelCommunicationListenerTest.cs#L207](test/AspNetCore/KestrelCommunicationListenerTest.cs#L207) constructs the SUT through the base class's `build` delegate field, which is typed `Func<string, AspNetCoreCommunicationListener, IWebHost>` at [test/AspNetCore/KestrelCommunicationListenerTest.cs#L34](test/AspNetCore/KestrelCommunicationListenerTest.cs#L34). The endpoint-backed theories at [L220](test/AspNetCore/KestrelCommunicationListenerTest.cs#L220) and [L240](test/AspNetCore/KestrelCommunicationListenerTest.cs#L240) and the not-in-manifest test at [L281](test/AspNetCore/KestrelCommunicationListenerTest.cs#L281) all flow exclusively through the `IWebHost` 3-arg ctor.

The `IHost` 3-arg ctor's `this.endpointName = endpointName` assignment at [src/AspNetCore.Kestrel/KestrelCommunicationListener.cs#L78-L86](src/AspNetCore.Kestrel/KestrelCommunicationListener.cs#L78-L86) is only reached via `GetListenerUrlReturnsDefaultHttpUrlWhenEndpointNameIsNull` at [L143](test/AspNetCore/KestrelCommunicationListenerTest.cs#L143), which passes `endpointName: null`. If that assignment were removed, no test would observe an endpoint-name regression isolated to the `IHost` overload.

**Recommendation.** Add an `IHost` variant of the endpoint-backed `GetListenerUrl` tests, or parameterize the `GetListenerUrl` fixture over both build-delegate shapes.

**Iterator note (oscillation):** An earlier round of this iteration (round ~7) explicitly directed us to COLLAPSE the `WithIHost`/`WithIWebHost` matrix in `GetListenerUrl`, with rationale "GetListenerUrl does not reference the build delegate at all; the IHost vs IWebHost branch is decided in AspNetCoreCommunicationListener's constructor and never reaches GetListenerUrl." We complied (and also applied the same collapse to HttpSysCommunicationListenerTest.cs for consistency). This round flips that position. Human decides which is correct; whichever way, please align both peer files.

---

## ⚠️ Should Fix — Active empty-`endpointName` tests pin the missing-`ParamName` bug

**Reported by GPT, agreed by Opus, Gemini abstained.**

The active tests at [L127](test/AspNetCore/KestrelCommunicationListenerTest.cs#L127) and [L174](test/AspNetCore/KestrelCommunicationListenerTest.cs#L174) assert `Assert.Equal(KestrelSR.EndpointNameEmptyExceptionMessage, exception.Message)` — exact equality on `Message`. `ArgumentException.Message` is formatted as `"{message} (Parameter '{paramName}')"` when `ParamName` is non-null. Once the SUT is fixed per the adjacent `[Fact(Explicit = true)]` tests at [L128-L137](test/AspNetCore/KestrelCommunicationListenerTest.cs#L128-L137) and [L177-L186](test/AspNetCore/KestrelCommunicationListenerTest.cs#L177-L186), `exception.Message` will gain the `" (Parameter 'endpointName')"` suffix, breaking the equality assertion. The SUT fix would simultaneously turn the explicit tests green and the active tests red.

**Recommendation.** Assert the invariant message prefix with `Assert.StartsWith(KestrelSR.EndpointNameEmptyExceptionMessage, exception.Message)` (or `Contains`) and leave `ParamName` to the explicit bug tests.

**Iterator note (oscillation):** An earlier round (round ~9) explicitly directed us to switch FROM `Assert.StartsWith` TO `Assert.Equal` for parity with `HttpSysCommunicationListenerTest.cs` and with rationale "When the bug is fixed, the explicit test becomes non-explicit and the regular test is updated alongside." We complied and aligned both peer files. This round flips that position. Human decides which is correct; whichever way, please align both peer files.