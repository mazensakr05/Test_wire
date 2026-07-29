# TestWire Tests - Plain English Guide

Welcome to the TestWire testing project! We have set up a few different types of automated tests to make sure everything in the TestWire CLI tool works perfectly. Here is a simple breakdown of what these tests do and why they are here.

## 1. Analysis Unit Tests (`/Analysis` Folder)
TestWire works by reading your C# code and trying to understand it. The tests in this folder make sure that our "reading" logic (the analyzer) is accurate. 
- **What they do:** They create small, fake pieces of C# code in memory and pass them to our analyzer.
- **What they check:** They check if the analyzer can correctly figure out things like: "Is this a GET request or a POST request?" or "Does this endpoint require authorization?". 

## 2. Generation Unit Tests (`/Generation` Folder)
Once TestWire understands your code, it has to write test code for you. The tests here make sure it writes the right things.
- **What they do:** They take fake endpoint information and ask our generator classes to build specific parts of the code.
- **What they check:** For example, the `RouteBuilderTests` check that if you have an endpoint like `api/[controller]/{id}`, it correctly transforms that into an actual URL like `api/products/1` for the tests to call. It also checks that query parameters like `?query=test&page=1` are added correctly.

## 3. End-to-End Snapshot Tests (`/Integration` Folder)
This is the ultimate check. We take a real, sample project (`SampleApi`), run the entire TestWire tool on it, and look at the final C# test files it spits out.
- **What they do:** They run the analyzer and the generator together on the `SampleApi` controllers.
- **What they check:** They compare the newly generated test files against "verified" copies we've saved previously. If even a single space or character changes in the generated output, the test will fail, letting us know exactly what changed.

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
