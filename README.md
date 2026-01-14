# Boids

A GPU-accelerated boids flocking simulation package for Unity, using compute shaders for high-performance rendering of thousands of entities.

## Features

- GPU-based flocking simulation via compute shaders
- Instanced rendering for efficient draw calls
- Classic boid behaviors: separation, alignment, cohesion
- Force providers for attraction/repulsion points
- Obstacle avoidance
- Procedural noise-based turbulence and current zones
- Customizable shader with:
  - Normal mapping
  - Color variation per instance
  - Tail wave animation
  - Speed-based emission

## Requirements

- Unity 6000.3 or later
- Universal Render Pipeline (URP)

## Installation

Add this package to your Unity project via the Package Manager using the git URL (https://github.com/Team-Crescendo-Games/Boids.git).

## Quick Start

1. Create an empty GameObject and add the `BoidManager` component
2. Assign the `BoidsCompute` compute shader
3. Assign a material using the `Custom/BoidsInstanced` shader
4. Assign a mesh for your boids (e.g., the included fish mesh)
5. Configure boid count, spawn radius, and behavior parameters
6. Press Play

## Components

### BoidManager

The main controller that spawns and simulates boids. Key settings:

| Parameter | Description |
|-----------|-------------|
| Boid Count | Number of boids to simulate |
| Spawn Radius | Initial spawn area radius |
| Move Speed | Base movement speed |
| Cell Radius | Neighbor detection range |
| Separation Weight | How strongly boids avoid crowding |
| Alignment Weight | How strongly boids match neighbor direction |
| Target Weight | Global multiplier for force providers |
| Obstacle Aversion Distance | How early boids react to obstacles |

### BoidForceProvider

Attach to any GameObject to create attraction or repulsion points.

- Positive weight: attracts boids
- Negative weight: repels boids

### BoidObstacle

Defines spherical obstacles that boids will steer around.

## Sample Assets

The package includes sample assets in `SampleAssets/`:
- Fish mesh and textures
- Example material
- Sample scene demonstrating the system

## License

See LICENSE file for details.
