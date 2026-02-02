using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JdeClient.Core.Interop;

/// <summary>
/// P/Invoke declarations for JDE Spec Encapsulation APIs (jdeSpec* functions).
/// </summary>
public static class JdeSpecEncapApi
{
    private const string DllName = "jdekrnl.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecInitXMLConvertHandle(
        out IntPtr hSpec,
        JdeStructures.JdeSpecFileType eType);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecConvertToXML_UTF16(
        IntPtr hSpec,
        ref JdeStructures.JdeSpecData pSpecBinaryData,
        ref JdeStructures.JdeSpecData pSpecXMLData);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecInsertRecordToConsolidatedBuffer(
        IntPtr hSpec,
        ref JdeStructures.JdeSpecData pSpecBinaryData);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecConvertConsolidatedToXML(
        IntPtr hSpec,
        ref JdeStructures.JdeSpecData pSpecXMLData);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int jdeSpecOpen(
        out IntPtr hSpec,
        JdeStructures.HUSER hUser,
        JdeStructures.JdeSpecFileType eType,
        JdeStructures.JdeSpecLocation eLoc,
        [MarshalAs(UnmanagedType.LPWStr)] string pathCode);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int jdeSpecOpenCentral(
        out IntPtr hSpec,
        JdeStructures.HUSER hUser,
        JdeStructures.JdeSpecFileType eType,
        [MarshalAs(UnmanagedType.LPWStr)] string pathCode);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int jdeSpecOpenCentralIndexed(
        out IntPtr hSpec,
        JdeStructures.HUSER hUser,
        JdeStructures.JdeSpecFileType eType,
        JdeStructures.ID idIndex,
        [MarshalAs(UnmanagedType.LPWStr)] string pathCode);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecOpenLocal(
        out IntPtr hSpec,
        JdeStructures.HUSER hUser,
        JdeStructures.JdeSpecFileType eType);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecOpenLocalIndexed(
        out IntPtr hSpec,
        JdeStructures.HUSER hUser,
        JdeStructures.JdeSpecFileType eType,
        JdeStructures.ID idIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecClose(
        IntPtr hSpec);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecSelectKeyed(
        IntPtr hSpec,
        IntPtr pKeyStruct,
        int iKeySize,
        int iElementCount);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecFetch(
        IntPtr hSpec,
        ref JdeStructures.JdeSpecData pSpecData);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecFetchSingle(
        IntPtr hSpec,
        ref JdeStructures.JdeSpecData pSpecData,
        IntPtr pKeyStruct,
        int iElementCount);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecFreeData(
        ref JdeStructures.JdeSpecData pSpecData);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeSpecGetLastErrorInfo(
        IntPtr hSpec,
        ref JdeStructures.JdeSpecLastError pLastError);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int jdeSpecGetResultText(
        StringBuilder pResultText,
        int iTextLen,
        int eRes);
}
