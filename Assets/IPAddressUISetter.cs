using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class IPAddressUISetter : MonoBehaviour
{
    public InitializationHandler handler;
    public TMP_Text ipText;
    // Update is called once per frame
    void Update()
    {
        ipText.text = "Local IP Address: " + handler.manualIP + Environment.NewLine +
                      "Initialization Port: " + handler.initializationPort + Environment.NewLine +
                      "Image Port: " + (handler.initializationPort + 1);
    }
}
