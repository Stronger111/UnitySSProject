using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;  //规定在编辑器模式下启作用
public class MyPipeline : RenderPipeline
{
    #region 配置信息
    /// <summary>
    /// 阴影贴图大小
    /// </summary>
    int shadowMapSize;
    #endregion
    CommandBuffer cameraBuffer = new CommandBuffer { name = "Render Camera" };

    CommandBuffer shadowBuffer = new CommandBuffer { name = "Render Shadows" };
    CullResults cull;

    Material errorMaterial;
    /// <summary>
    /// Draw Render Flags
    /// </summary>
    DrawRendererFlags drawFlags;
    /// <summary>
    /// 支持最大可见光数量
    /// </summary>
    const int maxVisibleLights = 16;
    static int visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    /// <summary>
    /// 灯光方向和位置  点光源也显示正确的光源方向 
    /// </summary>
    static int visibleLightDirectionsOrPositionId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    /// <summary>
    /// 设置点灯光的衰减范围
    /// </summary>
    static int visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    /// <summary>
    /// 聚光灯方向
    /// </summary>
    static int visibleLightSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");
    /// <summary>
    /// Unity灯光索引的数量
    /// </summary>
    static int lightIndicesOffsetAndCountID = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");

    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];

    #region 距离衰减  衰减范围

    #endregion
    #region 阴影贴图
    RenderTexture shadowMap;
    static int shadowMapId = Shader.PropertyToID("_ShadowMap");
    /// <summary>
    /// 0.05和0.01。
    /// </summary>
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    static int shadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    /// <summary>
    /// 世界空间到阴影裁剪空间
    /// </summary>
    static int worldToShadowMatrixId = Shader.PropertyToID("_WorldToShadowMatrix");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");
    const string shadowSoftKeyword = "_SHADOWS_SOFT";
    #endregion
    public MyPipeline(bool dynamicBatching, bool instanceing,int shadowMapSize)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        if (dynamicBatching)
            drawFlags = DrawRendererFlags.EnableDynamicBatching;

        if (instanceing)
            drawFlags |= DrawRendererFlags.EnableInstancing;
        this.shadowMapSize = shadowMapSize;
    }
    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);

        foreach (var camera in cameras)
        {
            Render(renderContext, camera);
        }
    }


    void Render(ScriptableRenderContext context, Camera camera)
    {
        ScriptableCullingParameters cullingParameters;

        if (!CullResults.GetCullingParameters(camera, out cullingParameters))
            return;
        //UI Scene
#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
#endif

        CullResults.Cull(ref cullingParameters, context, ref cull);
        //开始渲染阴影图
        RenderShadows(context);
        //正确设置渲染物体的VP矩阵 unity_MatrixVP
        context.SetupCameraProperties(camera);

        CameraClearFlags clearFlags = camera.clearFlags;

        cameraBuffer.ClearRenderTarget(
            (clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor
            );
        if (cull.visibleLights.Count > 0)
        {
            //配置灯光
            ConfigureLights();
        }
        else
        {
            cameraBuffer.SetGlobalVector(lightIndicesOffsetAndCountID, Vector4.zero); //还原数据
        }
        cameraBuffer.BeginSample("Render Camera");

        cameraBuffer.SetGlobalVectorArray(visibleLightColorsId, visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirectionsOrPositionId, visibleLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsId, visibleLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visibleLightSpotDirectionsId, visibleLightSpotDirections);

        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"))
        {
            flags = drawFlags,
            //rendererConfiguration = RendererConfiguration.PerObjectLightIndices8
        };
        if (cull.visibleLights.Count > 0)
        {
            drawSettings.rendererConfiguration = RendererConfiguration.PerObjectLightIndices8;
        }
        drawSettings.sorting.flags = SortFlags.CommonOpaque;
        //先画不透明物体
        var filterSettings = new FilterRenderersSettings(true)
        { renderQueueRange = RenderQueueRange.opaque };

        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
        //开始画天空盒
        context.DrawSkybox(camera);
        drawSettings.sorting.flags = SortFlags.CommonTransparent;
        //画半透明
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

        DrawDefaultPipline(context, camera);

        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();
        if (shadowMap)
        {
            RenderTexture.ReleaseTemporary(shadowMap);
            shadowMap = null;
        }
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    void DrawDefaultPipline(ScriptableRenderContext contex, Camera camera)
    {
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));

        drawSettings.SetOverrideMaterial(errorMaterial, 0);
        var filterSettings = new FilterRenderersSettings(true);

        contex.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }
    /// <summary>
    /// 配置灯光环境
    /// </summary>
    void ConfigureLights()
    {
        for (int i = 0; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisibleLights)  //超过最大灯光数量跳出循环
                break;
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1;
            VisibleLight light = cull.visibleLights[i];
            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorld.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visibleLightDirectionsOrPositions[i] = v;
            }
            else
            {
                visibleLightDirectionsOrPositions[i] = light.localToWorld.GetColumn(3);  //获取点光源位置
                attenuation.x = 1f / Mathf.Max(light.range * light.range, 0.00001f);

                if (light.lightType == LightType.Spot)
                {
                    Vector4 v = light.localToWorld.GetColumn(2);
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visibleLightSpotDirections[i] = v;
                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;
                }
            }
            visibleLightColors[i] = light.finalColor;
            visibleLightAttenuations[i] = attenuation;

        }
        //for (; i < maxVisibleLights; i++)   //还原清除后面灯光颜色(0,0,0,0)
        //{
        //    visibleLightColors[i] = Color.clear;
        //}
        //告诉Unity 超出最大灯光数量的索引设置为-1使其不起作用
        if (cull.visibleLights.Count > maxVisibleLights)
        {
            int[] lightIndices = cull.GetLightIndexMap();
            for (int i = maxVisibleLights; i < cull.visibleLights.Count; i++)
            {
                lightIndices[i] = -1;
            }
            cull.SetLightIndexMap(lightIndices);
        }
    }

    void RenderShadows(ScriptableRenderContext context)
    {
        shadowMap = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMap.filterMode = FilterMode.Bilinear;  //双线性过滤
        shadowMap.wrapMode = TextureWrapMode.Clamp;  //限制最后一个像素

        CoreUtils.SetRenderTarget(shadowBuffer, shadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Depth);
        shadowBuffer.BeginSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
        //V P矩阵
        Matrix4x4 viewMatrix, projectionMatrix;
        ShadowSplitData splitData;
        cull.ComputeSpotShadowMatricesAndCullingPrimitives(0, out viewMatrix, out projectionMatrix, out splitData);
        shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        shadowBuffer.SetGlobalFloat(shadowBiasId,cull.visibleLights[0].light.shadowBias);
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        var shadowSettings = new DrawShadowsSettings(cull, 0);
        context.DrawShadows(ref shadowSettings);
        if (SystemInfo.usesReversedZBuffer)
        {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }
        var scaleOffset = Matrix4x4.TRS(Vector3.one * 0.5f, Quaternion.identity, Vector3.one * 0.5f);
        // var scaleOffset=Matrix4x4.identity
        //scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        //scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
        Matrix4x4 worldShadowMatrix = scaleOffset*(projectionMatrix * viewMatrix);
        shadowBuffer.SetGlobalMatrix(worldToShadowMatrixId, worldShadowMatrix);
        shadowBuffer.SetGlobalTexture(shadowMapId, shadowMap);
        shadowBuffer.SetGlobalFloat(shadowStrengthId,cull.visibleLights[0].light.shadowStrength);
        float invShadowMapSize = 1 / shadowMapSize;
        shadowBuffer.SetGlobalVector(shadowMapSizeId,new Vector4(invShadowMapSize,invShadowMapSize,shadowMapSizeId,shadowMapSizeId));

        CoreUtils.SetKeyword(shadowBuffer,shadowSoftKeyword,cull.visibleLights[0].light.shadows==LightShadows.Soft);

        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }
}
