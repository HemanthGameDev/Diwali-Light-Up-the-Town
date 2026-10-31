# SurfaceWeaver v0.6.4 — short hotfix test plan

Run these tests in this order on D3D11. Use duplicate targets, profiles and new mask pages.

## 1. Plane orientation test
- Assign a Plane with one full 0–1 UV island and a non-convex MeshCollider.
- Select a new Red-channel mask page.
- Enable Seam-Aware Mesh Projection.
- Leave Filter by Surface Normal OFF.
- Paint one small dab near UV (0.2, 0.2), then another near (0.8, 0.8).
- Inspect the live mask preview and saved PNG.
- Pass: patches remain at the corresponding corners; no vertical flip, rotation, or chart-shaped block appears.

## 2. Cylinder with a longitudinal seam
- Use the default cylinder UV unwrap with a vertical seam at the side.
- Enable Seam-Aware, leave normal filtering OFF, and set projection depth to 0.75x radius initially.
- Paint directly over the physical seam.
- Pass: the 3D surface looks continuous and corresponding marks appear at both UV chart boundaries in the mask.

## 3. Smart UV cylinder
- Use Smart UV with multiple non-overlapping charts inside 0–1.
- Paint the side surface near a chart boundary.
- Pass: the physical mark stays on the intended local surface and the corresponding UV chart receives paint. No distant chart should receive a stamp.

## 4. Optional normal filter diagnostic
- Repeat the cylinder stamp with Filter by Surface Normal ON and cutoff 0.0.
- Compare against OFF. If OFF works and ON fails, report that; do not increase projection depth to compensate.

## 5. Persistence
- Save mask PNG, close/reopen Painter, reload mask page.
- Pass: orientation and location are unchanged.

## Report back
Send: (a) Plane mask preview, (b) default cylinder 3D view plus mask PNG preview, (c) Smart UV cylinder view plus mask preview, and any Console shader errors.
