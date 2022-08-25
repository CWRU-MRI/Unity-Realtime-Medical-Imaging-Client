
// TCP Source: https://msdn.microsoft.com/en-us/library/system.net.sockets.socket(v=vs.80).aspx

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.IO;
using Microsoft.MixedReality.Toolkit.UI;

public class RealtimeVolumetric : MonoBehaviour
{
    [HideInInspector]
    public Texture3D currentFrame;

    InitializationHandler initializationHandler;
    Initialization initializationSettings;

    [Header("Gadgetron Properties")]
    public int sliceDataPort = 4447;

    [Header("Visualization Options")]
    private VolumeRenderer volumeRenderer;
    [Range(0, 4096)]
    public int window = 60;
    [Range(0, 4096)]
    public int level = 60;
    private int windowMax = 1024;
    private int levelMax = 1024;
    public bool WindowLevelChanged = false;
    public Vector3 vrSubsampling = new Vector3(1, 1, 1);

    [Header("UI Object Options")]
    public PinchSlider windowSlider;
    public PinchSlider levelSlider;

    [Header("Utilities")]
    public bool enableVerbose = false;
    public Logger logger;
    public int frameNumber = 0;


    // PRIVATE VARIABLES
    private bool dataChanged = false;
    private List<ushort> mostRecentData;
    private Thread tcpListenerThread;
    private bool tcpActive = true;
    public Color[] pixels;
    private framelogger frameLogger;


    void Awake()
    {
        initializationHandler = FindObjectOfType<InitializationHandler>();
        volumeRenderer = FindObjectOfType<VolumeRenderer>();
        initializationSettings = initializationHandler.GetInitializationSettings();

        logger = initializationHandler.logger;
        initializeVolume();
        frameLogger = FindObjectOfType<framelogger>();
        frameLogger.realtimeVolumetric = this;
        frameNumber = 0;

    }

    private void initializeVolume()
    {
        //int x = (int)(initializationSettings.matrixSize[0] * vrSubsampling.x);
        //int y = (int)(initializationSettings.matrixSize[1] * vrSubsampling.y);
        //int z = (int)(initializationSettings.matrixSize[2] * vrSubsampling.z);
        currentFrame = new Texture3D((int)initializationSettings.matrixSize[0], (int)initializationSettings.matrixSize[1], (int)initializationSettings.matrixSize[2], TextureFormat.ARGB32, false);
        pixels = new Color[initializationSettings.matrixSize[0] * initializationSettings.matrixSize[1] * initializationSettings.matrixSize[2]];
        logger.WriteTimestampToLog("OnVolumeInitialized (FrameNumber=" + frameNumber + ")");
    }


