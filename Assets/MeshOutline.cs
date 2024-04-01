using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine;

public class MeshOutline : MonoBehaviour
{

    [System.Serializable]
    public class Edge
    {
        public Vector3 begin; 
        public Vector3 end;
    }
    [System.Serializable]
    public class Pair
    {
        public int first;
        public int second;
    }

    //public int speed;
    public Camera camera;
    private Mesh _mesh;
    private Edge[] localEdges;
    private float[][] positions;

    public float BasePeriod;
    public float BaseFrequency;
    public Notes.NoteSignature CurrentNoteIndex;
    public int edgesOffset;
    public int currentSpeed;

    public float[][] Positions { get { return positions; } }
    public Edge[] LocalEdges { get { return localEdges; } }
    

    void Start()
    {

        _mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = _mesh.vertices;
        int[] indices = _mesh.GetIndices(0);
        List<Vector3> verticesList = new List<Vector3>();
        List<List<int>> sameVertices = new List<List<int>>();
        for (int i = 0; i < vertices.Length; ++i)
        {
            int bestIndex = -1;
            for(int j = 0; j < verticesList.Count; ++j)
            {
                float currentDiff = (vertices[i] - verticesList[j]).magnitude;
                if (currentDiff < 0.00001f)
                {
                    bestIndex = j;
                }
            }
            if(bestIndex == -1)
            {
                verticesList.Add(vertices[i]);
                sameVertices.Add(new List<int> { i });
            }
            else
            {
                sameVertices[bestIndex].Add(i);
            }
        }

        Graph graph = new Graph(verticesList.Count);

        for (int i = 0; i < indices.Length; i += 3)
        {
            bool firstSet = false;
            bool secondSet = false;
            bool thirdSet = false;
            int firstIndex = indices[i + 0]; 
            int secondIndex = indices[i + 1];
            int thirdIndex = indices[i + 2];
            for (int listIndex = 0; listIndex < sameVertices.Count; ++listIndex)
            {
                List<int> currentSameVertices = sameVertices[listIndex];

                for (int indicesIndex = 0; indicesIndex < currentSameVertices.Count; ++indicesIndex)
                {
                    if (firstSet == false && currentSameVertices[indicesIndex] == firstIndex)
                    {
                        firstIndex = listIndex;
                        firstSet = true;
                    }
                    if (secondSet == false && currentSameVertices[indicesIndex] == secondIndex)
                    {
                        secondIndex = listIndex;
                        secondSet = true;
                    }
                    if (thirdSet == false && currentSameVertices[indicesIndex] == thirdIndex)
                    {
                        thirdIndex = listIndex;
                        thirdSet = true;
                    }
                    if (firstSet == true && secondSet == true && thirdSet == true)
                    {
                        break;
                    }

                }

                if (firstSet == true && secondSet == true && thirdSet == true)
                {
                    break;
                }
            }
            graph.AddSingleEdge(firstIndex, secondIndex, 1);
            graph.AddSingleEdge(secondIndex, thirdIndex, 1);
            graph.AddSingleEdge(firstIndex, thirdIndex, 1);
        }


        List<int> optimal = graph.FindOptimalRoute();
        localEdges = new Edge[optimal.Count - 1];
        for (int i = 0; i < optimal.Count - 1; i++)
        {
            Edge newEdge = new Edge();
            newEdge.begin = verticesList[optimal[i]];
            newEdge.end = verticesList[optimal[i + 1]];
            localEdges[i] = (newEdge);
        }
        positions = new float[(int)Notes.NoteSignature.Count][];
        //positions[0] = new float[speed*2*(localEdges.Length - 1)];
        BasePeriod = (float)(localEdges.Length) / 192000.0f;
        BaseFrequency = 1.0f / BasePeriod;

        int bestNoteIndex = 0;
        for (int i = 0; i < (int)Notes.NoteSignature.Count; ++i)
        {
            float diff = (BaseFrequency - Notes.frequencies[i]);
            if (diff > 0.0f)
            {
                bestNoteIndex = i;
            }
            else
            {
                break;
            }
        }
        bool bestPositionCountFound = false;
        int bestPositionCount = (localEdges.Length - 1);
        while (bestPositionCountFound == false)
        {
            int checkedPositionCount = bestPositionCount + 1;
            float checkedFrequency = 1.0f / (((float)checkedPositionCount) / 192000.0f);
            float checkedDiff = (checkedFrequency - Notes.frequencies[bestNoteIndex]);
            if (checkedDiff >= 0.0f)
            {
                bestPositionCount = checkedPositionCount;
            }
            else
            {
                bestPositionCountFound = true;
            }
        }

        for (int noteIndex = bestNoteIndex; noteIndex < (int)Notes.NoteSignature.Count; ++noteIndex)
        {
            positions[noteIndex] = new float[2 * bestPositionCount];
        }

        float nextBaseFrequency = BaseFrequency / 2;
        int steps = 1;

        for (int noteIndex = bestNoteIndex - 1; noteIndex >= 0; --noteIndex)
        {
            while (Notes.frequencies[noteIndex] <= nextBaseFrequency)
            {
                steps++;
                nextBaseFrequency = BaseFrequency / ((float)(steps + 1));
            }
            bestPositionCountFound = false;
            bestPositionCount = -1;
            while (bestPositionCountFound == false)
            {
                int checkedPositionCount = bestPositionCount + 1;
                float checkedFrequency = 1.0f / (((float)(steps*localEdges.Length + bestPositionCount)) / 192000.0f);
                float checkedDiff = (checkedFrequency - Notes.frequencies[noteIndex]);
                if (checkedDiff >= 0.0f)
                {
                    bestPositionCount = checkedPositionCount;
                }
                else
                {
                    bestPositionCountFound = true;
                }
            }
            positions[noteIndex] = new float[2 * (steps * localEdges.Length + bestPositionCount)];
        }


    }


    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.DownArrow) == true && CurrentNoteIndex != Notes.NoteSignature.C0)
        {
            CurrentNoteIndex = (Notes.NoteSignature)((int)CurrentNoteIndex - 1);
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) == true && CurrentNoteIndex != Notes.NoteSignature.B8)
        {
            CurrentNoteIndex = (Notes.NoteSignature)((int)CurrentNoteIndex + 1);
        }

        float[] currentNotePositions = positions[(int)CurrentNoteIndex];

        int positionsCount = currentNotePositions.Length / 2;
        float dSpeed = (float)positionsCount / (float)(localEdges.Length);
        edgesOffset = positionsCount - ((int)dSpeed) * localEdges.Length;
        currentSpeed = (int)dSpeed;
        float aspect = (float)Screen.width / (float)Screen.height;
        Matrix4x4 outlineMatrix = transform.localToWorldMatrix;
        Matrix4x4 cameraMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
        int edgeIndex = 0;
        float currSpeed = dSpeed;
        int speed = (int)Mathf.Round(currSpeed);
        int twoSpeed = 2 * speed;
        int baseSpeed = (int)dSpeed;
        int baseTwoSpeed = 2*baseSpeed;
        currSpeed -= (int)speed;
        int i;
        for (i = 0; i < currentNotePositions.Length; i += twoSpeed)
        {
            twoSpeed = 2 * speed;
            float dT = 1.0f / Mathf.Max((float)((int)dSpeed - 1), 1.0f);
            Vector3 begin = localEdges[edgeIndex].begin;
            Vector3 end = localEdges[edgeIndex].end;

            Vector3 result = begin;
            begin.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
            begin.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
            begin.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

            result = end;
            end.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
            end.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
            end.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

            result = begin;
            begin.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
            begin.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
            float w = 1.0f/(cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);

            float beginCamX = begin.x * w;
            float beginCamY = begin.y * w;

            result = end;
            end.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
            end.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
            w = 1.0f/(cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);


            float endCamX = end.x * w;
            float endCamY = end.y * w;

            beginCamX *= aspect;
            endCamX *= aspect;
            float diffX = dT*(endCamX - beginCamX);
            float diffY = dT*(endCamY - beginCamY);
            float currX = beginCamX;
            float currY = beginCamY;
            for (int j = 0; j < baseTwoSpeed; j += 2)
            {
                currentNotePositions[i + j + 0] = currX;
                currentNotePositions[i + j + 1] = currY;
                currX += diffX;
                currY += diffY;
            }
            currX -= diffX;
            currY -= diffY;
            for (int j = baseTwoSpeed; j < twoSpeed; j += 2)
            {

                currentNotePositions[i + j + 0] = currX;
                currentNotePositions[i + j + 1] = currY;
            }
            edgeIndex = (edgeIndex + 1) % localEdges.Length;
            currSpeed += dSpeed; 
            speed = (int)Mathf.Round(currSpeed);
            currSpeed -= (int)speed;
        }

        for (i = i; i < currentNotePositions.Length; i += twoSpeed)
        {
            float dT = 1.0f / Mathf.Max((float)(speed - 1), 1.0f);
            Vector3 begin = localEdges[edgeIndex].begin;
            Vector3 end = localEdges[edgeIndex].end;

            Vector3 result = begin;
            begin.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
            begin.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
            begin.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

            result = end;
            end.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
            end.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
            end.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

            result = begin;
            begin.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
            begin.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
            float w = 1.0f / (cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);

            float beginCamX = begin.x * w;
            float beginCamY = begin.y * w;

            result = end;
            end.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
            end.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
            w = 1.0f / (cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);


            float endCamX = end.x * w;
            float endCamY = end.y * w;

            beginCamX *= aspect;
            endCamX *= aspect;
            float diffX = dT * (endCamX - beginCamX);
            float diffY = dT * (endCamY - beginCamY);
            float currX = beginCamX;
            float currY = beginCamY;
            for (int j = 0; j < twoSpeed; j += 2)
            {
                if(i + j >= currentNotePositions.Length)
                {
                    break;
                }
                currentNotePositions[i + j + 0] = currX;
                currentNotePositions[i + j + 1] = currY;
                currX += diffX;
                currY += diffY;
            }
        }
    }

}
