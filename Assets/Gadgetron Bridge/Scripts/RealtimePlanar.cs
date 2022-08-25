
// TCP Source: https://msdn.microsoft.com/en-us/library/system.net.sockets.socket(v=vs.80).aspx

using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

public class RealtimePlanar : MonoBehaviour
{
    #region Variables
    // ENUMS

    InitializationHandler initializationHandler;
    Initialization initializationSettings;

    [Header("Gadgetron Properties")]
    public int sliceDataPort = 4447;
    public float dataBreakCharacter = 9000;
    public float initBreakCharacter = 9002;

    [Header("Visualization Options")]
    public bool applyWindowLevel = false;
    [Range(0, 4096)]
    public int window = 60;
    [Range(0, 4096)]
    public int level = 60;
    [Range(0, 10)]
    public float renderingScale = 1;
    // public bool usePlanarUI = false;
    //public PlanarUIManager planarUIManager;

    [Header("Localizer Options")]
    public Transform localizer;
    public Material localizerMaterial;
    public LayerMask sliceLayer;

    [Header("UI Object Options")]
    public PinchSlider windowSlider;
    public PinchSlider levelSlider;

    [Header("2D Slices")]
    [SerializeField]
    public List<Slice> slices;

    [Header("Utilities")]
    public bool waitForAllSlices = false;
    public bool enableVerbose = false;
    public Quaternion universalRotationCorrection = new Quaternion(0, -0.707f, 0.707f, 0f);
    public float scaleFactor = 0.001f;
    public int inactiveFrameCutoff = 10;
    public Logger logger;

    // PRIVATE VARIABLES
    private List<ushort> mostRecentData;
    //private IPAddress localAddr;
    private System.Diagnostics.Stopwatch watch;
    private List<long> frameMs;
    private bool tcpActive = true;
    public bool cancelled = false;
    private bool allSlicesRecieved;
    private float planeScaleCompensationRatio = 0.1f;
    private int windowMax = 1024;
    private int levelMax = 1024;

    private framelogger frameLogger;
    public int frameNumber;
    List<Thread> sliceThreads;

    #endregion

    void Awake()
    {
        initializationHandler = FindObjectOfType<InitializationHandler>();
        initializationSettings = initializationHandler.GetInitializationSettings();
        logger = initializationHandler.logger;
        frameLogger = FindObjectOfType<framelogger>();
        frameLogger.realtimePlanar = this;
        frameNumber = 0;
    }

    private void initializeSlices()
    {
        print("Number of slices: " + initializationSettings.matrixSize[2]);
        for (int i = 0; i < initializationSettings.matrixSize[2]; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.layer = 9;
            go.GetComponent<MeshCollider>().convex = true;
            Slice sl = go.AddComponent<Slice>();
            // sl.transform = go.transform;
            sl.transform.parent = localizer;
            Vector3 fov = initializationSettings.FOVDimensions;
            sl.transform.localScale = new Vector3(fov.x, fov.z, fov.y) * planeScaleCompensationRatio;
            sl.transform.localPosition = new Vector3(0, 0, 0);
            sl.xMatrixSize = (int)initializationSettings.matrixSize[0];
            sl.yMatrixSize = (int)initializationSettings.matrixSize[1];

            go.name = "Slice " + i;
            sl.name = go.name;
            sl.sliceNumber = i;
            // sl.renderer = go.GetComponent<MeshRenderer>();
            sl.GetComponent<Renderer>().material = localizerMaterial;
            sl.GetComponent<Renderer>().sharedMaterial.SetTexture("_MainTex", sl.texture);
            sl.GetComponent<Renderer>().transform.parent = localizer;


            if (enableVerbose)
                Console.WriteLine("Made New Slice:" + sl.transform.name);
            slices.Add(sl);
        }
        logger.WriteTimestampToLog("OnSlicesInitialized (FrameNumber=" + frameNumber + ")");
    }

