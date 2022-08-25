using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class framelogger : MonoBehaviour
{
    public RealtimePlanar realtimePlanar;
    public RealtimeVolumetric realtimeVolumetric;

    public Camera camera;

    private void Awake()
    {
        camera = this.GetComponent<Camera>();
    }



    RenderTexture myRenderTexture;
    void OnPreCull()
    {
        if (realtimePlanar != null)
        {
            realtimePlanar.LogStartRender();
        }
        if (realtimeVolumetric != null)
        {
            realtimeVolumetric.LogStartRender();
        }
    }
    void OnPostRender()
    {
        if (realtimePlanar != null)
        {
            realtimePlanar.LogFinishRender();
        }
        if (realtimeVolumetric != null)
        {
            realtimeVolumetric.LogFinishRender();
        }
    }

    //private void OnRenderImage(RenderTexture src, RenderTexture dest)
    //{
    //    if (realtimePlanar != null)
    //    {
    //        realtimePlanar.LogFrameInfo();
    //    }

    //    if (realtimeVolumetric != null)
    //    {
    //        realtimeVolumetric.LogFrameInfo();
    //    }
    //    Graphics.Blit(src, dest);
    //}
}
