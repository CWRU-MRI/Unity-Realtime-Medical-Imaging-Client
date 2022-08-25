using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.WSA.Input;

public class InitializationHandler : MonoBehaviour
{

    #region Public Variables
    [Header("Configuration")]
    public string manualIP = "192.168.0.105";
    public int initializationPort = 4447;
    public int initiliazationBufferSize = 64;
    public float initializationBreakCharacter = 9002;
    public float scaleFactor = 0.001f;
    public bool continuousInitialization;
    public int targetFramerate = 60;
    public int vsyncCount = 0;


    [Header("Unity Object References")]
    public UnityEngine.Object volumetricScene;
    public UnityEngine.Object planarScene;
    public Transform anchor;

    [Header("Debugging")]
    public bool startListening = false;
    public bool enableVerbose = false;
    public bool cancelFlag = false;
    public Logger logger;

    [SerializeField]
    public UnityAction action;

    #endregion

    #region Private Variables
    private IPAddress localAddr;
    private bool renderSceneLoaded = false;
    private Thread initializationThread;
    [SerializeField]
    private Initialization initializationSettings;
    // private GestureRecognizer gestureRecognizer;
    #endregion

    #region Getters/Setters
    public Initialization GetInitializationSettings()
    {
        return initializationSettings;
    }
    #endregion

    private void Awake()
    {
        QualitySettings.vSyncCount = vsyncCount;
        Application.targetFrameRate = targetFramerate;
    }

    private void Update()
    {
        if (startListening)
        {
            if (manualIP.Length < 1)
            {
                manualIP = GetLocalIPAddress();
            }
            initializationThread = new Thread(() => ListenForInitializationData(manualIP, initializationPort));
            initializationThread.Start();
            logger.WriteTimestampToLog("OnStartListening");
            startListening = false;
        }

        if (initializationSettings.initializationComplete && !renderSceneLoaded)
        {
            logger.WriteTimestampToLog("OnInitializationComplete");

            if (!continuousInitialization) //  TODO: THIS NEEDS A BETTTER WAY
                initializationThread.Abort();

            if (initializationSettings.isVolumetric && !renderSceneLoaded)
            {
                // Clean up loading scene stuff
                //DestroyImmediate(spatialMapping);
                //DestroyImmediate(gazeDot);
                //gestureRecognizer.StartCapturingGestures();
                //gestureRecognizer.Dispose();
                //GazeCanvas.SetActive(true);
                SceneManager.LoadSceneAsync(1, LoadSceneMode.Additive);
                renderSceneLoaded = true;
            }
            else if (!initializationSettings.isVolumetric && !renderSceneLoaded)
            {
                // Clean up loading scene stuff
                //DestroyImmediate(spatialMapping);
                //DestroyImmediate(gazeDot);
                //gestureRecognizer.StartCapturingGestures();
                //gestureRecognizer.Dispose();
                //GazeCanvas.SetActive(true);
                SceneManager.LoadSceneAsync(2, LoadSceneMode.Additive);
                renderSceneLoaded = true;

            }
        }
        else
        {
            //if (anchorPlaced)
            //{
            //    if (initializationThread == null)
            //    {
            //        initializationThread = new Thread(() => ListenForInitializationData(initializationPort));
            //        initializationThread.Start();
            //    }
            //}
            //else
            //{
            //    RaycastHit hitInfo;
            //    Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hitInfo, 10f, spatialMapLayer);
            //    gazeDot.transform.position = hitInfo.point;
            //}
        }

    }

    public IPAddress GetIPAddress()
    {
        return localAddr;
    }

