using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Blender : MonoBehaviour
{
    public ComputeShader compute;
    public Camera cam;
    public RenderTexture input;
    private RenderTexture output;
    public MeshRenderer rend;
    public Color overlay;
    [Range(0f, 1f)]
    public float afterGlow;
    public float threshold;
    private Material mat;

    private int kernel;
    private bool hasKernel = false;

    void Start()
    {
        kernel = compute.FindKernel("CSMain");
        hasKernel = compute.HasKernel("CSMain");

        mat = rend.material;
        output = new RenderTexture(input.width, input.height, 0)
        {
            enableRandomWrite = true
        };
        output.format = RenderTextureFormat.ARGBFloat;
        output.Create();
        mat.SetTexture("_BaseMap", output);
    }

    // Update is called once per frame
    void Update()
    {

        compute.SetTexture(kernel, "Result", output);
        compute.SetTexture(kernel, "ImageInput", input);
        compute.SetVector("color", overlay);
        float timePassed = Mathf.Min(afterGlow*(1.0f - Time.deltaTime), 0.99f);
        compute.SetFloat("timePassed", timePassed);
        compute.SetFloat("thresholdValue", threshold);
        compute.Dispatch(kernel, input.width / 8, input.height / 8, 1);
    }
}
