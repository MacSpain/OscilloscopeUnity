using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float xRot;
    public float yRot;
    public float zRot;

    private float xRotCumulated = 0.0f;
    private float yRotCumulated = 0.0f;
    private float zRotCumulated = 0.0f;

    void Update()
    {
        xRotCumulated += Time.deltaTime * xRot;
        yRotCumulated += Time.deltaTime * yRot;
        zRotCumulated += Time.deltaTime * zRot;
        transform.rotation = Quaternion.Euler(xRotCumulated, yRotCumulated, zRotCumulated);
    }
}
