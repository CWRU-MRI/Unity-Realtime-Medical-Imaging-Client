using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Slice : MonoBehaviour
{
    /*[HideInInspector]
    public string name;
    [SerializeField]
    public Transform transform;
    [HideInInspector]
    public MeshRenderer renderer;
    */
    [SerializeField]
    public Vector3 position;
    [SerializeField]
    public Quaternion rotation;
    [HideInInspector]
    public List<ushort> imageData;
    [SerializeField]
    public Texture2D texture;
    public bool firstDataReceived = false;
    public int sliceNumber;
    public bool dataChanged = false;
    public bool dataProcessed = false;
    public bool refreshRenderingFlag = false;
    public bool refreshRenderingFlagAcknowledged = false;

    public int inactiveFrames = 0;

    public Quaternion universalRotationCorrection;
    public int xMatrixSize;
    public int yMatrixSize;
    public Vector3 fov;
    public float scaleFactor;

    //private UIManager manager;
    public bool sliceActive;
    public Color[] pixels;
    private RealtimePlanar realtimePlanar;

    [Tooltip("Planes are 10m by default, this should be 0.1, probably dont touch it")]
    public float planeScaleCompensationRatio = 0.1f;

    public void tryProcessData(int window, int level, int xMatrixSize, int yMatrixSize, Vector3 fov, float scaleFactor, Quaternion universalRotationCorrection, int inactiveFrameCutoff)
    {
        if (dataChanged)
        {
            realtimePlanar.logger.WriteTimestampToLog("OnSliceProcessingThreadStarted (slice=" + sliceNumber + " FrameNumber=" + realtimePlanar.frameNumber + ")");
            inactiveFrames = 0;
            sliceActive = true;
            processData(window, level, xMatrixSize, yMatrixSize, fov, scaleFactor, universalRotationCorrection);
            realtimePlanar.logger.WriteTimestampToLog("OnSliceProcessingThreadFinished (slice=" + sliceNumber + " FrameNumber=" + realtimePlanar.frameNumber + ")");
            dataChanged = false;
            dataProcessed = true;
        }
        else
        {
            inactiveFrames++;
            if (inactiveFrames > inactiveFrameCutoff)
            {
                sliceActive = false;
            }
        }

    }

    public void processData(int window, int level, int xMatrixSize, int yMatrixSize, Vector3 fov, float scaleFactor, Quaternion universalRotationCorrection)
    {
        if (firstDataReceived)
        {
            this.universalRotationCorrection = universalRotationCorrection;
            this.xMatrixSize = xMatrixSize;
            this.yMatrixSize = yMatrixSize;
            this.fov = fov;
            this.scaleFactor = scaleFactor;
            int xLocation;
            int yLocation;

            #region applySliceData
            if (texture != null)
            {
                for (int data = 0; data < imageData.Count; data++)
                {
                    //// Find the Texture Index
                    //xLocation = (int)Math.Floor((double)(data) / (double)yMatrixSize); //try fixing it here
                    //yLocation = (data - (xLocation * yMatrixSize)); // this is wrong in some way
                    //                                                //print("Pixel:" + xLocation + ", "+ yLocation);
                    //                                                //yLocation = yMatrixSize - (data - (xLocation * yMatrixSize));

                    ushort currentData = imageData[data];

                    int offset = currentData - level;
                    float scalarDensity = offset / ((float)window / 2);
                    scalarDensity = (scalarDensity + 1) / 2;
                    scalarDensity = Clamp(scalarDensity, 0, 1);
                    pixels[data] = new Color(scalarDensity, scalarDensity, scalarDensity, 1);
                    //pixels[imageData.Count - data - 1] = new Color(scalarDensity, scalarDensity, scalarDensity, 1);
                    //print(pixels[data]);

                }
            }
            #endregion
        }
    }

    public bool CheckAndAcknowledgeRenderUpdateFlag()
    {
        if (dataProcessed)
        {
            bool shouldSliceTriggerRefresh = refreshRenderingFlag && !refreshRenderingFlagAcknowledged;
            refreshRenderingFlagAcknowledged = true; // Stops a slice from setting allSlicesRecieved more than once
            return shouldSliceTriggerRefresh;
        }
        else
        {
            return false;
        }
    }

    public void ApplyData()
    {
        if (sliceActive)
        {
            if (dataProcessed)
            //if (sliceActive || manager.showAllSlices)
            //if (sliceActive || ((manager.uiMode == UIManager.UIMode.Multislice) && manager.showAllSlices))
            {
                if (firstDataReceived)
                {
                    //applySliceRotation
                    transform.localRotation = rotation * universalRotationCorrection;

                    //ApplySlicePosition
                    transform.localPosition = (position * scaleFactor) + Vector3.Scale(transform.TransformDirection(Vector3.one / 2), new Vector3(fov.x / xMatrixSize, fov.z, fov.y / yMatrixSize));

                    //Apply texture colors
                    texture.SetPixels(pixels);
                    texture.Apply();
                    GetComponent<Renderer>().material.SetTexture("_MainTex", texture);
                }
                this.GetComponent<MeshRenderer>().enabled = true;
                dataProcessed = false;
                realtimePlanar.logger.WriteTimestampToLog("OnSliceDataApplied (slice=" + sliceNumber + " FrameNumber=" + realtimePlanar.frameNumber + ")");
            }
        }
        else
        {
            this.GetComponent<MeshRenderer>().enabled = false;

        }
    }

    void Start()
    {
        if (texture == null)
        {
            texture = new Texture2D(xMatrixSize, yMatrixSize);
            pixels = new Color[xMatrixSize * yMatrixSize];
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.anisoLevel = 0;
        }

        realtimePlanar = FindObjectOfType<RealtimePlanar>();
        // manager = FindObjectOfType<UIManager>();
    }

    public static float Clamp(float value, float min, float max)
    {
        return (value < min) ? min : (value > max) ? max : value;
    }
}