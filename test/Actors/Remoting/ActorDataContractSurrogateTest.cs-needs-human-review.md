### ❓ Needs Human Review — Verify `Bind` is called exactly once

*Reported by: gpt; gemini and opus disagreed; gpt insists*

**gpt original:** In `ReturnsBindResultWhenObjImplementsIActorReferenceAndTargetTypeImplementsIActor` at [test/Actors/Remoting/ActorDataContractSurrogateTest.cs](test/Actors/Remoting/ActorDataContractSurrogateTest.cs#L103-L109), add `reference.Verify(_ => _.Bind(It.IsAny<Type>()), Times.Once);`. If the SUT accidentally called `Bind` twice, the current test would still pass while allowing duplicate proxy creation.

**gemini (disagree):** Per [test.instructions.md](.github/instructions/test.instructions.md) §"Don't test what SUT doesn't do" — open-ended "doesn't do X" tests are only acceptable when fixing a specific bug or documenting key behavior. The test already validates that the return value is correctly passed through; asserting `Times.Once` over-specifies mechanics.

**opus (disagree):** The test's contract is about return value identity. Call count is not part of that contract. [moq.instructions.md](.github/instructions/moq.instructions.md) favors `Assert.*` over `Verify`. If excess `Bind` calls were a real concern, that deserves a separate dedicated test.

**gpt (insist):** [moq.instructions.md](.github/instructions/moq.instructions.md) prescribes the exact shape "specific-argument `Setup` + `Assert.Same` on return value + `Verify(..., Times.Once)` with `It.IsAny()`" for non-void dependency calls. The SUT branch has exactly one observable interaction — calling `Bind(targetType)` and returning its result — so this is the repo's prescribed pattern, not an open-ended "doesn't do X" assertion.

Human reviewer should decide whether the Moq convention for non-void dependency calls applies here or whether call-count verification is over-specification for this particular test.
