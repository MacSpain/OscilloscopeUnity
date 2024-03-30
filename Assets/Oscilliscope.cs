using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Mathematics;

using static Unity.Mathematics.math;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using System;

public class Oscilliscope : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    struct ExampleVertex
    {
        public float3 pos;
        public float4 uv;
    }
    [System.Serializable]
    public class ProcessedEdge
    {
        public int outlineIndex;
        public int edgeIndex;
        public int edgePosition;
    }


    [SerializeField]
    private AudioSource source;
    [SerializeField]
    private float strokeWeight;
    [SerializeField]
    private float intensity;
    [SerializeField]
    private MeshOutline[] outlines;
    [SerializeField]
    private Camera cam;


    public MeshFilter meshFilter;

    private float[] samplesData;
    private int baseSampleIndex;


    private ExampleVertex[] tempVerts;
    private Mesh mesh;
    private const float EPS = 0.000001f;
    private Material mat;
    private AudioClip generatedClip;
    private ProcessedEdge[] processedEdges;


    void Start()
    {
        generatedClip = AudioClip.Create("Oscilloscope", 192000, 2, 192000, false);
        samplesData = new float[2*192000];
        source.clip = generatedClip;
        source.loop = true;
        source.Play();

        if (meshFilter != null)
        {
            MeshRenderer rend = meshFilter.gameObject.GetComponent<MeshRenderer>();
            mat = rend.material;
            mesh = new Mesh();
            int verticesCount = 4 * (192000 - 1);
            int triangleIndexCount = 6 * ((verticesCount / 4) - 1);

            NativeArray<uint> indices = new NativeArray<uint>(triangleIndexCount, Allocator.Temp);

            tempVerts = new ExampleVertex[verticesCount];
            float dT = 1.0f / ((float)192000);
            for (int i = 0; i < verticesCount / 4; ++i)
            {
                tempVerts[i * 4 + 0].uv = new Vector4(0.0f, (i * dT), (-1.0f), (-1.0f));
                tempVerts[i * 4 + 1].uv = new Vector4(0.0f, (i * dT), (-1.0f), (1.0f));
                tempVerts[i * 4 + 2].uv = new Vector4(1.0f, (i * dT), (1.0f), (-1.0f));
                tempVerts[i * 4 + 3].uv = new Vector4(1.0f, (i * dT), (1.0f), (1.0f));

                if (i != (verticesCount / 4) - 1)
                {
                    indices[6 * i + 0] = (uint)(i * 2 + 0);
                    indices[6 * i + 1] = (uint)(i * 2 + 3);
                    indices[6 * i + 2] = (uint)(i * 2 + 1);
                    indices[6 * i + 3] = (uint)(i * 2 + 0);
                    indices[6 * i + 4] = (uint)(i * 2 + 2);
                    indices[6 * i + 5] = (uint)(i * 2 + 3);
                }

            }

            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            };
            mesh.SetVertexBufferParams(verticesCount, layout);
            mesh.SetVertexBufferData(tempVerts, 0, 0, verticesCount);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            meshFilter.mesh = mesh;
            mesh.MarkDynamic();

        }
        processedEdges = new ProcessedEdge[192000];
        for(int i = 0; i < 192000; ++i)
        {
            processedEdges[i] = new ProcessedEdge();
        }
        GenerateSamples(0);

    }

    public void GenerateSamples(int newPosition)
    {
        int sampleIndex = 2*newPosition;
        int samplePos = newPosition;
        int k = processedEdges[newPosition].outlineIndex;
        int i = processedEdges[newPosition].edgeIndex;
        int basePosition = processedEdges[newPosition].edgePosition;
        float[][] positions = outlines[k].Positions;
        int twoSpeed = 2*outlines[k].speed;
        int length = samplesData.Length / 2;
        int samplesProcessed = length;
        float[] currPositions = positions[i];

        while (samplesProcessed > 0)
        {
            for (int j = basePosition; j < twoSpeed && samplesProcessed > 0; j += 2, samplesProcessed -= 2)
            {

                samplesData[sampleIndex] = currPositions[j];
                samplesData[sampleIndex + 1] = currPositions[j + 1];
                processedEdges[samplePos].outlineIndex = k;
                processedEdges[samplePos].edgeIndex = i;
                processedEdges[samplePos].edgePosition = j;
                sampleIndex = (sampleIndex + 2) % samplesData.Length;
                samplePos = (samplePos + 1) % length;
            }

            if (samplesProcessed <= 0)
            {
                break;
            }
            basePosition = 0;
            i++;
            if (i == positions.Length)
            {
                k = (k + 1) % outlines.Length;
                positions = outlines[k].Positions;
                twoSpeed = 2 * outlines[k].speed;
                i = 0;
            }
            currPositions = positions[i];

        }

        generatedClip.SetData(samplesData, 0);
    }

    void Update()
    {
        baseSampleIndex = 2 * source.timeSamples;

        GenerateSamples(source.timeSamples);
        if (baseSampleIndex > 0)
        {


            int verticesCount = 4 * (192000 - 1);
            float size = strokeWeight / 1000.0f;
            int beginSample = baseSampleIndex - (2 * 192000 - 1);
            int firstPartBeginSample = (beginSample < 0) ? 0 : beginSample;
            int secondPartBeginSample = (beginSample < 0) ? (2 * 192000 + beginSample) : (2 * 192000);
            int vertexIndex = 4*(19200 - 1) - 4;

            float x = 0.0f;
            float y = 0.0f;
            float prevX = 0.0f;
            float prevY = 0.0f;
            float diffX = 0.0f;
            float diffY = 0.0f;
            float perpDiffX = 0.0f;
            float perpDiffY = 0.0f;

            x = samplesData[baseSampleIndex + 0];
            y = samplesData[baseSampleIndex + 1];

            float3 tempVector = float3(0,0,0);

            for (int i = baseSampleIndex - 2; vertexIndex >= 0; i -= 2)
            {
                if(i < 0)
                {
                    i = 2 * 192000 - 2;
                }
                prevX = samplesData[i + 0];
                prevY = samplesData[i + 1];
                diffX = x - prevX;
                diffY = y - prevY;
                float length = Mathf.Sqrt(diffX * diffX + diffY * diffY);

                if (length > EPS)
                {
                    diffX = diffX / length;
                    diffY = diffY / length;
                }
                else
                {
                    diffX = 1.0f;
                    diffY = 0.0f;
                }

                perpDiffX = -diffY;
                perpDiffY = diffX;

                diffX *= size;
                diffY *= size;
                perpDiffX *= size;
                perpDiffY *= size;
                float twoPerpDiffX = 2.0f * perpDiffX;
                float twoPerpDiffY = 2.0f * perpDiffY;

                prevX -= diffX;
                prevY -= diffY;
                x += diffX;
                y += diffY;

                tempVector.x = prevX - perpDiffX;
                tempVector.y = prevY - perpDiffY;
                tempVector.z = length;
                tempVerts[vertexIndex + 0].pos = tempVector;
                tempVector.x += twoPerpDiffX;
                tempVector.y += twoPerpDiffY;
                tempVerts[vertexIndex + 1].pos = tempVector;
                tempVector.x = x - perpDiffX;
                tempVector.y = y - perpDiffY;
                tempVerts[vertexIndex + 2].pos = tempVector;
                tempVector.x += twoPerpDiffX;
                tempVector.y += twoPerpDiffY;
                tempVerts[vertexIndex + 3].pos = tempVector;

                vertexIndex -= 4;

                x = prevX;
                y = prevY;
            }

            //mesh.SetVertices(vertices);
            mesh.SetVertexBufferData(tempVerts, 0, 0, verticesCount, 0, MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontResetBoneBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontRecalculateBounds);
            //mesh.SetVertexBufferData<float3>(vertices, 0, 0, verticesCount);
            mat.SetFloat("_Size", size);

            float meshIntensity = intensity;
            float baseIntensity = Mathf.Max(0.0f, meshIntensity - 0.4f) * 0.7f - 1000.0f * size / 500.0f;
            mat.SetFloat("_IntensityBase", baseIntensity);
            mat.SetFloat("_Intensity", meshIntensity);
        }
    }
}