    void Update()
    {
        // Set Rendering Size from Scan Matrix Size, Step Size, and Rendering Scale
        // Vector3 voxelSize = new Vector3(FOV.x / xMatrixSize, FOV.y / yMatrixSize, FOV.z / numberOfSlices);
        ///volumeLocalizer.transform.localScale = renderingScale * FOV;

        if (initializationSettings.initializationComplete)
        {
            if (tcpListenerThread == null)
            {
                tcpListenerThread = new Thread(() => ListenForMessages(sliceDataPort));
                tcpListenerThread.Start();
                logger.WriteTimestampToLog("OnVolumeDataListenerStarted (FrameNumber=" + frameNumber + ")");
            }

            if (dataChanged)
            {
                logger.WriteTimestampToLog("OnVolumeDisplayStartUpdating (FrameNumber=" + frameNumber + ")");

                // TODO: Fix Threading Conflict so that image values can be set from second thread
                currentFrame.SetPixels(pixels);
                currentFrame.Apply();
                volumeRenderer.volume = currentFrame;
                volumeRenderer.transform.localScale = initializationSettings.FOVDimensions;

                if (enableVerbose)
                    Debug.Log("New Data Recieved and Applied");

                logger.WriteTimestampToLog("OnVolumeDisplayFinishUpdating (FrameNumber=" + frameNumber + ")");
                dataChanged = false;
            }

            if (WindowLevelChanged)
            {
                logger.WriteTimestampToLog("OnVolumeDisplayUpdateWindowLevel (FrameNumber=" + frameNumber + ")");

                // TODO: Fix Threading Conflict so that image values can be set from second thread
                currentFrame.SetPixels(pixels);
                currentFrame.Apply();
                volumeRenderer.volume = currentFrame;
                volumeRenderer.transform.localScale = initializationSettings.FOVDimensions;

                if (enableVerbose)
                    Debug.Log("New Window / Level Applied");

                logger.WriteTimestampToLog("OnVolumeDisplayFinishUpdatingWindowLevel (FrameNumber=" + frameNumber + ")");
                WindowLevelChanged = false;
            }
        }

        if (initializationSettings.initializationUpdated)
        {
            if (enableVerbose)
                Console.WriteLine("New volume initialization data is available");

            initializeVolume();
            initializationSettings.initializationUpdated = false;
            logger.WriteTimestampToLog("OnInitializationDataUpdated (FrameNumber=" + frameNumber + ")");

            if (enableVerbose)
                Console.WriteLine("Applied new volume initialization data");
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

    public void UpdateLevel()
    {
        level = (int)(levelSlider.SliderValue * levelMax);
        ReparseData(mostRecentData);
        WindowLevelChanged = true;
    }

    public void UpdateWindow()
    {
        window = (int)(windowSlider.SliderValue * windowMax);
        ReparseData(mostRecentData);
        WindowLevelChanged = true;
    }

    public void ReparseData(List<ushort> data)
    {
        int xMatrixSize = (int)initializationSettings.matrixSize[0];
        int yMatrixSize = (int)initializationSettings.matrixSize[1];

        for (int index = 0; index < data.Count; index++)
        {
            int slice = (int)Math.Floor((double)index / (double)(xMatrixSize * yMatrixSize));
            int xLocation = (int)Math.Floor((double)(index - slice * xMatrixSize * yMatrixSize) / (double)yMatrixSize);
            int yLocation = index - slice * xMatrixSize * yMatrixSize - xLocation * yMatrixSize;
            ushort currentData = data[index];
            int offset = currentData - level;
            float scalarDensity = offset / ((float)window / 2);
            scalarDensity = (scalarDensity + 1) / 2;
            scalarDensity = Clamp(scalarDensity, 0, 1);

            Color color = new Color(scalarDensity, scalarDensity, scalarDensity, 1);
            pixels[index] = color;
        }
        logger.WriteTimestampToLog("OnVolumeDataReparsed (FrameNumber=" + frameNumber + ")");
        dataChanged = true;
    }

    public void ParseData(List<ushort> data)
    {
        if (enableVerbose)
            Debug.Log("Frame Number " + frameNumber + " was recieved");

        int xMatrixSize = (int)initializationSettings.matrixSize[0];
        int yMatrixSize = (int)initializationSettings.matrixSize[1];

        for (int index = 0; index < data.Count; index++)
        {
            int slice = (int)Math.Floor((double)index / (double)(xMatrixSize * yMatrixSize));
            int xLocation = (int)Math.Floor((double)(index - slice * xMatrixSize * yMatrixSize) / (double)yMatrixSize);
            int yLocation = index - slice * xMatrixSize * yMatrixSize - xLocation * yMatrixSize;
            ushort currentData = data[index];
            int offset = currentData - level;
            float scalarDensity = offset / ((float)window / 2);
            scalarDensity = (scalarDensity + 1) / 2;
            scalarDensity = Clamp(scalarDensity, 0, 1);

            Color color = new Color(scalarDensity, scalarDensity, scalarDensity, 1);
            pixels[index] = color;
        }
        logger.WriteTimestampToLog("OnVolumeDataParsed (New Data FrameNumber=" + frameNumber + ")");
        dataChanged = true;
    }

    public void ListenForMessages(int port)
    {
        TcpListener server = null;
        try
        {
            server = new TcpListener(initializationHandler.GetIPAddress(), port);
            server.Start();

            while (tcpActive)
            {
                if (enableVerbose)
                    Debug.Log("Waiting for a connection... ");

                using (TcpClient client = server.AcceptTcpClient())
                {
                    if (enableVerbose)
                        Debug.Log("Connected - Beginning Byte Stream Read");
                    
                    logger.WriteTimestampToLog("OnVolumeDataAvailable (FrameNumber=" + frameNumber + ")");

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

                    logger.WriteTimestampToLog("OnVolumeDataReceived (FrameNumber=" + frameNumber + ")");

                    print("Actual: "+allBytes.Count);
                    print("Desired: "+(int)initializationSettings.matrixSize[0] * (int)initializationSettings.matrixSize[1] * (int)initializationSettings.matrixSize[2] * 2);
                    if (allBytes.Count != (int)initializationSettings.matrixSize[0] * (int)initializationSettings.matrixSize[1] * (int)initializationSettings.matrixSize[2] * 2)
                    {
                        Debug.Log("FULL FRAME NOT RECIEVED - DISCARDED");
                    }
                    else
                    {
                        List<ushort> convertedData = new List<ushort>();

                        if (enableVerbose)
                            Debug.Log("Beginning Conversion");

                        for (int step = 0; step < allBytes.Count; step += 2)
                        {
                            byte[] temp = new byte[2];
                            temp[0] = allBytes[step];
                            temp[1] = allBytes[step + 1];
                            convertedData.Add(BitConverter.ToUInt16(temp, 0));
                        }
                        if (enableVerbose)
                        {
                            Debug.Log("Conversion Complete. Total Data Points: " + convertedData.Count);
                            Debug.Log("Setting Data Variables");
                        }

                        mostRecentData = convertedData;
                        Thread parseThread = new Thread(() => ParseData(mostRecentData));
                        parseThread.Start();
                    }

                    if (enableVerbose)
                        Debug.Log("Thread Completed");
                }
            }
        }
        catch (SocketException e)
        {
            Debug.LogError(String.Format("SocketException: {0}", e));
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
