using JdeClient.Core.Interop;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

internal interface IOmwApi
{
    JdeOmwReturn OMWCreateOMWObjectFactory(HENV hEnv, out IntPtr hObjectFactory);

    JdeOmwReturn OMWCreateParamObject(out IntPtr hParam);

    JdeOmwReturn OMWSetAttribute(
        ref IntPtr hObject,
        JdeOmwAttribute attribute,
        JdeOmwAttrUnion value,
        JdeOmwUnionValue valueType);

    JdeOmwReturn OMWGetAttribute(IntPtr hObject, JdeOmwAttribute attribute, out JdeOmwAttrUnion value);

    JdeOmwReturn OMWCallMethod(IntPtr hObject, JdeOmwMethod method, ref IntPtr hParam);

    JdeOmwReturn OMWCallSaveObjectToRepositoryEx(
        HUSER hUser,
        string objectId,
        string objectType,
        string pathCode,
        string projectName,
        bool doInsertOnly,
        out bool fileExists,
        int include64BitFiles);

    JdeOmwReturn OMWDeleteObject(ref IntPtr hObject);
}

internal sealed class OmwApi : IOmwApi
{
    public JdeOmwReturn OMWCreateOMWObjectFactory(HENV hEnv, out IntPtr hObjectFactory)
        => JdeOmwApi.OMWCreateOMWObjectFactory(hEnv, out hObjectFactory);

    public JdeOmwReturn OMWCreateParamObject(out IntPtr hParam)
        => JdeOmwApi.OMWCreateParamObject(out hParam);

    public JdeOmwReturn OMWSetAttribute(
        ref IntPtr hObject,
        JdeOmwAttribute attribute,
        JdeOmwAttrUnion value,
        JdeOmwUnionValue valueType)
        => JdeOmwApi.OMWSetAttribute(ref hObject, attribute, value, valueType);

    public JdeOmwReturn OMWGetAttribute(IntPtr hObject, JdeOmwAttribute attribute, out JdeOmwAttrUnion value)
        => JdeOmwApi.OMWGetAttribute(hObject, attribute, out value);

    public JdeOmwReturn OMWCallMethod(IntPtr hObject, JdeOmwMethod method, ref IntPtr hParam)
        => JdeOmwApi.OMWCallMethod(hObject, method, ref hParam);

    public JdeOmwReturn OMWCallSaveObjectToRepositoryEx(
        HUSER hUser,
        string objectId,
        string objectType,
        string pathCode,
        string projectName,
        bool doInsertOnly,
        out bool fileExists,
        int include64BitFiles)
        => JdeOmwApi.OMWCallSaveObjectToRepositoryEx(
            hUser,
            objectId,
            objectType,
            pathCode,
            projectName,
            doInsertOnly,
            out fileExists,
            include64BitFiles);

    public JdeOmwReturn OMWDeleteObject(ref IntPtr hObject)
        => JdeOmwApi.OMWDeleteObject(ref hObject);
}
