using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PBDStruct
{
    public struct EdgeStruct
    {
        public int startIndex;
        public int endIndex;
    };

    public struct DistanceConstraintStruct
    {
        public EdgeStruct edge;
        public float restLength;
    };

    public struct BendingConstraintStruct
    {
        public float restAngle;

        public int index0;
        public int index1;
        public int index2;
        public int index3;

    };

    public struct UInt3Struct
    {
        public uint deltaXInt;
        public uint deltaYInt;
        public uint deltaZInt;
    }

}
