using System.Collections.Generic;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    public sealed class MeshUvValidationReport
    {
        public int VertexCount { get; internal set; }
        public int TriangleCount { get; internal set; }
        public int UvCount { get; internal set; }
        public int OutOfRangeUvCount { get; internal set; }
        public int NonFiniteUvCount { get; internal set; }
        public int DegenerateUvTriangleCount { get; internal set; }
        public bool MeshReadable { get; internal set; }
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Infos = new List<string>();

        public bool IsValid => Errors.Count == 0;
    }
}
