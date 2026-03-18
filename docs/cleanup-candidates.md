# Cleanup candidates (runtime verification required)

These items are **not deleted** in this pass. They are marked obsolete/commented until runtime checks confirm safe removal.

## Login flow
- `Views/LoginWindow.axaml` + `.axaml.cs`
- `ViewModels/LoginViewModel.cs`

Verify:
- Startup still intentionally bypasses login window.
- If login should be active, wire it explicitly before deleting these files.

## Legacy import dialog APIs
- `IDialogService`: `ShowImportPreviewAsync`, `ShowKlantImportPreviewAsync`, `ShowAfwerkingImportPreviewAsync`
- `DialogService` corresponding methods
- `Views/ImportPreviewWindow.axaml.cs`
- `Views/KlantImportPreviewWindow.axaml(.cs)`
- `Views/AfwerkingImportPreviewWindow.axaml(.cs)`

Verify:
- Unified import preview flow opens for klanten/lijsten/afwerkingen.
- No runtime path calls legacy windows anymore.

## Legacy import services (status)
Removed:
- `IExcelImportService` / `ExcelImportService`
- `KlantExcelImportService`
- `AfwerkingsOptieExcelImportService`

Follow-up:
- Review whether `EPPlus` is still needed anywhere else now that the legacy import path is gone.

## Runtime smoke checklist
1. App launches.
2. Home navigation works.
3. Offertes list loads.
4. Opening an offerte works.
5. Planning window opens.
6. Creating a WerkTaak still works.
7. Unified import preview opens and commits.
