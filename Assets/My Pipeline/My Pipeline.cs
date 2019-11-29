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
    float shadowDistance;
    int shadowCascades;
    Vector3 shadowCascadeSplit;
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
    RenderTexture shadowMap, cascadedShadowMap;
    static int shadowMapId = Shader.PropertyToID("_ShadowMap");
    static int cascadeShadowMapId = Shader.PropertyToID("_CascadedShadowMap");
    static int globalShadowDataId = Shader.PropertyToID("_GlobalShadowData");
    /// <summary>
    /// 0.05和0.01。
    /// </summary>
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    //static int shadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    static int shadowDataId = Shader.PropertyToID("_ShadowData");
    /// <summary>
    /// 世界空间到阴影裁剪空间
    /// </summary>
    //static int worldToShadowMatrixId = Shader.PropertyToID("_WorldToShadowMatrix");
    static int worldToShadowMatricesId = Shader.PropertyToID("_WorldToShadowMatrices");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");
    const string shadowSoftKeyword = "_SHADOWS_SOFT";
    const string shadowsHardKeyword = "_SHADOWS_HARD";
    Matrix4x4[] worldToShadowMatrices = new Matrix4x4[maxVisibleLights];
    /// <summary>
    /// 阴影数据
    /// </summary>
    Vector4[] shadowData = new Vector4[maxVisibleLights];
    int shadowTileCount;
    /// <summary>
    /// 主光源是否存在
    /// </summary>
    bool mainLightExists;
    static int worldToShadowCascadeMatricesId = Shader.PropertyToID("_WorldToShadowCascadeMatrices");
    Matrix4x4[] worldToShadowCascadeMatrices = new Matrix4x4[4];
    #endregion
    public MyPipeline(bool dynamicBatching, bool instanceing, int shadowMapSize, float shadowDistance, int shadowCascades, Vector3 shadowCascadeSplit)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        if (dynamicBatching)
            drawFlags = DrawRendererFlags.EnableDynamicBatching;

        if (instanceing)
            drawFlags |= DrawRendererFlags.EnableInstancing;
        this.shadowMapSize = shadowMapSize;
        this.shadowDistance = shadowDistance;
        this.shadowCascades = shadowCascades;
        this.shadowCascadeSplit = shadowCascadeSplit;
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
        cullingParameters.shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane);
        //UI Scene
