## ❓ Needs Human Review — Pair specific `Verify` calls with `It.IsAny` to rule out extra invocations

*Reported by `opus` ⚠️; `gpt` and `gemini` partially disagreed; `opus` insisted for the `CancellationToken` cases only and retracted for `Dispose`.*

[moq.instructions.md](.github/instructions/moq.instructions.md) calls for a complementary `Verify(It.IsAny<...>, Times.Once)` alongside a specific-argument verify to prove no unexpected additional calls. Two tests fail this:

- `CloseAsync.PassesCancellationTokenToHostStopAsyncAfterOpenAsync` at [test/AspNetCore/AspNetCoreCommunicationListenerTest.cs](test/AspNetCore/AspNetCoreCommunicationListenerTest.cs#L74-L82)
- `OpenAsync.PassesCancellationTokenToHostStartAsync` at [test/AspNetCore/AspNetCoreCommunicationListenerTest.cs](test/AspNetCore/AspNetCoreCommunicationListenerTest.cs#L223-L228)

The specific-token verify alone does not rule out an additional call passing `CancellationToken.None` or another token. Add `webHost.Verify(_ => _.StartAsync(It.IsAny<CancellationToken>()), Times.Once)` (and equivalent for `StopAsync`).

`opus` retracted the same finding for `Abort.DisposesHostAfterOpenAsync` because `Dispose()` is parameterless — there is no argument to wildcard, so `Verify(_ => _.Dispose(), Times.Once)` is already complete. Flagged as `❓ Needs Human Review` because the instruction's wording in this context was not fully agreed across models; verify the intended scope of the rule before action.


---

### ❓ Needs Human Review — Consistently name target instances `sut`
*Reported by `gemini` 💡 and supported by `opus`; `gpt` disagreed during cross-check; `gemini` Insisted.*

`gemini`/`opus` view: [test.instructions.md](.github/instructions/test.instructions.md) directs identifying the test subject as `sut`. The `Constructor_..._IHost` nested class already shadows the base with `new readonly AspNetCoreCommunicationListener sut;`, establishing the pattern. The `var listener` locals in [ConfigureToUseUniqueServiceUrl](test/AspNetCore/AspNetCoreCommunicationListenerTest.cs#L42) (also L55, L69) and the `readonly TestListener listener` field in [GetEndpointResourceDescription](test/AspNetCore/AspNetCoreCommunicationListenerTest.cs#L140) all represent the SUT and should be named `sut` (shadowing the base with `new` where applicable). Repo examples in [inspector.instructions.md](.github/instructions/inspector.instructions.md) and [moq.instructions.md](.github/instructions/moq.instructions.md) uniformly use `sut`.

`gpt` view: No rule requires every local SUT instance to be named `sut`. The file already has a top-level `sut` field. The local `listener` variables are scenario-specific instances with custom contexts; renaming is stylistic, not required.

Flagged for human review to decide whether the `sut` convention applies to every local/nested instantiation of the SUT or only to the class-level field.

---

### ❓ Needs Human Review — `OpenAsync` branch coverage
*Reported by `gpt` ⚠️ and supported by `opus`; `gemini` disagreed during cross-check; `gpt` Insisted.*

`gpt`/`opus` view: The three current `OpenAsync` tests (build delegate arguments, cancellation forwarding, happy-path `http://+:0/`) miss several observable branches reachable through the SUT's public `OpenAsync`:

- `build` delegate returns `null` → `InvalidOperationException` (both adapters)
- `Addresses.FirstOrDefault()` yields no URL → `InvalidOperationException` (both)
- Generic host: `IServer` not resolved → `InvalidOperationException` ([GenericHostCommunicationListener.cs#L51-L87](src/AspNetCore/GenericHostCommunicationListener.cs#L51))
- `://[::]:` rewrite branch (both) — only `://+:` is currently covered
- Concrete server URL without wildcard rewrite (both)

`gpt` refines: keep the new tests on `AspNetCoreCommunicationListener.OpenAsync` through the existing `WithWebHost` / `WithGenericHost` style, not as direct internal-type tests. [test.instructions.md](.github/instructions/test.instructions.md) requires a separate test per logical branch in product code that produces observable behavior.

`gemini` view: The SUT's `OpenAsync` is a one-line delegate to the private internal listener; the branches belong to private `WebHostCommunicationListener` and `GenericHostCommunicationListener`. The user's scope is "Test AspNetCoreCommunicationListener… fixing bugs is out of scope", so adding tests for delegated internal logic stretches scope and overlaps with the separate finding that URL-shape testing belongs in dedicated internal-listener test files.

Tension: this finding overlaps with the elevated ⚠️ "URL-shape assertion belongs to internal listeners" finding above. If the URL-shape test is moved to dedicated internal-listener test files, the additional branch coverage should follow it there rather than be added to `AspNetCoreCommunicationListenerTest`. Flagged for human review to decide where these branch tests live.

---

## ❓ Needs Human Review — Fixture duplication between `WebHostFixture` and `GenericHostFixture`

*Reported by `opus` 💡; `gpt` Agree (narrow); `gemini` Disagree. `opus` Insisted in narrow form.*

`opus` view (insisted): `WebHostFixture` and `GenericHostFixture` duplicate URL capture, addresses-feature mock setup, and a literal copy of the `CompletedTask()` helper. [test.instructions.md](.github/instructions/test.instructions.md) says "Place helpers applicable to multiple targets in their first common base class, below the test classes" — *multiple*, not "three or more". This rule is more specific than the general Rule of Three and applies. Insist on extracting the duplicated helpers; do not insist on a generic fixture hierarchy.

`gpt` view: Real duplication exists and at minimum `CompletedTask()` and the address-feature creation should be shared; cautious about a heavy generic abstraction because the two host types expose features differently.

`gemini` view: Atwood's Rule of Three from [coding.instructions.md](.github/instructions/coding.instructions.md) — only two fixtures exist and they differ in mock-feature setup; extraction is premature abstraction.

Tension is between the general Rule of Three and the test-organization rule about helpers used by "multiple targets". Flagged for human decision on which rule governs here.


---

### ❓ Needs Human Review — Use `var` where the type is repeated in the initializer
*Reported by `opus` 💡; `gemini` Agree, `gpt` Disagree; `opus` Insisted.*

`opus`/`gemini` view: [csharp.instructions.md](.github/instructions/csharp.instructions.md) says "Use `var` to prevent duplication of the variable type in the initialization expression." The canonical example `DateTime today = DateTime.Today;` → `var` is structurally identical to `StatelessServiceContext context = fuzzy.StatelessServiceContext();`. Affected lines:
- `StatelessServiceContext context = fuzzy.StatelessServiceContext();`
- `StatefulServiceContext context = fuzzy.StatefulServiceContext();`
- `Guid trailing = Guid.Parse(...)`
- `StatelessServiceContext context = fuzzy.StatelessServiceContext();` (in OpenAsync return-value tests)

`gpt` view: `.editorconfig` (`csharp_style_var_when_type_is_apparent`) requires the type to appear *explicitly* in the initialization statement. `Guid trailing = Guid.Parse(...)` qualifies; `StatelessServiceContext context = fuzzy.StatelessServiceContext();` does not — the method name is a factory method whose return type is not syntactically present.

`opus` insisted: A factory method whose name matches the produced type (`fuzzy.StatelessServiceContext()`) makes the type "apparent" the same way `new StatelessServiceContext(...)` does (Roslyn IDE0007 treats them equivalently). `gpt`'s reading would also reject the rule's own canonical example.

Flagged for human review to decide whether the rule's "apparent type" applies to type-named factory methods on a different receiver.