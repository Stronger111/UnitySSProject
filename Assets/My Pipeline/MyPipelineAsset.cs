using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName ="Rendering/My Pipeline")]
public class MyPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool dynamicBatching;
    [SerializeField]
    bool instanceing;
    /// <summary>
    /// 创建Asset资源
    /// </summary>
    /// <returns></returns>
    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new MyPipeline(dynamicBatching, instanceing);
    }
}
