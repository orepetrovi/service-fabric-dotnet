#### ❓ Needs Human Review — Split `SetsOnDataLossAsyncDelegateThatRoutesToProtectedOnDataLossAsync`

[test/Services/Runtime/StatefulServiceBaseTest.cs](test/Services/Runtime/StatefulServiceBaseTest.cs#L128-L166) verifies result forwarding, cancellation token forwarding, and `RestoreContext`→`stateProviderReplica` routing in one method.

- Reported by: opus (initially proposed 3 tests, narrowed to 2 on re-evaluation).
- Cross-check: gemini Agree ("proxy mapping vs. wrapping of `RestoreContext` are distinct logical aspects"); gpt Disagree ("one forwarding contract; multiple assertions are permitted when they verify the same logical aspect per [.github/instructions/test.instructions.md](.github/instructions/test.instructions.md#L247-L250); pattern matches L286-298 in this file").
- Opus on re-evaluation: **Insist** on a narrowed split into two tests — keep result+token together as one forwarding contract; extract `RestoreContext` wiring (`SetsOnDataLossAsyncDelegateThatPassesRestoreContextRoutingToStateProviderReplica`) as its own test because `RestoreContext` wiring is a separate production-code responsibility from delegate forwarding.

Human reviewer should decide whether the `RestoreContext` wrapping verification is a separate logical aspect from delegate result/cancellation forwarding.


#### ❓ Needs Human Review — `BackupAsync` null `backupDescription` validation tests

Reviewer (gpt, agreed by gemini and opus) requested `[Fact(Explicit = true)]` tests asserting `ArgumentNullException` for `backupDescription` on both `BackupAsync` overloads, citing [test.instructions.md](.github/instructions/test.instructions.md#L391-L393).

The tests as proposed cannot be written: `BackupDescription` is a `struct` ([src/Data.Interfaces/BackupDescription.cs](src/Data.Interfaces/BackupDescription.cs#L15)) and the `BackupAsync` overloads take it by value ([src/Services/Runtime/StatefulServiceBase.cs](src/Services/Runtime/StatefulServiceBase.cs#L118)), so `sut.BackupAsync(null, ...)` does not compile (CS1503).

The underlying SUT behavior — a `NullReferenceException` thrown from `default(BackupDescription)` because its `BackupCallback` field is `null` — is a different defect. Reframing as a `default(BackupDescription)` test changes both the documented bug and the asserted exception type.

Human reviewer should decide whether to (a) drop this finding entirely, (b) add a `default(BackupDescription)` test documenting the `NullReferenceException`, or (c) defer until the SUT defines argument validation for the struct fields.

