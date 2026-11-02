# Build Notes — v0.7.7 UV Island Diagnostics

## Scope

This patch extends the existing Editor-only UV validator. It adds connected UV island grouping, per-island UV bounds summaries, and conservative classification of fully stacked, fully mirrored, and partial island overlaps.

## Implementation notes

- UV island connectivity is based on shared mesh-local geometric edges whose canonical UV endpoints match within the validator UV tolerance.
- Degenerate UV triangles remain reported and are excluded from island grouping.
- Island-pair overlap uses triangle intersection area, not only intersecting bounding boxes.
- Fully stacked/mirrored classification requires at least 98.5% coverage of both islands; at least 75% of overlap area must have matching/opposite UV triangle orientation, respectively.
- Partial overlaps are reported separately.
- Island-pair bounds checks, island triangle-pair overlap checks, and existing triangle-overlap checks are each bounded at five million candidate comparisons.
- Existing triangle-level overlap and geometric seam diagnostics remain available.
- No mesh, UV, profile, material, or mask data is modified.

## Validation status

Source changes and package structure have been checked outside Unity. The patch has not been compiled or executed in the Unity Editor in this environment. Follow `Manual_Test_Plan_v0.7.7.md` in the Unity project before treating the diagnostic as verified.
