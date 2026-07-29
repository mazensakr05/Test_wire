# TestWire Tests - Plain English Guide

Welcome to the TestWire testing project! We have set up a few different types of automated tests to make sure everything in the TestWire CLI tool works perfectly. Here is a simple breakdown of what these tests do and why they are here.

## 1. Analysis Unit Tests (`/Analysis` Folder)
These tests verify that TestWire's Roslyn-based analyzer correctly identifies API components from C# syntax trees.

**Implemented Tests:**
*   `ProjectAnalyzerTests.cs`
    *   `GetHttpVerb_IdentifiesCorrectVerb`: Verifies extraction of verbs (`[HttpGet]`, `[HttpPost]`, etc.) from method symbols.
    *   `GetHttpVerb_ReturnsNullWhenNoVerbAttribute`: Ensures methods without HTTP verb attributes are ignored.
    *   `HasAttribute_ReturnsTrueWhenAttributeIsPresent`: Verifies detection of specific attributes (e.g., `[Authorize]`).

## 2. Generation Unit Tests (`/Generation` Folder)
These tests verify that the generator logic builds correct C# code structures and URLs.

**Implemented Tests:**
*   `RouteBuilderTests.cs`
    *   `Build_ReplacesControllerToken_AndAppendsSegment`: Verifies `[controller]` is correctly replaced with the class name and segments are appended.
    *   `Build_AppendsQueryParametersCorrectly`: Verifies `[FromQuery]` parameters are appended as `?key=value` strings.
    *   `BuildNotFound_UsesNotFoundValues`: Verifies that NotFound scenarios use non-matching sentinel values (e.g., "99999").
*   `TestValuesTests.cs`
    *   `AsExpression_ReturnsCorrectCSharpExpression`: Verifies C# code literals (e.g., `"test"`, `Guid.NewGuid()`).
    *   `AsRouteSegment_ReturnsCorrectLiteral`: Verifies URL path literals.
    *   `AsNotFoundSegment_ReturnsFakeValues`: Verifies generation of guaranteed missing values.
    *   `ToStatusCodeExpression_MapsCorrectly`: Verifies mapping of integers to `HttpStatusCode` enums.
    *   `HasResponseBody_ReturnsCorrectBoolean`: Verifies whether a status code implies a deserializable JSON body.

## 3. End-to-End Snapshot Tests (`/Integration` Folder)
These tests run the entire pipeline against `SampleApi` and snapshot the generated output.

**Implemented Tests:**
*   `GeneratorSnapshotTests.cs`
    *   `ProductsController_SnapshotTest`: Verifies the complete generated test file for `ProductsController` matches the approved snapshot.
    *   `CategoriesController_SnapshotTest`: Verifies the complete generated test file for `CategoriesController` matches the approved snapshot.

---

## How to Run the Tests

If you want to run all these tests to make sure everything is working, just open your terminal in this folder and type:

```bash
dotnet test
```

### Dealing with Failing Snapshot Tests
If you intentionally change how TestWire generates code, the **Snapshot Tests** will fail (because the output changed!). 

To fix this:
1. Look in the `/Integration` folder. You will see new files ending in `.received.txt`. These are the newly generated files.
2. If the new code looks correct to you, delete the old `.verified.txt` files and rename the `.received.txt` files to `.verified.txt`.
3. Run `dotnet test` again, and they will pass!
