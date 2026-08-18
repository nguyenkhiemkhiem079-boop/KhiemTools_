# KhimTools - Developer Guide

Welcome to the **KhimTools** project for Revit. This project is configured for multi-targeting Revit version support from **Revit 2022 to Revit 2027**.

## Project Structure & Multi-Targeting
This Add-in is multi-targeted using MSBuild in `KhimTools.csproj`. It builds for:
- `.NET 4.8` (`net48`): For Revit versions 2022, 2023, and 2024 (.NET Framework).
- `.NET 8.0-windows` (`net8.0-windows`): For Revit versions 2025, 2026, and 2027 (.NET 8).

`KhimTools.csproj` conditionally selects Revit API assembly references based on the target framework. It uses a Post-Build event to compile and package DLLs and `.addin` manifests into standard Autodesk `.bundle` autoloader format located at `%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle`.

### Directory Layout
```
KhimTools/
├── Core/
│   ├── App.cs                  (Entry point: IExternalApplication implementation)
│   ├── ActionEventHandler.cs   (Safe thread handler for modeless/async Revit API calls)
│   └── RibbonBuilder.cs        (Central ribbon menu builder)
├── Tools/
│   ├── SlabJoin/               (Slab Join & Unjoin tool module)
│   │   ├── Commands/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   ├── Services/
│   │   └── Utilities/
│   └── RebarTool/              (Rebar generation & drawing tool module)
│       ├── Commands/
│       ├── Core/
│       ├── Forms/
│       ├── Models/
│       └── RebarShapes/
├── Resources/                  (Icons & PNG resources)
├── Deploy/                     (PackageContents.xml & KhimTools.addin files)
├── KhimTools.csproj
└── DEV_GUIDE.md
```

## ActionEventHandler (CRITICAL)
Due to the Revit API requiring all API calls to happen on the main Revit Thread, and modern UI (WPF) often firing commands from alternate UI threads or asynchronous Contexts, you **MUST** use the provided `ActionEventHandler`.

### Location
File: `[Core/ActionEventHandler.cs](file:///c:/Users/khiem.nguyen/Documents/KhimTools_v2/KhimTools/Core/ActionEventHandler.cs)`

### How to Use
The `App` initializes a static property: `KhimTools.Core.App.EventHandler`.
When you need to execute code that interacts with the Revit API (such as `Transaction`, `Create`, `Delete`, `Set()`) from a WPF Window, ViewModel, or asynchronous task, wrap it like so:

```csharp
KhimTools.Core.App.EventHandler.Raise(app =>
{
    var uiapp = app;
    var doc = uiapp.ActiveUIDocument.Document;

    using (Transaction t = new Transaction(doc, "My Action"))
    {
        t.Start();
        // Modify Revit Document Here
        t.Commit();
    }
});
```
This guarantees execution is synchronized with Revit's main thread and avoids API context exceptions.

## UI Design
- Use **WPF** for UI logic.
- Prefer **MVVM** pattern using the `CommunityToolkit.Mvvm` library.
- Always wrap modifications to observable properties from Revit threads in `Application.Current.Dispatcher.Invoke(() => { ... })` if the UI needs to reflect them.
- Avoid code-behind for business logic.

## Creating New Tools
1. Create a subfolder inside `Tools/<YourToolName>/` for your new tool module.
2. Create your WPF `Window.xaml`, `ViewModel`, and `Command.cs`.
3. The `Command.cs` should implement `IExternalCommand` to act as the entry point in Revit.
4. Register the tool in `Core/RibbonBuilder.cs` by adding buttons to the "Khim Tools" ribbon tab.
5. If your tool has icons, place `.png` files in `Resources/` directory.
