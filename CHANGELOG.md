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