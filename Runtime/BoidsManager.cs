using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace TeamCrescendo.Boids
{
    public class BoidsManager : MonoBehaviour
    {
        [SerializeField] private ComputeShader computeShader;
        [Tooltip("The material used to render the boids. Ensure GPU Instancing is enabled.")]
        [SerializeField] private Material boidMaterial;
        [Tooltip("The mesh geometry used for each individual boid.")]
        [SerializeField] private Mesh boidMesh;
        private ComputeShader computeShaderInstance;
        private int kernel;
    
        [Tooltip("List of objects that attracts/repels boids.")]
        [SerializeField] private List<BoidsForceProvider> forceProviders = new();
        
        [Tooltip("List of objects that boids avoid.")]
        [SerializeField] private List<BoidsObstacle> obstacles = new();
        private int TotalObstacleCount => obstacles.Count + BoidsObstacle.GlobalObstacles.Count;

        private GraphicsBuffer boidsBuffer;
        private GraphicsBuffer targetBuffer;
        private GraphicsBuffer obstacleBuffer;
        private GraphicsBuffer velocityBuffer;
        private GraphicsBuffer zoneBuffer;
        private GraphicsBuffer argsBuffer;

        private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
        private Boid[] boidsArray;
        private RenderParams renderParams;
    
        [Header("Settings")]
        [SerializeField, Min(1)] private int boidCount = 1000;
        [Tooltip("Whether boids cast shadows.")] 
        [SerializeField] private bool castShadows = false;

        private enum SpawnShape
        {
            Sphere,
            Box,
            Circle
        }
        
        [Header("Spawn")]
        [Tooltip("The shape of the volume where boids are initially spawned.")]
        [SerializeField] private SpawnShape spawnShape = SpawnShape.Sphere;
        [Tooltip("The radius used when spawning in a Sphere or Circle shape.")]
        [SerializeField] private float spawnRadius = 50f;
        [Tooltip("The dimensions of the box when using Box spawn shape.")]
        [SerializeField] private Vector3 spawnBoxSize = new (50f, 50f, 50f);

        [Header("Flocking Behavior")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float cellRadius = 5f;
        [SerializeField] private float separationWeight = 5f;
        [SerializeField] private float alignmentWeight = 2f;
        [SerializeField] private float targetWeight = 3f;
        [Tooltip("The 'panic' distance. Boids react this far AWAY from the obstacle's radius.")]
        [SerializeField] private float obstacleAversionDistance = 3f;
    
        [Header("Currents & Turbulence")]
        [SerializeField] private float noiseScale = 0.1f;
        [SerializeField] private Vector3 noiseScrollSpeed = new Vector3(0.5f, 0f, 0.5f);
        [SerializeField] private float minZoneSpeed = 0.5f;
        [SerializeField] private float maxZoneSpeed = 2.0f;
        [SerializeField] private float turbulencePower = 3.0f; // Controls "Surge" sharpness
    
        #region Shader Property IDs

        private static readonly int Boids_ID = Shader.PropertyToID("boids");
        private static readonly int BoidBuffer_ID = Shader.PropertyToID("boidBuffer");
        private static readonly int Targets_ID = Shader.PropertyToID("targets");
        private static readonly int Obstacles_ID = Shader.PropertyToID("obstacles");
        private static readonly int VelocityBuffer_ID = Shader.PropertyToID("velocityBuffer");
        private static readonly int Zones_ID = Shader.PropertyToID("zones");
    
        private static readonly int Time_ID = Shader.PropertyToID("time");
        private static readonly int DeltaTime_ID = Shader.PropertyToID("deltaTime");
        private static readonly int NumBoids_ID = Shader.PropertyToID("numBoids");
        private static readonly int NumTargets_ID = Shader.PropertyToID("numTargets");
        private static readonly int NumObstacles_ID = Shader.PropertyToID("numObstacles");
        private static readonly int NumZones_ID = Shader.PropertyToID("numZones");
    
        private static readonly int NoiseScale_ID = Shader.PropertyToID("noiseScale");
        private static readonly int NoiseScroll_ID = Shader.PropertyToID("noiseScroll");
        private static readonly int MinZoneSpeed_ID = Shader.PropertyToID("minZoneSpeed");
        private static readonly int MaxZoneSpeed_ID = Shader.PropertyToID("maxZoneSpeed");
        private static readonly int TurbulencePower_ID = Shader.PropertyToID("turbulencePower");

        private static readonly int MoveSpeed_ID = Shader.PropertyToID("moveSpeed");
        private static readonly int CellRadius_ID = Shader.PropertyToID("cellRadius");
        private static readonly int SeparationWeight_ID = Shader.PropertyToID("separationWeight");
        private static readonly int AlignmentWeight_ID = Shader.PropertyToID("alignmentWeight");
        private static readonly int TargetWeight_ID = Shader.PropertyToID("targetWeight");
        private static readonly int ObstacleAversionDistance_ID = Shader.PropertyToID("obstacleAversionDistance");
    
        #endregion

        private void Start()
        {
            if (computeShader != null)
                computeShaderInstance = Instantiate(computeShader);
            kernel = computeShaderInstance.FindKernel("SimulateBoids");
        
            renderParams = new RenderParams(boidMaterial);
            renderParams.worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            renderParams.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderParams.receiveShadows = true;
            renderParams.matProps = new MaterialPropertyBlock();
        
            InitializeBoids();
            InitializeArgs();
        }

        private void Update()
        {
            if (boidsBuffer == null || !boidsBuffer.IsValid()) return;

            UpdateDynamicBuffers();
            DispatchCompute();
            RenderBoids();
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            
            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (spawnShape)
            {
                case SpawnShape.Sphere:
                {
                    Gizmos.DrawWireSphere(Vector3.zero, spawnRadius);
                    break;
                }
                case SpawnShape.Box:
                {
                    Gizmos.DrawWireCube(Vector3.zero, spawnBoxSize);
                    break;
                }
                case SpawnShape.Circle:
                {
                    int segments = 32;
                    float angleStep = 360f / segments;
                    Vector3 prevPoint = new Vector3(spawnRadius, 0f, 0f);

                    for (int i = 1; i <= segments; i++)
                    {
                        float angle = i * angleStep * Mathf.Deg2Rad;
                        Vector3 nextPoint = new Vector3(
                            Mathf.Cos(angle) * spawnRadius, 
                            0f, 
                            Mathf.Sin(angle) * spawnRadius
                        );
                        
                        Gizmos.DrawLine(prevPoint, nextPoint);
                        prevPoint = nextPoint;
                    }
                    break;
                }
            }

            // Restore the original matrix to avoid affecting other gizmos
            Gizmos.matrix = originalMatrix;
        }

        private void InitializeBoids()
        {
            boidsArray = new Boid[boidCount];
            for (int i = 0; i < boidCount; i++)
            {
                Vector3 randomLocalPos = Vector3.zero;

                switch (spawnShape)
                {
                    case SpawnShape.Sphere:
                        randomLocalPos = Random.insideUnitSphere * spawnRadius;
                        break;
                    
                    case SpawnShape.Box:
                        randomLocalPos = new Vector3(
                            (Random.value - 0.5f) * spawnBoxSize.x,
                            (Random.value - 0.5f) * spawnBoxSize.y,
                            (Random.value - 0.5f) * spawnBoxSize.z
                        );
                        break;
                    
                    case SpawnShape.Circle:
                        Vector2 circle = Random.insideUnitCircle * spawnRadius;
                        randomLocalPos = new Vector3(circle.x, 0f, circle.y);
                        break;
                }

                boidsArray[i] = new Boid
                {
                    position = transform.TransformPoint(randomLocalPos),
                    direction = transform.TransformDirection(Random.onUnitSphere)
                };
            }

            boidsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, Boid.size);
            boidsBuffer.SetData(boidsArray);
        
            velocityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, sizeof(float) * 3);
        }

        private void InitializeArgs()
        {
            commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            commandData[0].indexCountPerInstance = boidMesh.GetIndexCount(0);
            commandData[0].instanceCount = (uint)boidCount;
            commandData[0].startIndex = boidMesh.GetIndexStart(0);
            commandData[0].baseVertexIndex = boidMesh.GetBaseVertex(0);
            commandData[0].startInstance = 0;

            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            argsBuffer.SetData(commandData);
        }

        private void ReallocateBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            int bufferSize = count > 0 ? count : 1;

            // Reallocate buffer if null or size has changed
            if (buffer == null || buffer.count != bufferSize)
            {
                buffer?.Release();
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, stride);
            }
        }

        private void UpdateDynamicBuffers()
        {
            int targetCount = forceProviders.Count > 0 ? forceProviders.Count : 1;
            
            ReallocateBuffer(ref targetBuffer, targetCount, TargetData.size);

            if (forceProviders.Count > 0)
            {
                TargetData[] targetDataArr = new TargetData[forceProviders.Count];
                for (int i = 0; i < forceProviders.Count; i++)
                {
                    if (forceProviders[i] != null)
                    {
                        targetDataArr[i] = new TargetData
                        {
                            position = forceProviders[i].transform.position,
                            weight = forceProviders[i].weight
                        };
                    }
                }
                targetBuffer.SetData(targetDataArr);
            }
            // If targets.Count == 0, we simply leave the size-1 buffer bound 
            // but don't care what data is in it, because we send numTargets = 0.

            // obstacles
            int realObstacleCount = TotalObstacleCount;
            int bufferObstacleCount = realObstacleCount > 0 ? realObstacleCount : 1;

            ReallocateBuffer(ref obstacleBuffer, bufferObstacleCount, ObstacleData.size);

            if (realObstacleCount > 0)
            {
                ObstacleData[] obsDataList = new ObstacleData[realObstacleCount];
                int obstacleIndex = 0;
                
                // Add local obstacles
                foreach (var obs in obstacles)
                {
                    obsDataList[obstacleIndex++] = new ObstacleData
                    {
                        position = obs.transform.position,
                        radius = obs.radius
                    };
                }
                // Add global obstacles
                foreach (var globalObs in BoidsObstacle.GlobalObstacles)
                {
                    obsDataList[obstacleIndex++] = new ObstacleData
                    {
                        position = globalObs.transform.position,
                        radius = globalObs.radius
                    };
                }
                obstacleBuffer.SetData(obsDataList);
            }

            // zones
            int realZoneCount = BoidsZone.ActiveZones.Count;
            int bufferZoneCount = realZoneCount > 0 ? realZoneCount : 1;

            ReallocateBuffer(ref zoneBuffer, bufferZoneCount, ZoneData.size);

            if (realZoneCount > 0)
            {
                var activeZones = BoidsZone.ActiveZones;
                ZoneData[] zoneDataArr = new ZoneData[realZoneCount];
                for(int i=0; i<realZoneCount; i++)
                {
                    var z = activeZones[i];
                    zoneDataArr[i] = new ZoneData
                    {
                        worldToLocal = z.transform.worldToLocalMatrix,
                        localToWorld = z.transform.localToWorldMatrix,
                        dimensions = z.dimensions,
                        type = (int)z.type,
                    };
                }
                zoneBuffer.SetData(zoneDataArr);
            }
        }

        private void DispatchCompute()
        {
            computeShaderInstance.SetBuffer(kernel, Boids_ID, boidsBuffer);
            computeShaderInstance.SetBuffer(kernel, VelocityBuffer_ID, velocityBuffer);    

            if (targetBuffer != null && targetBuffer.IsValid())
                computeShaderInstance.SetBuffer(kernel, Targets_ID, targetBuffer);
            if (obstacleBuffer != null && obstacleBuffer.IsValid())
                computeShaderInstance.SetBuffer(kernel, Obstacles_ID, obstacleBuffer);
            if (zoneBuffer != null && zoneBuffer.IsValid())
                computeShaderInstance.SetBuffer(kernel, Zones_ID, zoneBuffer);

            computeShaderInstance.SetFloat(Time_ID, Time.time);
            computeShaderInstance.SetFloat(DeltaTime_ID, Time.deltaTime);
        
            computeShaderInstance.SetFloat(NoiseScale_ID, noiseScale);
            computeShaderInstance.SetVector(NoiseScroll_ID, noiseScrollSpeed);
            computeShaderInstance.SetFloat(MinZoneSpeed_ID, minZoneSpeed);
            computeShaderInstance.SetFloat(MaxZoneSpeed_ID, maxZoneSpeed);
            computeShaderInstance.SetFloat(TurbulencePower_ID, turbulencePower);
        
            computeShaderInstance.SetInt(NumBoids_ID, boidCount);
            computeShaderInstance.SetInt(NumTargets_ID, forceProviders.Count);
            computeShaderInstance.SetInt(NumObstacles_ID, TotalObstacleCount);
            computeShaderInstance.SetInt(NumZones_ID, BoidsZone.ActiveZones.Count);

            computeShaderInstance.SetFloat(MoveSpeed_ID, moveSpeed);
            computeShaderInstance.SetFloat(CellRadius_ID, cellRadius);
            computeShaderInstance.SetFloat(SeparationWeight_ID, separationWeight);
            computeShaderInstance.SetFloat(AlignmentWeight_ID, alignmentWeight);
            computeShaderInstance.SetFloat(TargetWeight_ID, targetWeight);
            computeShaderInstance.SetFloat(ObstacleAversionDistance_ID, obstacleAversionDistance);

            int threadGroups = Mathf.CeilToInt(boidCount / 256f);
            computeShaderInstance.Dispatch(kernel, threadGroups, 1, 1);
        }

        private void RenderBoids()
        {
            renderParams.matProps.SetBuffer(BoidBuffer_ID, boidsBuffer);
            renderParams.matProps.SetBuffer(VelocityBuffer_ID, velocityBuffer);

            Graphics.RenderMeshIndirect(
                renderParams, 
                boidMesh, 
                argsBuffer, 
                1
            );
        }

        private void OnDestroy()
        {
            boidsBuffer?.Release();
            boidsBuffer = null;
        
            velocityBuffer?.Release();
            velocityBuffer = null;
        
            targetBuffer?.Release();
            targetBuffer = null;
        
            obstacleBuffer?.Release();
            obstacleBuffer = null;
        
            argsBuffer?.Release();
            argsBuffer = null;
            
            zoneBuffer?.Release();
            zoneBuffer = null;
        
            if (computeShaderInstance != null)
            {
                Destroy(computeShaderInstance);
                computeShaderInstance = null;
            }
        }
    }
}