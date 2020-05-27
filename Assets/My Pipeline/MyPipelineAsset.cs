using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName ="Rendering/My Pipeline")]
public class MyPipelineAsset : RenderPipelineAsset
{
    public enum ShadowMapSize
    {
        _256=256,
        _512=512,
        _1024=1024,
        _2048=2048,
        _4096=4096
    }
    public enum ShadowCascades
    {
        Zero=0,
        Two=2,
        Four=4
    }

    public enum MSAAMode
    {
        Off=1,
        _2x=2,
        _4x=4,
        _8x=8
    }
    [SerializeField]
    ShadowMapSize shadowMapSize=ShadowMapSize._1024;
    [SerializeField]
    float shadowDistance = 100f;
    [SerializeField]
    ShadowCascades shadowCascades = ShadowCascades.Four;
    [SerializeField,HideInInspector]
    float twoCasadesSplit = 0.25f;
    [SerializeField,HideInInspector]
    Vector3 fourCasadesSplit = new Vector3(0.067f,0.2f,0.467f);
    [SerializeField]
    bool dynamicBatching;
    [SerializeField]
    bool instanceing;
    [SerializeField,Range(0.25f, 2f)]
    float renderScale = 1f;
    [SerializeField]
    MSAAMode MSAA = MSAAMode.Off;
    [SerializeField]
    bool allowHDR;
    [SerializeField]
    Texture2D ditherTexture = null;
    [SerializeField, Range(0f, 120f)]
    float ditherAnimationSpeed = 30f;
    [SerializeField,Range(0.01f,2f)] //阴影距离
    float shadowFadeRange = 1f;
    [SerializeField]
    bool supportLODCrossFading = true;
    [SerializeField]
    MyPostProcessingStack defaultStack;
    public bool HasShadowCascades
    {
        get { return shadowCascades != ShadowCascades.Zero; }
    }
    public bool HasLODCrossFading
    {
        get { return supportLODCrossFading;}
    }
    /// <summary>
    /// 创建Asset资源
    /// </summary>
    /// <returns></returns>
    protected override IRenderPipeline InternalCreatePipeline()
    {
        Vector3 shadowCascadesSplit = shadowCascades == ShadowCascades.Four ? fourCasadesSplit : new Vector3(twoCasadesSplit,0f);
        return new MyPipeline(dynamicBatching, instanceing, defaultStack,ditherTexture, ditherAnimationSpeed,(int)shadowMapSize, shadowDistance, shadowFadeRange,(int)shadowCascades, shadowCascadesSplit, renderScale, (int)MSAA, allowHDR);
    }
}
