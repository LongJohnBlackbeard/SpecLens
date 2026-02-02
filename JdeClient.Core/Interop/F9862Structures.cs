using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

internal static class F9862Structures
{
    public const string TableName = "F9862";
    public const int IdObjectNameFunctionName = 1;

    internal static class Columns
    {
        public const string ObjectName = "OBNM";
        public const string FunctionName = "FCTNM";
        public const string DataStructureName = "DSTNM";
        public const string EventSpecKey = "EVSK";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct Key1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string ObjectName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string FunctionName;
    }
}
