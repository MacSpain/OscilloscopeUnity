using System;
using System.Collections.Generic;

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

    public void AddEdge(int source, int destination, int weight)
    {
        adjacencyList[source].Add(new Tuple<int, int>(destination, weight));
        adjacencyList[destination].Add(new Tuple<int, int>(source, weight));
    }

    private int CountOddDegrees()
    {
        int count = 0;
        for (int i = 0; i < vertices; i++)
        {
            if (adjacencyList[i].Count % 2 != 0)
            {
                count++;
            }
        }
        return count;
    }

    private int[,] CalculateShortestDistances()
    {
        int[,] distances = new int[vertices, vertices];
        for (int i = 0; i < vertices; i++)
        {
            for (int j = 0; j < vertices; j++)
            {
                distances[i, j] = int.MaxValue;
            }
            distances[i, i] = 0;
        }

        for (int i = 0; i < vertices; i++)
        {
            foreach (var edge in adjacencyList[i])
            {
                distances[i, edge.Item1] = edge.Item2;
            }
        }

        for (int k = 0; k < vertices; k++)
        {
            for (int i = 0; i < vertices; i++)
            {
                for (int j = 0; j < vertices; j++)
                {
                    if (distances[i, k] != int.MaxValue && distances[k, j] != int.MaxValue &&
                        distances[i, k] + distances[k, j] < distances[i, j])
                    {
                        distances[i, j] = distances[i, k] + distances[k, j];
                    }
                }
            }
        }

        return distances;
    }

    public List<int> FindOptimalRoute()
    {
        List<int> route = new List<int>();
        int oddVertices = CountOddDegrees();
        if (oddVertices > 2)
        {
            throw new InvalidOperationException("Graph has no Eulerian path.");
        }

        int startVertex = 0;
        for (int i = 0; i < vertices; i++)
        {
            if (adjacencyList[i].Count % 2 != 0)
            {
                startVertex = i;
                break;
            }
        }

        int[,] distances = CalculateShortestDistances();
        Stack<int> stack = new Stack<int>();
        stack.Push(startVertex);

        while (stack.Count > 0)
        {
            int currentVertex = stack.Peek();
            bool found = false;
            foreach (var edge in adjacencyList[currentVertex])
            {
                int neighbor = edge.Item1;
                if (distances[currentVertex, neighbor] > 0)
                {
                    stack.Push(neighbor);
                    distances[currentVertex, neighbor]--;
                    distances[neighbor, currentVertex]--;
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