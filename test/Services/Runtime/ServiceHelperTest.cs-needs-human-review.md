
### ❓ Needs Human Review — Missing argument-validation placeholder for `ObserveExceptionIfAny(tsk)`

*Reported by Opus; Gemini agreed; GPT disagreed; Opus insists.*

**Opus**: The rule in [test.instructions.md](.github/instructions/test.instructions.md) is unconditional: *""Create explicit tests for missing argument validation that can cause `NullReferenceException`, etc. in consuming members. Missing argument validation is a bug in SUT.""* It is keyed on the presence of a null-deref in a consuming member, not on whether the exception is observable to the caller. In [src/Services/Runtime/ServiceHelper.cs](src/Services/Runtime/ServiceHelper.cs#L45-L57), `ObserveExceptionIfAny(Task tsk)` dereferences `tsk` via `tsk.ConfigureAwait(false)`, which NREs when `tsk` is null. The surrounding `try/catch` inside the fire-and-forget `Task.Run` swallows the exception, but that's precisely why this is a SUT bug worth documenting — passing null silently no-ops, masking caller mistakes. Convention elsewhere in this test file is to add a `[Fact(Explicit = true)]` placeholder with a `// TODO: SUT bug` comment.

**GPT (Disagree)**: The SUT dereferences `tsk` inside the fire-and-forget async delegate, but that dereference is inside a broad `try/catch` at [src/Services/Runtime/ServiceHelper.cs](src/Services/Runtime/ServiceHelper.cs#L45-L56). The C# and test rules talk about missing validation that can cause low-level exceptions in consuming members; here, the null dereference is swallowed by the method's own catch block, so the finding's ""throws NRE asynchronously in an observable way"" claim doesn't hold.

**Gemini (Agree)**: Although the `catch { }` block swallows the exception and hides it from the caller, missing argument validation is still classified as a SUT bug per the cited rule.

Human reviewer should decide whether the missing-validation rule applies when the SUT itself catches and swallows the resulting NRE, or only when it propagates to a consumer.
