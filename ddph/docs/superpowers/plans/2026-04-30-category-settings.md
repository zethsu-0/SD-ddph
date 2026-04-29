# Category Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add category add, edit, and delete controls to Settings, backed by Firebase.

**Architecture:** Add `CategoryRepository` for category persistence and product rename updates. Keep Settings UI in existing code-behind because the tab is already built there.

**Tech Stack:** C#, WPF, Firebase Realtime Database REST client, console test runner.

---

### Task 1: Repository Tests

**Files:**
- Modify: `ddph.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests that call `CategoryRepository.AddCategoryAsync`, `RenameCategoryAsync`, and `DeleteCategoryAsync` using a fake Firebase client.

- [ ] **Step 2: Verify RED**

Run: `dotnet run --project ddph.Tests\ddph.Tests.csproj`

Expected: compile fails because `CategoryRepository` does not exist.

### Task 2: Category Repository

**Files:**
- Create: `ddph/data/CategoryRepository.cs`
- Modify: `ddph.Tests/Program.cs`

- [ ] **Step 1: Implement minimal repository**

Create `CategoryRepository` with:

- `GetCategoriesAsync()`
- `AddCategoryAsync(string name)`
- `RenameCategoryAsync(string oldName, string newName)`
- `DeleteCategoryAsync(string name)`

- [ ] **Step 2: Verify GREEN**

Run: `dotnet build ddph\ddph.csproj "-p:BaseIntermediateOutputPath=..\obj-codex\" "-p:OutputPath=..\build-codex\"`

Run: `dotnet run --project ddph.Tests\ddph.Tests.csproj`

Expected: tests pass.

### Task 3: Settings UI

**Files:**
- Modify: `ddph/MainWindow.xaml.cs`

- [ ] **Step 1: Add category card**

Add category input, save button, list, edit button, remove button, and status text to `CreateSettingsContent`.

- [ ] **Step 2: Wire repository**

Use `CategoryRepository` from the Settings tab.
Reload categories after add, rename, and delete.
Refresh register products after category changes.

- [ ] **Step 3: Verify build**

Run: `dotnet build ddph\ddph.csproj "-p:BaseIntermediateOutputPath=..\obj-codex\" "-p:OutputPath=..\build-codex\"`

Expected: build succeeds.

### Task 4: Final Verification

**Files:**
- Verify only.

- [ ] **Step 1: Run tests**

Run: `dotnet run --project ddph.Tests\ddph.Tests.csproj`

Expected: no exceptions.

- [ ] **Step 2: Check status**

Run: `git status --short`

Expected: only intended files changed.