#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
#endif

        CullResults.Cull(ref cullingParameters, context, ref cull);
        if (cull.visibleLights.Count > 0)
        {
            //配置灯光
            ConfigureLights();
            if (mainLightExists)
                RenderCascadedShadows(context);
            if (shadowTileCount > 0)
            {  //开始渲染阴影图
                RenderShadows(context);
            }
            else
            {
                //禁用掉硬软阴影
                cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
                cameraBuffer.DisableShaderKeyword(shadowSoftKeyword);
            }
        }
        else
        {
            cameraBuffer.SetGlobalVector(lightIndicesOffsetAndCountID, Vector4.zero); //还原数据
            cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
            cameraBuffer.DisableShaderKeyword(shadowSoftKeyword);
        }
        ConfigureLights();
        //正确设置渲染物体的VP矩阵 unity_MatrixVP
        context.SetupCameraProperties(camera);

        CameraClearFlags clearFlags = camera.clearFlags;

        cameraBuffer.ClearRenderTarget(
            (clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor
            );

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
        if (cascadedShadowMap)
        {
            RenderTexture.ReleaseTemporary(cascadedShadowMap);
            cascadedShadowMap = null;
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
        mainLightExists = false;
        shadowTileCount = 0;
        for (int i = 0; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisibleLights)  //超过最大灯光数量跳出循环
                break;
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1;
            VisibleLight light = cull.visibleLights[i];
            Vector4 shadow = Vector4.zero;
            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorld.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visibleLightDirectionsOrPositions[i] = v;
                shadow = ConfigureShadows(i, light.light);
                shadow.z = 1f;  //标识处理方向光
                if (i == 0 && shadow.x > 0f && shadowCascades > 0)
                {
                    mainLightExists = true;
                    shadowTileCount -= 1;
                }
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

                    //阴影
                    //Light shadowLight = light.light;
                    //Bounds shadowBounds;
                    //if (shadowLight.shadows != LightShadows.None && cull.GetShadowCasterBounds(i, out shadowBounds))
                    //{
                    //    shadowTileCount += 1;
                    //    shadow.x = shadowLight.shadowStrength;  //x分量存阴影强度
                    //    shadow.y = shadowLight.shadows == LightShadows.Soft ? 1f : 0f;
                    //}
                    shadow = ConfigureShadows(i, light.light);
                }
            }
            visibleLightColors[i] = light.finalColor;
            visibleLightAttenuations[i] = attenuation;
            shadowData[i] = shadow;

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
        int split;
        if (shadowTileCount <= 1)
            split = 1;
        else if (shadowTileCount <= 4)
            split = 2;
        else if (shadowTileCount <= 9)
            split = 3;
        else
            split = 4;
        float tileSize = shadowMapSize / split;
        float tileScale = 1f / split;
        Rect tileViewport = new Rect(0f, 0f, tileSize, tileSize);

        shadowMap = SetShadowRenderTarget();

        shadowBuffer.BeginSample("Render Shadows");
        shadowBuffer.SetGlobalVector(globalShadowDataId, new Vector4(tileScale, shadowDistance * shadowDistance));
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
        int tileIndex = 0;
        bool hardShadows = false;
        bool softShadows = false;
        for (int i = mainLightExists ? 1 : 0; i < cull.visibleLights.Count; i++)  //+
        {
            if (i == maxVisibleLights)
                break;
            if (shadowData[i].x <= 0f)
                break;
            //V P矩阵
            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;
            //cull.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData);
            bool validShadows;
            if (shadowData[i].z > 0f) //为方向光
            {
                validShadows = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.right, (int)tileSize, cull.visibleLights[i].light.shadowNearPlane,
                                    out viewMatrix, out projectionMatrix, out splitData);
            }
            else
            {
                validShadows = cull.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData);
            }
            if (!validShadows)
            {
                shadowData[i].x = 0f;
                continue;
            }
            //配置ShadowTile
            Vector2 tileOffset = ConfigureShadowTile(tileIndex, split, tileSize);

            shadowData[i].z = tileOffset.x * tileScale;
            shadowData[i].w = tileOffset.y * tileScale;

            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            shadowBuffer.SetGlobalFloat(shadowBiasId, cull.visibleLights[i].light.shadowBias);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            var shadowSettings = new DrawShadowsSettings(cull, i);
            shadowSettings.splitData.cullingSphere = splitData.cullingSphere;
            context.DrawShadows(ref shadowSettings);
            //计算世界到阴影图矩阵
            CalculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out worldToShadowMatrices[i]);
            tileIndex += 1;
            if (shadowData[i].y <= 0f)
            {
                hardShadows = true;
            }
            else
            {
                softShadows = true;
            }
        }
        //if (split > 1)
        //{
        shadowBuffer.DisableScissorRect();
        //}
        shadowBuffer.SetGlobalTexture(shadowMapId, shadowMap);
        //shadowBuffer.SetGlobalFloat(shadowStrengthId, cull.visibleLights[0].light.shadowStrength);
        shadowBuffer.SetGlobalMatrixArray(worldToShadowMatricesId, worldToShadowMatrices);
        shadowBuffer.SetGlobalVectorArray(shadowDataId, shadowData);
        float invShadowMapSize = 1 / shadowMapSize;
        shadowBuffer.SetGlobalVector(shadowMapSizeId, new Vector4(invShadowMapSize, invShadowMapSize, shadowMapSizeId, shadowMapSizeId));

        CoreUtils.SetKeyword(shadowBuffer, shadowsHardKeyword, hardShadows);
        CoreUtils.SetKeyword(shadowBuffer, shadowSoftKeyword, softShadows);

        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }
    /// <summary>
    /// 配置阴影数据
    /// </summary>
    /// <param name="lightIndex"></param>
    /// <param name="shadowLight"></param>
    /// <returns></returns>
    Vector4 ConfigureShadows(int lightIndex, Light shadowLight)
    {
        Vector4 shadow = Vector4.zero;
        Bounds shadowBounds;
        if (shadowLight.shadows != LightShadows.None && cull.GetShadowCasterBounds(lightIndex, out shadowBounds))
        {
            shadowTileCount += 1;
            shadow.x = shadowLight.shadowStrength;  //x分量存阴影强度
            shadow.y = shadowLight.shadows == LightShadows.Soft ? 1f : 0f;
        }
        return shadow;
    }

    RenderTexture SetShadowRenderTarget()
    {
        RenderTexture texture = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        texture.filterMode = FilterMode.Bilinear;  //双线性过滤
        texture.wrapMode = TextureWrapMode.Clamp;  //限制最后一个像素
        CoreUtils.SetRenderTarget(shadowBuffer, texture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Depth);
        return texture;
    }

    Vector2 ConfigureShadowTile(int tileIndex, int split, float tileSize)
    {
        Vector2 tileOffset;
        tileOffset.x = tileIndex % split;
        tileOffset.y = tileIndex / split;
        var tileViewport = new Rect(tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize);
        shadowBuffer.SetViewport(tileViewport);
        shadowBuffer.EnableScissorRect(new Rect(tileViewport.x + 4f, tileViewport.y + 4f, tileSize - 8f, tileSize - 8f));
        return tileOffset;
    }

    void CalculateWorldToShadowMatrix(ref Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix, out Matrix4x4 worldToShadowMatrix)
    {
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
        //Matrix4x4 worldShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
        worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
    }


    void RenderCascadedShadows(ScriptableRenderContext context)
    {
        float tileSize = shadowMapSize / 2;
        cascadedShadowMap = SetShadowRenderTarget();

        shadowBuffer.BeginSample("Render Shadows");
        shadowBuffer.SetGlobalVector(globalShadowDataId, new Vector4(0f, shadowDistance * shadowDistance));
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        Light shadowLight = cull.visibleLights[0].light;
        shadowBuffer.SetGlobalFloat(shadowBiasId, shadowLight.shadowBias);

        var shadowSettings = new DrawShadowsSettings(cull, 0);
        var tileMatrix = Matrix4x4.identity;
        tileMatrix.m00 = tileMatrix.m11 = 0.5f;

        for (int i = 0; i < shadowCascades; i++)
        {
            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(0, i, shadowCascades, shadowCascadeSplit, (int)tileSize, shadowLight.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);

            Vector2 tileOffset = ConfigureShadowTile(i, 2, tileSize);
            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            shadowSettings.splitData.cullingSphere = splitData.cullingSphere;
            context.DrawShadows(ref shadowSettings);

            //计算世界到阴影图矩阵
            CalculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out worldToShadowCascadeMatrices[i]);

            tileMatrix.m03 = tileOffset.x * 0.5f;
            tileMatrix.m13 = tileOffset.y * 0.5f;
            worldToShadowCascadeMatrices[i] = tileMatrix * worldToShadowCascadeMatrices[i];

        }

        shadowBuffer.DisableScissorRect();

        shadowBuffer.SetGlobalTexture(cascadeShadowMapId, cascadedShadowMap);

        shadowBuffer.SetGlobalMatrixArray(worldToShadowCascadeMatricesId, worldToShadowCascadeMatrices);
        float invShadowMapSize = 1 / shadowMapSize;


        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }

}
