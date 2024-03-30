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

public class OscilliscopeTest : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public float3 position, normal;
        public half4 tangent;
        public float4 texCoord0;
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

    private Vertex[] vertexArray;
    private uint[] indicesArray;
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

            int vertexAttributeCount = 4;
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            int verticesCount = 4*(192000 - 1);
            int triangleIndexCount = 6 * ((verticesCount / 4) - 1);
            vertexArray = new Vertex[verticesCount];
            indicesArray = new uint[triangleIndexCount];


            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(
                vertexAttributeCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory
            );
            vertexAttributes[0] = new VertexAttributeDescriptor(dimension: 3);
            vertexAttributes[1] = new VertexAttributeDescriptor(
                VertexAttribute.Normal, dimension: 3
            );
            vertexAttributes[2] = new VertexAttributeDescriptor(
                VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4
            );
            vertexAttributes[3] = new VertexAttributeDescriptor(
                VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4
            );
            meshData.SetVertexBufferParams(verticesCount, vertexAttributes);
            vertexAttributes.Dispose();
            meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt32);

            NativeArray<Vertex> verticesData = meshData.GetVertexData<Vertex>();
            NativeArray<uint> indices = meshData.GetIndexData<uint>();

            float dT = 1.0f / ((float)192000);
            for (int i = 0; i < verticesCount / 4; ++i)
            {
                vertexArray[i * 4 + 0] = new Vertex();
                vertexArray[i * 4 + 0].texCoord0 = float4(0.0f, (i * dT), (-1.0f), (-1.0f));
                vertexArray[i * 4 + 1] = new Vertex();
                vertexArray[i * 4 + 1].texCoord0 = float4(0.0f, (i * dT), (-1.0f), (1.0f));
                vertexArray[i * 4 + 2] = new Vertex();
                vertexArray[i * 4 + 2].texCoord0 = float4(1.0f, (i * dT), (1.0f), (-1.0f));
                vertexArray[i * 4 + 3] = new Vertex();
                vertexArray[i * 4 + 3].texCoord0 = float4(1.0f, (i * dT), (1.0f), (1.0f));

                if (i != (verticesCount / 4) - 1)
                {
                    indicesArray[6 * i + 0] = (uint)(i * 2 + 0);
                    indicesArray[6 * i + 1] = (uint)(i * 2 + 3);
                    indicesArray[6 * i + 2] = (uint)(i * 2 + 1);
                    indicesArray[6 * i + 3] = (uint)(i * 2 + 0);
                    indicesArray[6 * i + 4] = (uint)(i * 2 + 2);
                    indicesArray[6 * i + 5] = (uint)(i * 2 + 3);
                }

            }

            for(int i = 0; i < verticesCount; ++i)
            {
                verticesData[i] = vertexArray[i];
            }
            for (int i = 0; i < triangleIndexCount; ++i)
            {
                indices[i] = indicesArray[i];
            }

            var bounds = new Bounds(new Vector3(0.5f, 0.5f), new Vector3(1f, 1f));
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount)
            {
                bounds = bounds,
                vertexCount = verticesCount
            }, MeshUpdateFlags.DontRecalculateBounds);

            var mesh = new Mesh
            {
                bounds = bounds,
                name = "Procedural Mesh",
                indexFormat = IndexFormat.UInt32
            };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            meshFilter.mesh = mesh;

            //mesh.SetVertices(vertices);
            //mesh.SetUVs(0, uvs);
            //mesh.SetIndices(indices, MeshTopology.Triangles, 0);
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
        int samplesProcessed = 0;
        int k = processedEdges[newPosition].outlineIndex;
        int i = processedEdges[newPosition].edgeIndex;
        int basePosition = processedEdges[newPosition].edgePosition;
        float[][] positions = outlines[k].Positions;
        int twoSpeed = 2*outlines[k].speed;
        int length = samplesData.Length / 2;
        float[] currPositions = positions[i];

        while (samplesProcessed < length)
        {
            for(int j = basePosition; j < twoSpeed; j += 2)
            {

                samplesData[sampleIndex] = currPositions[j + 0];
                samplesData[sampleIndex + 1] = currPositions[j + 1];
                int samplePos = sampleIndex / 2;
                processedEdges[samplePos].outlineIndex = k;
                processedEdges[samplePos].edgeIndex = i;
                processedEdges[samplePos].edgePosition = j;
                sampleIndex = (sampleIndex + 2) % samplesData.Length;
                samplesProcessed += 2;
                if(samplesProcessed >= length)
                {
                    break;
                }
            }

            if (samplesProcessed >= length)
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

            int vertexAttributeCount = 4;
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];
            int verticesCount = 4 * (192000 - 1);
            int triangleIndexCount = 6 * ((verticesCount / 4) - 1);
            
            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(
                vertexAttributeCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory
            );
            vertexAttributes[0] = new VertexAttributeDescriptor(dimension: 3);
            vertexAttributes[1] = new VertexAttributeDescriptor(
                VertexAttribute.Normal, dimension: 3
            );
            vertexAttributes[2] = new VertexAttributeDescriptor(
                VertexAttribute.Tangent, VertexAttributeFormat.Float16, 4
            );
            vertexAttributes[3] = new VertexAttributeDescriptor(
                VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4
            );
            meshData.SetVertexBufferParams(verticesCount, vertexAttributes);
            vertexAttributes.Dispose();
            meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt32);

            NativeArray<Vertex> verticesData = meshData.GetVertexData<Vertex>();
            NativeArray<uint> indices = meshData.GetIndexData<uint>();


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

            for (int i = baseSampleIndex - 2; i >= firstPartBeginSample && vertexIndex >= 0; i -= 2)
            {
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

                prevX -= diffX;
                prevY -= diffY;
                x += diffX;
                y += diffY;

                vertexArray[vertexIndex + 0].position = float3(prevX - perpDiffX, prevY - perpDiffY, length);
                vertexArray[vertexIndex + 1].position = float3(prevX + perpDiffX, prevY + perpDiffY, length);
                vertexArray[vertexIndex + 2].position = float3(x - perpDiffX, y - perpDiffY, length);
                vertexArray[vertexIndex + 3].position = float3(x + perpDiffX, y + perpDiffY, length);

                vertexIndex -= 4;

                x = prevX;
                y = prevY;
            }

            for (int i = 2 * 192000 - 2; i >= secondPartBeginSample && vertexIndex >= 0; i -= 2)
            {
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

                prevX -= diffX;
                prevY -= diffY;
                x += diffX;
                y += diffY;

                vertexArray[vertexIndex + 0].position = float3(prevX - perpDiffX, prevY - perpDiffY, length);
                vertexArray[vertexIndex + 1].position = float3(prevX + perpDiffX, prevY + perpDiffY, length);
                vertexArray[vertexIndex + 2].position = float3(x - perpDiffX, y - perpDiffY, length);
                vertexArray[vertexIndex + 3].position = float3(x + perpDiffX, y + perpDiffY, length);

                vertexIndex -= 4;

                x = prevX;
                y = prevY;
            }

            for (int i = 0; i < verticesCount; ++i)
            {
                verticesData[i] = vertexArray[i];
            }
            for (int i = 0; i < triangleIndexCount; ++i)
            {
                indices[i] = indicesArray[i];
            }

            var bounds = new Bounds(new Vector3(0.5f, 0.5f), new Vector3(1f, 1f));
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndexCount)
            {
                bounds = bounds,
                vertexCount = verticesCount
            }, MeshUpdateFlags.DontRecalculateBounds);

            var mesh = new Mesh
            {
                bounds = bounds,
                name = "Procedural Mesh",
                indexFormat = IndexFormat.UInt32
            };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            meshFilter.mesh = mesh;

            //mesh.SetVertices(vertices);
            //meshFilter.mesh = mesh;
            //mat.SetFloat("_Size", size);

            float meshIntensity = intensity;
            float baseIntensity = Mathf.Max(0.0f, meshIntensity - 0.4f) * 0.7f - 1000.0f * size / 500.0f;
            mat.SetFloat("_IntensityBase", baseIntensity);
            mat.SetFloat("_Intensity", meshIntensity);
        }
    }
}
