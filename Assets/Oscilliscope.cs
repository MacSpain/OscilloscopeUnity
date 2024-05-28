using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;

using UnityEngine.Rendering;
using static NoteSheetSO;
using TMPro;

public class Oscilliscope : MonoBehaviour
{
    [System.Serializable]
    public class ProcessedEdge
    {
        public int outlineIndex;
        public int edgePosition;
        public Notes.NoteSignature note;
    }


    [SerializeField]
    private AudioSource source;
    [SerializeField]
    private float strokeWeight;
    [SerializeField]
    private float intensity;
    [SerializeField]
    private GameObject outlinesObject;
    [SerializeField]
    private Camera cam;
    [SerializeField]
    private Blender blender;
    [SerializeField]
    private NoteSheetSO sheet;


    public MeshFilter meshFilter;

    private MeshOutline[] outlines;
    private float[] samplesData;
    private int baseSampleIndex;

    private int frequency = 192000;
    private float3[] vertices;
    private Mesh mesh;
    private const float EPS = 0.000001f;
    private Material mat;
    private AudioClip generatedClip;
    private ProcessedEdge[] processedEdges;
    private int oldPosition;
    private int secondsElapsed;


    void Start()
    {
        outlines = outlinesObject.GetComponentsInChildren<MeshOutline>(true);
        generatedClip = AudioClip.Create("Oscilloscope", frequency, 2, frequency, false);
        samplesData = new float[2* frequency];
        source.clip = generatedClip;
        source.loop = true;
        source.Play();
        secondsElapsed = 0;

        if (meshFilter != null)
        {
            MeshRenderer rend = meshFilter.gameObject.GetComponent<MeshRenderer>();
            mat = rend.material;
            mesh = new Mesh();
            int verticesCount = 4 * (frequency - 1);
            int triangleIndexCount = 6 * ((verticesCount / 4));

            NativeArray<uint> indices = new NativeArray<uint>(triangleIndexCount, Allocator.Temp);

            Vector4[] tempVerts = new Vector4[verticesCount];
            vertices = new float3[verticesCount];
            float dT = 1.0f / ((float)frequency);
            for (int i = 0; i < verticesCount / 4; ++i)
            {
                tempVerts[i * 4 + 0] = new Vector4(0.0f, (i * dT), (-1.0f), (-1.0f));
                tempVerts[i * 4 + 1] = new Vector4(0.0f, (i * dT), (-1.0f), (1.0f));
                tempVerts[i * 4 + 2] = new Vector4(1.0f, (i * dT), (1.0f), (-1.0f));
                tempVerts[i * 4 + 3] = new Vector4(1.0f, (i * dT), (1.0f), (1.0f));

                indices[6 * i + 0] = (uint)(i * 4 + 0);
                indices[6 * i + 1] = (uint)(i * 4 + 3);
                indices[6 * i + 2] = (uint)(i * 4 + 1);
                indices[6 * i + 3] = (uint)(i * 4 + 0);
                indices[6 * i + 4] = (uint)(i * 4 + 2);
                indices[6 * i + 5] = (uint)(i * 4 + 3);

            }

            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream:0),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4, stream:1),
            };
            mesh.SetVertexBufferParams(verticesCount, layout);
            mesh.SetVertexBufferData(tempVerts, 0, 0, verticesCount, 1);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            meshFilter.mesh = mesh;
            mesh.MarkDynamic();

        }
        processedEdges = new ProcessedEdge[frequency];
        for(int i = 0; i < frequency; ++i)
        {
            processedEdges[i] = new ProcessedEdge();
        }
        GenerateSamples(0);

    }

    public void GenerateSamples(int newPosition)
    {
        if(newPosition < oldPosition)
        {
            secondsElapsed++;
        }
        float secsElapsed = (float)secondsElapsed + (((float)newPosition) / (float)frequency);
        float dT = 1.0f / (float)frequency;
        oldPosition = newPosition;
        int sampleIndex = 2*newPosition;
        int samplePos = newPosition;
        int k = processedEdges[samplePos].outlineIndex;
        Notes.NoteSignature currentNote = Notes.NoteSignature.C0;
        float currentPatternBeat = 0;
        if (sheet != null)
        {
            currentNote = processedEdges[samplePos].note;
            float bpmStep = (float)sheet.BPM / 60f;
            currentPatternBeat = bpmStep * secsElapsed;
            if (secsElapsed == 0.0f)
            {
                currentNote = sheet.activeTicks[0].events[0].note;
            }
        }
        else
        {
            currentNote = outlines[k].CurrentNoteIndex;

        }
        int basePosition = processedEdges[samplePos].edgePosition;

        outlines[k].ComputeNewPositions(currentNote);

        float[] positions = outlines[k].Positions[(int)currentNote];
        int positionsCount = positions.Length;
        int samplesProcessed = samplesData.Length;
        int samplesCount = samplesData.Length;
        int length = processedEdges.Length;

        while (samplesProcessed > 0)
        {
            for (int j = basePosition; j < positionsCount && samplesProcessed > 0; j += 2, samplesProcessed -= 2)
            {
                samplesData[sampleIndex] = positions[j];
                samplesData[sampleIndex + 1] = positions[j + 1];
                processedEdges[samplePos].outlineIndex = k;
                processedEdges[samplePos].edgePosition = j;
                processedEdges[samplePos].note = currentNote;
                sampleIndex = (sampleIndex + 2) % samplesCount;
                samplePos = (samplePos + 1) % length;
                secsElapsed += dT;

            }

            if (samplesProcessed <= 0)
            {
                break;
            }
            basePosition = 0;
            k = (k + 1) % outlines.Length;

            if (sheet != null)
            {
                int currentPatternIndex = (((int)currentPatternBeat) >> 6) % sheet.activeTicks.Length;
                int currentPatternBeatIndex = (int)currentPatternBeat & 0x3F;

                bool activeFound = false;
                while (activeFound == false)
                {
                    for (currentPatternBeatIndex = currentPatternBeatIndex; currentPatternBeatIndex >= 0; --currentPatternBeatIndex)
                    {
                        NoteEvent currentNoteEvent = sheet.activeTicks[currentPatternIndex].events[currentPatternBeatIndex];
                        if (currentNoteEvent.active == true)
                        {
                            activeFound = true;
                            currentNote = currentNoteEvent.note;
                            break;
                        }
                    }
                    --currentPatternIndex;
                    currentPatternBeatIndex = 63;
                }
            }

            outlines[k].ComputeNewPositions(currentNote);
            positions = outlines[k].Positions[(int)currentNote];
            positionsCount = positions.Length;

        }

        generatedClip.SetData(samplesData, 0);
    }

    void Update()
    {
        baseSampleIndex = 2 * source.timeSamples;
        int oldPos = oldPosition;
        GenerateSamples(source.timeSamples);
        if (baseSampleIndex > 0)
        {

            int vertexIndexDiff;
            if (source.timeSamples < oldPos)
            {
                vertexIndexDiff = (4 * (source.timeSamples + (frequency - oldPos)));
            }
            else
            {
                vertexIndexDiff = (4 * (source.timeSamples - oldPos));
            }

            float size = strokeWeight / 1000.0f;
            int vertexIndex = 4 * (frequency - 2);
            int endVertexIndex = vertexIndex - vertexIndexDiff;

            float x;
            float y;
            float prevX;
            float prevY;
            float diffX;
            float diffY;

            x = samplesData[baseSampleIndex + 0];
            y = samplesData[baseSampleIndex + 1];

            float3[] tempVector = 
            { 
                Vector3.zero,
                Vector3.zero,
            };

            for (int i = 0; i < endVertexIndex; i++)
            {
                vertices[i] = vertices[i + vertexIndexDiff];
            }

            for (int i = baseSampleIndex - 2; vertexIndex >= endVertexIndex; i -= 2)
            {
                if (i < 0)
                {
                    i = 2 * frequency - 2;
                }
                prevX = samplesData[i + 0];
                prevY = samplesData[i + 1];

                tempVector[0].x = prevX;
                tempVector[0].y = prevY;
                tempVector[1].x = x;
                tempVector[1].y = y;

                diffX = x - prevX;
                diffY = y - prevY;

                x = prevX;
                y = prevY;

                float length = Mathf.Sqrt(diffX * diffX + diffY * diffY);

                tempVector[0].z = length;
                tempVector[1].z = length;

                if (length > EPS)
                {
                    diffX = (size * diffX) / length;
                    diffY = (size * diffY) / length;
                }
                else
                {
                    diffX = size;
                    diffY = 0.0f;
                }

                tempVector[0].x -= diffX - diffY;
                tempVector[0].y -= diffY + diffX;
                tempVector[1].x += diffX + diffY;
                tempVector[1].y += diffY - diffX;

                float twoDiffX = 2.0f * diffX;
                float twoDiffY = 2.0f * diffY;



                vertices[vertexIndex + 0] = tempVector[0];
                tempVector[0].x += -twoDiffY;
                tempVector[0].y += twoDiffX;
                vertices[vertexIndex + 1] = tempVector[0];
                vertices[vertexIndex + 2] = tempVector[1];
                tempVector[1].x += -twoDiffY;
                tempVector[1].y += twoDiffX;
                vertices[vertexIndex + 3] = tempVector[1];

                vertexIndex -= 4;

            }

            int verticesCount = 4 * (frequency - 1);
            mesh.SetVertexBufferData(vertices, 0, 0, verticesCount, 0, MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontResetBoneBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontRecalculateBounds);
            mat.SetFloat("_Size", size);

            float meshIntensity = intensity;
            float baseIntensity = Mathf.Max(0.0f, meshIntensity - 0.4f) * 0.7f - 1000.0f * size / 500.0f;
            mat.SetFloat("_IntensityBase", baseIntensity);
            mat.SetFloat("_Intensity", meshIntensity);
        }
    }
}
