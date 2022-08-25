using System;
using UnityEngine;

[Serializable]
public class Initialization
{
    [SerializeField]
    public bool initializationComplete = false;
    [SerializeField]
    public bool initializationUpdated = false;
    [SerializeField]
    public UInt32[] matrixSize;
    [SerializeField]
    public Vector3 FOVDimensions;
    [SerializeField]
    public bool isVolumetric;
    [SerializeField]
    public UInt32 DataBufferSize;

    public Initialization()
    {
        matrixSize = new UInt32[3];
        FOVDimensions = new Vector3();
    }
}