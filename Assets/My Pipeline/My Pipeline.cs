using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;  //规定在编辑器模式下启作用
public class MyPipeline : RenderPipeline
{
    CommandBuffer cameraBuffer = new CommandBuffer { name = "Render Camera" };

    CullResults cull;

    Material errorMaterial;
    /// <summary>
    /// Draw Render Flags
    /// </summary>
    DrawRendererFlags drawFlags;
    /// <summary>
    /// 支持最大可见光数量
    /// </summary>
    const int maxVisibleLights = 4;
    static int visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    /// <summary>
    /// 灯光方向和位置  点光源也显示正确的光源方向 
    /// </summary>
    static int visibleLightDirectionsOrPositionId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    /// <summary>
    /// 设置灯光的衰减范围
    /// </summary>
    static int visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    #region 距离衰减  衰减范围

    #endregion
    public MyPipeline(bool dynamicBatching,bool instanceing)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        if (dynamicBatching)
            drawFlags = DrawRendererFlags.EnableDynamicBatching;

        if (instanceing)
            drawFlags |= DrawRendererFlags.EnableInstancing;
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

        CullResults.Cull(ref cullingParameters,context,ref cull);
        //正确设置渲染物体的VP矩阵 unity_MatrixVP
        context.SetupCameraProperties(camera);

        CameraClearFlags clearFlags = camera.clearFlags;
     
        cameraBuffer.ClearRenderTarget(
            (clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor
            );
        //配置灯光
        ConfigureLights();
        cameraBuffer.BeginSample("Render Camera");

        cameraBuffer.SetGlobalVectorArray(visibleLightColorsId,visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirectionsOrPositionId, visibleLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsId,visibleLightAttenuations);
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        var drawSettings = new DrawRendererSettings(camera,new ShaderPassName("SRPDefaultUnlit"));

        drawSettings.flags = drawFlags;

        drawSettings.sorting.flags = SortFlags.CommonOpaque;
        //先画不透明物体
        var filterSettings = new FilterRenderersSettings(true)
        { renderQueueRange= RenderQueueRange.opaque };

        context.DrawRenderers(cull.visibleRenderers, ref drawSettings,filterSettings);
        //开始画天空盒
        context.DrawSkybox(camera);
        drawSettings.sorting.flags = SortFlags.CommonTransparent;
        //画半透明
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

        DrawDefaultPipline(context,camera);

        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    void DrawDefaultPipline(ScriptableRenderContext contex,Camera camera)
    {
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var drawSettings = new DrawRendererSettings(camera,new ShaderPassName("ForwardBase"));
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));

        drawSettings.SetOverrideMaterial(errorMaterial, 0);
        var filterSettings = new FilterRenderersSettings(true);

        contex.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }

    void ConfigureLights()
    {
        int i = 0;
        for (;i<cull.visibleLights.Count;i++)
        {
            if (i == maxVisibleLights)  //超过最大灯光数量跳出循环
                break;
            Vector4 attenuation = Vector4.zero;
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
            }
            visibleLightColors[i] = light.finalColor;
            visibleLightAttenuations[i] = attenuation;
        }
        for (; i < maxVisibleLights; i++)   //还原清除后面灯光颜色(0,0,0,0)
        {
            visibleLightColors[i] = Color.clear;
        }
    }
}