    public void UpdateLevel()
    {
        level = (int)(levelSlider.SliderValue * levelMax);
    }

    public void UpdateWindow()
    {
        window = (int)(windowSlider.SliderValue * windowMax);
    }

    void Update()
    {
        if (initializationSettings.initializationComplete)
        {
            if (logger.writeFrametimesToLog)
                logger.WriteTimestampToLog("OnNewFrame (FrameNumber=" + frameNumber + ")");

            if (sliceThreads == null)
            {
                initializeSlices();
                uint numThreads = initializationSettings.matrixSize[2];
                sliceThreads = new List<Thread>();
                for (int thread = 1; thread < numThreads; thread++)
                {
                    Thread temp = new Thread(() => ListenForData(sliceDataPort));
                    temp.Start();
                    sliceThreads.Add(temp);
                }
                logger.WriteTimestampToLog("OnSliceDataListenerStarted (FrameNumber=" + frameNumber + ")");
            }
            else
            {

                if (applyWindowLevel)
                {
                    foreach (Slice slice in slices)
                    {
                        slice.dataChanged = true;
                        Thread sliceProcessor = new Thread(() => slice.tryProcessData(window, level, (int)initializationSettings.matrixSize[0], (int)initializationSettings.matrixSize[1], initializationSettings.FOVDimensions, scaleFactor, universalRotationCorrection, inactiveFrameCutoff));
                        sliceProcessor.Start();
                        
                    }
                    applyWindowLevel = false;

                }

                // If WaitForAllSlices and AllRecieved, apply the new data
                if (waitForAllSlices == true)
                {
                    allSlicesRecieved = false;
                    foreach (Slice slice in slices)
                    {
                       allSlicesRecieved = slice.CheckAndAcknowledgeRenderUpdateFlag() || allSlicesRecieved; // 9.14.21: Added OR statement to prevent writing false to update thread flag  || 9.16.21: Moved to process data
                    }

                    if (allSlicesRecieved)
                    {
                        foreach (Slice slice in slices)
                        {
                            slice.ApplyData(); // Must run on main thread b/c of textures
                        }
                        logger.WriteTimestampToLog("OnAllSlicesUpdated(WaitForAll FrameNumber=" + frameNumber + ")");
                    }
                    allSlicesRecieved = false;
                }

                else
                {
                    foreach (Slice slice in slices)
                    {
                        slice.ApplyData(); // Must run on main thread b/c of textures
                    }
                    logger.WriteTimestampToLog("OnAllSlicesUpdated(NoWaitForAll FrameNumber=" + frameNumber + ")");
                }

                //planarUIManager.UpdateSlices(slices);
            }
        }

        if (initializationSettings.initializationUpdated)
        {
            Console.WriteLine("New slice initialization data is available");

            foreach (Slice slice in slices)
            {
                slice.sliceActive = false;
            }

            uint numberOfActiveSlices = initializationSettings.matrixSize[2];
            for (int i = 0; i < numberOfActiveSlices; i++)
            {
                slices[i].sliceActive = true;
                slices[i].processData(window, level, (int)initializationSettings.matrixSize[0], (int)initializationSettings.matrixSize[1], initializationSettings.FOVDimensions, scaleFactor, universalRotationCorrection);
            }

            initializationSettings.initializationUpdated = false;
            logger.WriteTimestampToLog("OnInitializationDataUpdated");
            Console.WriteLine("Applied new slice initialization data (" + numberOfActiveSlices + " slice dataset)");
        }
    }

    public void LogStartRender()
    {
        if (logger.writeFrametimesToLog)
            logger.WriteTimestampToLog("OnStartRenderFrame (FrameNumber=" + frameNumber + ")");

        frameNumber++;
    }

    public void LogFinishRender()
    {
        if (logger.writeFrametimesToLog)
            logger.WriteTimestampToLog("OnFinishRenderFrame (FrameNumber=" + frameNumber + ")");

        frameNumber++;
    }

