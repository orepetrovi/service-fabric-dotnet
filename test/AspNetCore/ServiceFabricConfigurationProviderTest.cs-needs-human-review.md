### ⚠️ Test project layout mismatch

**Reported by:** gemini. **Cross-check:** gpt Agree, opus Agree.

The file tests code in the `Microsoft.ServiceFabric.AspNetCore.Configuration` product project but lives in [test/AspNetCore/Microsoft.ServiceFabric.AspNetCore.Tests.csproj](test/AspNetCore/Microsoft.ServiceFabric.AspNetCore.Tests.csproj), which references all four `AspNetCore.*` src projects. Per [test.instructions.md](.github/instructions/test.instructions.md) ("test sub-folders containing test projects should have the same names as the src sub-folders") and [CONTRIBUTING.md](CONTRIBUTING.md), it should live in `test/AspNetCore.Configuration/Microsoft.ServiceFabric.AspNetCore.Configuration.Tests.csproj`.

**Action:** Relocate to `test/AspNetCore.Configuration/`. Note (opus): the layout violation is pre-existing and pervasive across HttpSys, Kestrel, and other Configuration tests — scope this as a separate project-wide split rather than blocking only this file.
