using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

public class MeshOutline : MonoBehaviour
{

    [System.Serializable]
    public class Pair : IEquatable<Pair>
    {
        public int first;
        public int second;
        public Pair(int firstSet, int secondSet)
        {
            first = firstSet;
            second = secondSet;
        }

        public bool Equals(Pair other)
        {
            if(other.first == first && other.second == second)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    private Transform[] children;
    //public int speed;
    public Camera camera;
    private Mesh _mesh;
    public Vector3[] path;
    private float[][] positions;

    public Notes.NoteSignature CurrentNoteIndex;
    public List<Pair> allPairs;
    public bool[] forbiddenPairs;
    public List<Vector3> verticesList;
    public List<List<int>> sameVertices;
    public bool looping = true;

    public float[][] Positions { get { return positions; } }

    private void Start()
    {
        int linesCount = looping == true ? path.Length : path.Length - 1;
        positions = new float[(int)Notes.NoteSignature.Count][];
        float BasePeriod = (float)(linesCount) / 192000.0f;
        float BaseFrequency = 1.0f / BasePeriod;

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
        int bestPositionCount = (linesCount - 1);
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

        float nextBaseFrequency = BaseFrequency;
        int steps = 1;

        for (int noteIndex = bestNoteIndex - 1; noteIndex >= 0; --noteIndex)
        {
            while (Notes.frequencies[noteIndex] <= nextBaseFrequency)
            {
                steps++;
                nextBaseFrequency = BaseFrequency / ((float)(steps));
            }
            bestPositionCountFound = false;
            bestPositionCount = -1;
            while (bestPositionCountFound == false)
            {
                int checkedPositionCount = bestPositionCount + 1;
                float checkedFrequency = 1.0f / (((float)(steps * linesCount + bestPositionCount)) / 192000.0f);
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
            positions[noteIndex] = new float[2 * (steps * linesCount + bestPositionCount)];
        }
    }
    public void GatherVertices()
    {
        _mesh = GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = _mesh.vertices;
        verticesList = new List<Vector3>();
        sameVertices = new List<List<int>>();
        allPairs = new List<Pair>();
        for (int i = 0; i < vertices.Length; ++i)
        {
            int bestIndex = -1;
            for (int j = 0; j < verticesList.Count; ++j)
            {
                float currentDiff = (vertices[i] - verticesList[j]).magnitude;
                if (currentDiff < 0.00001f)
                {
                    bestIndex = j;
                }
            }
            if (bestIndex == -1)
            {
                verticesList.Add(vertices[i]);
                sameVertices.Add(new List<int> { i });
            }
            else
            {
                sameVertices[bestIndex].Add(i);
            }
        }

        int[] indices = _mesh.GetIndices(0);

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
            Pair newPair = (firstIndex < secondIndex) ? new Pair(firstIndex, secondIndex) : new Pair(secondIndex, firstIndex);
            bool found = false;
            for (int pairIndex = 0; pairIndex < allPairs.Count; ++pairIndex)
            {
                if (allPairs[pairIndex].Equals(newPair) == true)
                {
                    found = true;
                    break;
                }
            }
            if (found == false)
            {
                bool added = false;
                for (int pairIndex = 0; pairIndex < allPairs.Count; ++pairIndex)
                {
                    Pair currentPair = allPairs[pairIndex];
                    if(newPair.first > currentPair.first || (currentPair.first == newPair.first && newPair.second > currentPair.second))
                    { 
                        allPairs.Insert(pairIndex, newPair);
                        added = true;
                        break;
                    }
                }

                if (added == false)
                {
                    allPairs.Add(newPair);
                }
            }
            newPair = (secondIndex < thirdIndex) ? new Pair(secondIndex, thirdIndex) : new Pair(thirdIndex, secondIndex);
            found = false;
            for (int pairIndex = 0; pairIndex < allPairs.Count; ++pairIndex)
            {
                if (allPairs[pairIndex].Equals(newPair) == true)
                {
                    found = true;
                    break;
                }
            }
            if (found == false)
            {
                bool added = false;
                for (int pairIndex = 0; pairIndex < allPairs.Count; ++pairIndex)
                {
                    Pair currentPair = allPairs[pairIndex];
                    if (newPair.first > currentPair.first || (currentPair.first == newPair.first && newPair.second > currentPair.second))
                    {
                        allPairs.Insert(pairIndex, newPair);
                        added = true;
                        break;
                    }
                }

                if (added == false)
                {
                    allPairs.Add(newPair);
                }
            }
            newPair = (firstIndex < thirdIndex) ? new Pair(firstIndex, thirdIndex) : new Pair(thirdIndex, firstIndex);
            found = false;
            for (int pairIndex = 0; pairIndex < allPairs.Count; ++pairIndex)
            {
                if (allPairs[pairIndex].Equals(newPair) == true)
                {
                    found = true;
                    break;
                }
            }
            if (found == false)
            {
                bool added = false;
                for (int pairIndex = 0; pairIndex < allPairs.Count; ++pairIndex)
                {
                    Pair currentPair = allPairs[pairIndex];
                    if(newPair.first > currentPair.first || (currentPair.first == newPair.first && newPair.second > currentPair.second))
                    { 
                        allPairs.Insert(pairIndex, newPair);
                        added = true;
                        break;
                    }
                }

                if (added == false)
                {
                    allPairs.Add(newPair);
                }
            }

        }

        forbiddenPairs = new bool[allPairs.Count];

    }
    public void MakeEdges()
    {

        Graph graph = new Graph(verticesList.Count);

        for(int i = 0; i < allPairs.Count; ++i)
        {
            Pair currentPair = allPairs[i];
            if (forbiddenPairs[i] == false)
            {
                graph.AddSingleEdge(currentPair.first, currentPair.second, 1);
            }
        }


        List<int> optimal = graph.FindOptimalRoute();
        path = new Vector3[optimal.Count - 1];
        for (int i = 0; i < optimal.Count - 1; i++)
        {
            path[i] = verticesList[optimal[i]];
        }
        

    }

    private void OnDrawGizmos()
    {
        if(allPairs != null && allPairs.Count > 0)
        {
            Vector3 basePos = transform.position;
            for(int i = 0; i < allPairs.Count; ++i)
            {
                if (forbiddenPairs[i] == false)
                {
                    Vector3 firstVertex = verticesList[allPairs[i].first];
                    Vector3 secondVertex = verticesList[allPairs[i].second];
                    Gizmos.DrawLine(basePos + firstVertex, basePos + secondVertex);
                }
            }
        }
        children = GetComponentsInChildren<Transform>();
        for(int i = 1; i < children.Length; ++i)
        {
            Gizmos.DrawSphere(children[i].position, 0.025f);
            if(i == children.Length - 1)
            {

                if (looping == true)
                {
                    Gizmos.DrawLine(children[i].position, children[1].position);
                }
            }
            else
            {

                Gizmos.DrawLine(children[i].position, children[i + 1].position);
            }
        }
    }

    public void ComputeNewPositions(Notes.NoteSignature newNoteIndex)
    {
        int linesCount = looping == true ? path.Length : path.Length - 1;
        //CurrentNoteIndex = newNoteIndex;
        float[] currentNotePositions = positions[(int)newNoteIndex];
        int positionsCount = currentNotePositions.Length / 2;
        float dSpeed = (float)positionsCount / (float)(linesCount);
        float aspect = (float)Screen.width / (float)Screen.height;
        Matrix4x4 outlineMatrix = transform.localToWorldMatrix;
        Matrix4x4 cameraMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
        int edgeIndex = 0;
        int nextEdgeIndex = 1;
        float currSpeed = dSpeed;
        int speed = (int)Mathf.Round(currSpeed);
        int twoSpeed = 2 * speed;
        int baseSpeed = (int)dSpeed;
        int baseTwoSpeed = 2 * baseSpeed;
        currSpeed -= (int)speed;
        int i;
        for (i = 0; i < currentNotePositions.Length; i += twoSpeed)
        {
            twoSpeed = 2 * speed;
            float dT = 1.0f / Mathf.Max((float)((int)dSpeed - 1), 1.0f);
            Vector3 begin = path[edgeIndex];
            Vector3 end = path[nextEdgeIndex];

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
            edgeIndex = (edgeIndex + 1) % path.Length;
            nextEdgeIndex = (nextEdgeIndex + 1) % path.Length;
            currSpeed += dSpeed;
            speed = (int)Mathf.Round(currSpeed);
            currSpeed -= (int)speed;
        }

        

    }
    
    public void PathFromChildren()
    {
        Transform[] pathChildren = GetComponentsInChildren<Transform>();
        path = new Vector3[pathChildren.Length - 1];
        for(int i = 1; i < pathChildren.Length; i++)
        {
            path[i - 1] = pathChildren[i].localPosition;
        }
    }

    private void Update()
    {

        //float[] currentNotePositions = positions[(int)CurrentNoteIndex];

        //int positionsCount = currentNotePositions.Length / 2;
        //float dSpeed = (float)positionsCount / (float)(localEdges.Length);
        //float aspect = (float)Screen.width / (float)Screen.height;
        //Matrix4x4 outlineMatrix = transform.localToWorldMatrix;
        //Matrix4x4 cameraMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;
        //int edgeIndex = 0;
        //float currSpeed = dSpeed;
        //int speed = (int)Mathf.Round(currSpeed);
        //int twoSpeed = 2 * speed;
        //int baseSpeed = (int)dSpeed;
        //int baseTwoSpeed = 2*baseSpeed;
        //currSpeed -= (int)speed;
        //int i;
        //for (i = 0; i < currentNotePositions.Length; i += twoSpeed)
        //{
        //    twoSpeed = 2 * speed;
        //    float dT = 1.0f / Mathf.Max((float)((int)dSpeed - 1), 1.0f);
        //    Vector3 begin = localEdges[edgeIndex].begin;
        //    Vector3 end = localEdges[edgeIndex].end;

        //    Vector3 result = begin;
        //    begin.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
        //    begin.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
        //    begin.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

        //    result = end;
        //    end.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
        //    end.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
        //    end.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

        //    result = begin;
        //    begin.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
        //    begin.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
        //    float w = 1.0f/(cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);

        //    float beginCamX = begin.x * w;
        //    float beginCamY = begin.y * w;

        //    result = end;
        //    end.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
        //    end.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
        //    w = 1.0f/(cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);


        //    float endCamX = end.x * w;
        //    float endCamY = end.y * w;

        //    beginCamX *= aspect;
        //    endCamX *= aspect;
        //    float diffX = dT*(endCamX - beginCamX);
        //    float diffY = dT*(endCamY - beginCamY);
        //    float currX = beginCamX;
        //    float currY = beginCamY;
        //    for (int j = 0; j < baseTwoSpeed; j += 2)
        //    {
        //        currentNotePositions[i + j + 0] = currX;
        //        currentNotePositions[i + j + 1] = currY;
        //        currX += diffX;
        //        currY += diffY;
        //    }
        //    currX -= diffX;
        //    currY -= diffY;
        //    for (int j = baseTwoSpeed; j < twoSpeed; j += 2)
        //    {

        //        currentNotePositions[i + j + 0] = currX;
        //        currentNotePositions[i + j + 1] = currY;
        //    }
        //    edgeIndex = (edgeIndex + 1) % localEdges.Length;
        //    currSpeed += dSpeed; 
        //    speed = (int)Mathf.Round(currSpeed);
        //    currSpeed -= (int)speed;
        //}

        //for (i = i; i < currentNotePositions.Length; i += twoSpeed)
        //{
        //    float dT = 1.0f / Mathf.Max((float)(speed - 1), 1.0f);
        //    Vector3 begin = localEdges[edgeIndex].begin;
        //    Vector3 end = localEdges[edgeIndex].end;

        //    Vector3 result = begin;
        //    begin.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
        //    begin.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
        //    begin.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

        //    result = end;
        //    end.x = outlineMatrix.m00 * result.x + outlineMatrix.m01 * result.y + outlineMatrix.m02 * result.z + outlineMatrix.m03;
        //    end.y = outlineMatrix.m10 * result.x + outlineMatrix.m11 * result.y + outlineMatrix.m12 * result.z + outlineMatrix.m13;
        //    end.z = outlineMatrix.m20 * result.x + outlineMatrix.m21 * result.y + outlineMatrix.m22 * result.z + outlineMatrix.m23;

        //    result = begin;
        //    begin.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
        //    begin.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
        //    float w = 1.0f / (cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);

        //    float beginCamX = begin.x * w;
        //    float beginCamY = begin.y * w;

        //    result = end;
        //    end.x = cameraMatrix.m00 * result.x + cameraMatrix.m01 * result.y + cameraMatrix.m02 * result.z + cameraMatrix.m03;
        //    end.y = cameraMatrix.m10 * result.x + cameraMatrix.m11 * result.y + cameraMatrix.m12 * result.z + cameraMatrix.m13;
        //    w = 1.0f / (cameraMatrix.m30 * result.x + cameraMatrix.m31 * result.y + cameraMatrix.m32 * result.z + cameraMatrix.m33);


        //    float endCamX = end.x * w;
        //    float endCamY = end.y * w;

        //    beginCamX *= aspect;
        //    endCamX *= aspect;
        //    float diffX = dT * (endCamX - beginCamX);
        //    float diffY = dT * (endCamY - beginCamY);
        //    float currX = beginCamX;
        //    float currY = beginCamY;
        //    for (int j = 0; j < twoSpeed; j += 2)
        //    {
        //        if(i + j >= currentNotePositions.Length)
        //        {
        //            break;
        //        }
        //        currentNotePositions[i + j + 0] = currX;
        //        currentNotePositions[i + j + 1] = currY;
        //        currX += diffX;
        //        currY += diffY;
        //    }
        //}
    }

}

#if UNITY_EDITOR
[CustomEditor(typeof(MeshOutline))]
public class MeshOutlineEditor : Editor
{

    public override void OnInspectorGUI()
    {
        MeshOutline myTarget = (target as MeshOutline);

        base.OnInspectorGUI();

        if (GUILayout.Button("Setup graph"))
        {
            myTarget.GatherVertices();
        }
        if (GUILayout.Button("Resolve edges"))
        {
            myTarget.MakeEdges();
        }
        if (GUILayout.Button("Make Path from children"))
        {
            myTarget.PathFromChildren();
        }
    }

}
#endif
