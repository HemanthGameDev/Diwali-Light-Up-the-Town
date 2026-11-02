# Build Notes — v0.7.8 UV Overlap Classification Refinement

## Why this update exists

Unity Editor tests on a subdivided/overlapped plane and a plane mirrored across a center seam showed two classification gaps in v0.7.7:

- One smaller island was completely covered by part of a larger island. The report called the pair a partial overlap without clearly describing the containment.
- Mirrored faces created by a mirror workflow remained in one connected UV island. The island-pair pass could not classify them because it only compares separate islands, even though the triangle-level pass detected positive-area overlaps.

## Changes

- Added a **Contained island overlap** category when one island has at least 98.5% of its area covered but the other island is not also at least 98.5% covered. This is distinct from a full stack/mirror pair and from a general partial overlap.
- Added same-island triangle overlap classification. Opposite UV signed orientation is reported as a **mirrored-overlap candidate**; matching orientation is reported as a **same-orientation overlap**.
- Added triangle-pair details under **Overlaps Within Connected Islands**.
- Retained separate-island full stack/mirror classification based on near-complete mutual area coverage and dominant relative orientation.
- Clamped displayed island coverage to 0–100% to keep diagnostic percentages readable when malformed source islands contain internal overlaps.

## Interpretation caveat

Opposite/same UV signed orientation is a diagnostic clue, not an absolute proof of artist intent. Inconsistent mesh triangle winding, duplicated geometry, non-manifold topology, or unusual overlapping faces can produce similar patterns. Review the listed triangle pairs in the source DCC tool.

## Validation status

Source-level structural checks and offline geometry/math checks have been performed. This patch has **not** been compiled or executed in Unity in this environment. Follow `Manual_Test_Plan_v0.7.8.md` in the Unity Editor.
