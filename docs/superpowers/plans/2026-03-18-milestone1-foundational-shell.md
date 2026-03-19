# Milestone 1+ Foundational Shell Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the foundational shell of Stable Diffusion Studio — a working Blazor Server app with Aspire, project CRUD, local model scanning with metadata parsing, background jobs, settings, and MudBlazor UI.

**Architecture:** Modular monolith with 6 projects (AppHost, ServiceDefaults, Web, Application, Domain, Infrastructure). Vertical slice approach — each task produces working, testable software. Dependencies flow inward: Web → Application → Domain, Infrastructure → Application/Domain.

**Tech Stack:** .NET 10, ASP.NET Core Blazor Server, MudBlazor, .NET Aspire 13+, EF Core + SQLite, FluentValidation, SignalR, xUnit + FluentAssertions + NSubstitute + bUnit

**Spec:** `docs/superpowers/specs/2026-03-18-milestone1-foundational-shell-design.md`

---

## Chunk 1: Foundation (Tasks 1–5)

### Task 1: Solution Scaffold and Aspire Wiring

**Files:**
- Create: `StableDiffusionStudio.sln`
- Create: `src/StableDiffusionStudio.AppHost/Program.cs`
- Create: `src/StableDiffusionStudio.AppHost/StableDiffusionStudio.AppHost.csproj`
- Create: `src/StableDiffusionStudio.ServiceDefaults/Extensions.cs`
- Create: `src/StableDiffusionStudio.ServiceDefaults/StableDiffusionStudio.ServiceDefaults.csproj`
- Create: `src/StableDiffusionStudio.Web/Program.cs`
- Create: `src/StableDiffusionStudio.Web/StableDiffusionStudio.Web.csproj`
- Create: `src/StableDiffusionStudio.Application/StableDiffusionStudio.Application.csproj`
- Create: `src/StableDiffusionStudio.Domain/StableDiffusionStudio.Domain.csproj`
- Create: `src/StableDiffusionStudio.Infrastructure/StableDiffusionStudio.Infrastructure.csproj`
- Create: `tests/StableDiffusionStudio.Domain.Tests/StableDiffusionStudio.Domain.Tests.csproj`
- Create: `tests/StableDiffusionStudio.Application.Tests/StableDiffusionStudio.Application.Tests.csproj`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/StableDiffusionStudio.Infrastructure.Tests.csproj`
- Create: `tests/StableDiffusionStudio.Web.Tests/StableDiffusionStudio.Web.Tests.csproj`

- [ ] **Step 1: Create solution and source projects**

Use the `dotnet` CLI to scaffold the solution. The Aspire AppHost and ServiceDefaults should use the Aspire project templates. The Web project uses the Blazor Web App template with interactive server rendering.

```bash
# Create solution
dotnet new sln -n StableDiffusionStudio

# Aspire AppHost
dotnet new aspire-apphost -n StableDiffusionStudio.AppHost -o src/StableDiffusionStudio.AppHost
# Aspire ServiceDefaults
dotnet new aspire-servicedefaults -n StableDiffusionStudio.ServiceDefaults -o src/StableDiffusionStudio.ServiceDefaults

# Blazor Web App (Interactive Server)
dotnet new blazor -n StableDiffusionStudio.Web -o src/StableDiffusionStudio.Web --interactivity Server --empty

# Class libraries
dotnet new classlib -n StableDiffusionStudio.Application -o src/StableDiffusionStudio.Application
dotnet new classlib -n StableDiffusionStudio.Domain -o src/StableDiffusionStudio.Domain
dotnet new classlib -n StableDiffusionStudio.Infrastructure -o src/StableDiffusionStudio.Infrastructure

# Add all to solution
dotnet sln add src/StableDiffusionStudio.AppHost
dotnet sln add src/StableDiffusionStudio.ServiceDefaults
dotnet sln add src/StableDiffusionStudio.Web
dotnet sln add src/StableDiffusionStudio.Application
dotnet sln add src/StableDiffusionStudio.Domain
dotnet sln add src/StableDiffusionStudio.Infrastructure
```

- [ ] **Step 2: Create test projects**

```bash
dotnet new xunit -n StableDiffusionStudio.Domain.Tests -o tests/StableDiffusionStudio.Domain.Tests
dotnet new xunit -n StableDiffusionStudio.Application.Tests -o tests/StableDiffusionStudio.Application.Tests
dotnet new xunit -n StableDiffusionStudio.Infrastructure.Tests -o tests/StableDiffusionStudio.Infrastructure.Tests
dotnet new xunit -n StableDiffusionStudio.Web.Tests -o tests/StableDiffusionStudio.Web.Tests

dotnet sln add tests/StableDiffusionStudio.Domain.Tests
dotnet sln add tests/StableDiffusionStudio.Application.Tests
dotnet sln add tests/StableDiffusionStudio.Infrastructure.Tests
dotnet sln add tests/StableDiffusionStudio.Web.Tests
```

- [ ] **Step 3: Wire project references (dependency flow)**

```bash
# Web → Application, ServiceDefaults
cd src/StableDiffusionStudio.Web
dotnet add reference ../StableDiffusionStudio.Application
dotnet add reference ../StableDiffusionStudio.ServiceDefaults
dotnet add reference ../StableDiffusionStudio.Infrastructure
cd ../..

# Application → Domain
cd src/StableDiffusionStudio.Application
dotnet add reference ../StableDiffusionStudio.Domain
cd ../..

# Infrastructure → Application, Domain
cd src/StableDiffusionStudio.Infrastructure
dotnet add reference ../StableDiffusionStudio.Application
dotnet add reference ../StableDiffusionStudio.Domain
cd ../..

# AppHost → Web
cd src/StableDiffusionStudio.AppHost
dotnet add reference ../StableDiffusionStudio.Web
cd ../..

# Test projects → their targets
cd tests/StableDiffusionStudio.Domain.Tests
dotnet add reference ../../src/StableDiffusionStudio.Domain
cd ../..

cd tests/StableDiffusionStudio.Application.Tests
dotnet add reference ../../src/StableDiffusionStudio.Application
dotnet add reference ../../src/StableDiffusionStudio.Domain
dotnet add reference ../../src/StableDiffusionStudio.Infrastructure
cd ../..

cd tests/StableDiffusionStudio.Infrastructure.Tests
dotnet add reference ../../src/StableDiffusionStudio.Infrastructure
dotnet add reference ../../src/StableDiffusionStudio.Application
dotnet add reference ../../src/StableDiffusionStudio.Domain
cd ../..

cd tests/StableDiffusionStudio.Web.Tests
dotnet add reference ../../src/StableDiffusionStudio.Web
dotnet add reference ../../src/StableDiffusionStudio.Application
dotnet add reference ../../src/StableDiffusionStudio.Domain
cd ../..
```

- [ ] **Step 4: Add NuGet packages**

```bash
# Domain — no external packages (pure domain)

# Application
cd src/StableDiffusionStudio.Application
dotnet add package FluentValidation
cd ../..

# Infrastructure
cd src/StableDiffusionStudio.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
cd ../..

# Web
cd src/StableDiffusionStudio.Web
dotnet add package MudBlazor
cd ../..

# Test packages
for proj in tests/StableDiffusionStudio.Domain.Tests tests/StableDiffusionStudio.Application.Tests tests/StableDiffusionStudio.Infrastructure.Tests tests/StableDiffusionStudio.Web.Tests; do
  cd "$proj"
  dotnet add package FluentAssertions
  dotnet add package NSubstitute
  cd ../..
done

cd tests/StableDiffusionStudio.Infrastructure.Tests
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
cd ../..

cd tests/StableDiffusionStudio.Web.Tests
dotnet add package bunit
dotnet add package MudBlazor
cd ../..
```

- [ ] **Step 5: Configure Aspire AppHost Program.cs**

Write `src/StableDiffusionStudio.AppHost/Program.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.StableDiffusionStudio_Web>("web");

builder.Build().Run();
```

- [ ] **Step 6: Configure ServiceDefaults Extensions.cs**

Verify the Aspire template generated `Extensions.cs` with `AddServiceDefaults()` and `MapDefaultEndpoints()`. It should include OpenTelemetry, health checks, and resilience. If the template is minimal, ensure it wires:
- OpenTelemetry logging, tracing, metrics
- Health check endpoints
- HTTP client resilience

- [ ] **Step 7: Configure Web Program.cs with ServiceDefaults**

Write `src/StableDiffusionStudio.Web/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

- [ ] **Step 8: Verify build succeeds**

Run: `dotnet build StableDiffusionStudio.sln`
Expected: Build succeeded with 0 errors.

- [ ] **Step 9: Verify tests run (empty)**

Run: `dotnet test StableDiffusionStudio.sln`
Expected: All test projects discovered, 0 tests run (or template tests pass).

- [ ] **Step 10: Commit**

```bash
git init
git add -A
git commit -m "feat: scaffold solution with Aspire, Blazor Server, and project structure

Six source projects (AppHost, ServiceDefaults, Web, Application, Domain,
Infrastructure) and four test projects. Aspire wires OpenTelemetry,
health checks, and dashboard. Web uses interactive server rendering."
```

---