    public static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    void ListenForInitializationData(string ip, int port)
    {
        TcpListener server = null;
        try
        {
            if (enableVerbose)
                Debug.Log("New Initialization Thread started. ");

            localAddr = IPAddress.Parse(ip);

            if (enableVerbose)
                Debug.Log(localAddr);

            server = new TcpListener(localAddr, port);
            server.Start();

            while (!initializationSettings.initializationComplete || continuousInitialization)
            {
                if (enableVerbose)
                    Debug.Log("Waiting for a Slice Initialization connection... ");

                using (TcpClient client = server.AcceptTcpClient())
                {
                    logger.WriteTimestampToLog("OnConnectToGadgetron");

                    if (enableVerbose)
                        Debug.Log("Connected to Initialization Data client - Beginning Byte Stream Read");
                    logger.WriteTimestampToLog("OnInitializationAvailable");
                    List<byte> allBytes = new List<byte>();
                    Byte[] bytes = new Byte[initiliazationBufferSize];

                    #region Read Data Until Socket Empty
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

                    logger.WriteTimestampToLog("OnInitializationReceived");

                    if (enableVerbose)
                        Debug.Log("Received Initialization Packet. # of Bytes: " + allBytes.Count);

                    if (allBytes.Count >= 37) // TODO: What is this?
                    {
                        if (enableVerbose)
                            Debug.Log("Starting Initialization Conversion");

                        #region Convert Matrix Size Data (Uint32x3)
                        initializationSettings.matrixSize[0] = BitConverter.ToUInt32(getByteArray(allBytes, 4), 0);
                        allBytes.RemoveRange(0, 4);
                        initializationSettings.matrixSize[1] = BitConverter.ToUInt32(getByteArray(allBytes, 4), 0);
                        allBytes.RemoveRange(0, 4);
                        initializationSettings.matrixSize[2] = BitConverter.ToUInt32(getByteArray(allBytes, 4), 0);
                        allBytes.RemoveRange(0, 4);
                        if (enableVerbose)
                            Debug.Log("Received Matrix Size: " + initializationSettings.matrixSize[0] + "x" + initializationSettings.matrixSize[1] + "x" + initializationSettings.matrixSize[2]);
                        #endregion

                        #region Convert FOV Dimensions (Floatx3)
                        initializationSettings.FOVDimensions.x = BitConverter.ToSingle(getByteArray(allBytes, 4), 0) * scaleFactor;
                        allBytes.RemoveRange(0, 4);
                        initializationSettings.FOVDimensions.y = BitConverter.ToSingle(getByteArray(allBytes, 4), 0) * scaleFactor;
                        allBytes.RemoveRange(0, 4);
                        initializationSettings.FOVDimensions.z = BitConverter.ToSingle(getByteArray(allBytes, 4), 0) * scaleFactor;
                        allBytes.RemoveRange(0, 4);
                        if (enableVerbose)
                            Debug.Log("Received FOV Dimensions");
                        #endregion

                        #region Convert Volumetric Flag (Bool)
                        initializationSettings.isVolumetric = (allBytes[0] != 0);
                        allBytes.RemoveRange(0, 1);
                        if (enableVerbose)
                            Debug.Log("Received Volumetric State");
                        #endregion

                        #region Convert Slice Thickness (Float)
                        initializationSettings.FOVDimensions.z = BitConverter.ToSingle(getByteArray(allBytes, 4), 0) * scaleFactor;
                        allBytes.RemoveRange(0, 4);
                        if (enableVerbose)
                            Debug.Log("Received Slice Thickness");
                        #endregion

                        #region Convert Data Buffer Size (Uint32)
                        initializationSettings.DataBufferSize = BitConverter.ToUInt32(getByteArray(allBytes, 4), 0);
                        allBytes.RemoveRange(0, 4);
                        if (enableVerbose)
                            Debug.Log("Received Data Buffer Size");
                        #endregion

                        #region Look for the Break Character
                        while (!cancelFlag)
                        {
                            float result = BitConverter.ToSingle(getByteArray(allBytes, 4), 0);
                            allBytes.RemoveRange(0, 4);

                            if (result == initializationBreakCharacter)
                            {
                                initializationSettings.initializationComplete = true;
                                initializationSettings.initializationUpdated = true;
                                break;
                            }
                            else
                            {
                                if (enableVerbose)
                                {
                                    Debug.Log("Received Non-Break Character: " + result);
                                }
                            }
                        }
                        #endregion

                        if (enableVerbose)
                        {
                            Debug.Log("Initialization Data Recieved Successfully");
                        }
                    }
                    else
                    {
                        Debug.Log("Initialization Failed Due to Incomplete Data Length - restarting...");
                    }
                    logger.WriteTimestampToLog("OnInitializationParsed");
                }

                if (enableVerbose)
                    Debug.Log("Finished using initialization client.");
            }

            if (enableVerbose)
                Debug.Log("Single shot initialization complete. Stopping Initialization Server.");
        }
        catch (Exception e)
        {
            if (enableVerbose)
            {
                Debug.Log("BROKE SOMETHING");
                Debug.Log(e.Message);
                Debug.Log(e.ToString());
            }
        }
        finally
        {
            server.Stop();
            if (enableVerbose)
                Debug.Log("Initialization Server stopped.");

        }
    }

    /// <summary>
    /// Create a smaller Byte array from a larger Byte list
    /// </summary>
    /// <param name="byteList"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public byte[] getByteArray(List<byte> byteList, int count)
    {
        byte[] temp = new byte[count];
        for (int i = 0; i < count; i++)
        {
            temp[i] = byteList[i];
        }
        return temp;
    }
}
