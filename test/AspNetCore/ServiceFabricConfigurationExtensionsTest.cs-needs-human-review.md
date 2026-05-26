### ❓ Needs Human Review — Test internal types without reflection
*Reported by gemini; gpt and opus both Agreed on cross-check. Flagged for human review because it requires modifying the SUT project, which may or may not fall under the stated scope.*

Per `.github/instructions/inspector.instructions.md`: "Test `internal` types and members without Reflection... Add `[assembly: InternalsVisibleTo(...)]` to the product project."

The test resolves the `ICodePackageActivationContext` property of the SUT-created configuration source via Inspector at L103 and L128 in `test/AspNetCore/ServiceFabricConfigurationExtensionsTest.cs`. `ServiceFabricConfigurationSource` is internal (`src/AspNetCore.Configuration/ServiceFabricConfigurationSource.cs#L11`), and its `ActivationContext` is already a `public` property — adding `[assembly: InternalsVisibleTo("Microsoft.ServiceFabric.AspNetCore.Tests" + PublicKey)]` to `Microsoft.ServiceFabric.AspNetCore.Configuration.csproj` would allow casting `source` to `ServiceFabricConfigurationSource` and reading `.ActivationContext` directly.

Precedent: the sibling `src/AspNetCore/AssemblyInfo.cs#L9` already does this. Opus notes this is a testability hook, not a bug fix, so it may still be compatible with the stated scope — confirm with the user.

---

### ❓ Needs Human Review — Don't access private members when alternatives exist
*Reported by gemini; gpt and opus both Agreed on cross-check (with an adjustment from gpt about field vs property). Flagged for human review because it requires modifying the SUT.*

Per `.github/instructions/inspector.instructions.md`: "Don't access `private` members when alternatives exist... first evaluate the existing APIs and consider the possibility of adding new APIs to the SUT."

The test accesses the private `ServiceFabricConfigurationOptions options` backing field via Inspector at L97 and L129 in `test/AspNetCore/ServiceFabricConfigurationExtensionsTest.cs`. Gemini's recommendation: expose `internal ServiceFabricConfigurationOptions Options { get; }` on `ServiceFabricConfigurationSource`.

**Adjustment from gpt cross-check:** `.github/instructions/csharp.instructions.md` prefers fields over properties for internal APIs, so an `internal readonly ServiceFabricConfigurationOptions Options` field may fit local guidance better than an internal auto-property. Confirm with the user whether the SUT change is in scope and which form is preferred.

---

### ❓ Needs Human Review — `[Fact(Explicit = true)]` + `NotImplementedException` for SUT testability limitation
*Reported by opus; gemini and gpt both Agreed on cross-check. Flagged for human review because the proposed fix conflicts with the codified pattern in `.github/instructions/test.instructions.md`.*

The reviewers flagged `AddsServiceFabricConfigurationSourceForEachConfigurationPackage` inside the nested
`AddServiceFabricConfiguration_IConfigurationBuilder` class (L23-L33) because the body throws `NotImplementedException`
under `[Fact(Explicit = true)]` — when the test is explicitly selected (IDE or filter), it runs and fails with
`NotImplementedException`, reading as an unfinished test rather than a documented testability gap. Proposed fixes
were to delete the test or replace it with `[Fact(Skip = "...")]` and an empty body.

**Conflict:** `.github/instructions/test.instructions.md` codifies the exact pattern currently in the file:

```
- **Create explicit tests to demonstrate SUT testability limitations**.
  - Include `// TODO: SUT testability limitation. {brief explanation}`.
  - Have them `throw new NotImplementedException()`.
```

Several sibling tests across `test/AspNetCore/` (e.g. `GenericHostCommunicationListenerTest.cs`,
`WebHostCommunicationListenerTest.cs`, `ServiceFabricSetupFilterTest.cs`, `WebHostBuilderServiceFabricExtensionTest.cs`)
follow the same codified pattern for SUT bugs/limitations.

Per the user's direction on this finding, when the codified pattern matches the current code, document the conflict
here instead of changing the code. Decision needed from a human: either update the codified pattern in
`test.instructions.md` (and apply the new pattern consistently across the affected tests), or dismiss the review
finding.
