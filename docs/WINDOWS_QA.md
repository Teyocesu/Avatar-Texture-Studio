# Windows 11 acceptance checklist

Status: **NOT TESTED**. Record editor version, Windows build, GPU/driver, API, color space, package commit and observations with screenshots/logs. Do not mark a gate passed from source inspection or from this checklist's existence.

## Prepare a separate project

- Use licensed Unity **2022.3.22f1**, **Built-in**, **Linear**, **Direct3D 11**. Close other instances using the test project.
- Open `Tests/UnityProject` from a fresh checkout; allow Unity to generate its remaining default project settings. Confirm the local package resolves, Console has no compile/shader errors, and no original VRC Texture Painter is installed.
- Also install the local tarball in a separate empty 3D Built-in project to verify distribution. Open Tools > Avatar Texture Studio. No runtime scripts/components should appear on scene meshes.

## Execute the GPU suites

Run from the repository root in PowerShell, with the editor closed. Do **not** add `-nographics`: these tests need a real graphics device. The suites create/delete synthetic assets, so never point them at an avatar project.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe'
$project = (Resolve-Path '.\Tests\UnityProject').Path
New-Item -ItemType Directory -Force '.\artifacts' | Out-Null
$methods = @(
  'AvatarTextureStudio.Painting.Tests.MTPTests.Run',
  'AvatarTextureStudio.Tests.StudioTests.Run'
)
foreach ($method in $methods) {
  $log = Join-Path (Resolve-Path '.\artifacts').Path ($method + '.log')
  $arguments = "-batchmode -force-d3d11 -projectPath `"$project`" -executeMethod $method -logFile `"$log`""
  $process = Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru
  if ($process.ExitCode -ne 0) { throw "Suite failed: $method. Read $log" }
  Select-String -Path $log -Pattern 'MTPTEST|ATSTEST|ATSCREATIVE|error CS|Shader error'
}
```

Require the final `MTPTEST DONE failures=0` and `ATSTEST DONE exit=0`, no compile/shader errors, and logs showing Linear / Direct3D11. Do not substitute an exit code alone for the completion markers. The upstream suite covers existing painting/mask/layer/project/export behavior. Studio checks reject malformed files, require explicit source overwrite, preserve assets on default export, retain a session across Compare, accept non-readable comparison assets, and check difference pixels including alpha.

StudioTests also calls CreativeIntegrationTests: actual Unity source UV7/submesh association and pose refresh; capture through native GPU readback; source image/importer preservation; Library/SessionState recovery and failed-open retention; and owned TextureLab snapshots with the default asset-only guard preserved. Require its `ATSCREATIVE PASS` lines as well. These checks are implemented but **NOT TESTED** until this command really runs.

Optional development check: `dotnet run --project Tests/FileSafety/FileSafety.csproj -- <absolute-repository-path>` requires .NET 8 and link-creation permission. It tests filesystem failure/escape behavior, CPU UV/region/color/history/sidecars and C# syntax, independently of Unity. It does not replace either GPU suite.

## Independent review F1 and replacement recovery

- Record native per-format Render/Sample/Linear/Blend/ReadPixels capability results against the matrix in VERIFICATION.md. Confirm ARGB32 and RGHalf blending remain required. RFloat/R8/ARGBHalf/RHalf blending is not a painting requirement. Where a device reports one of those unused blend capabilities false but all required operations true, confirm painting can start; no such hardware case has been executed yet.
- Verify occlusion, ordinary/max stroke accumulation, gradient, padding, blur and Blend/Flatten (including generated capture mips), plus sRGB/data previews and undo/copying. Capability reporting alone does not pass these flows.
- In the isolated project, exercise Unity Mono existing-file replacement on Windows: successful overwrite, a locked/read-only destination, and explicit cancellation. Check original/recovery bytes and visible errors. Retain any recovery paths named by a replacement error; copy a retained project to a new `.mtpaint` path or image to a new `.png` path to inspect it. The .NET test injects documented Win32 partial-failure states; it does not execute actual native error 1176/1177 or prove crash/power-loss durability.

## Manual interaction and asset safety

- Select a plain mesh and a synthetic skinned mesh; then a root with multiple meshes. Confirm material/texture/UV selection, invalid/missing targets and unsupported mappings are clear. Include mirrored/overlapping UVs, a seam, non-square textures, multiple material slots and multiple color textures/UV channels.
- Exercise hard/soft/blur/blend/eraser/gradient/custom/stamp, mirror, pressure if hardware exists, occlusion/falloff, posed mesh rebaking, padding and alpha. Confirm orientation and color appearance on asymmetric images.
- Exercise add/visibility/opacity/all blend modes/reorder/rename/duplicate/merge/fill/clear/import, then undo/redo through mixed strokes and layer edits. Compare restored pixels and layer order.
- Test Masks on Standard's Detail Mask, then owner-installed Poiyomi/lilToon in a **disposable** shader test project. Paint independent channels, blur/eraser/gradient/stamp, Fill/Clear/Invert, show-on-model and undo. Preserve alpha and untouched channels. Do not bundle those shaders/avatar assets in this repository.
- Save and reopen both project kinds using PNG and autosave encodings; also open genuine upstream version 1/2 fixtures if available. Verify source linkage, layer properties, color/data flags and pixels. Test a corrupt/truncated file without losing an existing session.
- While dirty, switch Painting → Masks → Compare → Painting. Verify both sessions/history remain; Scene painting and temporary preview stop in Compare. Check repeated window open/close for duplicate strokes/events or lingering preview. A current stroke must be committed before autosave.
- Trigger script recompile and Edit/Play/Edit with domain reload on and off. Verify both sessions recover, including when Compare was selected. Save/Discard/Cancel on close must behave as labelled; cancelling the Save dialog must keep the window. Simulate an unwritable save location and inspect the error and retained old file. Manually reopen a Library recovery file after editor restart.
- Hash original texture and `.meta`/material files before painting. Confirm painting/preview/Compare/default export leave them unchanged; stop/close restores any existing property block. A new PNG must retain expected dimensions, alpha, orientation, wrap/mip/import settings. Existing-path replacement must ask; cancel leaves bytes unchanged. Explicit assignment must ask and support Undo; check shared and embedded material behavior.

## Compare visuals and negative cases

- Use asymmetric A/B textures with RGB-only, alpha-only and transparent-pixel differences. Verify side-by-side, A/B, overlay endpoints/intermediate opacity, Difference, gain and Alpha only against known pixels. Confirm a checker does not hide alpha differences.
- Compare sRGB color textures and linear/data masks with Raw data values on/off. Inspect both Gamma (secondary) and Linear projects; verify display conversion using known ramps. Do not treat the difference image as a perceptual metric.
- Test shared scroll zoom, drag pan, fit, narrow/wide window resizing, tab changes during drag, and unequal aspect/dimensions. Overlay/difference must explain mismatched sizes; individual views remain usable.
- Test null/deleted/reimported assets, Read/Write off, compressed GPU-supported textures, normal-map imports, HDR/float, virtual-only assets where available, and unsupported graphics formats. No importer toggle or source-file rewrite is allowed.

Acceptance stays blocked until compilation, GPU suites, the critical manual workflows and Windows visual/avatar integration are actually executed. Tablet, shader or unusual-mesh gaps must remain explicitly NOT TESTED. Repeat an appropriate subset on macOS Metal separately; a Windows pass does not establish Metal support.

## Shot 2 creative acceptance — all NOT TESTED

- In the isolated harness, use asymmetric non-square RGBA ramps, a synthetic triangle/quad with UV0/UV7, two material slots, overlapping/mirrored UVs, a collapsed UV triangle and out-of-range tiles. Test MeshRenderer and SkinnedMeshRenderer with non-uniform/mirrored scale, bone poses and blend shapes. Verify source UVs, slot/submesh, material scale/offset, wrapped UV and bottom-left pixel coordinates against known points. Compare source mesh/texture/material/meta hashes before/after.
- In UV Explorer, toggle raw/wrapped layout and triangle edges; pick from Scene and UV. Multiple inverse/tied hits must list all candidates and leave selection empty until explicit choice. UV choice highlights the selected surface even with Scene picking off. Other renderers are not an occlusion gate. Test Repeat/Mirror negative tiles, Clamp/MirrorOnce border warnings, collapsed triangles and >16-tile/fragment-budget rejection. In-place source mesh edits require Refresh source geometry.
- Exercise shared wheel zoom, middle/Alt pan, fit, resizing, switching the three creative workspaces and dragging outside the window. Check no stuck control or duplicate Scene subscriptions. Test unsupported renderer/topology, empty UV channels, missing textures and deleted/reimported sources without losing the creative document.
- Confirm Standard/ordinary Unlit `_MainTex` UV0 mapping with non-identity scale/offset. Small transform changes (for example 0.001 offset) must refresh the overlay. Custom/non-UV/decal or UV-channel disagreements must remain explicitly unverified with triangle masks/model preview blocked. Verify existing temporary preview restores when a mapping becomes unsupported. Test source Read/Write off, normal/data-mask/HDR/virtual-only color rejection and raw geometric inspection.
- Capture a color source and a matching painting composite. Create/rename multiple regions and Face flags; add/subtract chosen triangles; refine with fast and slow add/subtract mask strokes. No gaps between pointer samples. Undo/redo whole brush gestures, masks and adjustments. Ensure isolated unselected RGBA and all alpha bytes, including transparent selected pixels, match exactly. Use saturated ramps and linear-color sources; check Oklab lightness/quantization, overlapping region order and gamut clipping visually and by pixels.
- Update/cancel large previews. Edits remain editable after cancellation; no stale result is exported. Model preview must respect Repeat/Mirror/Clamp/MirrorOnce and source filtering, with correct orientation/sRGB/mips. Mask tint must be separate from exported color. Test 4096/8192 textures and same-size baseline refresh Undo within the documented memory ceiling; record performance and practical memory limits instead of assuming them.
- Export a new PNG, including non-square/NPOT and >2048 dimensions. Verify bytes/dimensions/alpha/color transfer and new importer size/wrap/NPOT/compression flags. Existing PNG/meta paths, outside-Assets/link paths and cancelled dialogs must preserve originals. Export never assigns shared materials; source image/meta/material/mesh hashes stay unchanged. Shader visuals, export import behavior and Windows locks remain native gates.
- Save/reopen standalone `.atsstudio` with masks, all parameters, Face flags, baseline, navigation and binding. Save a related `.mtpaint` and inspect its companion. Verify ordinary MTPAINT1/2 still opens. Cancel replacement of a different existing companion, then induce an unwritable/locked companion destination: painting may already be saved, creative must remain dirty, with a useful error. Invalid magic/version/length/truncation, oversized fields/dimensions and trailing decoded data must not replace current work.
- Enter/exit Play and recompile scripts with domain reload on/off from every tab, including Compare. Painting/Masks and creative projects recover their dirty/saved states, and previews/input stop correctly. Save/Discard/Cancel on close must include creative work. Failed autosave/restore retains recovery paths; after restarting Unity, manually open the saved/Library files. Repeated open/close must not leak textures, property blocks or event handlers.
- In Face Studio, manually create/name presets and reuse shared regions/navigation/sampling. Activate existing painting tools/layers on the matching target; a different open painting target must not be discarded. Region restrictions apply only to Colorway. Non-identity tiling/offset must block raw-UV Face painting with a clear reason. Compare baseline/current painting and baseline/Colorway in TextureLab; return to each workspace without losing state/history. No anatomy detection, mesh edits or runtime/avatar components may appear.

Shot 2 and inherited Shot 1 acceptance remain blocked until these actual native gates pass. Do not start Shot 3 on an assumed pass.
