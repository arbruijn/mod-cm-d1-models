using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Classic
{
    public static class ClassicUnityExt {
    public static Vector3 ToVector3(this vms_vector v)
    {
        return new Vector3(v.x.ToFloat(), v.y.ToFloat(), v.z.ToFloat());
    }
}

public class ClassicModel : MonoBehaviour {

    // Use this for initialization
    void Start () {
        /*
        GetClassic
        var ri = 0;
        foreach (var go in ClassicRobots) {
            var lgo = UnityEngine.Object.Instantiate<GameObject>(go);
            lgo.transform.parent = gameObject.transform;
            lgo.transform.localPosition = Vector3.right * ri++ * 10f;
        }
        */
    }

    // Update is called once per frame
    void Update () {
    }
}
}
