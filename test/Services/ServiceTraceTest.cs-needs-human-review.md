### ❓ Needs Human Review — `expected` local in `ReturnsPartitionIdAndReplicaIdSeparatedByColon` could be inlined

Reported by **opus** (💡), cross-check support split: **gemini** Agree, **gpt** Disagree. Author insisted after cross-check.

**Opus (original):** The `expected` local in [test/Services/ServiceTraceTest.cs](test/Services/ServiceTraceTest.cs#L23-L26) is used once and its construction matches the SUT''s body literally. Inlining matches the "Combine sections in simple, well-factored tests" guidance in [.github/instructions/test.instructions.md](.github/instructions/test.instructions.md).

**Gemini (cross-check, Agree):** Inlining aligns with "Combine sections in simple, well-factored tests" and `coding.instructions.md` rules ("Make the code as concise as possible" / "Don''t use unnecessary braces") — consolidating to a single expression allows expression-bodied method, eliminating braces. The scope restriction applies to product code, not tests.

**GPT (cross-check, Disagree):** Inlining is a possible style preference but not a repo-convention improvement. This test already combines act and assert in the `Assert.Equal` call; the one-line `expected` local is arrange data that keeps the expected and actual sides readable. The `expected` local being used once is not a violation by itself.

**Opus (Insist):** Proposed concrete inlined form:
```csharp
[Fact]
public void ReturnsPartitionIdAndReplicaIdSeparatedByColon() =>
    Assert.Equal(
        partitionId.ToString("B") + ":" + replicaId.ToString(CultureInfo.InvariantCulture),
        ServiceTrace.GetTraceIdForReplica(partitionId, replicaId));
```
Justification: explicit alignment with the "Combine sections" canonical example, conciseness rules in `coding.instructions.md`, three concrete reductions (local, braces → expression body) with no information lost. GPT''s "readability" concern is preserved because the expected expression is short and placed on its own line within `Assert.Equal`. The `TraceTest.Error` example uses a field because it is reused conceptually with the SUT setup; that does not apply here.
