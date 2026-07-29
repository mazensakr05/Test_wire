# TestWire Tests

This project contains the automated tests for the TestWire CLI tool.

## Test Structure

- **/Analysis**: Unit tests for the Roslyn syntax tree analysis. These tests verify that TestWire correctly identifies controllers, endpoints, and parameters.
- **/Generation**: Unit tests for the code generation logic. These tests ensure the correct C# code is produced from internal models.
- **/Commands**: Unit tests for the CLI command parsing.
- **/Integration**: Snapshot tests that run the full pipeline against sample controllers (e.g., from `SampleApi`) and verify the output using `VerifyTests`.

## Running the Tests

To run the unit tests:
```bash
dotnet test
```

## Snapshot Testing with Verify

This project uses [Verify.Xunit](https://github.com/VerifyTests/Verify) for snapshot testing the generated output.

When a snapshot test is run for the first time, or when the generated output changes intentionally, the test will fail and Verify will create a `*.received.txt` or `*.received.cs` file.

To approve the new snapshot:
1. Review the changes in the `*.received.*` file to ensure the output is correct.
2. If correct, rename the `*.received.*` file to `*.verified.*` (or use a diff tool with Verify integration to auto-accept it).
3. Re-run the tests and they should pass.
