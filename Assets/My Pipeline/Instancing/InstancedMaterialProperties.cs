using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstancedMaterialProperties : MonoBehaviour
{
    [SerializeField]
    Color color = Color.white;

    [SerializeField, Range(0f, 1f)]
    float smoothness = 0.5f;

    [SerializeField, Range(0f, 1f)]
    float metallic;
    [SerializeField, ColorUsage(false, true)]
    Color emissionColor = Color.black;

    [SerializeField]
    float pulseEmissionFreqency;

    static MaterialPropertyBlock propertyBlock;
    static int colorID = Shader.PropertyToID("_Color");
    static int smoothnessId = Shader.PropertyToID("_Smoothness");
    static int metallicId = Shader.PropertyToID("_Metallic");
    static int emissionColorId = Shader.PropertyToID("_EmissionColor");
    void Awake()
    {
        OnValidate();
        if (pulseEmissionFreqency <= 0f)
        {
            enabled = false;
        }

    }
    void OnValidate()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(emissionColorId,emissionColor);
        propertyBlock.SetColor(colorID, color);
        propertyBlock.SetFloat(metallicId,metallic);
        propertyBlock.SetFloat(smoothnessId,smoothness);
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Color originalEmissionColor = emissionColor;
        emissionColor *= 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * pulseEmissionFreqency * Time.time);
        OnValidate();
        //GetComponent<MeshRenderer>().UpdateGIMaterials();
        DynamicGI.SetEmissive(GetComponent<MeshRenderer>(), emissionColor);
        emissionColor = originalEmissionColor;
    }
}
