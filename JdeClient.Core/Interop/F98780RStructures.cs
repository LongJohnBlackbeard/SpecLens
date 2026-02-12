using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

internal static class F98780RStructures
{
    public const string TableName = "F98780R";
    public const int IdObjectReleaseVersion = 1;

    internal static class Columns
    {
        public const string ObjectId = "OMWOBJID";
        public const string Release = "RLS";
        public const string Version = "JDEVERS";
        public const string ObjectType = "OMWOT";
        public const string ObjectRepositoryBlob = "OMRBLOB";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct Key1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 201)]
        public string ObjectId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string Release;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string Version;
    }
}
