using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

public class Graph
{
    private List<Tuple<int, int>>[] adjacencyList;
    private int vertices;

    public Graph(int vertices)
    {
        this.vertices = vertices;
        adjacencyList = new List<Tuple<int, int>>[vertices];
        for (int i = 0; i < vertices; i++)
        {
            adjacencyList[i] = new List<Tuple<int, int>>();
        }
    }

    public void AddSingleEdge(int source, int destination, int weight)
    {
        Tuple<int, int> newAdjacency = new Tuple<int, int>(destination, weight);
        if (adjacencyList[source].Contains(newAdjacency) == false)
        {
            adjacencyList[source].Add(new Tuple<int, int>(destination, weight));
        }
        newAdjacency = new Tuple<int, int>(source, weight);
        if (adjacencyList[destination].Contains(newAdjacency) == false)
        {
            adjacencyList[destination].Add(new Tuple<int, int>(source, weight));
        }
    }
    public void AddEdge(int source, int destination, int weight)
    {
        adjacencyList[source].Add(new Tuple<int, int>(destination, weight));
        adjacencyList[destination].Add(new Tuple<int, int>(source, weight));
    }

    public struct DistancesCalculationResult
    {
        public int[,] distances;
        public List<int>[,] intermediateVertices;
    }

    private DistancesCalculationResult CalculateShortestDistances()
    {
        DistancesCalculationResult result = new DistancesCalculationResult();
        result.distances = new int[vertices, vertices];
        result.intermediateVertices = new List<int>[vertices, vertices];

        for (int i = 0; i < vertices; i++)
        {
            for (int j = 0; j < vertices; j++)
            {
                result.distances[i, j] = int.MaxValue;
                result.intermediateVertices[i, j] = new List<int>();
            }
            result.distances[i, i] = 0;
        }

        for (int i = 0; i < vertices; i++)
        {
            foreach (var edge in adjacencyList[i])
            {
                result.distances[i, edge.Item1] = edge.Item2;
            }
        }

        for (int m = 0; m < vertices; m++)
        {
            for (int k = 0; k < vertices; k++)
            {
                for (int i = 0; i < vertices; i++)
                {
                    for (int j = 0; j < vertices; j++)
                    {
                        if (result.distances[i, k] != int.MaxValue && result.distances[k, j] != int.MaxValue &&
                            result.distances[i, k] + result.distances[k, j] < result.distances[i, j])
                        {
                            result.distances[i, j] = result.distances[i, k] + result.distances[k, j];

                            for (int intermediate = 0; intermediate < result.intermediateVertices[i, k].Count; ++intermediate)
                            {
                                result.intermediateVertices[i, j].Add(result.intermediateVertices[i, k][intermediate]);
                            }
                            result.intermediateVertices[i, j].Add(k);
                            for (int intermediate = result.intermediateVertices[k, j].Count - 1; intermediate >= 0; --intermediate)
                            {
                                result.intermediateVertices[i, j].Add(result.intermediateVertices[k, j][intermediate]);
                            }
                        }
                    }
                }
            }
        }

        return result;
    }
    private List<int> FindOddVertices()
    {
        List<int> oddVertices = new List<int>();
        for (int i = 0; i < vertices; i++)
        {
            if (adjacencyList[i].Count % 2 != 0)
            {
                oddVertices.Add(i);
            }
        }
        return oddVertices;
    }
    private List<int>[,] AugmentGraph(List<int> oddVertices, int vertexCount)
    {
        bool[] pairedVertices = new bool[oddVertices.Count];
        List<int>[,] potentialIntermediates = new List<int>[vertexCount, vertexCount];
        for (int i = 0; i < oddVertices.Count; i++)
        {
            if (pairedVertices[i] == false)
            {
                for (int j = 0; j < oddVertices.Count; j++)
                {
                    if (i != j && pairedVertices[j] == false)
                    {
                        DistancesCalculationResult distances = CalculateShortestDistances();
                        int distance = distances.distances[oddVertices[i], oddVertices[j]];
                        AddEdge(oddVertices[i], oddVertices[j], distance);
                        potentialIntermediates[oddVertices[i], oddVertices[j]] = distances.intermediateVertices[oddVertices[i], oddVertices[j]];
                        potentialIntermediates[oddVertices[j], oddVertices[i]] = distances.intermediateVertices[oddVertices[j], oddVertices[i]];
                        pairedVertices[i] = true;
                        pairedVertices[j] = true;
                        break;
                    }
                }
            }
        }
        return potentialIntermediates;
    }

    public List<int> FindOptimalRoute()
    {
        List<int> route = new List<int>();
        List<int> oddVertices = FindOddVertices();
        int startVertex = 0;
        for (int i = 0; i < vertices; i++)
        {
            if (adjacencyList[i].Count % 2 != 0)
            {
                startVertex = i;
                break;
            }
        }
        List<int>[,] intermediates = AugmentGraph(oddVertices, vertices);


        DistancesCalculationResult distances = CalculateShortestDistances();
        Stack<int> stack = new Stack<int>();
        stack.Push(startVertex);

        while (stack.Count > 0)
        {
            int currentVertex = stack.Peek();
            bool found = false;
            foreach (var edge in adjacencyList[currentVertex])
            {
                int neighbor = edge.Item1;
                if (distances.distances[currentVertex, neighbor] > 0 || intermediates[currentVertex, neighbor] != null)
                {
                    if (intermediates[currentVertex, neighbor] != null && intermediates[currentVertex, neighbor].Count > 0)
                    {

                        for (int i = 0; i < intermediates[currentVertex, neighbor].Count; ++i)
                        {
                            stack.Push(intermediates[currentVertex, neighbor][i]);
                        }
                    }
                    stack.Push(neighbor);
                    if (distances.distances[currentVertex, neighbor] > 0)
                    {
                        distances.distances[currentVertex, neighbor] = 0;
                        distances.distances[neighbor, currentVertex] = 0;
                    }
                    else
                    {
                        intermediates[currentVertex, neighbor] = null;
                        intermediates[neighbor, currentVertex] = null;
                    }
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                route.Add(stack.Pop());
            }
        }

        route.Reverse();
        return route;
    }
}