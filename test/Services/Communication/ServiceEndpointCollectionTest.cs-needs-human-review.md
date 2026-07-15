### ❓ Needs Human Review — Drop the `Constructor` nested test class

**Author:** Gemini (Insist). **Cross-check:** Opus Agree, GPT Disagree.

Gemini''s original recommendation:
> `Constructor` ([test/Services/Communication/ServiceEndpointCollectionTest.cs](test/Services/Communication/ServiceEndpointCollectionTest.cs#L86-L94)) only asserts that the parameterless ctor yields an empty state, which is implicitly verified by other tests against an empty SUT (e.g., `ToString.ReturnsEmptyStringWhenCollectionIsEmpty`, `TryGetEndpointAddress.ReturnsFalseAndOutputsNullWhenCollectionIsEmpty`, `TryParseEndpointsString.ReturnsTrueAndOutputsEmptyCollectionWhenEndpointsStringIsEmpty`). Per [test.instructions.md](.github/instructions/test.instructions.md): *"Don''t create a test class for the SUT constructor when it would duplicate tests for other members."* Drop the class entirely.

Opus agreed: "adds no unique information."

GPT disagreed: the parameterless ctor''s initialization of `endpoints`/`endpointsLock` is independently observable via `ToReadOnlyDictionary()`; the `Constructor` test could fail with a different signal than `ToString`/`TryGet*`/`TryParse*` empty-collection tests. The instructions also support testing initialization in constructor tests.

Gemini Insists after seeing both responses, citing the rule''s example that the constructor test should be dropped when state is observable indirectly via another member (like `ToString`).

A human should decide whether the example in `test.instructions.md` is sufficiently on point to require dropping this class.

### ❓ Needs Human Review — `AddEndpoints` empty-listener duplicate coverage

**Author:** GPT (Insist, with revised assertion). **Cross-check:** Gemini Agree, Opus Disagree.

GPT''s original recommendation:
> `AddEndpointCallerHoldsLock` has a distinct duplicate-empty-listener branch ([src/Services/Communication/ServiceEndpointCollection.cs](src/Services/Communication/ServiceEndpointCollection.cs#L186-L198)). `AddEndpoints` tests cover only the duplicate non-empty path ([test/Services/Communication/ServiceEndpointCollectionTest.cs](test/Services/Communication/ServiceEndpointCollectionTest.cs#L84-L88)); the empty-listener branch is covered only for `AddEndpoint`. Per [test.instructions.md](.github/instructions/test.instructions.md): tests must cover every branch in SUT callees.

Gemini agreed.

Opus disagreed: the two branches throw the *same* exception type (`FabricElementAlreadyExistsException`); they differ only in message. The existing `AddEndpoint` empty-listener test asserts only type, so duplicating it for `AddEndpoints` tests an implementation detail, not externally visible behavior.

GPT Insists, with a revised proposal: add the `AddEndpoints` empty-listener duplicate test **and assert the exception message** (the two branches choose different resource strings — `SR.ErrorListenerNameNotSpecified` vs. `SR.ErrorListenerAlreadyExists` — making the distinction externally observable on `Exception.Message`).

A human should decide whether to (a) accept GPT''s revised proposal asserting the message, (b) also strengthen the existing `AddEndpoint` empty-listener test to assert the message for consistency, or (c) accept Opus''s view that branch coverage by exception type is sufficient.


### ❓ `AddEndpoints` Coverage — Empty source collection is untested (reported by `gpt`, supported by `opus`, disputed by `gemini`)

**Original finding (`gpt`, ⚠️):** `AddEndpoints` has tests for copying multiple endpoints and for duplicate-listener failure, but not for the zero-iteration path where `newEndpoints` is empty — [test/Services/Communication/ServiceEndpointCollectionTest.cs](test/Services/Communication/ServiceEndpointCollectionTest.cs#L89-L112). The SUT locks and iterates `newEndpoints.endpoints` — [src/Services/Communication/ServiceEndpointCollection.cs](src/Services/Communication/ServiceEndpointCollection.cs#L86-L92). When empty, the loop body is skipped and the observable contract should be that the existing collection is unchanged. Add a test like `KeepsCollectionUnchangedWhenNewEndpointsIsEmpty` that calls `sut.AddEndpoints(new ServiceEndpointCollection())` and asserts the original endpoint remains the only entry.

**Cross-check `opus` (Agree):** The empty-collection input is a recognized edge case and the zero-iteration `foreach` is a distinct observable path (collection unchanged vs. mutated). Maps directly to `test.instructions.md` "logical branch" and "edge case" guidance.

**Cross-check `gemini` (Disagree):** `test.instructions.md` says "Create a separate test for each logical branch in the product code." `AddEndpoints` is a straightforward `foreach` with no explicit conditional branch in SUT product code for the empty state, so the instruction does not require this test.

**`gpt` re-evaluation (Insist):** The broader instruction covers every statement and independently reachable logical branch and explicitly addresses branches in SUT callees. Passing an empty `ServiceEndpointCollection` is a valid input state with an observable no-op contract (no duplicate checks, no additions). Narrowing "logical branch" to only explicit `if`/`switch` is too strict — the file already treats empty-collection behavior as worth testing for adjacent members like `ToString` and `TryGetFirstEndpointAddress` (`ServiceEndpointCollectionTest.cs` L182-L234). Refined emphasis: this is fundamentally an empty-valid-input/zero-iteration case in the target member itself.

**Human reviewer should decide:** Whether the repo's "logical branch" coverage rule extends to zero-iteration loop paths on valid empty inputs.