### Task 2: Domain Core — Project Entity and Enums

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Entities/Project.cs`
- Create: `src/StableDiffusionStudio.Domain/Enums/ProjectStatus.cs`
- Create: `src/StableDiffusionStudio.Domain/Enums/ModelFamily.cs`
- Create: `src/StableDiffusionStudio.Domain/Enums/ModelFormat.cs`
- Create: `src/StableDiffusionStudio.Domain/Enums/ModelStatus.cs`
- Create: `src/StableDiffusionStudio.Domain/Enums/JobStatus.cs`
- Create: `src/StableDiffusionStudio.Domain/ValueObjects/StorageRoot.cs`
- Create: `src/StableDiffusionStudio.Domain/ValueObjects/ModelIdentifier.cs`
- Create: `src/StableDiffusionStudio.Domain/ValueObjects/FileLocation.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/Entities/ProjectTests.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/ValueObjects/StorageRootTests.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/ValueObjects/ModelIdentifierTests.cs`

- [ ] **Step 1: Write failing tests for Project entity**

Create `tests/StableDiffusionStudio.Domain.Tests/Entities/ProjectTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ProjectTests
{
    [Fact]
    public void Create_WithValidName_SetsPropertiesCorrectly()
    {
        var project = Project.Create("My Project", "A description");

        project.Id.Should().NotBeEmpty();
        project.Name.Should().Be("My Project");
        project.Description.Should().Be("A description");
        project.Status.Should().Be(ProjectStatus.Active);
        project.IsPinned.Should().BeFalse();
        project.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        var act = () => Project.Create(name!, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rename_WhenActive_UpdatesName()
    {
        var project = Project.Create("Old Name", null);
        project.Rename("New Name");
        project.Name.Should().Be("New Name");
        project.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Rename_WhenArchived_ThrowsInvalidOperationException()
    {
        var project = Project.Create("Test", null);
        project.Archive();

        var act = () => project.Rename("New Name");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        var project = Project.Create("Test", null);
        project.Archive();
        project.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public void Restore_FromArchived_SetsStatusToActive()
    {
        var project = Project.Create("Test", null);
        project.Archive();
        project.Restore();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void Pin_SetsIsPinnedTrue()
    {
        var project = Project.Create("Test", null);
        project.Pin();
        project.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Unpin_SetsIsPinnedFalse()
    {
        var project = Project.Create("Test", null);
        project.Pin();
        project.Unpin();
        project.IsPinned.Should().BeFalse();
    }

    [Fact]
    public void UpdateDescription_SetsDescription()
    {
        var project = Project.Create("Test", null);
        project.UpdateDescription("New description");
        project.Description.Should().Be("New description");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/StableDiffusionStudio.Domain.Tests --no-build 2>&1 || dotnet test tests/StableDiffusionStudio.Domain.Tests`
Expected: Compilation errors — `Project` class does not exist.

- [ ] **Step 3: Implement enums**

Create `src/StableDiffusionStudio.Domain/Enums/ProjectStatus.cs`:
```csharp
namespace StableDiffusionStudio.Domain.Enums;

public enum ProjectStatus
{
    Active,
    Archived,
    Deleted
}
```

Create `src/StableDiffusionStudio.Domain/Enums/ModelFamily.cs`:
```csharp
namespace StableDiffusionStudio.Domain.Enums;

public enum ModelFamily
{
    Unknown,
    SD15,
    SDXL,
    Flux
}
```

Create `src/StableDiffusionStudio.Domain/Enums/ModelFormat.cs`:
```csharp
namespace StableDiffusionStudio.Domain.Enums;

public enum ModelFormat
{
    Unknown,
    SafeTensors,
    CKPT,
    GGUF,
    Diffusers
}
```

Create `src/StableDiffusionStudio.Domain/Enums/ModelStatus.cs`:
```csharp
namespace StableDiffusionStudio.Domain.Enums;

public enum ModelStatus
{
    Available,
    Missing,
    Scanning
}
```

Create `src/StableDiffusionStudio.Domain/Enums/JobStatus.cs`:
```csharp
namespace StableDiffusionStudio.Domain.Enums;

public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

- [ ] **Step 4: Implement Project entity**

Create `src/StableDiffusionStudio.Domain/Entities/Project.cs`:

```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class Project
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ProjectStatus Status { get; private set; }
    public bool IsPinned { get; private set; }

    private Project() { } // EF Core

    public static Project Create(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description,
            CreatedAt = now,
            UpdatedAt = now,
            Status = ProjectStatus.Active,
            IsPinned = false
        };
    }

    public void Rename(string newName)
    {
        if (Status == ProjectStatus.Archived)
            throw new InvalidOperationException("Cannot rename an archived project.");

        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Project name is required.", nameof(newName));

        Name = newName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        Status = ProjectStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Restore()
    {
        Status = ProjectStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Pin()
    {
        IsPinned = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Unpin()
    {
        IsPinned = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 5: Implement value objects**

Create `src/StableDiffusionStudio.Domain/ValueObjects/StorageRoot.cs`:

```csharp
namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record StorageRoot
{
    public string Path { get; }
    public string DisplayName { get; }

    public StorageRoot(string path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Storage root path is required.", nameof(path));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        Path = path.Trim();
        DisplayName = displayName.Trim();
    }
}
```

Create `src/StableDiffusionStudio.Domain/ValueObjects/ModelIdentifier.cs`:

```csharp
namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record ModelIdentifier(string Source, string ExternalId)
{
    public string Source { get; } = !string.IsNullOrWhiteSpace(Source)
        ? Source : throw new ArgumentException("Source is required.", nameof(Source));
    public string ExternalId { get; } = !string.IsNullOrWhiteSpace(ExternalId)
        ? ExternalId : throw new ArgumentException("ExternalId is required.", nameof(ExternalId));
}
```

Create `src/StableDiffusionStudio.Domain/ValueObjects/FileLocation.cs`:

```csharp
namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record FileLocation
{
    public string Path { get; }

    public FileLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path is required.", nameof(path));
        Path = path;
    }
}
```

- [ ] **Step 6: Write value object tests**

Create `tests/StableDiffusionStudio.Domain.Tests/ValueObjects/StorageRootTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class StorageRootTests
{
    [Fact]
    public void Create_WithValidInputs_SetsProperties()
    {
        var root = new StorageRoot("/models", "My Models");
        root.Path.Should().Be("/models");
        root.DisplayName.Should().Be("My Models");
    }

    [Theory]
    [InlineData(null, "name")]
    [InlineData("", "name")]
    [InlineData("path", null)]
    [InlineData("path", "")]
    public void Create_WithInvalidInputs_ThrowsArgumentException(string? path, string? name)
    {
        var act = () => new StorageRoot(path!, name!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new StorageRoot("/models", "Models");
        var b = new StorageRoot("/models", "Models");
        a.Should().Be(b);
    }
}
```

Create `tests/StableDiffusionStudio.Domain.Tests/ValueObjects/ModelIdentifierTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class ModelIdentifierTests
{
    [Fact]
    public void Create_WithValidInputs_SetsProperties()
    {
        var id = new ModelIdentifier("local-folder", "path/to/model.safetensors");
        id.Source.Should().Be("local-folder");
        id.ExternalId.Should().Be("path/to/model.safetensors");
    }

    [Theory]
    [InlineData(null, "id")]
    [InlineData("", "id")]
    [InlineData("source", null)]
    [InlineData("source", "")]
    public void Create_WithInvalidInputs_ThrowsArgumentException(string? source, string? externalId)
    {
        var act = () => new ModelIdentifier(source!, externalId!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ModelIdentifier("hf", "model-123");
        var b = new ModelIdentifier("hf", "model-123");
        a.Should().Be(b);
    }
}
```

- [ ] **Step 7: Run all domain tests**

Run: `dotnet test tests/StableDiffusionStudio.Domain.Tests -v normal`
Expected: All tests pass (10 tests).

- [ ] **Step 8: Delete placeholder Class1.cs files**

Remove auto-generated `Class1.cs` from Domain, Application, and Infrastructure class library projects.

- [ ] **Step 9: Commit**

```bash
git add src/StableDiffusionStudio.Domain tests/StableDiffusionStudio.Domain.Tests
git commit -m "feat: add Project entity, enums, and value objects with TDD

Project entity with Create, Rename, Archive, Restore, Pin, Unpin,
UpdateDescription. StorageRoot, ModelIdentifier, FileLocation value
objects. All enums: ProjectStatus, ModelFamily, ModelFormat, ModelStatus,
JobStatus. Full test coverage."
```

---

### Task 3: Persistence — EF Core, AppDbContext, Project Repository

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ProjectRepository.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IProjectRepository.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ProjectDto.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ProjectFilter.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/ProjectRepositoryTests.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/TestDbContextFactory.cs`

- [ ] **Step 1: Define IProjectRepository interface**

Create `src/StableDiffusionStudio.Application/Interfaces/IProjectRepository.cs`:

```csharp
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(ProjectFilter filter, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

Create `src/StableDiffusionStudio.Application/DTOs/ProjectFilter.cs`:

```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ProjectFilter(
    string? SearchTerm = null,
    ProjectStatus? Status = null,
    bool? IsPinned = null,
    int Skip = 0,
    int Take = 50);
```

- [ ] **Step 2: Write failing repository integration tests**

Create `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/TestDbContextFactory.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public static class TestDbContextFactory
{
    public static (AppDbContext context, SqliteConnection connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}
```

Create `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/ProjectRepositoryTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public class ProjectRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly ProjectRepository _repo;

    public ProjectRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new ProjectRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsProject()
    {
        var project = Project.Create("Test Project", "Description");

        await _repo.AddAsync(project);
        var retrieved = await _repo.GetByIdAsync(project.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Project");
        retrieved.Description.Should().Be("Description");
    }

    [Fact]
    public async Task ListAsync_WithSearchTerm_FiltersResults()
    {
        await _repo.AddAsync(Project.Create("Alpha Project", null));
        await _repo.AddAsync(Project.Create("Beta Project", null));
        await _repo.AddAsync(Project.Create("Gamma", null));

        var results = await _repo.ListAsync(new ProjectFilter(SearchTerm: "Project"));

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_FiltersResults()
    {
        var active = Project.Create("Active", null);
        var archived = Project.Create("Archived", null);
        archived.Archive();

        await _repo.AddAsync(active);
        await _repo.AddAsync(archived);

        var results = await _repo.ListAsync(new ProjectFilter(Status: ProjectStatus.Archived));

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Archived");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var project = Project.Create("Original", null);
        await _repo.AddAsync(project);

        project.Rename("Updated");
        await _repo.UpdateAsync(project);

        var retrieved = await _repo.GetByIdAsync(project.Id);
        retrieved!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_RemovesProject()
    {
        var project = Project.Create("To Delete", null);
        await _repo.AddAsync(project);

        await _repo.DeleteAsync(project.Id);

        var retrieved = await _repo.GetByIdAsync(project.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WithPinnedFilter_FiltersResults()
    {
        var pinned = Project.Create("Pinned", null);
        pinned.Pin();
        var unpinned = Project.Create("Unpinned", null);

        await _repo.AddAsync(pinned);
        await _repo.AddAsync(unpinned);

        var results = await _repo.ListAsync(new ProjectFilter(IsPinned: true));

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Pinned");
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/StableDiffusionStudio.Infrastructure.Tests`
Expected: Compilation errors — `AppDbContext` and `ProjectRepository` do not exist.

- [ ] **Step 4: Implement AppDbContext**

Create `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Implement Project EF configuration**

Create `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.IsPinned);
        builder.HasIndex(p => p.CreatedAt);
    }
}
```

- [ ] **Step 6: Implement ProjectRepository**

Create `src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ProjectRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Projects.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<Project>> ListAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        var query = _context.Projects.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            query = query.Where(p => p.Name.Contains(filter.SearchTerm));

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.IsPinned.HasValue)
            query = query.Where(p => p.IsPinned == filter.IsPinned.Value);

        return await query
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.UpdatedAt)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects.FindAsync([id], ct);
        if (project is not null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 7: Run infrastructure tests**

Run: `dotnet test tests/StableDiffusionStudio.Infrastructure.Tests -v normal`
Expected: All 6 tests pass.

- [ ] **Step 8: Create initial EF migration**

```bash
cd src/StableDiffusionStudio.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../StableDiffusionStudio.Web -- --connection "DataSource=studio.db"
cd ../..
```

Note: If `dotnet ef` is not installed: `dotnet tool install --global dotnet-ef`

- [ ] **Step 9: Commit**

```bash
git add src/StableDiffusionStudio.Application src/StableDiffusionStudio.Infrastructure tests/StableDiffusionStudio.Infrastructure.Tests
git commit -m "feat: add EF Core persistence with SQLite and Project repository

AppDbContext with Project configuration via Fluent API. ProjectRepository
implements IProjectRepository with filtering, search, and pagination.
Integration tests use in-memory SQLite. Initial EF migration."
```

---

### Task 4: Project Application Service with Validation

**Files:**
- Create: `src/StableDiffusionStudio.Application/Services/ProjectService.cs`
- Create: `src/StableDiffusionStudio.Application/Commands/CreateProjectCommand.cs`
- Create: `src/StableDiffusionStudio.Application/Commands/RenameProjectCommand.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ProjectDto.cs`
- Create: `src/StableDiffusionStudio.Application/Validation/CreateProjectCommandValidator.cs`
- Create: `src/StableDiffusionStudio.Application/Validation/RenameProjectCommandValidator.cs`
- Create: `tests/StableDiffusionStudio.Application.Tests/Services/ProjectServiceTests.cs`
- Create: `tests/StableDiffusionStudio.Application.Tests/Validation/CreateProjectCommandValidatorTests.cs`

- [ ] **Step 1: Write failing tests for ProjectService**

Create `tests/StableDiffusionStudio.Application.Tests/Services/ProjectServiceTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Tests.Services;

public class ProjectServiceTests
{
    private readonly IProjectRepository _repo = Substitute.For<IProjectRepository>();
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _service = new ProjectService(_repo);
    }

    [Fact]
    public async Task CreateAsync_WithValidCommand_ReturnsProjectDto()
    {
        var command = new CreateProjectCommand("Test Project", "Description");

        var result = await _service.CreateAsync(command);

        result.Should().NotBeNull();
        result.Name.Should().Be("Test Project");
        result.Description.Should().Be("Description");
        result.Status.Should().Be(ProjectStatus.Active);
        await _repo.Received(1).AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_WithValidCommand_RenamesProject()
    {
        var project = Project.Create("Old Name", null);
        _repo.GetByIdAsync(project.Id).Returns(project);

        var command = new RenameProjectCommand(project.Id, "New Name");
        await _service.RenameAsync(command);

        project.Name.Should().Be("New Name");
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_ProjectNotFound_ThrowsKeyNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var act = () => _service.RenameAsync(new RenameProjectCommand(Guid.NewGuid(), "Name"));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ArchiveAsync_ArchivesProject()
    {
        var project = Project.Create("Test", null);
        _repo.GetByIdAsync(project.Id).Returns(project);

        await _service.ArchiveAsync(project.Id);

        project.Status.Should().Be(ProjectStatus.Archived);
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_DelegatesToRepository()
    {
        var filter = new ProjectFilter();
        var projects = new List<Project> { Project.Create("A", null), Project.Create("B", null) };
        _repo.ListAsync(filter).Returns(projects);

        var results = await _service.ListAsync(filter);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        await _service.DeleteAsync(id);
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PinAsync_PinsProject()
    {
        var project = Project.Create("Test", null);
        _repo.GetByIdAsync(project.Id).Returns(project);

        await _service.PinAsync(project.Id);

        project.IsPinned.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/StableDiffusionStudio.Application.Tests`
Expected: Compilation errors — `ProjectService` and commands do not exist.

- [ ] **Step 3: Implement commands and DTOs**

Create `src/StableDiffusionStudio.Application/Commands/CreateProjectCommand.cs`:
```csharp
namespace StableDiffusionStudio.Application.Commands;

public record CreateProjectCommand(string Name, string? Description);
```

Create `src/StableDiffusionStudio.Application/Commands/RenameProjectCommand.cs`:
```csharp
namespace StableDiffusionStudio.Application.Commands;

public record RenameProjectCommand(Guid Id, string NewName);
```

Create `src/StableDiffusionStudio.Application/DTOs/ProjectDto.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    ProjectStatus Status,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

- [ ] **Step 4: Implement ProjectService**

Create `src/StableDiffusionStudio.Application/Services/ProjectService.cs`:

```csharp
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Services;

public class ProjectService
{
    private readonly IProjectRepository _repository;

    public ProjectService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectCommand command, CancellationToken ct = default)
    {
        var project = Project.Create(command.Name, command.Description);
        await _repository.AddAsync(project, ct);
        return ToDto(project);
    }

    public async Task RenameAsync(RenameProjectCommand command, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(command.Id, ct);
        project.Rename(command.NewName);
        await _repository.UpdateAsync(project, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Archive();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Restore();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task PinAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Pin();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task UnpinAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Unpin();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        var projects = await _repository.ListAsync(filter, ct);
        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        return project is null ? null : ToDto(project);
    }

    private async Task<Project> GetOrThrowAsync(Guid id, CancellationToken ct)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        return project ?? throw new KeyNotFoundException($"Project {id} not found.");
    }

    private static ProjectDto ToDto(Project p) =>
        new(p.Id, p.Name, p.Description, p.Status, p.IsPinned, p.CreatedAt, p.UpdatedAt);
}
```

- [ ] **Step 5: Implement FluentValidation validators**

Create `src/StableDiffusionStudio.Application/Validation/CreateProjectCommandValidator.cs`:

```csharp
using FluentValidation;
using StableDiffusionStudio.Application.Commands;

namespace StableDiffusionStudio.Application.Validation;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name must be 200 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must be 2000 characters or fewer.");
    }
}
```

Create `src/StableDiffusionStudio.Application/Validation/RenameProjectCommandValidator.cs`:

```csharp
using FluentValidation;
using StableDiffusionStudio.Application.Commands;

namespace StableDiffusionStudio.Application.Validation;

public class RenameProjectCommandValidator : AbstractValidator<RenameProjectCommand>
{
    public RenameProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name must be 200 characters or fewer.");
    }
}
```

- [ ] **Step 6: Write validator tests**

Create `tests/StableDiffusionStudio.Application.Tests/Validation/CreateProjectCommandValidatorTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Validation;

namespace StableDiffusionStudio.Application.Tests.Validation;

public class CreateProjectCommandValidatorTests
{
    private readonly CreateProjectCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_IsValid()
    {
        var result = _validator.Validate(new CreateProjectCommand("Test", null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_IsInvalid()
    {
        var result = _validator.Validate(new CreateProjectCommand("", null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameTooLong_IsInvalid()
    {
        var result = _validator.Validate(new CreateProjectCommand(new string('A', 201), null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DescriptionTooLong_IsInvalid()
    {
        var result = _validator.Validate(new CreateProjectCommand("OK", new string('A', 2001)));
        result.IsValid.Should().BeFalse();
    }
}
```

- [ ] **Step 7: Run all application tests**

Run: `dotnet test tests/StableDiffusionStudio.Application.Tests -v normal`
Expected: All tests pass (11 tests).

- [ ] **Step 8: Run full test suite**

Run: `dotnet test StableDiffusionStudio.sln -v normal`
Expected: All tests pass across all projects.

- [ ] **Step 9: Commit**

```bash
git add src/StableDiffusionStudio.Application tests/StableDiffusionStudio.Application.Tests
git commit -m "feat: add ProjectService with CRUD operations and FluentValidation

ProjectService orchestrates create, rename, archive, restore, pin,
unpin, delete, list. FluentValidation for CreateProject and
RenameProject commands. Full unit test coverage with NSubstitute mocks."
```

---

### Task 5: App Shell UI — MudBlazor Layout, Navigation, Dark Theme

**Files:**
- Create: `src/StableDiffusionStudio.Web/Components/App.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Routes.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Layout/MainLayout.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Layout/MainLayout.razor.css`
- Create: `src/StableDiffusionStudio.Web/Components/Layout/NavMenu.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Pages/Home.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/EmptyState.razor`
- Create: `src/StableDiffusionStudio.Web/Theme/StudioTheme.cs`
- Modify: `src/StableDiffusionStudio.Web/Program.cs` — add MudBlazor, DI registrations
- Create: `src/StableDiffusionStudio.Web/_Imports.razor`
- Create: `tests/StableDiffusionStudio.Web.Tests/Components/Layout/MainLayoutTests.cs`

- [ ] **Step 1: Create StudioTheme**

Create `src/StableDiffusionStudio.Web/Theme/StudioTheme.cs`:

```csharp
using MudBlazor;

namespace StableDiffusionStudio.Web.Theme;

public static class StudioTheme
{
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#58a6ff",
            Secondary = "#bc8cff",
            AppbarBackground = "#161b22",
            Background = "#0d1117",
            Surface = "#161b22",
            DrawerBackground = "#0d1117",
            TextPrimary = "#e6edf3",
            TextSecondary = "#8b949e",
            ActionDefault = "#8b949e",
            Success = "#3fb950",
            Warning = "#d29922",
            Error = "#f85149",
            Info = "#58a6ff"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#58a6ff",
            Secondary = "#bc8cff",
            AppbarBackground = "#161b22",
            Background = "#0d1117",
            Surface = "#161b22",
            DrawerBackground = "#0d1117",
            TextPrimary = "#e6edf3",
            TextSecondary = "#8b949e",
            ActionDefault = "#8b949e",
            Success = "#3fb950",
            Warning = "#d29922",
            Error = "#f85149",
            Info = "#58a6ff"
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px"
        }
    };
}
```

- [ ] **Step 2: Create App.razor**

Create `src/StableDiffusionStudio.Web/Components/App.razor`:

```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>
</html>
```

- [ ] **Step 3: Create Routes.razor**

Create `src/StableDiffusionStudio.Web/Components/Routes.razor`:

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

- [ ] **Step 4: Create _Imports.razor**

Create `src/StableDiffusionStudio.Web/Components/_Imports.razor`:

```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using MudBlazor
@using StableDiffusionStudio.Web.Components
@using StableDiffusionStudio.Web.Components.Layout
@using StableDiffusionStudio.Web.Components.Shared
```

- [ ] **Step 5: Create MainLayout with MudBlazor shell**

Create `src/StableDiffusionStudio.Web/Components/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<MudThemeProvider Theme="_theme" IsDarkMode="true" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1" Dense="true">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit"
                       Edge="Edge.Start" OnClick="ToggleDrawer" />
        <MudText Typo="Typo.h6" Class="ml-2">Stable Diffusion Studio</MudText>
        <MudSpacer />
        <MudIconButton Icon="@Icons.Material.Filled.DarkMode" Color="Color.Inherit" />
        <MudIconButton Icon="@Icons.Material.Filled.Notifications" Color="Color.Inherit" />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" Elevation="2" ClipMode="DrawerClipMode.Always">
        <NavMenu />
    </MudDrawer>

    <MudMainContent Class="pa-4">
        @Body
    </MudMainContent>
</MudLayout>

@code {
    private MudTheme _theme = StableDiffusionStudio.Web.Theme.StudioTheme.Create();
    private bool _drawerOpen = true;

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;
}
```

- [ ] **Step 6: Create NavMenu**

Create `src/StableDiffusionStudio.Web/Components/Layout/NavMenu.razor`:

```razor
<MudNavMenu>
    <MudNavLink Href="/" Match="NavLinkMatch.All"
                Icon="@Icons.Material.Filled.Home">Home</MudNavLink>
    <MudNavLink Href="/projects" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Folder">Projects</MudNavLink>
    <MudNavLink Href="/models" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.ViewInAr">Models</MudNavLink>
    <MudNavLink Href="/jobs" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.WorkHistory">Jobs</MudNavLink>
    <MudDivider Class="my-2" />
    <MudNavLink Href="/settings" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.Settings">Settings</MudNavLink>
</MudNavMenu>
```

- [ ] **Step 7: Create EmptyState component**

Create `src/StableDiffusionStudio.Web/Components/Shared/EmptyState.razor`:

```razor
<MudPaper Class="pa-8 d-flex flex-column align-center justify-center" Style="min-height: 300px;" Elevation="0">
    <MudIcon Icon="@Icon" Size="Size.Large" Color="Color.Default" Class="mb-4" />
    <MudText Typo="Typo.h6" Color="Color.Default">@Title</MudText>
    <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-4">@Subtitle</MudText>
    @if (ActionText is not null)
    {
        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="OnAction">
            @ActionText
        </MudButton>
    }
</MudPaper>

@code {
    [Parameter] public string Icon { get; set; } = Icons.Material.Filled.Info;
    [Parameter] public string Title { get; set; } = "Nothing here yet";
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public string? ActionText { get; set; }
    [Parameter] public EventCallback OnAction { get; set; }
}
```

- [ ] **Step 8: Create Home page**

Create `src/StableDiffusionStudio.Web/Components/Pages/Home.razor`:

```razor
@page "/"

<MudText Typo="Typo.h4" Class="mb-4">Dashboard</MudText>

<MudGrid>
    <MudItem xs="12" md="6">
        <MudPaper Class="pa-4" Elevation="1">
            <MudText Typo="Typo.h6" Class="mb-2">Quick Start</MudText>
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Add"
                       Href="/projects" Class="mb-2" FullWidth="true">
                New Project
            </MudButton>
        </MudPaper>
    </MudItem>
    <MudItem xs="12" md="6">
        <MudPaper Class="pa-4" Elevation="1">
            <MudText Typo="Typo.h6" Class="mb-2">Recent Projects</MudText>
            <EmptyState Title="No projects yet"
                        Subtitle="Create your first project to get started"
                        Icon="@Icons.Material.Filled.Folder" />
        </MudPaper>
    </MudItem>
</MudGrid>
```

- [ ] **Step 9: Update Program.cs with MudBlazor and DI**

Update `src/StableDiffusionStudio.Web/Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// MudBlazor
builder.Services.AddMudServices();

// EF Core + SQLite
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "StableDiffusionStudio", "Database", "studio.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"DataSource={dbPath}"));

// Application services
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ProjectService>();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

// Auto-migrate in development
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<StableDiffusionStudio.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

- [ ] **Step 10: Write bUnit smoke test for MainLayout**

Create `tests/StableDiffusionStudio.Web.Tests/Components/Layout/MainLayoutTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using StableDiffusionStudio.Web.Components.Layout;

namespace StableDiffusionStudio.Web.Tests.Components.Layout;

public class MainLayoutTests : TestContext
{
    public MainLayoutTests()
    {
        Services.AddMudServices();
        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
        JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void MainLayout_RendersAppTitle()
    {
        var cut = RenderComponent<MainLayout>(parameters =>
            parameters.Add(p => p.Body, "<p>Test content</p>"));

        cut.Markup.Should().Contain("Stable Diffusion Studio");
    }

    [Fact]
    public void MainLayout_RendersNavMenu()
    {
        var cut = RenderComponent<MainLayout>(parameters =>
            parameters.AddChildContent("<p>Test content</p>"));

        cut.Markup.Should().Contain("Home");
        cut.Markup.Should().Contain("Projects");
        cut.Markup.Should().Contain("Models");
        cut.Markup.Should().Contain("Jobs");
        cut.Markup.Should().Contain("Settings");
    }
}
```

- [ ] **Step 11: Run web tests**

Run: `dotnet test tests/StableDiffusionStudio.Web.Tests -v normal`
Expected: Tests pass (2 tests). If bUnit + MudBlazor has compatibility issues, note them and adjust JSInterop mocking.

- [ ] **Step 12: Verify app launches via Aspire**

Run: `dotnet run --project src/StableDiffusionStudio.AppHost`
Expected: Aspire dashboard opens, web app is accessible, MudBlazor dark theme loads, navigation works.
Stop the app after verification.

- [ ] **Step 13: Commit**

```bash
git add src/StableDiffusionStudio.Web tests/StableDiffusionStudio.Web.Tests
git commit -m "feat: add MudBlazor app shell with dark theme and navigation

MainLayout with drawer, app bar, and nav menu. StudioTheme with dark-first
creative-tool palette. Home dashboard with quick start. EmptyState
component. DI wiring for EF Core, repositories, and services. Auto-migrate
on startup. bUnit smoke tests for layout."
```

---

## Chunk 2: Model Scanning Vertical Slice (Tasks 6–10)

### Task 6: Project CRUD UI Pages

**Files:**
- Create: `src/StableDiffusionStudio.Web/Components/Pages/Projects.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Pages/ProjectDetail.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/ProjectCard.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Dialogs/CreateProjectDialog.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Dialogs/RenameProjectDialog.razor`
- Create: `tests/StableDiffusionStudio.Web.Tests/Components/Pages/ProjectsPageTests.cs`

- [ ] **Step 1: Create ProjectCard component**

Create `src/StableDiffusionStudio.Web/Components/Shared/ProjectCard.razor`:

```razor
<MudCard Elevation="1" Class="cursor-pointer" @onclick="() => OnClick.InvokeAsync(Project)">
    <MudCardContent>
        <div class="d-flex align-center justify-space-between">
            <MudText Typo="Typo.h6">@Project.Name</MudText>
            <div>
                @if (Project.IsPinned)
                {
                    <MudIcon Icon="@Icons.Material.Filled.PushPin" Size="Size.Small" Color="Color.Primary" />
                }
                <MudChip T="string" Size="Size.Small" Color="StatusColor">@Project.Status</MudChip>
            </div>
        </div>
        @if (!string.IsNullOrWhiteSpace(Project.Description))
        {
            <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">@Project.Description</MudText>
        }
        <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-2">
            Updated @Project.UpdatedAt.Humanize()
        </MudText>
    </MudCardContent>
</MudCard>

@code {
    [Parameter, EditorRequired] public required StableDiffusionStudio.Application.DTOs.ProjectDto Project { get; set; }
    [Parameter] public EventCallback<StableDiffusionStudio.Application.DTOs.ProjectDto> OnClick { get; set; }

    private Color StatusColor => Project.Status switch
    {
        Domain.Enums.ProjectStatus.Active => Color.Success,
        Domain.Enums.ProjectStatus.Archived => Color.Warning,
        _ => Color.Default
    };
}
```

Note: Add `Humanize` nuget package to Web project, or replace with `Project.UpdatedAt.ToString("g")` if preferring no extra dependency.

- [ ] **Step 2: Create CreateProjectDialog**

Create `src/StableDiffusionStudio.Web/Components/Dialogs/CreateProjectDialog.razor`:

```razor
@inject StableDiffusionStudio.Application.Services.ProjectService ProjectService

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">New Project</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_name" Label="Project Name" Required="true"
                      RequiredError="Name is required" Class="mb-3" AutoFocus="true" />
        <MudTextField @bind-Value="_description" Label="Description (optional)"
                      Lines="3" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled"
                   OnClick="Submit" Disabled="string.IsNullOrWhiteSpace(_name)">
            Create
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    private string _name = string.Empty;
    private string? _description;

    private async Task Submit()
    {
        var result = await ProjectService.CreateAsync(
            new Application.Commands.CreateProjectCommand(_name, _description));
        MudDialog.Close(DialogResult.Ok(result));
    }

    private void Cancel() => MudDialog.Cancel();
}
```

- [ ] **Step 3: Create RenameProjectDialog**

Create `src/StableDiffusionStudio.Web/Components/Dialogs/RenameProjectDialog.razor`:

```razor
@inject StableDiffusionStudio.Application.Services.ProjectService ProjectService

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">Rename Project</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_name" Label="New Name" Required="true"
                      RequiredError="Name is required" AutoFocus="true" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled"
                   OnClick="Submit" Disabled="string.IsNullOrWhiteSpace(_name)">
            Rename
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public Guid ProjectId { get; set; }
    [Parameter] public string CurrentName { get; set; } = string.Empty;

    private string _name = string.Empty;

    protected override void OnInitialized() => _name = CurrentName;

    private async Task Submit()
    {
        await ProjectService.RenameAsync(
            new Application.Commands.RenameProjectCommand(ProjectId, _name));
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog.Cancel();
}
```

- [ ] **Step 4: Create Projects page**

Create `src/StableDiffusionStudio.Web/Components/Pages/Projects.razor`:

```razor
@page "/projects"
@inject StableDiffusionStudio.Application.Services.ProjectService ProjectService
@inject IDialogService DialogService
@inject ISnackbar Snackbar
@inject NavigationManager Nav

<MudText Typo="Typo.h4" Class="mb-4">Projects</MudText>

<MudToolBar Dense="true" Class="mb-4 px-0">
    <MudTextField @bind-Value="_searchTerm" Placeholder="Search projects..."
                  Adornment="Adornment.Start" AdornmentIcon="@Icons.Material.Filled.Search"
                  Immediate="true" DebounceInterval="300" OnDebounceIntervalElapsed="LoadProjects"
                  Variant="Variant.Outlined" Margin="Margin.Dense" Class="mr-2" />
    <MudSpacer />
    <MudButton Variant="Variant.Filled" Color="Color.Primary"
               StartIcon="@Icons.Material.Filled.Add" OnClick="CreateProject">
        New Project
    </MudButton>
</MudToolBar>

@if (_projects is null)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (_projects.Count == 0)
{
    <EmptyState Title="No projects yet"
                Subtitle="Create your first project to start generating images"
                Icon="@Icons.Material.Filled.Folder"
                ActionText="New Project"
                OnAction="CreateProject" />
}
else
{
    <MudGrid>
        @foreach (var project in _projects)
        {
            <MudItem xs="12" sm="6" md="4">
                <ProjectCard Project="project" OnClick="p => Nav.NavigateTo($\"/projects/{p.Id}\")" />
            </MudItem>
        }
    </MudGrid>
}

@code {
    private IReadOnlyList<Application.DTOs.ProjectDto>? _projects;
    private string? _searchTerm;

    protected override async Task OnInitializedAsync() => await LoadProjects();

    private async Task LoadProjects()
    {
        _projects = await ProjectService.ListAsync(
            new Application.DTOs.ProjectFilter(SearchTerm: _searchTerm));
    }

    private async Task CreateProject()
    {
        var dialog = await DialogService.ShowAsync<Dialogs.CreateProjectDialog>("New Project");
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            await LoadProjects();
            Snackbar.Add("Project created", MudBlazor.Severity.Success);
        }
    }
}
```

- [ ] **Step 5: Create ProjectDetail page**

Create `src/StableDiffusionStudio.Web/Components/Pages/ProjectDetail.razor`:

```razor
@page "/projects/{Id:guid}"
@inject StableDiffusionStudio.Application.Services.ProjectService ProjectService
@inject IDialogService DialogService
@inject ISnackbar Snackbar
@inject NavigationManager Nav

@if (_project is null)
{
    <MudProgressLinear Indeterminate="true" />
}
else
{
    <MudBreadcrumbs Items="_breadcrumbs" Class="mb-2 px-0" />

    <div class="d-flex align-center justify-space-between mb-4">
        <MudText Typo="Typo.h4">@_project.Name</MudText>
        <MudMenu Icon="@Icons.Material.Filled.MoreVert">
            <MudMenuItem OnClick="RenameProject" Icon="@Icons.Material.Filled.Edit">Rename</MudMenuItem>
            <MudMenuItem OnClick="TogglePin" Icon="@Icons.Material.Filled.PushPin">
                @(_project.IsPinned ? "Unpin" : "Pin")
            </MudMenuItem>
            <MudMenuItem OnClick="ArchiveProject" Icon="@Icons.Material.Filled.Archive">Archive</MudMenuItem>
            <MudMenuItem OnClick="DeleteProject" Icon="@Icons.Material.Filled.Delete" IconColor="Color.Error">Delete</MudMenuItem>
        </MudMenu>
    </div>

    <MudText Typo="Typo.body1" Color="Color.Secondary" Class="mb-4">
        @(_project.Description ?? "No description")
    </MudText>

    <MudPaper Class="pa-6" Elevation="1">
        <EmptyState Title="Generation workspace coming soon"
                    Subtitle="Model selection and image generation will appear here in Milestone 3"
                    Icon="@Icons.Material.Filled.Image" />
    </MudPaper>
}

@code {
    [Parameter] public Guid Id { get; set; }

    private Application.DTOs.ProjectDto? _project;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override async Task OnInitializedAsync() => await LoadProject();

    private async Task LoadProject()
    {
        _project = await ProjectService.GetByIdAsync(Id);
        _breadcrumbs = new()
        {
            new("Projects", "/projects"),
            new(_project?.Name ?? "Unknown", null, disabled: true)
        };
    }

    private async Task RenameProject()
    {
        var parameters = new DialogParameters<Dialogs.RenameProjectDialog>
        {
            { x => x.ProjectId, Id },
            { x => x.CurrentName, _project!.Name }
        };
        var dialog = await DialogService.ShowAsync<Dialogs.RenameProjectDialog>("Rename", parameters);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            await LoadProject();
            Snackbar.Add("Project renamed", MudBlazor.Severity.Success);
        }
    }

    private async Task TogglePin()
    {
        if (_project!.IsPinned)
            await ProjectService.UnpinAsync(Id);
        else
            await ProjectService.PinAsync(Id);
        await LoadProject();
    }

    private async Task ArchiveProject()
    {
        await ProjectService.ArchiveAsync(Id);
        Nav.NavigateTo("/projects");
        Snackbar.Add("Project archived", MudBlazor.Severity.Info);
    }

    private async Task DeleteProject()
    {
        var confirmed = await DialogService.ShowMessageBox(
            "Delete Project", "Are you sure? This action cannot be undone.",
            yesText: "Delete", cancelText: "Cancel");
        if (confirmed == true)
        {
            await ProjectService.DeleteAsync(Id);
            Nav.NavigateTo("/projects");
            Snackbar.Add("Project deleted", MudBlazor.Severity.Warning);
        }
    }
}
```

- [ ] **Step 6: Write bUnit tests for Projects page**

Create `tests/StableDiffusionStudio.Web.Tests/Components/Pages/ProjectsPageTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Web.Components.Pages;

namespace StableDiffusionStudio.Web.Tests.Components.Pages;

public class ProjectsPageTests : TestContext
{
    private readonly IProjectRepository _repo;

    public ProjectsPageTests()
    {
        _repo = Substitute.For<IProjectRepository>();
        Services.AddMudServices();
        Services.AddSingleton(_repo);
        Services.AddScoped<ProjectService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Projects_WhenEmpty_ShowsEmptyState()
    {
        _repo.ListAsync(Arg.Any<ProjectFilter>())
            .Returns(new List<Domain.Entities.Project>());

        var cut = RenderComponent<Projects>();

        cut.Markup.Should().Contain("No projects yet");
    }

    [Fact]
    public void Projects_WithData_ShowsProjectCards()
    {
        var projects = new List<Domain.Entities.Project>
        {
            Domain.Entities.Project.Create("Alpha", null),
            Domain.Entities.Project.Create("Beta", null)
        };
        _repo.ListAsync(Arg.Any<ProjectFilter>()).Returns(projects);

        var cut = RenderComponent<Projects>();

        cut.Markup.Should().Contain("Alpha");
        cut.Markup.Should().Contain("Beta");
    }
}
```

- [ ] **Step 7: Run web tests**

Run: `dotnet test tests/StableDiffusionStudio.Web.Tests -v normal`
Expected: All tests pass.

- [ ] **Step 8: Verify UI in browser**

Run: `dotnet run --project src/StableDiffusionStudio.AppHost`
Manually verify: navigate to Projects page, create a project, see it in the list, open detail, rename, archive, delete. Stop app.

- [ ] **Step 9: Commit**

```bash
git add src/StableDiffusionStudio.Web tests/StableDiffusionStudio.Web.Tests
git commit -m "feat: add Project CRUD UI pages with MudBlazor

Projects list page with search and grid layout. ProjectDetail page with
rename, pin, archive, delete actions via MudBlazor dialogs. ProjectCard
component. CreateProject and RenameProject dialogs. bUnit tests for
empty state and data rendering."
```

---

### Task 7: Domain — ModelRecord Entity and ModelFileAnalyzer

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Entities/ModelRecord.cs`
- Create: `src/StableDiffusionStudio.Domain/Services/ModelFileAnalyzer.cs`
- Create: `src/StableDiffusionStudio.Domain/Services/ModelFileInfo.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/Entities/ModelRecordTests.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/Services/ModelFileAnalyzerTests.cs`

- [ ] **Step 1: Write failing tests for ModelRecord**

Create `tests/StableDiffusionStudio.Domain.Tests/Entities/ModelRecordTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ModelRecordTests
{
    [Fact]
    public void Create_WithValidInputs_SetsProperties()
    {
        var record = ModelRecord.Create(
            title: "Stable Diffusion v1.5",
            filePath: "/models/sd-v1-5.safetensors",
            modelFamily: ModelFamily.SD15,
            format: ModelFormat.SafeTensors,
            fileSize: 4_000_000_000L,
            source: "local-folder");

        record.Id.Should().NotBeEmpty();
        record.Title.Should().Be("Stable Diffusion v1.5");
        record.FilePath.Should().Be("/models/sd-v1-5.safetensors");
        record.ModelFamily.Should().Be(ModelFamily.SD15);
        record.Format.Should().Be(ModelFormat.SafeTensors);
        record.FileSize.Should().Be(4_000_000_000L);
        record.Source.Should().Be("local-folder");
        record.Status.Should().Be(ModelStatus.Available);
    }

    [Fact]
    public void Create_WithoutTitle_DefaultsToFilename()
    {
        var record = ModelRecord.Create(
            title: null,
            filePath: "/models/my-cool-model.safetensors",
            modelFamily: ModelFamily.Unknown,
            format: ModelFormat.SafeTensors,
            fileSize: 1000,
            source: "local");

        record.Title.Should().Be("my-cool-model.safetensors");
    }

    [Fact]
    public void Create_WithEmptyFilePath_ThrowsArgumentException()
    {
        var act = () => ModelRecord.Create(null, "", ModelFamily.Unknown, ModelFormat.Unknown, 0, "local");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkMissing_SetsStatusToMissing()
    {
        var record = ModelRecord.Create(null, "/path/model.ckpt", ModelFamily.Unknown, ModelFormat.CKPT, 1000, "local");
        record.MarkMissing();
        record.Status.Should().Be(ModelStatus.Missing);
    }

    [Fact]
    public void MarkAvailable_SetsStatusToAvailable()
    {
        var record = ModelRecord.Create(null, "/path/model.ckpt", ModelFamily.Unknown, ModelFormat.CKPT, 1000, "local");
        record.MarkMissing();
        record.MarkAvailable();
        record.Status.Should().Be(ModelStatus.Available);
    }

    [Fact]
    public void UpdateMetadata_UpdatesFields()
    {
        var record = ModelRecord.Create(null, "/path/model.safetensors", ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "local");

        record.UpdateMetadata(
            title: "Updated Title",
            modelFamily: ModelFamily.SDXL,
            description: "A fine model",
            tags: new[] { "landscape", "photorealistic" },
            previewImagePath: "/path/preview.png",
            compatibilityHints: "Requires 8GB VRAM");

        record.Title.Should().Be("Updated Title");
        record.ModelFamily.Should().Be(ModelFamily.SDXL);
        record.Description.Should().Be("A fine model");
        record.Tags.Should().BeEquivalentTo(new[] { "landscape", "photorealistic" });
        record.PreviewImagePath.Should().Be("/path/preview.png");
        record.CompatibilityHints.Should().Be("Requires 8GB VRAM");
    }
}
```

- [ ] **Step 2: Write failing tests for ModelFileAnalyzer**

Create `tests/StableDiffusionStudio.Domain.Tests/Services/ModelFileAnalyzerTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class ModelFileAnalyzerTests
{
    [Theory]
    [InlineData("model.safetensors", ModelFormat.SafeTensors)]
    [InlineData("model.ckpt", ModelFormat.CKPT)]
    [InlineData("model.gguf", ModelFormat.GGUF)]
    [InlineData("model.bin", ModelFormat.Unknown)]
    [InlineData("model.txt", ModelFormat.Unknown)]
    public void InferFormat_FromExtension_ReturnsCorrectFormat(string fileName, ModelFormat expected)
    {
        var info = new ModelFileInfo(fileName, 1000, null);
        var result = ModelFileAnalyzer.InferFormat(info);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("model.safetensors", 2_000_000_000L, ModelFamily.SD15)]   // ~2GB = SD 1.5
    [InlineData("model.safetensors", 4_200_000_000L, ModelFamily.SD15)]   // ~4GB = SD 1.5 (with EMA)
    [InlineData("model.safetensors", 6_500_000_000L, ModelFamily.SDXL)]   // ~6.5GB = SDXL
    [InlineData("model.safetensors", 7_200_000_000L, ModelFamily.SDXL)]   // ~7GB = SDXL
    [InlineData("model.safetensors", 12_000_000_000L, ModelFamily.Flux)]  // ~12GB = Flux
    [InlineData("model.safetensors", 500_000_000L, ModelFamily.Unknown)]  // too small
    public void InferFamily_FromSizeHeuristic_ReturnsCorrectFamily(string fileName, long fileSize, ModelFamily expected)
    {
        var info = new ModelFileInfo(fileName, fileSize, null);
        var result = ModelFileAnalyzer.InferFamily(info);
        result.Should().Be(expected);
    }

    [Fact]
    public void InferFamily_WithHeaderHint_OverridesSizeHeuristic()
    {
        // If header contains SDXL architecture keys, trust that over size
        var info = new ModelFileInfo("model.safetensors", 2_000_000_000L,
            headerHint: "conditioner.embedders.1.model.transformer");
        var result = ModelFileAnalyzer.InferFamily(info);
        result.Should().Be(ModelFamily.SDXL);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/StableDiffusionStudio.Domain.Tests`
Expected: Compilation errors — `ModelRecord`, `ModelFileAnalyzer`, `ModelFileInfo` do not exist.

- [ ] **Step 4: Implement ModelRecord entity**

Create `src/StableDiffusionStudio.Domain/Entities/ModelRecord.cs`:

```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class ModelRecord
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ModelFamily ModelFamily { get; private set; }
    public ModelFormat Format { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string? Checksum { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; private set; } = Array.Empty<string>();
    public string? Description { get; private set; }
    public string? PreviewImagePath { get; private set; }
    public string? CompatibilityHints { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }
    public DateTimeOffset? LastVerifiedAt { get; private set; }
    public ModelStatus Status { get; private set; }

    private ModelRecord() { } // EF Core

    public static ModelRecord Create(
        string? title,
        string filePath,
        ModelFamily modelFamily,
        ModelFormat format,
        long fileSize,
        string source)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        return new ModelRecord
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileName(filePath) : title,
            ModelFamily = modelFamily,
            Format = format,
            FilePath = filePath,
            FileSize = fileSize,
            Source = source,
            DetectedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow,
            Status = ModelStatus.Available
        };
    }

    public void MarkMissing()
    {
        Status = ModelStatus.Missing;
    }

    public void MarkAvailable()
    {
        Status = ModelStatus.Available;
        LastVerifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateMetadata(
        string? title = null,
        ModelFamily? modelFamily = null,
        string? description = null,
        IReadOnlyList<string>? tags = null,
        string? previewImagePath = null,
        string? compatibilityHints = null)
    {
        if (title is not null) Title = title;
        if (modelFamily.HasValue) ModelFamily = modelFamily.Value;
        if (description is not null) Description = description;
        if (tags is not null) Tags = tags;
        if (previewImagePath is not null) PreviewImagePath = previewImagePath;
        if (compatibilityHints is not null) CompatibilityHints = compatibilityHints;
        LastVerifiedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 5: Implement ModelFileInfo and ModelFileAnalyzer**

Create `src/StableDiffusionStudio.Domain/Services/ModelFileInfo.cs`:

```csharp
namespace StableDiffusionStudio.Domain.Services;

public record ModelFileInfo(string FileName, long FileSize, string? HeaderHint);
```

Create `src/StableDiffusionStudio.Domain/Services/ModelFileAnalyzer.cs`:

```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Services;

public static class ModelFileAnalyzer
{
    private static readonly Dictionary<string, ModelFormat> FormatMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".safetensors"] = ModelFormat.SafeTensors,
        [".ckpt"] = ModelFormat.CKPT,
        [".gguf"] = ModelFormat.GGUF,
    };

    public static ModelFormat InferFormat(ModelFileInfo info)
    {
        var ext = Path.GetExtension(info.FileName);
        return FormatMap.GetValueOrDefault(ext, ModelFormat.Unknown);
    }

    public static ModelFamily InferFamily(ModelFileInfo info)
    {
        // Header hint takes priority
        if (!string.IsNullOrEmpty(info.HeaderHint))
        {
            if (info.HeaderHint.Contains("conditioner.embedders.1.model.transformer", StringComparison.OrdinalIgnoreCase))
                return ModelFamily.SDXL;
            if (info.HeaderHint.Contains("double_blocks", StringComparison.OrdinalIgnoreCase))
                return ModelFamily.Flux;
        }

        // Size-based heuristics for safetensors/ckpt
        var sizeGb = info.FileSize / 1_000_000_000.0;

        return sizeGb switch
        {
            >= 10.0 => ModelFamily.Flux,
            >= 5.5 => ModelFamily.SDXL,
            >= 1.5 => ModelFamily.SD15,
            _ => ModelFamily.Unknown
        };
    }
}
```

- [ ] **Step 6: Run all domain tests**

Run: `dotnet test tests/StableDiffusionStudio.Domain.Tests -v normal`
Expected: All tests pass (~20 tests).

- [ ] **Step 7: Commit**

```bash
git add src/StableDiffusionStudio.Domain tests/StableDiffusionStudio.Domain.Tests
git commit -m "feat: add ModelRecord entity and ModelFileAnalyzer domain service

ModelRecord with Create, MarkMissing, MarkAvailable, UpdateMetadata.
ModelFileAnalyzer infers format from extension and family from file size
heuristics with header hint override. Full TDD coverage."
```

---

### Task 8: Model Scanning Infrastructure — LocalFolderAdapter and Catalog Repository

**Files:**
- Create: `src/StableDiffusionStudio.Application/Interfaces/IModelCatalogRepository.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IModelSourceAdapter.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ModelFilter.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ModelSourceCapabilities.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ModelRecordConfiguration.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ModelCatalogRepository.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/ModelSources/LocalFolderAdapter.cs`
- Modify: `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs` — add ModelRecords DbSet
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/ModelCatalogRepositoryTests.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/LocalFolderAdapterTests.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/TestFixtures/` — sample model files

- [ ] **Step 1: Define application interfaces and DTOs**

Create `src/StableDiffusionStudio.Application/Interfaces/IModelCatalogRepository.cs`:

```csharp
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IModelCatalogRepository
{
    Task<ModelRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ModelRecord>> ListAsync(ModelFilter filter, CancellationToken ct = default);
    Task<ModelRecord?> GetByFilePathAsync(string filePath, CancellationToken ct = default);
    Task UpsertAsync(ModelRecord record, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
```

Create `src/StableDiffusionStudio.Application/Interfaces/IModelSourceAdapter.cs`:

```csharp
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IModelSourceAdapter
{
    string SourceName { get; }
    Task<IReadOnlyList<ModelRecord>> ScanAsync(StorageRoot root, CancellationToken ct = default);
    ModelSourceCapabilities GetCapabilities();
}
```

Create `src/StableDiffusionStudio.Application/DTOs/ModelFilter.cs`:

```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelFilter(
    string? SearchTerm = null,
    ModelFamily? Family = null,
    ModelFormat? Format = null,
    ModelStatus? Status = null,
    string? Source = null,
    int Skip = 0,
    int Take = 50);
```

Create `src/StableDiffusionStudio.Application/DTOs/ModelSourceCapabilities.cs`:

```csharp
namespace StableDiffusionStudio.Application.DTOs;

public record ModelSourceCapabilities(
    bool CanScanLocal,
    bool CanDownload,
    bool CanSearch,
    bool RequiresAuth);
```

- [ ] **Step 2: Write failing repository tests**

Create `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/ModelCatalogRepositoryTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public class ModelCatalogRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly ModelCatalogRepository _repo;

    public ModelCatalogRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new ModelCatalogRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpsertAsync_NewRecord_AddsToDatabase()
    {
        var record = ModelRecord.Create("Test Model", "/path/model.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "local");

        await _repo.UpsertAsync(record);
        var retrieved = await _repo.GetByIdAsync(record.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test Model");
    }

    [Fact]
    public async Task GetByFilePathAsync_ExistingPath_ReturnsRecord()
    {
        var record = ModelRecord.Create(null, "/models/test.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local");
        await _repo.UpsertAsync(record);

        var found = await _repo.GetByFilePathAsync("/models/test.safetensors");

        found.Should().NotBeNull();
        found!.Id.Should().Be(record.Id);
    }

    [Fact]
    public async Task ListAsync_WithFamilyFilter_FiltersCorrectly()
    {
        await _repo.UpsertAsync(ModelRecord.Create("SD15", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local"));
        await _repo.UpsertAsync(ModelRecord.Create("SDXL", "/b.safetensors", ModelFamily.SDXL, ModelFormat.SafeTensors, 1000, "local"));

        var results = await _repo.ListAsync(new ModelFilter(Family: ModelFamily.SD15));

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("SD15");
    }

    [Fact]
    public async Task ListAsync_WithSearchTerm_SearchesTitleAndTags()
    {
        var record = ModelRecord.Create("Dreamshaper", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local");
        await _repo.UpsertAsync(record);
        await _repo.UpsertAsync(ModelRecord.Create("Realistic", "/b.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local"));

        var results = await _repo.ListAsync(new ModelFilter(SearchTerm: "Dream"));

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("Dreamshaper");
    }

    [Fact]
    public async Task RemoveAsync_DeletesRecord()
    {
        var record = ModelRecord.Create(null, "/path/m.safetensors", ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "local");
        await _repo.UpsertAsync(record);

        await _repo.RemoveAsync(record.Id);

        var retrieved = await _repo.GetByIdAsync(record.Id);
        retrieved.Should().BeNull();
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/StableDiffusionStudio.Infrastructure.Tests`
Expected: Compilation errors — `ModelCatalogRepository` does not exist, `AppDbContext` lacks `ModelRecords` DbSet.

- [ ] **Step 4: Add ModelRecords to AppDbContext**

Modify `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ModelRecord> ModelRecords => Set<ModelRecord>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Implement ModelRecord EF configuration**

Create `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ModelRecordConfiguration.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ModelRecordConfiguration : IEntityTypeConfiguration<ModelRecord>
{
    public void Configure(EntityTypeBuilder<ModelRecord> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).IsRequired().HasMaxLength(500);
        builder.Property(m => m.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.Source).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Description).HasMaxLength(5000);
        builder.Property(m => m.PreviewImagePath).HasMaxLength(1000);
        builder.Property(m => m.CompatibilityHints).HasMaxLength(2000);
        builder.Property(m => m.Checksum).HasMaxLength(128);

        builder.Property(m => m.ModelFamily).HasConversion<string>();
        builder.Property(m => m.Format).HasConversion<string>();
        builder.Property(m => m.Status).HasConversion<string>();

        builder.Property(m => m.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasMaxLength(4000);

        builder.HasIndex(m => m.FilePath).IsUnique();
        builder.HasIndex(m => m.ModelFamily);
        builder.HasIndex(m => m.Format);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.Source);
    }
}
```

- [ ] **Step 6: Implement ModelCatalogRepository**

Create `src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ModelCatalogRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class ModelCatalogRepository : IModelCatalogRepository
{
    private readonly AppDbContext _context;

    public ModelCatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ModelRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ModelRecords.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<ModelRecord>> ListAsync(ModelFilter filter, CancellationToken ct = default)
    {
        var query = _context.ModelRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            query = query.Where(m => m.Title.Contains(filter.SearchTerm));

        if (filter.Family.HasValue)
            query = query.Where(m => m.ModelFamily == filter.Family.Value);

        if (filter.Format.HasValue)
            query = query.Where(m => m.Format == filter.Format.Value);

        if (filter.Status.HasValue)
            query = query.Where(m => m.Status == filter.Status.Value);

        if (!string.IsNullOrWhiteSpace(filter.Source))
            query = query.Where(m => m.Source == filter.Source);

        return await query
            .OrderBy(m => m.Title)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }

    public async Task<ModelRecord?> GetByFilePathAsync(string filePath, CancellationToken ct = default)
    {
        return await _context.ModelRecords
            .FirstOrDefaultAsync(m => m.FilePath == filePath, ct);
    }

    public async Task UpsertAsync(ModelRecord record, CancellationToken ct = default)
    {
        var existing = await _context.ModelRecords.FindAsync([record.Id], ct);
        if (existing is null)
            _context.ModelRecords.Add(record);
        else
            _context.Entry(existing).CurrentValues.SetValues(record);

        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _context.ModelRecords.FindAsync([id], ct);
        if (record is not null)
        {
            _context.ModelRecords.Remove(record);
            await _context.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 7: Run repository tests**

Run: `dotnet test tests/StableDiffusionStudio.Infrastructure.Tests -v normal`
Expected: All repository tests pass.

- [ ] **Step 8: Create test fixture files for LocalFolderAdapter**

Create small dummy files in `tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/TestFixtures/`:
- `test-model.safetensors` — a tiny file (even 1 byte is fine for path scanning tests)
- `test-model.ckpt` — tiny dummy
- `not-a-model.txt` — should be ignored by scanner
- `test-model.preview.png` — sidecar preview

```bash
mkdir -p tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/TestFixtures
echo "fake-safetensors" > tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/TestFixtures/test-model.safetensors
echo "fake-ckpt" > tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/TestFixtures/test-model.ckpt
echo "not a model" > tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/TestFixtures/not-a-model.txt
echo "fake-png" > tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/TestFixtures/test-model.preview.png
```

Mark these as `CopyToOutputDirectory` in the `.csproj`:

```xml
<ItemGroup>
  <None Include="ModelSources\TestFixtures\**\*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 9: Write failing tests for LocalFolderAdapter**

Create `tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/LocalFolderAdapterTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.ModelSources;

namespace StableDiffusionStudio.Infrastructure.Tests.ModelSources;

public class LocalFolderAdapterTests
{
    private readonly LocalFolderAdapter _adapter = new();
    private readonly string _fixturesPath;

    public LocalFolderAdapterTests()
    {
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "ModelSources", "TestFixtures");
    }

    [Fact]
    public async Task ScanAsync_FindsModelFiles()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");

        var results = await _adapter.ScanAsync(root);

        results.Should().HaveCount(2); // .safetensors and .ckpt, not .txt
    }

    [Fact]
    public async Task ScanAsync_InfersFormatFromExtension()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");

        var results = await _adapter.ScanAsync(root);

        results.Should().Contain(r => r.Format == ModelFormat.SafeTensors);
        results.Should().Contain(r => r.Format == ModelFormat.CKPT);
    }

    [Fact]
    public async Task ScanAsync_DetectsSidecarPreview()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");

        var results = await _adapter.ScanAsync(root);

        var safetensorsModel = results.First(r => r.Format == ModelFormat.SafeTensors);
        safetensorsModel.PreviewImagePath.Should().NotBeNullOrEmpty();
        safetensorsModel.PreviewImagePath.Should().EndWith("test-model.preview.png");
    }

    [Fact]
    public async Task ScanAsync_SetsSourceName()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");

        var results = await _adapter.ScanAsync(root);

        results.Should().OnlyContain(r => r.Source == "local-folder");
    }

    [Fact]
    public void GetCapabilities_ReturnsLocalScanCapability()
    {
        var caps = _adapter.GetCapabilities();
        caps.CanScanLocal.Should().BeTrue();
        caps.CanDownload.Should().BeFalse();
    }

    [Fact]
    public void SourceName_ReturnsLocalFolder()
    {
        _adapter.SourceName.Should().Be("local-folder");
    }
}
```

- [ ] **Step 10: Implement LocalFolderAdapter**

Create `src/StableDiffusionStudio.Infrastructure/ModelSources/LocalFolderAdapter.cs`:

```csharp
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.ModelSources;

public class LocalFolderAdapter : IModelSourceAdapter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".safetensors", ".ckpt", ".gguf"
    };

    private static readonly HashSet<string> PreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public string SourceName => "local-folder";

    public Task<IReadOnlyList<ModelRecord>> ScanAsync(StorageRoot root, CancellationToken ct = default)
    {
        var results = new List<ModelRecord>();

        if (!Directory.Exists(root.Path))
            return Task.FromResult<IReadOnlyList<ModelRecord>>(results);

        var files = Directory.EnumerateFiles(root.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);
            var modelFileInfo = new ModelFileInfo(fileInfo.Name, fileInfo.Length, ReadHeaderHint(filePath));

            var format = ModelFileAnalyzer.InferFormat(modelFileInfo);
            var family = ModelFileAnalyzer.InferFamily(modelFileInfo);
            var previewPath = FindPreviewImage(filePath);

            var record = ModelRecord.Create(
                title: null,
                filePath: filePath,
                modelFamily: family,
                format: format,
                fileSize: fileInfo.Length,
                source: SourceName);

            if (previewPath is not null)
                record.UpdateMetadata(previewImagePath: previewPath);

            results.Add(record);
        }

        return Task.FromResult<IReadOnlyList<ModelRecord>>(results);
    }

    public ModelSourceCapabilities GetCapabilities() =>
        new(CanScanLocal: true, CanDownload: false, CanSearch: false, RequiresAuth: false);

    private static string? FindPreviewImage(string modelFilePath)
    {
        var dir = Path.GetDirectoryName(modelFilePath);
        if (dir is null) return null;

        var baseName = Path.GetFileNameWithoutExtension(modelFilePath);

        foreach (var ext in PreviewExtensions)
        {
            var previewPath = Path.Combine(dir, $"{baseName}.preview{ext}");
            if (File.Exists(previewPath))
                return previewPath;
        }

        return null;
    }

    private static string? ReadHeaderHint(string filePath)
    {
        // For safetensors, read the header length (first 8 bytes) then the header JSON
        if (!filePath.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            if (stream.Length < 8) return null;

            var lengthBytes = new byte[8];
            stream.ReadExactly(lengthBytes);
            var headerLength = BitConverter.ToInt64(lengthBytes);

            if (headerLength <= 0 || headerLength > 10_000_000) return null;

            var headerBytes = new byte[Math.Min(headerLength, 4096)]; // Read up to 4KB
            stream.ReadExactly(headerBytes);

            return System.Text.Encoding.UTF8.GetString(headerBytes);
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 11: Run all infrastructure tests**

Run: `dotnet test tests/StableDiffusionStudio.Infrastructure.Tests -v normal`
Expected: All tests pass.

- [ ] **Step 12: Add EF migration for ModelRecord**

```bash
cd src/StableDiffusionStudio.Infrastructure
dotnet ef migrations add AddModelRecord --startup-project ../StableDiffusionStudio.Web
cd ../..
```

- [ ] **Step 13: Commit**

```bash
git add src/ tests/
git commit -m "feat: add model scanning infrastructure with LocalFolderAdapter

IModelCatalogRepository and IModelSourceAdapter interfaces. LocalFolderAdapter
scans directories for .safetensors/.ckpt/.gguf files, infers format and family
via ModelFileAnalyzer, detects sidecar preview images, reads safetensors headers.
ModelCatalogRepository with upsert, search, and filtering. EF migration for
ModelRecord table. Integration tests with fixture files."
```

---

### Task 9: Model Catalog Application Service

**Files:**
- Create: `src/StableDiffusionStudio.Application/Services/ModelCatalogService.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ModelRecordDto.cs`
- Create: `src/StableDiffusionStudio.Application/Commands/ScanModelsCommand.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IStorageRootProvider.cs`
- Create: `tests/StableDiffusionStudio.Application.Tests/Services/ModelCatalogServiceTests.cs`

- [ ] **Step 1: Write failing tests for ModelCatalogService**

Create `tests/StableDiffusionStudio.Application.Tests/Services/ModelCatalogServiceTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Tests.Services;

public class ModelCatalogServiceTests
{
    private readonly IModelCatalogRepository _catalogRepo = Substitute.For<IModelCatalogRepository>();
    private readonly IModelSourceAdapter _adapter = Substitute.For<IModelSourceAdapter>();
    private readonly IStorageRootProvider _rootProvider = Substitute.For<IStorageRootProvider>();
    private readonly ModelCatalogService _service;

    public ModelCatalogServiceTests()
    {
        _adapter.SourceName.Returns("test-adapter");
        _service = new ModelCatalogService(_catalogRepo, new[] { _adapter }, _rootProvider);
    }

    [Fact]
    public async Task ScanAsync_ScansAllRoots_UpsertDiscoveredModels()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync().Returns(new[] { root });

        var scannedModel = ModelRecord.Create("Found Model", "/models/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "test-adapter");
        _adapter.ScanAsync(root).Returns(new[] { scannedModel });
        _catalogRepo.GetByFilePathAsync("/models/m.safetensors").Returns((ModelRecord?)null);

        var result = await _service.ScanAsync(new ScanModelsCommand(null));

        result.NewCount.Should().Be(1);
        await _catalogRepo.Received(1).UpsertAsync(Arg.Any<ModelRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_WithSpecificRoot_ScansOnlyThatRoot()
    {
        var root = new StorageRoot("/specific", "Specific");
        _rootProvider.GetRootsAsync().Returns(new[] { root, new StorageRoot("/other", "Other") });

        _adapter.ScanAsync(Arg.Is<StorageRoot>(r => r.Path == "/specific"))
            .Returns(Array.Empty<ModelRecord>());

        await _service.ScanAsync(new ScanModelsCommand("/specific"));

        await _adapter.Received(1).ScanAsync(Arg.Is<StorageRoot>(r => r.Path == "/specific"), Arg.Any<CancellationToken>());
        await _adapter.DidNotReceive().ScanAsync(Arg.Is<StorageRoot>(r => r.Path == "/other"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_ExistingModel_UpdatesInsteadOfCreating()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync().Returns(new[] { root });

        var existing = ModelRecord.Create("Existing", "/models/m.safetensors",
            ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "test-adapter");
        _catalogRepo.GetByFilePathAsync("/models/m.safetensors").Returns(existing);

        var scanned = ModelRecord.Create("Scanned", "/models/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "test-adapter");
        _adapter.ScanAsync(root).Returns(new[] { scanned });

        var result = await _service.ScanAsync(new ScanModelsCommand(null));

        result.UpdatedCount.Should().Be(1);
        result.NewCount.Should().Be(0);
    }

    [Fact]
    public async Task ListAsync_DelegatesToRepository()
    {
        var filter = new ModelFilter();
        var records = new List<ModelRecord>
        {
            ModelRecord.Create("A", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local")
        };
        _catalogRepo.ListAsync(filter).Returns(records);

        var result = await _service.ListAsync(filter);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("A");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/StableDiffusionStudio.Application.Tests`
Expected: Compilation errors.

- [ ] **Step 3: Implement supporting types**

Create `src/StableDiffusionStudio.Application/Commands/ScanModelsCommand.cs`:
```csharp
namespace StableDiffusionStudio.Application.Commands;

public record ScanModelsCommand(string? StorageRootPath);
```

Create `src/StableDiffusionStudio.Application/DTOs/ModelRecordDto.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelRecordDto(
    Guid Id,
    string Title,
    ModelFamily ModelFamily,
    ModelFormat Format,
    string FilePath,
    long FileSize,
    string Source,
    IReadOnlyList<string> Tags,
    string? Description,
    string? PreviewImagePath,
    string? CompatibilityHints,
    ModelStatus Status,
    DateTimeOffset DetectedAt);
```

Create `src/StableDiffusionStudio.Application/DTOs/ScanResult.cs`:
```csharp
namespace StableDiffusionStudio.Application.DTOs;

public record ScanResult(int NewCount, int UpdatedCount, int MissingCount);
```

Create `src/StableDiffusionStudio.Application/Interfaces/IStorageRootProvider.cs`:
```csharp
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IStorageRootProvider
{
    Task<IReadOnlyList<StorageRoot>> GetRootsAsync(CancellationToken ct = default);
    Task AddRootAsync(StorageRoot root, CancellationToken ct = default);
    Task RemoveRootAsync(string path, CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement ModelCatalogService**

Create `src/StableDiffusionStudio.Application/Services/ModelCatalogService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Services;

public class ModelCatalogService
{
    private readonly IModelCatalogRepository _repository;
    private readonly IEnumerable<IModelSourceAdapter> _adapters;
    private readonly IStorageRootProvider _rootProvider;
    private readonly ILogger<ModelCatalogService>? _logger;

    public ModelCatalogService(
        IModelCatalogRepository repository,
        IEnumerable<IModelSourceAdapter> adapters,
        IStorageRootProvider rootProvider,
        ILogger<ModelCatalogService>? logger = null)
    {
        _repository = repository;
        _adapters = adapters;
        _rootProvider = rootProvider;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(ScanModelsCommand command, CancellationToken ct = default)
    {
        var roots = await _rootProvider.GetRootsAsync(ct);

        if (command.StorageRootPath is not null)
            roots = roots.Where(r => r.Path == command.StorageRootPath).ToList();

        int newCount = 0, updatedCount = 0;

        foreach (var root in roots)
        {
            foreach (var adapter in _adapters)
            {
                _logger?.LogInformation("Scanning {Root} with {Adapter}", root.Path, adapter.SourceName);

                var discovered = await adapter.ScanAsync(root, ct);

                foreach (var record in discovered)
                {
                    var existing = await _repository.GetByFilePathAsync(record.FilePath, ct);

                    if (existing is not null)
                    {
                        existing.UpdateMetadata(
                            modelFamily: record.ModelFamily,
                            previewImagePath: record.PreviewImagePath);
                        existing.MarkAvailable();
                        await _repository.UpsertAsync(existing, ct);
                        updatedCount++;
                    }
                    else
                    {
                        await _repository.UpsertAsync(record, ct);
                        newCount++;
                    }
                }
            }
        }

        return new ScanResult(newCount, updatedCount, MissingCount: 0);
    }

    public async Task<IReadOnlyList<ModelRecordDto>> ListAsync(ModelFilter filter, CancellationToken ct = default)
    {
        var records = await _repository.ListAsync(filter, ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<ModelRecordDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _repository.GetByIdAsync(id, ct);
        return record is null ? null : ToDto(record);
    }

    private static ModelRecordDto ToDto(ModelRecord r) =>
        new(r.Id, r.Title, r.ModelFamily, r.Format, r.FilePath, r.FileSize,
            r.Source, r.Tags, r.Description, r.PreviewImagePath,
            r.CompatibilityHints, r.Status, r.DetectedAt);
}
```

- [ ] **Step 5: Run application tests**

Run: `dotnet test tests/StableDiffusionStudio.Application.Tests -v normal`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/StableDiffusionStudio.Application tests/StableDiffusionStudio.Application.Tests
git commit -m "feat: add ModelCatalogService with scan orchestration

ModelCatalogService coordinates adapters and storage roots for model
scanning. Upserts discovered models, updates existing records. ScanResult
reports new/updated/missing counts. IStorageRootProvider interface.
Full unit test coverage with NSubstitute."
```

---

### Task 10: Models UI — Browser Page, Cards, Filters

**Files:**
- Create: `src/StableDiffusionStudio.Web/Components/Pages/Models.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/ModelCard.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Dialogs/AddStorageRootDialog.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Dialogs/ModelDetailDialog.razor`
- Modify: `src/StableDiffusionStudio.Web/Program.cs` — register model services
- Create: `tests/StableDiffusionStudio.Web.Tests/Components/Pages/ModelsPageTests.cs`

- [ ] **Step 1: Create ModelCard component**

Create `src/StableDiffusionStudio.Web/Components/Shared/ModelCard.razor`:

```razor
@using StableDiffusionStudio.Application.DTOs

<MudCard Elevation="1" Class="cursor-pointer" @onclick="() => OnClick.InvokeAsync(Model)">
    <MudCardContent>
        <MudText Typo="Typo.subtitle1" Class="mb-1">@Model.Title</MudText>
        <div class="d-flex gap-2 mb-2">
            <MudChip T="string" Size="Size.Small" Color="Color.Primary">@Model.ModelFamily</MudChip>
            <MudChip T="string" Size="Size.Small" Color="Color.Secondary">@Model.Format</MudChip>
        </div>
        <MudText Typo="Typo.caption" Color="Color.Secondary">
            @FormatFileSize(Model.FileSize) &bull; @Model.Source
        </MudText>
    </MudCardContent>
</MudCard>

@code {
    [Parameter, EditorRequired] public required ModelRecordDto Model { get; set; }
    [Parameter] public EventCallback<ModelRecordDto> OnClick { get; set; }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            >= 1_000_000_000 => $"{bytes / 1_000_000_000.0:F1} GB",
            >= 1_000_000 => $"{bytes / 1_000_000.0:F1} MB",
            _ => $"{bytes / 1_000.0:F1} KB"
        };
    }
}
```

- [ ] **Step 2: Create AddStorageRootDialog**

Create `src/StableDiffusionStudio.Web/Components/Dialogs/AddStorageRootDialog.razor`:

```razor
@using StableDiffusionStudio.Application.Interfaces
@using StableDiffusionStudio.Domain.ValueObjects
@inject IStorageRootProvider StorageRootProvider

<MudDialog>
    <TitleContent><MudText Typo="Typo.h6">Add Model Directory</MudText></TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_path" Label="Directory Path" Required="true"
                      Placeholder="C:\Models\StableDiffusion" Class="mb-3" AutoFocus="true" />
        <MudTextField @bind-Value="_displayName" Label="Display Name" Required="true"
                      Placeholder="My Models" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled"
                   OnClick="Submit" Disabled="@(!IsValid)">Add</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    private string _path = string.Empty;
    private string _displayName = string.Empty;

    private bool IsValid => !string.IsNullOrWhiteSpace(_path) && !string.IsNullOrWhiteSpace(_displayName);

    private async Task Submit()
    {
        await StorageRootProvider.AddRootAsync(new StorageRoot(_path, _displayName));
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog.Cancel();
}
```

- [ ] **Step 3: Create ModelDetailDialog**

Create `src/StableDiffusionStudio.Web/Components/Dialogs/ModelDetailDialog.razor`:

```razor
@using StableDiffusionStudio.Application.DTOs

<MudDialog>
    <TitleContent><MudText Typo="Typo.h6">@Model.Title</MudText></TitleContent>
    <DialogContent>
        <MudSimpleTable Dense="true" Striped="true">
            <tbody>
                <tr><td><strong>Family</strong></td><td>@Model.ModelFamily</td></tr>
                <tr><td><strong>Format</strong></td><td>@Model.Format</td></tr>
                <tr><td><strong>Size</strong></td><td>@FormatSize(Model.FileSize)</td></tr>
                <tr><td><strong>Source</strong></td><td>@Model.Source</td></tr>
                <tr><td><strong>Path</strong></td><td style="word-break:break-all">@Model.FilePath</td></tr>
                <tr><td><strong>Status</strong></td><td>@Model.Status</td></tr>
                <tr><td><strong>Detected</strong></td><td>@Model.DetectedAt.ToString("g")</td></tr>
                @if (Model.Description is not null)
                {
                    <tr><td><strong>Description</strong></td><td>@Model.Description</td></tr>
                }
                @if (Model.Tags.Count > 0)
                {
                    <tr><td><strong>Tags</strong></td><td>@string.Join(", ", Model.Tags)</td></tr>
                }
            </tbody>
        </MudSimpleTable>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Close">Close</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public required ModelRecordDto Model { get; set; }

    private void Close() => MudDialog.Close();

    private static string FormatSize(long bytes) => bytes >= 1_000_000_000
        ? $"{bytes / 1_000_000_000.0:F1} GB"
        : $"{bytes / 1_000_000.0:F1} MB";
}
```

- [ ] **Step 4: Create Models page**

Create `src/StableDiffusionStudio.Web/Components/Pages/Models.razor`:

```razor
@page "/models"
@using StableDiffusionStudio.Application.DTOs
@using StableDiffusionStudio.Application.Services
@using StableDiffusionStudio.Application.Commands
@using StableDiffusionStudio.Domain.Enums
@inject ModelCatalogService CatalogService
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudText Typo="Typo.h4" Class="mb-4">Models</MudText>

<MudToolBar Dense="true" Class="mb-4 px-0">
    <MudTextField @bind-Value="_searchTerm" Placeholder="Search models..."
                  Adornment="Adornment.Start" AdornmentIcon="@Icons.Material.Filled.Search"
                  Immediate="true" DebounceInterval="300" OnDebounceIntervalElapsed="LoadModels"
                  Variant="Variant.Outlined" Margin="Margin.Dense" Class="mr-2" />
    <MudSelect T="ModelFamily?" Label="Family" Value="_familyFilter" ValueChanged="OnFamilyChanged"
               Variant="Variant.Outlined" Margin="Margin.Dense" Clearable="true" Class="mr-2" Style="width:150px">
        <MudSelectItem T="ModelFamily?" Value="ModelFamily.SD15">SD 1.5</MudSelectItem>
        <MudSelectItem T="ModelFamily?" Value="ModelFamily.SDXL">SDXL</MudSelectItem>
        <MudSelectItem T="ModelFamily?" Value="ModelFamily.Flux">Flux</MudSelectItem>
    </MudSelect>
    <MudSpacer />
    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Class="mr-2"
               StartIcon="@Icons.Material.Filled.CreateNewFolder" OnClick="AddStorageRoot">
        Add Directory
    </MudButton>
    <MudButton Variant="Variant.Filled" Color="Color.Primary"
               StartIcon="@Icons.Material.Filled.Refresh" OnClick="ScanModels"
               Disabled="_isScanning">
        @(_isScanning ? "Scanning..." : "Scan Now")
    </MudButton>
</MudToolBar>

@if (_isScanning)
{
    <MudProgressLinear Indeterminate="true" Class="mb-4" />
}

@if (_models is null)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (_models.Count == 0)
{
    <EmptyState Title="No models discovered"
                Subtitle="Add a model directory and scan to discover your models"
                Icon="@Icons.Material.Filled.ViewInAr"
                ActionText="Add Directory"
                OnAction="AddStorageRoot" />
}
else
{
    <MudGrid>
        @foreach (var model in _models)
        {
            <MudItem xs="12" sm="6" md="4" lg="3">
                <ModelCard Model="model" OnClick="ShowModelDetail" />
            </MudItem>
        }
    </MudGrid>
}

@code {
    private IReadOnlyList<ModelRecordDto>? _models;
    private string? _searchTerm;
    private ModelFamily? _familyFilter;
    private bool _isScanning;

    protected override async Task OnInitializedAsync() => await LoadModels();

    private async Task LoadModels()
    {
        _models = await CatalogService.ListAsync(new ModelFilter(
            SearchTerm: _searchTerm, Family: _familyFilter));
    }

    private async Task OnFamilyChanged(ModelFamily? family)
    {
        _familyFilter = family;
        await LoadModels();
    }

    private async Task ScanModels()
    {
        _isScanning = true;
        StateHasChanged();

        try
        {
            var result = await CatalogService.ScanAsync(new ScanModelsCommand(null));
            Snackbar.Add($"Scan complete: {result.NewCount} new, {result.UpdatedCount} updated", MudBlazor.Severity.Success);
            await LoadModels();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Scan failed: {ex.Message}", MudBlazor.Severity.Error);
        }
        finally
        {
            _isScanning = false;
        }
    }

    private async Task AddStorageRoot()
    {
        var dialog = await DialogService.ShowAsync<Dialogs.AddStorageRootDialog>("Add Directory");
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            Snackbar.Add("Storage directory added. Click Scan Now to discover models.", MudBlazor.Severity.Info);
        }
    }

    private async Task ShowModelDetail(ModelRecordDto model)
    {
        var parameters = new DialogParameters<Dialogs.ModelDetailDialog> { { x => x.Model, model } };
        await DialogService.ShowAsync<Dialogs.ModelDetailDialog>(model.Title, parameters);
    }
}
```

- [ ] **Step 5: Update Program.cs with model service registrations**

Add to `src/StableDiffusionStudio.Web/Program.cs` in the DI section:

Note: `DbStorageRootProvider` will be implemented in Task 13 (Settings). For now, create a simple in-memory implementation and register all model services:

Create `src/StableDiffusionStudio.Infrastructure/Storage/InMemoryStorageRootProvider.cs`:

```csharp
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Storage;

public class InMemoryStorageRootProvider : IStorageRootProvider
{
    private readonly List<StorageRoot> _roots = new();

    public Task<IReadOnlyList<StorageRoot>> GetRootsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<StorageRoot>>(_roots.AsReadOnly());
    }

    public Task AddRootAsync(StorageRoot root, CancellationToken ct = default)
    {
        if (!_roots.Any(r => r.Path == root.Path))
            _roots.Add(root);
        return Task.CompletedTask;
    }

    public Task RemoveRootAsync(string path, CancellationToken ct = default)
    {
        _roots.RemoveAll(r => r.Path == path);
        return Task.CompletedTask;
    }
}
```

Register services in Program.cs:
```csharp
// Model scanning (InMemory storage roots replaced by DbStorageRootProvider in Task 13)
builder.Services.AddSingleton<IStorageRootProvider, InMemoryStorageRootProvider>();
builder.Services.AddScoped<IModelCatalogRepository, ModelCatalogRepository>();
builder.Services.AddScoped<IModelSourceAdapter, LocalFolderAdapter>();
builder.Services.AddScoped<ModelCatalogService>();
```

- [ ] **Step 6: Write bUnit test for Models page**

Create `tests/StableDiffusionStudio.Web.Tests/Components/Pages/ModelsPageTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Web.Components.Pages;

namespace StableDiffusionStudio.Web.Tests.Components.Pages;

public class ModelsPageTests : TestContext
{
    private readonly IModelCatalogRepository _catalogRepo;
    private readonly IStorageRootProvider _rootProvider;

    public ModelsPageTests()
    {
        _catalogRepo = Substitute.For<IModelCatalogRepository>();
        _rootProvider = Substitute.For<IStorageRootProvider>();
        var adapter = Substitute.For<IModelSourceAdapter>();

        Services.AddMudServices();
        Services.AddSingleton(_catalogRepo);
        Services.AddSingleton(_rootProvider);
        Services.AddSingleton<IEnumerable<IModelSourceAdapter>>(new[] { adapter });
        Services.AddScoped<ModelCatalogService>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Models_WhenEmpty_ShowsEmptyState()
    {
        _catalogRepo.ListAsync(Arg.Any<ModelFilter>())
            .Returns(new List<Domain.Entities.ModelRecord>());

        var cut = RenderComponent<Models>();

        cut.Markup.Should().Contain("No models discovered");
    }
}
```

- [ ] **Step 7: Run all tests**

Run: `dotnet test StableDiffusionStudio.sln -v normal`
Expected: All tests pass.

- [ ] **Step 8: Verify in browser**

Run: `dotnet run --project src/StableDiffusionStudio.AppHost`
Verify: Models page loads, Add Directory dialog works, Scan Now button triggers (with empty results if no models). Stop app.

- [ ] **Step 9: Commit**

```bash
git add src/ tests/
git commit -m "feat: add Models browser UI with scanning and filtering

Models page with card grid, family filter, search, scan button.
ModelCard, ModelDetailDialog, AddStorageRootDialog components.
InMemoryStorageRootProvider for initial state management. bUnit tests.
Full vertical slice: domain → infrastructure → service → UI."
```

---

## Chunk 3: Background Jobs, Settings, and Polish (Tasks 11–14)

### Task 11: Background Job System

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Entities/JobRecord.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IJobQueue.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/JobRecordDto.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/JobRecordConfiguration.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Jobs/ChannelJobQueue.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Jobs/BackgroundJobProcessor.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Jobs/IJobHandler.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Jobs/ModelScanJobHandler.cs`
- Modify: `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs` — add JobRecords DbSet
- Create: `tests/StableDiffusionStudio.Domain.Tests/Entities/JobRecordTests.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Jobs/ChannelJobQueueTests.cs`

- [ ] **Step 1: Write failing tests for JobRecord entity**

Create `tests/StableDiffusionStudio.Domain.Tests/Entities/JobRecordTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class JobRecordTests
{
    [Fact]
    public void Create_SetsInitialState()
    {
        var job = JobRecord.Create("model-scan", "scan:/models");
        job.Id.Should().NotBeEmpty();
        job.Type.Should().Be("model-scan");
        job.Status.Should().Be(JobStatus.Pending);
        job.Progress.Should().Be(0);
        job.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Start_SetsStatusToRunning()
    {
        var job = JobRecord.Create("model-scan", "data");
        job.Start();
        job.Status.Should().Be(JobStatus.Running);
        job.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProgress_SetsProgressAndPhase()
    {
        var job = JobRecord.Create("model-scan", "data");
        job.Start();
        job.UpdateProgress(50, "Scanning files");
        job.Progress.Should().Be(50);
        job.Phase.Should().Be("Scanning files");
    }

    [Fact]
    public void Complete_SetsStatusAndTimestamp()
    {
        var job = JobRecord.Create("model-scan", "data");
        job.Start();
        job.Complete("3 models found");
        job.Status.Should().Be(JobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
        job.ResultData.Should().Be("3 models found");
    }

    [Fact]
    public void Fail_SetsStatusAndError()
    {
        var job = JobRecord.Create("model-scan", "data");
        job.Start();
        job.Fail("Directory not found");
        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be("Directory not found");
    }

    [Fact]
    public void Cancel_SetsStatusToCancelled()
    {
        var job = JobRecord.Create("model-scan", "data");
        job.Cancel();
        job.Status.Should().Be(JobStatus.Cancelled);
    }
}
```

- [ ] **Step 2: Implement JobRecord entity**

Create `src/StableDiffusionStudio.Domain/Entities/JobRecord.cs`:

```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class JobRecord
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string? Data { get; private set; }
    public JobStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string? Phase { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ResultData { get; private set; }

    private JobRecord() { } // EF Core

    public static JobRecord Create(string type, string? data)
    {
        return new JobRecord
        {
            Id = Guid.NewGuid(),
            Type = type,
            Data = data,
            Status = JobStatus.Pending,
            Progress = 0,
            CorrelationId = Guid.NewGuid().ToString("N")[..12],
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Start()
    {
        Status = JobStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProgress(int progress, string? phase = null)
    {
        Progress = Math.Clamp(progress, 0, 100);
        if (phase is not null) Phase = phase;
    }

    public void Complete(string? resultData = null)
    {
        Status = JobStatus.Completed;
        Progress = 100;
        CompletedAt = DateTimeOffset.UtcNow;
        ResultData = resultData;
    }

    public void Fail(string errorMessage)
    {
        Status = JobStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void Cancel()
    {
        Status = JobStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 3: Run domain tests**

Run: `dotnet test tests/StableDiffusionStudio.Domain.Tests -v normal`
Expected: All tests pass.

- [ ] **Step 4: Implement IJobQueue interface, DTOs, and ChannelJobQueue**

Create `src/StableDiffusionStudio.Application/Interfaces/IJobQueue.cs`:
```csharp
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IJobQueue
{
    Task<Guid> EnqueueAsync(string type, string? data, CancellationToken ct = default);
    Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<JobRecord>> ListAsync(bool activeOnly = false, CancellationToken ct = default);
    Task CancelAsync(Guid jobId, CancellationToken ct = default);
}
```

Create `src/StableDiffusionStudio.Application/DTOs/JobRecordDto.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record JobRecordDto(
    Guid Id,
    string Type,
    JobStatus Status,
    int Progress,
    string? Phase,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    string? ResultData);
```

- [ ] **Step 5: Write failing tests for ChannelJobQueue**

Create `tests/StableDiffusionStudio.Infrastructure.Tests/Jobs/ChannelJobQueueTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Jobs;
using StableDiffusionStudio.Infrastructure.Tests.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Jobs;

public class ChannelJobQueueTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly ChannelJobQueue _queue;

    public ChannelJobQueueTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _queue = new ChannelJobQueue(_context, new JobChannel());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task EnqueueAsync_CreatesJobRecordInDb()
    {
        var jobId = await _queue.EnqueueAsync("model-scan", "/models");

        var job = await _queue.GetStatusAsync(jobId);
        job.Should().NotBeNull();
        job!.Type.Should().Be("model-scan");
        job.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task ListAsync_ActiveOnly_FiltersCorrectly()
    {
        await _queue.EnqueueAsync("scan-1", null);
        var completedId = await _queue.EnqueueAsync("scan-2", null);

        // Simulate completion
        var completed = await _queue.GetStatusAsync(completedId);
        completed!.Start();
        completed.Complete();
        _context.SaveChanges();

        var active = await _queue.ListAsync(activeOnly: true);
        active.Should().HaveCount(1);
    }

    [Fact]
    public async Task CancelAsync_SetsStatusToCancelled()
    {
        var jobId = await _queue.EnqueueAsync("scan", null);
        await _queue.CancelAsync(jobId);

        var job = await _queue.GetStatusAsync(jobId);
        job!.Status.Should().Be(JobStatus.Cancelled);
    }
}
```

- [ ] **Step 6: Implement ChannelJobQueue and BackgroundJobProcessor**

Add `JobRecords` to AppDbContext and create configuration:

Create `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/JobRecordConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class JobRecordConfiguration : IEntityTypeConfiguration<JobRecord>
{
    public void Configure(EntityTypeBuilder<JobRecord> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Type).IsRequired().HasMaxLength(100);
        builder.Property(j => j.Status).HasConversion<string>().IsRequired();
        builder.Property(j => j.CorrelationId).IsRequired().HasMaxLength(20);
        builder.Property(j => j.Phase).HasMaxLength(200);
        builder.Property(j => j.ErrorMessage).HasMaxLength(5000);
        builder.Property(j => j.ResultData).HasMaxLength(5000);
        builder.Property(j => j.Data).HasMaxLength(5000);

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.CreatedAt);
    }
}
```

Update AppDbContext to add `JobRecords`:
```csharp
public DbSet<JobRecord> JobRecords => Set<JobRecord>();
```

Create `src/StableDiffusionStudio.Infrastructure/Jobs/JobChannel.cs` (singleton — owns the channel):

```csharp
using System.Threading.Channels;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class JobChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ChannelReader<Guid> Reader => _channel.Reader;
    public ChannelWriter<Guid> Writer => _channel.Writer;
}
```

Create `src/StableDiffusionStudio.Infrastructure/Jobs/ChannelJobQueue.cs` (scoped — uses DbContext):

```csharp
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class ChannelJobQueue : IJobQueue
{
    private readonly AppDbContext _context;
    private readonly JobChannel _channel;

    public ChannelJobQueue(AppDbContext context, JobChannel channel)
    {
        _context = context;
        _channel = channel;
    }

    public async Task<Guid> EnqueueAsync(string type, string? data, CancellationToken ct = default)
    {
        var job = JobRecord.Create(type, data);
        _context.JobRecords.Add(job);
        await _context.SaveChangesAsync(ct);
        await _channel.Writer.WriteAsync(job.Id, ct);
        return job.Id;
    }

    public async Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct = default)
    {
        return await _context.JobRecords.FindAsync([jobId], ct);
    }

    public async Task<IReadOnlyList<JobRecord>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _context.JobRecords.AsQueryable();

        if (activeOnly)
            query = query.Where(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Running);

        return await query.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);
    }

    public async Task CancelAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _context.JobRecords.FindAsync([jobId], ct);
        if (job is not null && job.Status is JobStatus.Pending or JobStatus.Running)
        {
            job.Cancel();
            await _context.SaveChangesAsync(ct);
        }
    }
}
```

Create `src/StableDiffusionStudio.Infrastructure/Jobs/BackgroundJobProcessor.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class BackgroundJobProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobChannel _channel;
    private readonly ILogger<BackgroundJobProcessor> _logger;

    public BackgroundJobProcessor(
        IServiceScopeFactory scopeFactory,
        JobChannel channel,
        ILogger<BackgroundJobProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background job processor started");

        await foreach (var jobId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing job {JobId}", jobId);
            }
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await context.JobRecords.FindAsync([jobId], ct);
        if (job is null || job.Status == JobStatus.Cancelled) return;

        job.Start();
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("Processing job {JobId} ({Type}) [{CorrelationId}]",
            job.Id, job.Type, job.CorrelationId);

        try
        {
            var handler = scope.ServiceProvider.GetKeyedService<IJobHandler>(job.Type);
            if (handler is null)
            {
                job.Fail($"No handler registered for job type: {job.Type}");
            }
            else
            {
                await handler.HandleAsync(job, ct);
                if (job.Status == JobStatus.Running)
                    job.Complete();
            }
        }
        catch (OperationCanceledException)
        {
            job.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
            job.Fail(ex.Message);
        }

        await context.SaveChangesAsync(ct);
    }
}
```

Create `src/StableDiffusionStudio.Infrastructure/Jobs/IJobHandler.cs`:

```csharp
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public interface IJobHandler
{
    Task HandleAsync(JobRecord job, CancellationToken ct);
}
```

Create `src/StableDiffusionStudio.Infrastructure/Jobs/ModelScanJobHandler.cs`:

```csharp
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class ModelScanJobHandler : IJobHandler
{
    private readonly ModelCatalogService _catalogService;
    private readonly ILogger<ModelScanJobHandler> _logger;

    public ModelScanJobHandler(ModelCatalogService catalogService, ILogger<ModelScanJobHandler> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        job.UpdateProgress(10, "Starting model scan");

        var result = await _catalogService.ScanAsync(new ScanModelsCommand(job.Data), ct);

        job.UpdateProgress(100, "Scan complete");
        job.Complete($"{result.NewCount} new, {result.UpdatedCount} updated, {result.MissingCount} missing");

        _logger.LogInformation("Model scan complete: {New} new, {Updated} updated",
            result.NewCount, result.UpdatedCount);
    }
}
```

- [ ] **Step 7: Run infrastructure tests**

Run: `dotnet test tests/StableDiffusionStudio.Infrastructure.Tests -v normal`
Expected: All tests pass.

- [ ] **Step 8: Add EF migration for JobRecord**

```bash
cd src/StableDiffusionStudio.Infrastructure
dotnet ef migrations add AddJobRecord --startup-project ../StableDiffusionStudio.Web
cd ../..
```

- [ ] **Step 9: Register job services in Program.cs**

Add to DI section in `src/StableDiffusionStudio.Web/Program.cs`:

```csharp
// Background jobs
builder.Services.AddSingleton<JobChannel>();
builder.Services.AddScoped<ChannelJobQueue>();
builder.Services.AddScoped<IJobQueue>(sp => sp.GetRequiredService<ChannelJobQueue>());
builder.Services.AddHostedService<BackgroundJobProcessor>();
builder.Services.AddKeyedScoped<IJobHandler, ModelScanJobHandler>("model-scan");
```

- [ ] **Step 10: Commit**

```bash
git add src/ tests/
git commit -m "feat: add background job system with Channel-based queue

JobRecord entity with lifecycle methods. ChannelJobQueue persists to
SQLite and feeds a Channel<Guid>. BackgroundJobProcessor runs as
hosted service, dispatches to keyed IJobHandler implementations.
ModelScanJobHandler triggers catalog scan. Integration tests."
```

---

### Task 12: Jobs UI and SignalR Real-Time Updates

**Files:**
- Create: `src/StableDiffusionStudio.Web/Components/Pages/Jobs.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/JobProgressCard.razor`
- Create: `src/StableDiffusionStudio.Web/Hubs/StudioHub.cs`
- Modify: `src/StableDiffusionStudio.Web/Program.cs` — map SignalR hub
- Create: `tests/StableDiffusionStudio.Web.Tests/Components/Pages/JobsPageTests.cs`

- [ ] **Step 1: Create StudioHub**

Create `src/StableDiffusionStudio.Web/Hubs/StudioHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace StableDiffusionStudio.Web.Hubs;

public class StudioHub : Hub
{
    // Client methods called from server:
    // - JobProgressUpdated(Guid jobId, int progress, string? phase, string status)
    // - ModelScanCompleted(int newCount, int updatedCount)
    // - ProjectChanged(Guid projectId, string changeType)
}
```

- [ ] **Step 2: Create JobProgressCard component**

Create `src/StableDiffusionStudio.Web/Components/Shared/JobProgressCard.razor`:

```razor
@using StableDiffusionStudio.Application.DTOs
@using StableDiffusionStudio.Domain.Enums

<MudCard Elevation="1" Class="mb-2">
    <MudCardContent>
        <div class="d-flex align-center justify-space-between mb-2">
            <MudText Typo="Typo.subtitle1">@Job.Type</MudText>
            <MudChip T="string" Size="Size.Small" Color="StatusColor">@Job.Status</MudChip>
        </div>

        @if (Job.Status == JobStatus.Running)
        {
            <MudProgressLinear Value="Job.Progress" Max="100" Color="Color.Primary"
                               Striped="true" Class="mb-1" />
            @if (Job.Phase is not null)
            {
                <MudText Typo="Typo.caption" Color="Color.Secondary">@Job.Phase</MudText>
            }
        }

        @if (Job.Status == JobStatus.Failed && Job.ErrorMessage is not null)
        {
            <MudAlert Severity="Severity.Error" Dense="true" Class="mt-2">@Job.ErrorMessage</MudAlert>
        }

        @if (Job.ResultData is not null && Job.Status == JobStatus.Completed)
        {
            <MudText Typo="Typo.caption" Color="Color.Success" Class="mt-1">@Job.ResultData</MudText>
        }

        <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-1">
            @Job.CorrelationId &bull; Created @Job.CreatedAt.ToString("g")
        </MudText>
    </MudCardContent>
</MudCard>

@code {
    [Parameter, EditorRequired] public required JobRecordDto Job { get; set; }

    private Color StatusColor => Job.Status switch
    {
        JobStatus.Pending => Color.Default,
        JobStatus.Running => Color.Primary,
        JobStatus.Completed => Color.Success,
        JobStatus.Failed => Color.Error,
        JobStatus.Cancelled => Color.Warning,
        _ => Color.Default
    };
}
```

- [ ] **Step 3: Create Jobs page**

Create `src/StableDiffusionStudio.Web/Components/Pages/Jobs.razor`:

```razor
@page "/jobs"
@using StableDiffusionStudio.Application.DTOs
@using StableDiffusionStudio.Application.Interfaces
@using StableDiffusionStudio.Domain.Enums
@inject IJobQueue JobQueue
@implements IDisposable

<MudText Typo="Typo.h4" Class="mb-4">Jobs</MudText>

<MudToggleGroup T="string" Value="_filter" ValueChanged="OnFilterChanged" Class="mb-4">
    <MudToggleItem Value="@("all")">All</MudToggleItem>
    <MudToggleItem Value="@("active")">Active</MudToggleItem>
</MudToggleGroup>

@if (_jobs is null)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (_jobs.Count == 0)
{
    <EmptyState Title="No jobs yet"
                Subtitle="Jobs appear here when you scan for models or run other background tasks"
                Icon="@Icons.Material.Filled.WorkHistory" />
}
else
{
    @foreach (var job in _jobs)
    {
        <JobProgressCard Job="@ToDto(job)" />
    }
}

@code {
    private IReadOnlyList<Domain.Entities.JobRecord>? _jobs;
    private string _filter = "all";
    private Timer? _refreshTimer;

    protected override async Task OnInitializedAsync()
    {
        await LoadJobs();
        _refreshTimer = new Timer(async _ =>
        {
            await LoadJobs();
            await InvokeAsync(StateHasChanged);
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private async Task LoadJobs()
    {
        _jobs = await JobQueue.ListAsync(activeOnly: _filter == "active");
    }

    private async Task OnFilterChanged(string filter)
    {
        _filter = filter;
        await LoadJobs();
    }

    private static JobRecordDto ToDto(Domain.Entities.JobRecord j) =>
        new(j.Id, j.Type, j.Status, j.Progress, j.Phase, j.CorrelationId,
            j.CreatedAt, j.StartedAt, j.CompletedAt, j.ErrorMessage, j.ResultData);

    public void Dispose() => _refreshTimer?.Dispose();
}
```

- [ ] **Step 4: Map SignalR hub in Program.cs**

Add to `src/StableDiffusionStudio.Web/Program.cs`:

```csharp
builder.Services.AddSignalR();
// ... after app.MapRazorComponents:
app.MapHub<StableDiffusionStudio.Web.Hubs.StudioHub>("/hubs/studio");
```

- [ ] **Step 5: Write bUnit test for Jobs page**

Create `tests/StableDiffusionStudio.Web.Tests/Components/Pages/JobsPageTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Web.Components.Pages;

namespace StableDiffusionStudio.Web.Tests.Components.Pages;

public class JobsPageTests : TestContext
{
    private readonly IJobQueue _jobQueue;

    public JobsPageTests()
    {
        _jobQueue = Substitute.For<IJobQueue>();
        Services.AddMudServices();
        Services.AddSingleton(_jobQueue);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Jobs_WhenEmpty_ShowsEmptyState()
    {
        _jobQueue.ListAsync(Arg.Any<bool>()).Returns(new List<JobRecord>());

        var cut = RenderComponent<Jobs>();

        cut.Markup.Should().Contain("No jobs yet");
    }
}
```

- [ ] **Step 6: Run all tests**

Run: `dotnet test StableDiffusionStudio.sln -v normal`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/ tests/
git commit -m "feat: add Jobs UI and SignalR hub for real-time updates

Jobs page with active/all filter and auto-refresh polling.
JobProgressCard with status, progress bar, error display.
StudioHub SignalR endpoint for future push notifications.
bUnit tests for empty state."
```

---

### Task 13: Settings — Storage Roots and Appearance

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Entities/Setting.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/SettingConfiguration.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/ISettingsProvider.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Settings/DbSettingsProvider.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Storage/DbStorageRootProvider.cs`
- Create: `src/StableDiffusionStudio.Web/Components/Pages/Settings.razor`
- Modify: `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs` — add Settings DbSet
- Modify: `src/StableDiffusionStudio.Web/Program.cs` — replace InMemoryStorageRootProvider
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Settings/DbSettingsProviderTests.cs`

- [ ] **Step 1: Implement Setting entity and EF config**

Create `src/StableDiffusionStudio.Domain/Entities/Setting.cs`:
```csharp
namespace StableDiffusionStudio.Domain.Entities;

public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
```

Create `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/SettingConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(200);
        builder.Property(s => s.Value).HasMaxLength(10000);
    }
}
```

Add to AppDbContext: `public DbSet<Setting> Settings => Set<Setting>();`

- [ ] **Step 2: Define ISettingsProvider and implement DbSettingsProvider**

Create `src/StableDiffusionStudio.Application/Interfaces/ISettingsProvider.cs`:
```csharp
namespace StableDiffusionStudio.Application.Interfaces;

public interface ISettingsProvider
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
}
```

Create `src/StableDiffusionStudio.Infrastructure/Settings/DbSettingsProvider.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Settings;

public class DbSettingsProvider : ISettingsProvider
{
    private readonly AppDbContext _context;

    public DbSettingsProvider(AppDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var setting = await _context.Settings.FindAsync([key], ct);
        if (setting is null) return default;
        return JsonSerializer.Deserialize<T>(setting.Value);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        var setting = await _context.Settings.FindAsync([key], ct);

        if (setting is null)
        {
            _context.Settings.Add(new Setting
            {
                Key = key,
                Value = json,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            setting.Value = json;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Implement DbStorageRootProvider (replaces InMemory)**

Create `src/StableDiffusionStudio.Infrastructure/Storage/DbStorageRootProvider.cs`:

```csharp
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Storage;

public class DbStorageRootProvider : IStorageRootProvider
{
    private readonly ISettingsProvider _settings;
    private const string StorageRootsKey = "storage-roots";

    public DbStorageRootProvider(ISettingsProvider settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<StorageRoot>> GetRootsAsync(CancellationToken ct = default)
    {
        var entries = await _settings.GetAsync<List<StorageRootEntry>>(StorageRootsKey, ct);
        return entries?.Select(e => new StorageRoot(e.Path, e.DisplayName)).ToList()
               ?? new List<StorageRoot>();
    }

    public async Task AddRootAsync(StorageRoot root, CancellationToken ct = default)
    {
        var entries = await _settings.GetAsync<List<StorageRootEntry>>(StorageRootsKey, ct) ?? new();
        if (!entries.Any(e => e.Path == root.Path))
        {
            entries.Add(new StorageRootEntry(root.Path, root.DisplayName));
            await _settings.SetAsync(StorageRootsKey, entries, ct);
        }
    }

    public async Task RemoveRootAsync(string path, CancellationToken ct = default)
    {
        var entries = await _settings.GetAsync<List<StorageRootEntry>>(StorageRootsKey, ct) ?? new();
        entries.RemoveAll(e => e.Path == path);
        await _settings.SetAsync(StorageRootsKey, entries, ct);
    }

    private record StorageRootEntry(string Path, string DisplayName);
}
```

- [ ] **Step 4: Write tests for DbSettingsProvider**

Create `tests/StableDiffusionStudio.Infrastructure.Tests/Settings/DbSettingsProviderTests.cs`:

```csharp
using FluentAssertions;
using StableDiffusionStudio.Infrastructure.Settings;
using StableDiffusionStudio.Infrastructure.Tests.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Settings;

public class DbSettingsProviderTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly DbSettingsProvider _provider;

    public DbSettingsProviderTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _provider = new DbSettingsProvider(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SetAndGet_RoundTripsValue()
    {
        await _provider.SetAsync("test-key", "hello world");
        var result = await _provider.GetAsync<string>("test-key");
        result.Should().Be("hello world");
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsDefault()
    {
        var result = await _provider.GetAsync<string>("missing");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ExistingKey_UpdatesValue()
    {
        await _provider.SetAsync("key", "v1");
        await _provider.SetAsync("key", "v2");
        var result = await _provider.GetAsync<string>("key");
        result.Should().Be("v2");
    }

    [Fact]
    public async Task SetAndGet_ComplexObject_RoundTrips()
    {
        var data = new List<string> { "alpha", "beta" };
        await _provider.SetAsync("list", data);
        var result = await _provider.GetAsync<List<string>>("list");
        result.Should().BeEquivalentTo(data);
    }
}
```

- [ ] **Step 5: Create Settings page**

Create `src/StableDiffusionStudio.Web/Components/Pages/Settings.razor`:

```razor
@page "/settings"
@using StableDiffusionStudio.Application.Interfaces
@using StableDiffusionStudio.Domain.ValueObjects
@inject IStorageRootProvider StorageRootProvider
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudText Typo="Typo.h4" Class="mb-4">Settings</MudText>

<MudExpansionPanels MultiExpansion="true">
    <MudExpansionPanel Text="Storage Directories" IsInitiallyExpanded="true">
        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-3">
            Directories where the app looks for model files.
        </MudText>

        @if (_roots is not null && _roots.Count > 0)
        {
            <MudList T="StorageRoot" Dense="true">
                @foreach (var root in _roots)
                {
                    <MudListItem T="StorageRoot">
                        <div class="d-flex align-center justify-space-between" style="width:100%">
                            <div>
                                <MudText Typo="Typo.body1">@root.DisplayName</MudText>
                                <MudText Typo="Typo.caption" Color="Color.Secondary">@root.Path</MudText>
                            </div>
                            <MudIconButton Icon="@Icons.Material.Filled.Delete" Color="Color.Error"
                                           Size="Size.Small" OnClick="() => RemoveRoot(root.Path)" />
                        </div>
                    </MudListItem>
                }
            </MudList>
        }
        else
        {
            <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-2">No directories configured.</MudText>
        }

        <MudButton Variant="Variant.Outlined" Color="Color.Primary" Class="mt-2"
                   StartIcon="@Icons.Material.Filled.CreateNewFolder" OnClick="AddRoot">
            Add Directory
        </MudButton>
    </MudExpansionPanel>

    <MudExpansionPanel Text="Appearance">
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            Theme settings will be available in a future update. The app uses a dark theme by default.
        </MudText>
    </MudExpansionPanel>

    <MudExpansionPanel Text="Diagnostics">
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            View telemetry and traces in the Aspire Dashboard.
        </MudText>
    </MudExpansionPanel>
</MudExpansionPanels>

@code {
    private IReadOnlyList<StorageRoot>? _roots;

    protected override async Task OnInitializedAsync() => await LoadRoots();

    private async Task LoadRoots()
    {
        _roots = await StorageRootProvider.GetRootsAsync();
    }

    private async Task AddRoot()
    {
        var dialog = await DialogService.ShowAsync<Dialogs.AddStorageRootDialog>("Add Directory");
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            await LoadRoots();
            Snackbar.Add("Directory added", MudBlazor.Severity.Success);
        }
    }

    private async Task RemoveRoot(string path)
    {
        var confirmed = await DialogService.ShowMessageBox(
            "Remove Directory", "Stop scanning this directory for models?",
            yesText: "Remove", cancelText: "Cancel");
        if (confirmed == true)
        {
            await StorageRootProvider.RemoveRootAsync(path);
            await LoadRoots();
            Snackbar.Add("Directory removed", MudBlazor.Severity.Info);
        }
    }
}
```

- [ ] **Step 6: Update Program.cs — replace InMemory with Db providers**

Replace the InMemoryStorageRootProvider registration with:

```csharp
// Settings and storage
builder.Services.AddScoped<ISettingsProvider, DbSettingsProvider>();
builder.Services.AddScoped<IStorageRootProvider, DbStorageRootProvider>();
```

Remove the `InMemoryStorageRootProvider` registration.

- [ ] **Step 7: Add EF migration**

```bash
cd src/StableDiffusionStudio.Infrastructure
dotnet ef migrations add AddSettings --startup-project ../StableDiffusionStudio.Web
cd ../..
```

- [ ] **Step 8: Run all tests**

Run: `dotnet test StableDiffusionStudio.sln -v normal`
Expected: All tests pass.

- [ ] **Step 9: Commit**

```bash
git add src/ tests/
git commit -m "feat: add Settings with persistent storage roots and SQLite settings

DbSettingsProvider stores typed settings as JSON in SQLite. DbStorageRootProvider
replaces InMemoryStorageRootProvider for persistent storage directory config.
Settings page with storage root management. EF migration for Setting table."
```

---

### Task 14: Polish — Home Dashboard, Empty States, Aspire Telemetry

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Components/Pages/Home.razor` — wire real data
- Modify: `src/StableDiffusionStudio.Web/Program.cs` — add custom metrics
- Create: `src/StableDiffusionStudio.Infrastructure/Telemetry/StudioMetrics.cs`

- [ ] **Step 1: Wire Home dashboard to real data**

Update `src/StableDiffusionStudio.Web/Components/Pages/Home.razor`:

```razor
@page "/"
@using StableDiffusionStudio.Application.DTOs
@using StableDiffusionStudio.Application.Services
@using StableDiffusionStudio.Application.Interfaces
@using StableDiffusionStudio.Domain.Enums
@inject ProjectService ProjectService
@inject ModelCatalogService CatalogService
@inject IJobQueue JobQueue
@inject NavigationManager Nav
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudText Typo="Typo.h4" Class="mb-4">Dashboard</MudText>

<MudGrid>
    <MudItem xs="12" md="4">
        <MudPaper Class="pa-4" Elevation="1">
            <MudText Typo="Typo.h6" Class="mb-3">Quick Start</MudText>
            <MudButton Variant="Variant.Filled" Color="Color.Primary" FullWidth="true"
                       StartIcon="@Icons.Material.Filled.Add" OnClick="CreateProject">
                New Project
            </MudButton>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="4">
        <MudPaper Class="pa-4" Elevation="1">
            <MudText Typo="Typo.h6" Class="mb-1">Models</MudText>
            <MudText Typo="Typo.h3" Color="Color.Primary">@_modelCount</MudText>
            <MudText Typo="Typo.caption" Color="Color.Secondary">discovered models</MudText>
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="4">
        <MudPaper Class="pa-4" Elevation="1">
            <MudText Typo="Typo.h6" Class="mb-1">Active Jobs</MudText>
            <MudText Typo="Typo.h3" Color="Color.Primary">@_activeJobCount</MudText>
            <MudText Typo="Typo.caption" Color="Color.Secondary">running tasks</MudText>
        </MudPaper>
    </MudItem>

    <MudItem xs="12">
        <MudPaper Class="pa-4" Elevation="1">
            <MudText Typo="Typo.h6" Class="mb-3">Recent Projects</MudText>
            @if (_recentProjects is not null && _recentProjects.Count > 0)
            {
                <MudGrid>
                    @foreach (var project in _recentProjects)
                    {
                        <MudItem xs="12" sm="6" md="4">
                            <ProjectCard Project="project"
                                         OnClick="p => Nav.NavigateTo($\"/projects/{p.Id}\")" />
                        </MudItem>
                    }
                </MudGrid>
            }
            else
            {
                <EmptyState Title="No projects yet"
                            Subtitle="Create your first project to get started"
                            Icon="@Icons.Material.Filled.Folder"
                            ActionText="New Project"
                            OnAction="CreateProject" />
            }
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    private IReadOnlyList<ProjectDto>? _recentProjects;
    private int _modelCount;
    private int _activeJobCount;

    protected override async Task OnInitializedAsync()
    {
        _recentProjects = await ProjectService.ListAsync(new ProjectFilter(Take: 6));
        var models = await CatalogService.ListAsync(new ModelFilter(Take: 1000));
        _modelCount = models.Count;
        var jobs = await JobQueue.ListAsync(activeOnly: true);
        _activeJobCount = jobs.Count;
    }

    private async Task CreateProject()
    {
        var dialog = await DialogService.ShowAsync<Dialogs.CreateProjectDialog>("New Project");
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            _recentProjects = await ProjectService.ListAsync(new ProjectFilter(Take: 6));
            Snackbar.Add("Project created", MudBlazor.Severity.Success);
        }
    }
}
```

- [ ] **Step 2: Add custom OpenTelemetry metrics**

Create `src/StableDiffusionStudio.Infrastructure/Telemetry/StudioMetrics.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace StableDiffusionStudio.Infrastructure.Telemetry;

public class StudioMetrics
{
    private readonly Counter<long> _projectsCreated;
    private readonly Counter<long> _modelsScanned;
    private readonly Counter<long> _jobsCompleted;
    private readonly Counter<long> _jobsFailed;

    public StudioMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("StableDiffusionStudio");
        _projectsCreated = meter.CreateCounter<long>("studio.projects.created");
        _modelsScanned = meter.CreateCounter<long>("studio.models.scanned");
        _jobsCompleted = meter.CreateCounter<long>("studio.jobs.completed");
        _jobsFailed = meter.CreateCounter<long>("studio.jobs.failed");
    }

    public void ProjectCreated() => _projectsCreated.Add(1);
    public void ModelsScanned(long count) => _modelsScanned.Add(count);
    public void JobCompleted() => _jobsCompleted.Add(1);
    public void JobFailed() => _jobsFailed.Add(1);
}
```

Register in Program.cs:
```csharp
builder.Services.AddSingleton<StudioMetrics>();
```

- [ ] **Step 3: Run full test suite**

Run: `dotnet test StableDiffusionStudio.sln -v normal`
Expected: All tests pass.

- [ ] **Step 4: Verify full app end-to-end**

Run: `dotnet run --project src/StableDiffusionStudio.AppHost`

Manual verification checklist:
1. Dashboard loads with stats
2. Create a project from dashboard
3. Navigate to Projects, see the project, open it, rename it
4. Navigate to Settings, add a model directory
5. Navigate to Models, click Scan Now
6. Jobs page shows the scan job
7. Models page shows discovered models (if real models exist in directory)
8. Aspire dashboard shows traces and logs
9. All navigation works correctly

Stop app.

- [ ] **Step 5: Add .gitignore for app data**

Create or update `.gitignore`:
```
*.db
*.db-journal
bin/
obj/
.vs/
.superpowers/
AppData/
```

- [ ] **Step 6: Commit**

```bash
git add src/ tests/ .gitignore
git commit -m "feat: polish dashboard, add telemetry metrics, wire end-to-end

Home dashboard shows real project/model/job counts. StudioMetrics
provides OpenTelemetry counters for projects, models, and jobs.
Full end-to-end flow verified: create project → add directory →
scan models → view in catalog."
```

- [ ] **Step 7: Run final full test suite**

Run: `dotnet test StableDiffusionStudio.sln -v normal`
Expected: All tests pass. This is the final verification.

---

## Summary

| Task | Description | Key Outputs |
|------|-------------|-------------|
| 1 | Solution scaffold | 6 src + 4 test projects, Aspire wiring |
| 2 | Domain: Project entity | Project, enums, value objects, 10+ tests |
| 3 | Persistence | AppDbContext, ProjectRepository, EF migration |
| 4 | Project service | ProjectService, FluentValidation, 11+ tests |
| 5 | App shell UI | MudBlazor layout, dark theme, nav, home page |
| 6 | Project CRUD UI | Projects/ProjectDetail pages, dialogs |
| 7 | Domain: ModelRecord | ModelRecord, ModelFileAnalyzer, 12+ tests |
| 8 | Model scanning | LocalFolderAdapter, ModelCatalogRepository |
| 9 | Model catalog service | ModelCatalogService, scan orchestration |
| 10 | Models UI | Model browser, cards, filters, storage root config |
| 11 | Background jobs | JobRecord, ChannelJobQueue, BackgroundJobProcessor |
| 12 | Jobs UI | Jobs page, SignalR hub, progress cards |
| 13 | Settings | DbSettingsProvider, DbStorageRootProvider, Settings page |
| 14 | Polish | Dashboard wiring, telemetry metrics, final verification |
