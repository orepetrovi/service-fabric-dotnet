#### ❓ Needs Human Review — Variable suffix names (`runAsyncToken`, `existingCts`)

Reported by Gemini ⚠️. Cross-check: GPT Disagree (with the field portion only), Opus Agree. After cross-check, Gemini retracted the field portion (`readonly CancellationToken cancellationToken` matches the SUT parameter name per `test.instructions.md`) and insisted on the locals.

- `runAsyncToken` in `OpenAsync.InvokesUserServiceRunAsync` ([test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L376-L377)) carries a redundant `Token` suffix in a scope without a `CancellationTokenSource` peer. Consider `runAsyncCancellation`.
- `existingCts` in `Abort.CancelsRunAsyncCancellationTokenSource` ([test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L77-L78)) abbreviates `CancellationTokenSource` and carries a redundant `Source` suffix when spelled out. Consider `existingCancellation` or `existingCancellationSource`.

Per [.github/instructions/csharp.instructions.md](.github/instructions/csharp.instructions.md): "Omit redundant suffixes from field, variable and parameter names". Human reviewer to confirm whether locals fall under the same SUT-parameter-name exception that GPT cited for the fields.

#### ❓ Needs Human Review — Private-field arrangement via `Inspector` in `Abort`/`CloseAsync` (narrowed)

Reported by Gemini 💡. Cross-check: GPT Disagree as written (too broad — faulted-task arrangements aren't reachable through `OpenAsync`), Opus Agree. After cross-check, Gemini insisted with narrowed scope.

In `Abort` and `CloseAsync` tests, `IList<CommunicationListenerInfo>` and `CancellationTokenSource` private state is arranged via `sut.Field<...>().Set(...)` ([test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L41), [L77-L78](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L77-L78), [L101](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L101), [L137-L138](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L137-L138)) when `await sut.OpenAsync(...)` would set up the same state through the actual workflow. Per [.github/instructions/inspector.instructions.md](.github/instructions/inspector.instructions.md): "Don't access `private` members when alternatives exist". Excluded from this finding: arrangements that inject a faulted `executeRunAsyncTask` (e.g. [L151-L168](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L151-L168)), which `OpenAsync` cannot produce because `ExecuteRunAsync` swallows user exceptions via `ServiceHelper`.

Human reviewer to confirm scope and decide whether converting these arrangements to `OpenAsync`-driven setup is desirable given the user's "SUT changes out of scope" constraint.

#### ❓ Needs Human Review — Variable suffix names (`runAsyncToken`, `existingCts`)

Reported by Gemini ⚠️. Cross-check: GPT Disagree (with the field portion only), Opus Agree. After cross-check, Gemini retracted the field portion (`readonly CancellationToken cancellationToken` matches the SUT parameter name per `test.instructions.md`) and insisted on the locals.

- `runAsyncToken` in `OpenAsync.InvokesUserServiceRunAsync` ([test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L376-L377)) carries a redundant `Token` suffix in a scope without a `CancellationTokenSource` peer. Consider `runAsyncCancellation`.
- `existingCts` in `Abort.CancelsRunAsyncCancellationTokenSource` ([test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L77-L78)) abbreviates `CancellationTokenSource` and carries a redundant `Source` suffix when spelled out. Consider `existingCancellation` or `existingCancellationSource`.

Per [.github/instructions/csharp.instructions.md](.github/instructions/csharp.instructions.md): "Omit redundant suffixes from field, variable and parameter names". Human reviewer to confirm whether locals fall under the same SUT-parameter-name exception that GPT cited for the fields.

#### ❓ Needs Human Review — Private-field arrangement via `Inspector` in `Abort`/`CloseAsync` (narrowed)

Reported by Gemini 💡. Cross-check: GPT Disagree as written (too broad — faulted-task arrangements aren't reachable through `OpenAsync`), Opus Agree. After cross-check, Gemini insisted with narrowed scope.

In `Abort` and `CloseAsync` tests, `IList<CommunicationListenerInfo>` and `CancellationTokenSource` private state is arranged via `sut.Field<...>().Set(...)` ([test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L41), [L77-L78](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L77-L78), [L101](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L101), [L137-L138](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L137-L138)) when `await sut.OpenAsync(...)` would set up the same state through the actual workflow. Per [.github/instructions/inspector.instructions.md](.github/instructions/inspector.instructions.md): "Don't access `private` members when alternatives exist". Excluded from this finding: arrangements that inject a faulted `executeRunAsyncTask` (e.g. [L151-L168](test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs#L151-L168)), which `OpenAsync` cannot produce because `ExecuteRunAsync` swallows user exceptions via `ServiceHelper`.

Human reviewer to confirm scope and decide whether converting these arrangements to `OpenAsync`-driven setup is desirable given the user's "SUT changes out of scope" constraint.


#### Needs Human Review — Constructor argument-null tests use nameof(field) instead of Inspector

Original (opus): test/Services/Runtime/StatelessServiceInstanceAdapterTest.cs L221-L231 uses Assert.Equal(nameof(context), exception.ParamName) and Assert.Equal(nameof(userServiceInstance), exception.ParamName). The expression evaluates the test field name, not the SUT parameter name; it currently agrees only because the test fields were named to match. Prefer sut.Constructor().Parameter<StatelessServiceContext>().Name so the assertion is anchored to the SUT contract.

- gpt Agree: The repo test guidance specifically says to test exception properties and shows using Inspector constructor parameter metadata for ParamName (.github/instructions/test.instructions.md L406-L413).
- gemini Disagree: The .instructions.md guidelines expressly model using nameof(field) for argument-null tests... Introducing Inspector here goes against the repository preferred pattern.
- opus Insist: .github/instructions/test.instructions.md L412-L413 explicitly models the Inspector pattern for ParamName: Assert.Equal(sut.Constructor().Parameter<string>().Name, actual.ParamName). Gemini quoted snippet does not appear in this instructions file.

Human review needed: confirm which pattern test.instructions.md actually documents as preferred for ParamName assertions.
