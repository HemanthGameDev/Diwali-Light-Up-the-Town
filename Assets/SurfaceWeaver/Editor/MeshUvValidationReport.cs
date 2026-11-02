using System.Collections.Generic;

namespace HemanthGameDev.SurfaceWeaver.Editor
{
    /// <summary>Detailed UV diagnostics for a mesh surface-paint target.</summary>
    public sealed class MeshUvValidationReport
    {
        public int VertexCount { get; internal set; }
        public int TriangleCount { get; internal set; }
        public int UvCount { get; internal set; }
        public int OutOfRangeUvCount { get; internal set; }
        public int NonFiniteUvCount { get; internal set; }
        public int DegenerateUvTriangleCount { get; internal set; }
        public int OverlappingUvTrianglePairCount { get; internal set; }
        public int UvSeamEdgeCount { get; internal set; }
        public int UvIslandCount { get; internal set; }
        public int OverlappingUvIslandPairCount { get; internal set; }
        public int StackedUvIslandPairCount { get; internal set; }
        public int MirroredUvIslandPairCount { get; internal set; }
        public int PartialUvIslandOverlapPairCount { get; internal set; }
        public int ContainedUvIslandOverlapPairCount { get; internal set; }
        public int SameIslandOverlappingTrianglePairCount { get; internal set; }
        public int MirroredOverlapWithinIslandTrianglePairCount { get; internal set; }
        public int SameOrientationOverlapWithinIslandTrianglePairCount { get; internal set; }
        public long OverlapCandidatePairTests { get; internal set; }
        public long IslandPairCandidateTests { get; internal set; }
        public long IslandOverlapCandidateTrianglePairTests { get; internal set; }
        public bool MeshReadable { get; internal set; }
        public bool OverlapAnalysisCompleted { get; internal set; }
        public bool IslandOverlapAnalysisCompleted { get; internal set; }

        internal readonly List<MeshUvValidator.UvTriangle> RasterTriangles = new List<MeshUvValidator.UvTriangle>();
        public readonly List<string> UvIslandSummaries = new List<string>();
        public readonly List<string> UvIslandOverlapSummaries = new List<string>();
        public readonly List<string> SameIslandOverlapSummaries = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Infos = new List<string>();

        public bool IsValid => Errors.Count == 0;
        public bool HasUvOverlaps => OverlappingUvTrianglePairCount > 0;
        public bool HasUvSeams => UvSeamEdgeCount > 0;
        public bool HasStackedUvIslands => StackedUvIslandPairCount > 0;
        public bool HasMirroredUvIslands => MirroredUvIslandPairCount > 0;
    }
}
