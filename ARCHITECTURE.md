# TestWire Architecture

This document outlines the architecture of **TestWire**, a CLI tool for auto-generating integration test stubs for ASP.NET Core controllers.

## 1. High-Level Process Flow

TestWire operates in a simple, linear pipeline: parsing CLI arguments, analyzing the target project, generating the test structure, and writing the files.

```mermaid
flowchart LR
    A[CLI Input] --> B[Commands]
    B --> C[Analysis Phase]
    C --> D[Generation Phase]
    D --> E[File Output]

    subgraph TestWire.cli
    B
    C
    D
    end

    subgraph Output
    E[Test Project & Files]
    end
```

## 2. Component Architecture

### Core Modules

TestWire is divided into three primary modules:
1. **Commands (`TestWire.cli.Commands`)**: Handles CLI argument parsing using `System.CommandLine` and coordinates the pipeline.
2. **Analysis (`TestWire.cli.Analysis`)**: Leverages Microsoft.CodeAnalysis (Roslyn) and MSBuildLocator to load the target `.csproj`, compile it, and extract controller metadata using syntax trees and semantic models.
3. **Generation (`TestWire.cli.Generation`)**: Takes the extracted metadata and generates C# test code using specific builders.

```mermaid
classDiagram
    class Program {
        +Main(args)
    }
    class GenerateCommand {
        -ExecuteAsync(targetProject, outputDir)
    }
    
    class ProjectAnalyzer {
        +AnalyzeAsync(projectPath) List~ControllerInfo~
    }
    
    class ControllerInfo {
        +Name : string
        +Namespace : string
        +Endpoints : List~EndpointInfo~
    }

    class TestProjectGenerator {
        +GenerateProject(context, outputPath)
    }
    class TestFileGenerator {
        +Generate(context, controller, outputPath)
    }

    Program --> GenerateCommand
    GenerateCommand --> ProjectAnalyzer : Uses
    GenerateCommand --> TestProjectGenerator : Uses
    GenerateCommand --> TestFileGenerator : Uses
    ProjectAnalyzer ..> ControllerInfo : Returns
    TestFileGenerator ..> ControllerInfo : Consumes
```

## 3. Analysis Phase Details

The Analysis phase uses Roslyn to deeply inspect the target ASP.NET Core project.

```mermaid
sequenceDiagram
    participant Command as GenerateCommand
    participant Analyzer as ProjectAnalyzer
    participant Workspace as MSBuildWorkspace
    participant Compilation as Roslyn Compilation

    Command->>Analyzer: AnalyzeAsync(projectPath)
    Analyzer->>Workspace: OpenProjectAsync(projectPath)
    Workspace-->>Analyzer: Project
    Analyzer->>Compilation: GetCompilationAsync()
    Compilation-->>Analyzer: SemanticModel & SyntaxTrees
    
    Note over Analyzer, Compilation: Extract Controller Classes
    
    loop For Each Controller
        Analyzer->>Analyzer: Extract Endpoints (Routes, Http Methods)
        Analyzer->>Analyzer: Extract Parameters & Dependencies
    end
    
    Analyzer-->>Command: List<ControllerInfo>
```

## 4. Generation Phase Details

The Generation module breaks down the code generation into specific builders for maintainability.

```mermaid
flowchart TD
    Ctx[GenerationContext] --> TG[TestFileGenerator]
    TG --> MB[MethodBodyBuilder]
    TG --> RB[RouteBuilder]
    TG --> TV[TestValues]
    
    TG --> FileOut[Write to .cs File]
    
    MB --> FileOut
    RB --> FileOut
    TV --> FileOut
    
    Auth[AuthScaffoldGenerator] --> Proj[TestProjectGenerator]
    Proj --> CsprojOut[Write .csproj]
```

- **MethodBodyBuilder**: Generates the Arrange/Act/Assert blocks for each endpoint.
- **RouteBuilder**: Reconstructs the exact API route based on controller and action route attributes.
- **TestValues**: Provides mock data and default values for DTOs and parameters.
- **AuthScaffoldGenerator**: Generates authentication handlers/helpers for integration testing protected endpoints.
