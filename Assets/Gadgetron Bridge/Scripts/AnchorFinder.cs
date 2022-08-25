using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorFinder : MonoBehaviour {
    [Header("Settings")]
    public Vector3 offset = new Vector3(0,0,0);
    public Vector3 BarrelRoll = new Vector3(-1, 1, 1);
    public bool DoABarrelRoll = false;

    public bool usePostion = true;
    public bool useRotation = true;

    [Header("Found Characteristics")]
    public Vector3 AnchorPosition;
    public Quaternion AnchorRotation;
    public Vector3 AnchorScale;

    void Start () {
        AnchorPosition = FindObjectOfType<InitializationHandler>().anchor.transform.position;
        AnchorRotation = FindObjectOfType<InitializationHandler>().anchor.transform.rotation;
        AnchorScale = FindObjectOfType<InitializationHandler>().anchor.transform.localScale;

        if(usePostion)
            this.transform.position = AnchorPosition + offset;
        if(useRotation)
            this.transform.rotation = AnchorRotation;
        if (DoABarrelRoll)
        {
            this.transform.localScale = Vector3.Scale(AnchorScale,BarrelRoll);
        }
        else
        {
            this.transform.localScale = AnchorScale;
        }

        FindObjectOfType<InitializationHandler>().anchor.gameObject.SetActive(false);
    }

    private void Update()
    {
        //if (DoABarrelRoll)
        //{
        //    this.transform.localScale = Vector3.Scale(AnchorScale, BarrelRoll);
        //    DoABarrelRoll = false;
        //}
        //this.transform.position = AnchorPosition + offset;

        //if (facePlayer)
        //{
        //    this.transform.LookAt(Camera.main.transform);
        //}
    }


}
