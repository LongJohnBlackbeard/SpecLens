using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

internal static class F98741Structures
{
    public const string TableName = "F98741";
    public const int IdEventRulesSpecsUuid = 2;

    internal static class Columns
    {
        public const string EventSpecKey = "EVSK";
        public const string EventSequence = "EVSEQ";
        public const string EventBlob = "ERBLOB";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct Key2
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 37)]
        public string EventSpecKey;

        public int EventSequence;
    }
}
