### ❓ Needs Human Review — Verify `Bind` is called exactly once

*Reported by: gpt; gemini and opus disagreed; gpt insists*

**gpt original:** In `ReturnsBindResultWhenObjImplementsIActorReferenceAndTargetTypeImplementsIActor` at [test/Actors/Remoting/ActorDataContractSurrogateTest.cs](test/Actors/Remoting/ActorDataContractSurrogateTest.cs#L103-L109), add `reference.Verify(_ => _.Bind(It.IsAny<Type>()), Times.Once);`. If the SUT accidentally called `Bind` twice, the current test would still pass while allowing duplicate proxy creation.

**gemini (disagree):** Per [test.instructions.md](.github/instructions/test.instructions.md) §"Don't test what SUT doesn't do" — open-ended "doesn't do X" tests are only acceptable when fixing a specific bug or documenting key behavior. The test already validates that the return value is correctly passed through; asserting `Times.Once` over-specifies mechanics.

**opus (disagree):** The test's contract is about return value identity. Call count is not part of that contract. [moq.instructions.md](.github/instructions/moq.instructions.md) favors `Assert.*` over `Verify`. If excess `Bind` calls were a real concern, that deserves a separate dedicated test.

**gpt (insist):** [moq.instructions.md](.github/instructions/moq.instructions.md) prescribes the exact shape "specific-argument `Setup` + `Assert.Same` on return value + `Verify(..., Times.Once)` with `It.IsAny()`" for non-void dependency calls. The SUT branch has exactly one observable interaction — calling `Bind(targetType)` and returning its result — so this is the repo's prescribed pattern, not an open-ended "doesn't do X" assertion.

Human reviewer should decide whether the Moq convention for non-void dependency calls applies here or whether call-count verification is over-specification for this particular test.


### ❓ Needs Human Review — `Instance` test: `Assert.IsType<T>` vs `Assert.Same(typeof(...), .GetType())`

*Reported by reviewer; contradicts a previously-addressed finding in the same iteration.*

Earlier in the iteration a finding (cross-confirmed by gpt/opus/gemini) cited [.github/instructions/test.instructions.md](.github/instructions/test.instructions.md) as explicitly forbidding `Assert.IsType<T>()` and prescribing type casting / `.GetType()` comparisons. The test was changed accordingly to:

```csharp
Assert.Same(typeof(ActorDataContractSurrogate), ActorDataContractSurrogate.Instance.GetType());
```

A subsequent reviewer now recommends the opposite — using `Assert.IsType<ActorDataContractSurrogate>(ActorDataContractSurrogate.Instance)` — citing the *"Use strongest xUnit assertions available"* clause in the same instruction file. Both clauses live in `test.instructions.md` and the two reviewer rounds reach opposite conclusions.

Human reviewer should reconcile the two clauses for this case and decide which form should win for the `Instance` test.


### ❓ Needs Human Review — Cross-member dependency in `ReturnsActorReferenceWhenObjImplementsIActor`

*Reported by: opus. Cross-check: gpt agreed; gemini disagreed. opus insists with refinement.*

**opus (original):** [test/Actors/Remoting/ActorDataContractSurrogateTest.cs](test/Actors/Remoting/ActorDataContractSurrogateTest.cs#L132-L153) asserts on `reference.ActorId`, `reference.ServiceUri`, `reference.ListenerName` — properties populated by `ActorReference.Get(obj)`, not by the SUT. Per [test.instructions.md](.github/instructions/test.instructions.md) §"Minimize cross-member dependencies in assertions", a bug in `ActorReference.Get` would also fail this test. A simpler `Assert.IsType<ActorReference>(result)` would isolate the SUT.

**gpt (agree):** The SUT branch simply returns `ActorReference.Get(obj)` ([src/Actors/Remoting/ActorDataContractSurrogate.cs](src/Actors/Remoting/ActorDataContractSurrogate.cs#L29-L37)). Property population happens inside `ActorReference.Get`/`GetActorReference` ([src/Actors/ActorReference.cs](src/Actors/ActorReference.cs#L88-L90)). A defect there could fail this test even if the SUT made the right branch decision. Verifying field copying belongs in `ActorReferenceTest`.

**gemini (disagree):** The instruction's example contrasts two SUT-internal members; it doesn't prohibit inspecting the public properties of the returned payload object. `ActorReference.Get` is a static factory that can't be intercepted with Moq, so asserting properties generated from the `fuzzy` input is the only way to prove the SUT processed the actual `actor` object instead of returning an empty `new ActorReference()`. Reducing to `Assert.IsType<ActorReference>` would let "return empty ActorReference" pass.

**opus (insist, refined):** Conceding the routing-coverage point — a bare `Assert.IsType<ActorReference>(result)` is insufficient. But one identity link is enough; the remaining assertions on `ServiceUri`/`ListenerName` (and the `partitionClient` mock setup) only verify `ActorReference.Get`'s field-copying:

```csharp
var reference = Assert.IsType<ActorReference>(result);
Assert.Same(actorId, reference.ActorId);
```

This preserves the routing coverage gemini correctly defended while eliminating redundant dependencies on a different type's contract.

Human reviewer should decide whether to keep all three field assertions, adopt opus's single-identity-assertion refinement, or leave as-is.