using System;
using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

public enum JdeOmwReturn
{
    Success = 0,
    Error = 1,
    NotSupported = 2
}

public enum JdeOmwUnionValue
{
    String = 0,
    MathNumeric = 1,
    JdeDate = 2,
    Pointer = 3,
    Int = 4,
    Bool = 5,
    Char = 6,
    Id = 7
}

public enum JdeOmwMethod
{
    Save = 8,
    CaExtract = 39,
    GetProject = 92
}

public enum JdeOmwAttribute
{
    ObjectType = 700,
    ObjectId = 701,
    ProjectId = 702,
    Release = 704,
    AutoHUser = 739,
    Location = 780,
    ExternalSaveZipLocation = 7096,
    OmwParam7031 = 7031,
    OmwParam7034 = 7034,
    OmwParam7035 = 7035,
    OmwParam718 = 718,
    OmwParam727 = 727,
    OmwParam740 = 740,
    OmwParamObjectInterfacePointer = 723,
    OmwParam7003 = 7003,
    OmwParam7004 = 7004,
    OmwParamSbfJdevLoc = 7090,
    OmwParamSbfJdevReturn = 7091,
    OmwParamSbfErrorMsg = 7092
}

// ATTRUNION includes MATH_NUMERIC (50 bytes) and is 8-byte aligned on x64 (56 bytes).
[StructLayout(LayoutKind.Explicit, Size = 56)]
public struct JdeOmwAttrUnion
{
    [FieldOffset(0)]
    public IntPtr StringPtr;

    [FieldOffset(0)]
    public IntPtr Pointer;

    [FieldOffset(0)]
    public int IntValue;

    [FieldOffset(0)]
    public int BoolValue;

    [FieldOffset(0)]
    public char CharValue;

    [FieldOffset(0)]
    public JdeStructures.ID IdValue;
}
