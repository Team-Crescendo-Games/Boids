using UnityEngine;

internal struct Boid
{
    public Vector3 position; 
    public Vector3 direction;
    public const int size = 24;
} 

internal struct ObstacleData
{
    public Vector3 position; 
    public float radius;
    public const int size = 16;
}

internal struct TargetData
{
    public Vector3 position;
    public float weight;
    public float influenceRange;
    public const int size = 20;
}

internal struct ZoneData
{
    public Matrix4x4 worldToLocal;
    public Matrix4x4 localToWorld;
    public Vector3 dimensions;
    public int type;
    public const int size = 144; // 64+64+12+4 = 144 bytes
}