    public void ListenForData(int port)
    {
        TcpListener server = null;
        try
        {
            server = new TcpListener(initializationHandler.GetIPAddress(), port);
            server.Start();

            while (tcpActive)
            {
                if (enableVerbose)
                    Console.WriteLine("Waiting for a Slice Data connection... ");
                
                int currentSliceNumber = -1;

                using (TcpClient client = server.AcceptTcpClient())
                {

                    if (enableVerbose)
                        Console.WriteLine("Connected to Slice Data client - Beginning Byte Stream Read");
                    logger.WriteTimestampToLog("OnSliceDataAvailable");

                    #region readDataFromSocket
                    List<byte> allBytes = new List<byte>();
                    Byte[] bytes = new Byte[initializationSettings.DataBufferSize];
                    bool endReached = false;
                    while (!endReached)
                    {
                        int numberRecieved = client.Client.Receive(bytes);
                        if (numberRecieved == 0)
                        {
                            endReached = true;
                        }
                        for (int i = 0; i < numberRecieved; i++)
                        {
                            allBytes.Add(bytes[i]);
                        }
                    }
                    #endregion
                    logger.WriteTimestampToLog("OnSliceDataReceived");

                    List<float> floatList = new List<float>();
                    List<ushort> imageDataList = new List<ushort>();

                    #region sliceNumberConversion
                    // Collect Slice Number
                    if (enableVerbose)
                        Console.WriteLine(allBytes.Count + " Bytes Recieved. Beginning Slice Number Conversion");

                    while (!cancelled)
                    {
                        byte[] temp = new byte[4];

                        temp[0] = allBytes[0];
                        temp[1] = allBytes[1];
                        temp[2] = allBytes[2];
                        temp[3] = allBytes[3];

                        if (BitConverter.ToSingle(temp, 0) != dataBreakCharacter)
                        {
                            currentSliceNumber = (int)BitConverter.ToUInt32(temp, 0);
                            allBytes.RemoveRange(0, 4);
                        }
                        else
                        {
                            allBytes.RemoveRange(0, 4);
                            break;
                        }
                    }

                    if (enableVerbose)
                        Console.WriteLine("Finished Slice Number Conversion. Slice = " + currentSliceNumber);

                    #endregion

                    if (currentSliceNumber != -1)
                    {
                        #region positionConversion
                        // Collect Position Data
                        if (enableVerbose)
                            Console.WriteLine("Beginning Position Conversion");

                        floatList.Clear();
                        while (!cancelled)
                        {
                            byte[] temp = new byte[4];
                            for (int offset = 0; offset < 4; offset++)
                            {
                                temp[offset] = allBytes[offset];
                            }
                            allBytes.RemoveRange(0, 4);

                            float result = BitConverter.ToSingle(temp, 0);
                            if (result != 9000)
                            {
                                floatList.Add(result);
                            }
                            else
                            {
                                Vector3 position = new Vector3(floatList[0], floatList[1], floatList[2]);
                                if (enableVerbose)
                                {
                                    print(position);
                                }
                                try
                                {
                                    slices[currentSliceNumber].position = position;
                                }
                                catch
                                {
                                    print("Unable to set slice position data for slice number: " + currentSliceNumber);
                                }
                                break;
                            }
                        }
                        #endregion

                        #region rotationConversion
                        // Collect Rotation Data
                        if (enableVerbose)
                            print("Beginning Rotation Conversion");

                        floatList.Clear();
                        while (!cancelled)
                        {
                            byte[] temp = new byte[4];
                            for (int offset = 0; offset < 4; offset++)
                            {
                                temp[offset] = allBytes[offset];
                            }
                            allBytes.RemoveRange(0, 4);

                            float result = BitConverter.ToSingle(temp, 0);
                            if (result != 9000)
                            {
                                floatList.Add(result);
                            }
                            else
                            {
                                // floatList Indexes represent orderfloats were received from TCP. 
                                // Assign them to the proper x,y,z,w elements for the object transform

                                float x = floatList[0];
                                float y = floatList[1];
                                float z = floatList[2];
                                float w = floatList[3];
                                Quaternion rotation = new Quaternion(x, y, z, w);
                                try
                                {
                                    slices[currentSliceNumber].rotation = rotation;
                                }
                                catch
                                {
                                    print("Unable to set slice rotation data for slice number: " + currentSliceNumber);
                                }
                                break;
                            }
                        }
                        #endregion

                        #region RefreshRenderingFlagConversion
                        if (enableVerbose)
                            print("Beginning Update Flag Conversion");

                        slices[currentSliceNumber].refreshRenderingFlag = allBytes[0] != 0; // TODO: ACTAULLY TIE TO SLICE UPDATING
                        slices[currentSliceNumber].refreshRenderingFlagAcknowledged = false;

                        allBytes.RemoveRange(0, 1);
                        if (!cancelled)
                        {
                            byte[] temp = new byte[4];
                            for (int offset = 0; offset < 4; offset++)
                            {
                                temp[offset] = allBytes[offset];
                            }
                            allBytes.RemoveRange(0, 4);

                            float result = BitConverter.ToSingle(temp, 0);
                            if (result != 9000)
                            {
                                print("Error: End of Header Break Not Received.");
                            }
                            else
                            {
                                if (enableVerbose)
                                {
                                    print("Received Update Flag. Received Full Header.");
                                }
                            }
                        }

                        #endregion

                        #region sliceDataConversion
                        if (enableVerbose)
                            Console.WriteLine("Beginning Slice Conversion");

                        try
                        {
                            for (int step = 0; step < allBytes.Count; step += 2)
                            {
                                byte[] temp = new byte[2];
                                temp[0] = allBytes[step];
                                temp[1] = allBytes[step + 1];
                                imageDataList.Add(BitConverter.ToUInt16(temp, 0));
                            }
                        }
                        catch (Exception ex)
                        {
                            print(ex);
                            print("ERROR: cant convert slice data");
                        }
                        try
                        {
                            slices[currentSliceNumber].imageData = imageDataList;
                            slices[currentSliceNumber].dataChanged = true;
                            slices[currentSliceNumber].firstDataReceived = true;
                        }
                        catch
                        {
                            print("Unable to set slice image data for slice number: " + currentSliceNumber);
                        }

                        if (enableVerbose)
                        {
                            Console.WriteLine("Conversion Complete. Total Data Points: " + imageDataList.Count);
                        }
                        #endregion
                    }
                    else
                    {
                        Console.WriteLine("Data Conversion Failed Due to Unrecognized Slice Number");
                    }

                    if (enableVerbose)
                        Console.WriteLine("Thread Completed");
                    logger.WriteTimestampToLog("OnSliceDataParsed (slice=" + currentSliceNumber + ", refreshRendering=" + slices[currentSliceNumber].refreshRenderingFlag + ")");

                    // allSlicesRecieved = refreshRendering | allSlicesRecieved; // 9.14.21: Added OR statement to prevent writing false to update thread flag  || 9.16.21: Moved to process data

                    //Deploy Processing thread for newly recieved data
                }

                Thread sliceProcessor = new Thread(() => slices[currentSliceNumber].tryProcessData(window, level, (int)initializationSettings.matrixSize[0], (int)initializationSettings.matrixSize[1], initializationSettings.FOVDimensions, scaleFactor, universalRotationCorrection, inactiveFrameCutoff));
                sliceProcessor.Start();
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine(String.Format("SocketException: {0}", e));
        }
        finally
        {
            server.Stop();
        }
    }

    // SOURCE: https://stackoverflow.com/questions/2683442/where-can-i-find-the-clamp-function-in-net
    // AUTHOR: Jwosty :: 06/2013
    public static float Clamp(float value, float min, float max)
    {
        return (value < min) ? min : (value > max) ? max : value;
    }
}
