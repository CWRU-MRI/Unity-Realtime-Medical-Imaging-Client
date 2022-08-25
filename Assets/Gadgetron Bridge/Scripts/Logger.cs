using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Events;

public class Logger : MonoBehaviour
{
    public string logFilepath;
    StreamWriter writer;
    public bool writeLogToFile = false;
    public bool printLogToConsole = false;
    public bool writeFrametimesToLog = false;

    public long startTicks;

    Queue<string> ToBeWritten;

    public void WriteTimestampToLog(string eventName)
    {
        float currentTimeMillis = (float)(DateTime.Now.Ticks - startTicks) / (float)TimeSpan.TicksPerMillisecond;
        ToBeWritten.Enqueue(currentTimeMillis + ", " + eventName);
    }

    // Start is called before the first frame update
    void Awake()
    {
        ToBeWritten = new Queue<string>();
        startTicks = DateTime.Now.Ticks; 
        if (writeLogToFile)
        {
            if (File.Exists(logFilepath))
            {
                logFilepath = logFilepath.Split('.')[0] + "_" + DateTime.Now.Minute + "_" + DateTime.Now.Second + ".csv";
            }
            writer = new StreamWriter(logFilepath);
            writer.AutoFlush = true;

            writer.WriteLine("----New Session Started---- (ElapsedTime|EventName)");
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < ToBeWritten.Count; i++)
        {
            string current = ToBeWritten.Dequeue();
            if (writeLogToFile)
                writer.WriteLine(current);
            if (printLogToConsole)
                print(current);
        }

    }

    private void OnApplicationQuit()
    {
        writer.Close();
    }

    public struct LoggingJob : IJob
    {
        public UnityAction action;
        public float time;
        public string eventName;

        public void Execute()
        {
            
        }
    }
}
