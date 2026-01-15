using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace TeamCrescendo.Boids
{
    public class BoidsManager : MonoBehaviour
    {
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Material boidMaterial;
        [SerializeField] private Mesh boidMesh;
        private ComputeShader computeShaderInstance;
        private int kernel;
    
        [SerializeField] private List<BoidsForceProvider> targets = new();
        [SerializeField] private List<BoidsObstacle> obstacles = new();
        private int totalObstacleCount => obstacles.Count + BoidsObstacle.GlobalObstacles.Count;

        private GraphicsBuffer boidBuffer;
        private GraphicsBuffer targetBuffer;
        private GraphicsBuffer obstacleBuffer;
        private GraphicsBuffer velocityBuffer;
        private GraphicsBuffer argsBuffer;

        private struct Boid
        {
            public Vector3 position; 
            public Vector3 direction;
            public const int size = 24;
        }

        private struct ObstacleData
        {
            public Vector3 position; 
            public float radius;
            public const int size = 16;
        }
    
        private struct TargetData
        {
            public Vector3 position;
            public float weight; // Encoded in the .w component of a float4 in shader
            public const int size = 16;
        }

        // Array to hold args data before setting buffer
        private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
        private Boid[] boidArray;
        private RenderParams renderParams;
    
        [Header("Settings")]
        [SerializeField] private int boidCount = 1000;
        [SerializeField] private float spawnRadius = 50f;
        [SerializeField] private bool castShadows = false;

        [Header("Boid Behavior")]
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
    
        private static readonly int Time_ID = Shader.PropertyToID("time");
        private static readonly int DeltaTime_ID = Shader.PropertyToID("deltaTime");
        private static readonly int NumBoids_ID = Shader.PropertyToID("numBoids");
        private static readonly int NumTargets_ID = Shader.PropertyToID("numTargets");
        private static readonly int NumObstacles_ID = Shader.PropertyToID("numObstacles");
    
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
            if (boidBuffer == null || !boidBuffer.IsValid()) return;

            UpdateDynamicBuffers();
            DispatchCompute();
            RenderBoids();
        }

        private void InitializeBoids()
        {
            boidArray = new Boid[boidCount];
            for (int i = 0; i < boidCount; i++)
            {
                boidArray[i] = new Boid
                {
                    position = transform.TransformPoint(Random.insideUnitSphere * spawnRadius),
                    direction = transform.TransformDirection(Random.onUnitSphere)
                };
            }

            boidBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, Boid.size);
            boidBuffer.SetData(boidArray);
        
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

        private void UpdateDynamicBuffers()
        {
            if (targets.Count > 0)
            {
                // Reallocate if count changes
                if (targetBuffer == null || targetBuffer.count != targets.Count)
                {
                    targetBuffer?.Release();
                    targetBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, targets.Count, TargetData.size);
                }

                TargetData[] targetDataArr = new TargetData[targets.Count];
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null)
                    {
                        targetDataArr[i] = new TargetData
                        {
                            position = targets[i].transform.position,
                            // If influenceRange is needed on GPU, we could pack it, 
                            // but for now we pack Weight.
                            weight = targets[i].weight 
                        };
                    }
                }
                targetBuffer.SetData(targetDataArr);
            }

            int obstacleCount = totalObstacleCount;
            if (obstacleCount > 0)
            {
                if (obstacleBuffer == null || obstacleBuffer.count != obstacleCount)
                {
                    obstacleBuffer?.Release();
                    obstacleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, obstacleCount,
                        ObstacleData.size);
                }

                ObstacleData[] obsDataList = new ObstacleData[obstacleCount];

                int obstacleIndex = 0;
                foreach (var obs in obstacles)
                {
                    if (obs.global)
                        throw new ArgumentException(
                            "Global obstacles should not be added to the local obstacles list.");
                    obsDataList[obstacleIndex] = new ObstacleData
                    {
                        position = obs.transform.position,
                        radius = obs.radius
                    };
                    obstacleIndex++;
                }

                // add global obstacles
                foreach (var globalObs in BoidsObstacle.GlobalObstacles)
                {
                    Assert.IsTrue(globalObs.global);
                    obsDataList[obstacleIndex] = new ObstacleData
                    {
                        position = globalObs.transform.position,
                        radius = globalObs.radius
                    };
                    obstacleIndex++;
                }

                obstacleBuffer.SetData(obsDataList);
            }
            else
            {
                obstacleBuffer?.Release();
                obstacleBuffer = null;
            }
        }

        private void DispatchCompute()
        {
            computeShaderInstance.SetBuffer(kernel, Boids_ID, boidBuffer);
            computeShaderInstance.SetBuffer(kernel, VelocityBuffer_ID, velocityBuffer);    

            if (targetBuffer != null && targetBuffer.IsValid())
                computeShaderInstance.SetBuffer(kernel, Targets_ID, targetBuffer);

            if (obstacleBuffer != null && obstacleBuffer.IsValid())
                computeShaderInstance.SetBuffer(kernel, Obstacles_ID, obstacleBuffer);

            computeShaderInstance.SetFloat(Time_ID, Time.time);
            computeShaderInstance.SetFloat(DeltaTime_ID, Time.deltaTime);
        
            computeShaderInstance.SetFloat(NoiseScale_ID, noiseScale);
            computeShaderInstance.SetVector(NoiseScroll_ID, noiseScrollSpeed);
            computeShaderInstance.SetFloat(MinZoneSpeed_ID, minZoneSpeed);
            computeShaderInstance.SetFloat(MaxZoneSpeed_ID, maxZoneSpeed);
            computeShaderInstance.SetFloat(TurbulencePower_ID, turbulencePower);
        
            computeShaderInstance.SetInt(NumBoids_ID, boidCount);
            computeShaderInstance.SetInt(NumTargets_ID, targets.Count);
            computeShaderInstance.SetInt(NumObstacles_ID, totalObstacleCount);

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
            renderParams.matProps.SetBuffer(BoidBuffer_ID, boidBuffer);
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
            boidBuffer?.Release();
            boidBuffer = null;
        
            velocityBuffer?.Release();
            velocityBuffer = null;
        
            targetBuffer?.Release();
            targetBuffer = null;
        
            obstacleBuffer?.Release();
            obstacleBuffer = null;
        
            argsBuffer?.Release();
            argsBuffer = null;
        
            if (computeShaderInstance != null)
            {
                Destroy(computeShaderInstance);
                computeShaderInstance = null;
            }
        }
    }
}