# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-14

### Added

- Initial release
- GPU-accelerated boids simulation using compute shaders
- `BoidManager` component for spawning and controlling boid flocks
- `BoidForceProvider` component for attraction/repulsion points
- `BoidObstacle` component for spherical obstacle avoidance
- `BoidsCompute` compute shader with:
  - Separation, alignment, and cohesion behaviors
  - Multi-target force accumulation
  - Obstacle avoidance
  - 3D simplex noise for turbulence and current zones
- `BoidsInstanced` URP shader with:
  - GPU instancing support
  - Normal mapping
  - Per-instance color variation
  - Procedural tail wave animation
  - Speed-based emission
  - Shadow casting support
- Sample assets including fish mesh, textures, material, and demo scene

## [1.1.0] - 2026-01-14
### Fixed
- Shader not working on Windows

### Added
- Global obstacles
- Ability to scale boids on the shader

## [1.2.0] - 2026-01-15
### Added
- Hard clamp zones
- Spawning shapes
- Tooltips!

## [1.3.0] - 2026-07-23
### Changed
- **Breaking:** Zones are now assigned per `BoidsManager` via the `zones` list instead of applying globally. The static `BoidsZone.ActiveZones` registry has been removed. This fixes broken behavior when multiple zones existed, where every manager clamped its boids through all zones in the scene.

### Added
- Custom `BoidsManager` inspector with buttons to collect `BoidsForceProvider` and `BoidsZone` components from children
- Custom `BoidsZone` inspector showing only the fields relevant to the selected zone type (Thickness for slabs, Radius for spheres)
- `BoidsForceProvider.influenceRange` is now applied in the simulation: boids outside the range ignore the force provider

### Fixed
- Additional lights now respect distance attenuation in `BoidsInstanced` shader (point/spot lights previously lit boids at full intensity regardless of range)
- Ambient (SH) lighting contribution so shadowed boids are no longer pure black
- Light cookies and light layers are now honored in the lighting loops
- Metal shader warning from `pow` with a potentially negative base in the turbulence calculation