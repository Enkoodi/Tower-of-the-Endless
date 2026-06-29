using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct CellInfo
{
    public CellType type;
    public GameObject gameObject;
    public Vector3 position;
    
    public CellInfo(CellType type, GameObject obj, Vector3 pos)
    {
        this.type = type;
        this.gameObject = obj;
        this.position = pos;
    }
}