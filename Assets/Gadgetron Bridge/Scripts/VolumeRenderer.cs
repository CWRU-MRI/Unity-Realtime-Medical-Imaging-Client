
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class VolumeRenderer : MonoBehaviour
{

    [SerializeField] protected Shader shader;
    protected Material material;

    [SerializeField] public Texture3D volume;

    [Header("UI Object Options")]
    public PinchSlider blackCutoffSlider;
    public PinchSlider raymarchAlphaSlider;
    public PinchSlider maxStepsSlider;


    protected void Awake()
    {
        material = new Material(shader);
        GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void Update()
    {
        material.SetTexture("_Volume", volume);
    }

    public void SetBlackCutoff()
    {
        material.SetFloat("_BlackCutoff", blackCutoffSlider.SliderValue);
    }

    public void SetRaymarchAlpha()
    {
        material.SetFloat("_RaymarchAlpha", raymarchAlphaSlider.SliderValue);
    }

    public void SetMaxSteps()
    {
        material.SetFloat("_MaxSteps", maxStepsSlider.SliderValue * 256);
    }

    void OnDestroy()
    {
        Destroy(material);
    }

}



