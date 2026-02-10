using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

/// <summary>
/// P/Invoke declarations for OMW APIs (jdeomw.dll).
/// </summary>
public static class JdeOmwApi
{
    private const string DllName = "jdeomw.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwReturn OMWCreateOMWObjectFactory(
        JdeStructures.HUSER hUser,
        out IntPtr hObjectFactory);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwReturn OMWCreateOMWObjectFactory(
        JdeStructures.HENV hEnv,
        out IntPtr hObjectFactory);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwReturn OMWCreateParamObject(out IntPtr hParam);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwReturn OMWDeleteObject(ref IntPtr hObject);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwReturn OMWSetAttribute(
        ref IntPtr hObject,
        JdeOmwAttribute attribute,
        JdeOmwAttrUnion value,
        JdeOmwUnionValue valueType);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwReturn OMWGetAttribute(
        IntPtr hObject,
        JdeOmwAttribute attribute,
        out JdeOmwAttrUnion value);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwUnionValue OMWGetAttributeDataType(
        IntPtr hObject,
        JdeOmwAttribute attribute);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern JdeOmwReturn OMWCallMethod(
        IntPtr hObject,
        JdeOmwMethod method,
        ref IntPtr hParam);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern JdeOmwReturn OMWCallSaveObjectToRepositoryEx(
        JdeStructures.HUSER hUser,
        [MarshalAs(UnmanagedType.LPWStr)] string objectId,
        [MarshalAs(UnmanagedType.LPWStr)] string objectType,
        [MarshalAs(UnmanagedType.LPWStr)] string pathCode,
        [MarshalAs(UnmanagedType.LPWStr)] string projectName,
        [MarshalAs(UnmanagedType.Bool)] bool doInsertOnly,
        [MarshalAs(UnmanagedType.Bool)] out bool fileExists,
        int include64BitFiles);
}
