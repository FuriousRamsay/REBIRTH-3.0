# Approved native wrap compression, 2026-11-04

Scope: the frozen 64 `NativeBodyIds` in the verified backup manifest. Legacy
bundles are backup inputs only. No game launch, artwork regeneration, mesh edit,
shader rebuild, or canonical mask/source modification is part of this workflow.

Originals: `F:/REBIRTH_Wrap_Backups/2026-11-04_PreOptimization`, including
`CurrentBundles`, retained production/authoring sources, original generated PNGs,
and `VERIFIED_MANIFEST.json` with SHA-256 for each of 13,788 files (18.58 GB).
Two additional untouched Undead Chill legacy bundles (81.36 MB) are covered by
`LEGACY_BUNDLE_SUPPLEMENT.json`. The backup contains all 67 current wrap bundles.

## Why the bundles were large

All 832 textures in the approved bundles were RGBA32, including their mip chains.
PNG source compression does not survive Unity's conversion into GPU texture data.
The existing importer/packaging rules permit that RGBA32 fallback for native
non-power-of-two canvases. Explicit BC7 import still fell back to RGBA32 in the
tested Unity 2022.3.62f2 importer, even for non-power-of-two dimensions aligned to
four; direct BC7 textures with aligned dimensions loaded successfully.

## Selected runtime profile

- BC7 RGBA, etcpak high-quality search (`uber=2`, all modes, 64 partitions).
- Top-level dimensions rounded UP to a multiple of four (zero to three pixels
  added per axis), using channel-wise Lanczos resampling across the same UV domain.
  This changes the GPU representation only; exact canonical authoring PNGs and
  masks remain unchanged. Padding the top-level canvas would shift artwork and
  is deliberately not used. No downscaling to a lower resolution tier.
- Preserve every original mip level, resampling its pixel grid only as needed.
- Preserve texture sampling/readability settings and all object IDs, shaders,
  surface-layout TextAssets and non-texture object bytes.
- LZ4HC bundle compression. No PNG-at-runtime decoding or LZMA memory expansion.

`compress_candidate.py` rejects an unapproved name, an unverified backup, a
changed installed source, unexpected texture formats, or an existing candidate.
It reopens the candidate and checks stream ranges/payload hashes/object identity.
`run_batch.py` stages candidates on F: with eight independent encoder processes.
`quality_batch.py` compares all 832 decoded surfaces with their originals.

The `Unity` directory retains the GPU probes/checker. An isolated Unity 2022.3.62f2
project is generated locally (empty Packages manifest, Assets/Editor scripts).
Run its editor with `-batchmode -force-d3d11`; do not use `-nographics` for GPU
checks. `WrapBatchGpuCheck.Run -gpuPlan <json>` accepts
`{"directory":".../BC7HQ2","bundles":["approved-name.unity3d"]}` and emits a
receipt covering texture format, dimensions, mip counts and GPU readback hashes.

Native visual comparison uses the existing `RebirthWrapCollectionReview`, editor
adapters, switch regression helper and current production appearance/catalog
sources copied into that isolated project. It loads the exact installed camper
prefab read-only. The compression sweep skips redundant closeups but retains five
native cameras and the actual rear door open pose; no game process is used.

Navezgane Explorer pilot: 84,143,874 -> 23,094,591 bytes; all 13 BC7 textures passed
actual RTX 3080 GPU load/readback. Original/candidate native views and detail/door
renders were compared. Seven matched native views had image RMSE 1.05..1.40 on
0..255 channels. These are editor checks, not in-game playtesting.

Do not rerun the old importer and assume its output remains compressed. The
post-build texture compression stage and GPU checks are required to produce this
profile. Preserve the original sealed authoring receipts alongside new runtime
compression receipts; neither substitutes for the other.

## Installed result

All 64 approved bundles installed and post-install SHA-256 verified:
4,767,417,962 -> 1,430,060,681 bytes (70.0% smaller; 3.34 GB saved).
Their texture payload totals 10,440,300,868 -> 2,614,118,576 bytes (74.96% smaller).
Individual optimized bundles are 17.14..23.13 MB. Original source PNGs were not
modified. All three legacy bundles remain unchanged.

Validation: 832 decoded-surface comparisons passed (maximum sampled RGB RMSE
3.81/255, alpha RMSE 1.91/255). All 832 textures in all 64 exact candidate hashes
passed actual Unity 2022.3.62f2 / RTX 3080 GPU load/readback. Every wrap rendered in
both native camper and pickup configurations; five native views and rear-door
open views were retained. Quarter contact pages for all 128 configurations and
the original/candidate pilot closeups were inspected. This did not launch the game.

Evidence: `F:/REBIRTH_Wrap_Optimization/2026-11-04/installation.json`,
`gpu-plan.json.receipt.json`, `Candidates/quality.json`, `NativeReview`.
The installer requires those checks and a closed game. Windows transient
memory-mapped-file conflicts required bounded copy retries; known originals and
candidates were hash-checked during recovery. Final installation passed for all
64, with no installation temporary files left behind. No commit or push made.
