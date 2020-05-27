using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
/// <summary>
/// 后期处理
/// </summary>
[CreateAssetMenu(menuName ="Rendering/My Post-Processing Stack")]
public class MyPostProcessingStack : ScriptableObject
{
    static Mesh fullScreenTriangle;
    static Material material;
    static int mainTexId = Shader.PropertyToID("_MainTex");
    static int depthTexId = Shader.PropertyToID("_DepthTex");

    static int tempTexId = Shader.PropertyToID("_MyPostProcessingStackTempTex");
    static int resolvedTexId = Shader.PropertyToID("_MyPostProcessingStackResolvedTex");
    [SerializeField,Range(1,10)]
    int blurStength;
    [SerializeField]
    bool depthStripes;
    [SerializeField]
    bool toneMapping;
    [SerializeField, Range(1f, 100f)]
    float toneMappingRange = 100f;
    enum Pass
    {
        Copy,
        Blur,
        DepthStripes,
        ToneMapping
    };
    public bool NeedsDepth
    {
        get { return depthStripes; }
    }
    static void InitializeStatic()
    {
        if (fullScreenTriangle)
            return;
        fullScreenTriangle = new Mesh
        {
            name = "My Post-Process Stack Full-Screen Triangle",
            vertices = new Vector3[] { new Vector3(-1f,-1f,0f),
                          new Vector3(-1f,3f,0f),new Vector3(3f,-1f,0f)              },
            triangles=new int[] { 0,1,2},
        };
        material = new Material(Shader.Find("Hidden/My Pipeline/PostEffectStack")) { name="My Post-Processing Stack material",hideFlags=HideFlags.HideAndDontSave};
    }
   public void RenderAfterTransparent(CommandBuffer cb,int cameraColorId,int cameraDepthId,int width,int height,int samples, RenderTextureFormat format)
    {
        //InitializeStatic();
        //Blit(cb, cameraDepthId, cameraColorId, Pass.DepthStripes);
       // DepthStripes(cb,cameraColorId,cameraDepthId,width,height);
        if (blurStength>0)
        {
            if(toneMapping|| samples>1)
            {
                cb.GetTemporaryRT(resolvedTexId,width,height,0,FilterMode.Bilinear);
                if(toneMapping)
                {
                    ToneMapping(cb,cameraColorId, resolvedTexId);
                }
                else
                {
                    Blit(cb, cameraColorId, resolvedTexId);
                }
                Blur(cb, resolvedTexId, width, height);
                cb.ReleaseTemporaryRT(resolvedTexId);
            }
            else
            {
                Blur(cb, cameraColorId, width, height);
            }
            
        }else if(toneMapping)
        {
            ToneMapping(cb,cameraColorId,BuiltinRenderTextureType.CameraTarget);
        }
        else
        {
            Blit(cb, cameraColorId, BuiltinRenderTextureType.CameraTarget);
        }
        //cb.GetTemporaryRT(tempTexId,width,height,0,FilterMode.Bilinear);
        //Blit(cb,cameraColorId, tempTexId,Pass.Blur);
        //Blit(cb, tempTexId, BuiltinRenderTextureType.CameraTarget, Pass.Blur);
        //cb.ReleaseTemporaryRT(tempTexId);
    }

    public void RenderAfterOpaque(CommandBuffer cb, int cameraColorId, int cameraDepthId,
        int width, int height, int samples, RenderTextureFormat format)
    {
        InitializeStatic();
        if(depthStripes)
        {
            DepthStripes(cb, cameraColorId, cameraDepthId, width, height, format);
        }
    }
     
    public void RenderAfterTransparent()
    {

    }
    void Blit(CommandBuffer cb,
        RenderTargetIdentifier sourceId, RenderTargetIdentifier destinationId,
        Pass pass = Pass.Copy)
    {
        cb.SetGlobalTexture(mainTexId, sourceId);
        //cb.Blit(cameraColorId, BuiltinRenderTextureType.CameraTarget);//自己本身已经设置渲染目标啦
        cb.SetRenderTarget(destinationId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cb.DrawMesh(fullScreenTriangle, Matrix4x4.identity, material, 0, (int)pass);
    }

    void Blur(CommandBuffer cb,int cameraColorId,int width,int height)
    {
        cb.BeginSample("Blur");
        if (blurStength == 1)
        {
            Blit(
                cb, cameraColorId, BuiltinRenderTextureType.CameraTarget, Pass.Blur
            );
            cb.EndSample("Blur");
            return;
        }
        cb.GetTemporaryRT(tempTexId, width, height, 0, FilterMode.Bilinear);
        int passLeft;
        for (passLeft=blurStength;passLeft>2;passLeft-=2)
        {
            Blit(cb, cameraColorId, tempTexId, Pass.Blur);
            Blit(cb, tempTexId, cameraColorId, Pass.Blur);
        }
        if(passLeft > 1)
        {
            Blit(cb,cameraColorId,tempTexId,Pass.Blur);
            Blit(cb,tempTexId, BuiltinRenderTextureType.CameraTarget,Pass.Blur);
        }else
        {
            Blit(cb, cameraColorId, BuiltinRenderTextureType.CameraTarget, Pass.Blur);
        }
        cb.ReleaseTemporaryRT(tempTexId);
        cb.EndSample("Blur");
    }

    void DepthStripes(CommandBuffer cb,int cameraColorId,int cameraDepthId,int width,int height, RenderTextureFormat format)
    {
        cb.BeginSample("Depth Stripes");
        cb.GetTemporaryRT(tempTexId,width,height,0,FilterMode.Point,format);
        cb.SetGlobalTexture(depthTexId,cameraDepthId);
        Blit(cb,cameraColorId,tempTexId,Pass.DepthStripes);
        Blit(cb,tempTexId,cameraColorId);
        cb.ReleaseTemporaryRT(tempTexId);
        cb.EndSample("Depth Stripes");
    }

    void ToneMapping(CommandBuffer cb,RenderTargetIdentifier sourceId,RenderTargetIdentifier destinationId)
    {
        cb.BeginSample("Tone Mapping");
        cb.SetGlobalFloat(
            "_ReinhardModifier", 1f / (toneMappingRange * toneMappingRange)
        );
        Blit(cb, sourceId, destinationId, Pass.ToneMapping);
        cb.EndSample("Tone Mapping");
    }
}
