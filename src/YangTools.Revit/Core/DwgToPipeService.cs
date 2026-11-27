using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace YangTools.Revit.Core
{
    public sealed class DwgCurveChain
    {
        public string LayerName { get; set; } = "(未分层)";
        public bool IsPolyLine { get; set; }
        public bool Is3D { get; set; }
        public List<XYZ> Points { get; set; } = new();
    }

    public sealed class DwgLayerSummary
    {
        public string LayerName { get; set; } = "(未分层)";
        public int LineCount { get; set; }
        public int PolyLineCount { get; set; }
        public int ThreeDCount { get; set; }
        public int TotalSegments { get; set; }
        public bool Has3D => ThreeDCount > 0;
        public string ThreeDTag => ThreeDCount > 0 ? $"含 3D ({ThreeDCount})" : "2D";
    }

    public sealed class DwgToPipeRequest
    {
        public HashSet<string> SelectedLayers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public ElementId PipingSystemTypeId { get; set; } = ElementId.InvalidElementId;
        public ElementId PipeTypeId { get; set; } = ElementId.InvalidElementId;
        public ElementId LevelId { get; set; } = ElementId.InvalidElementId;
        public double DiameterMm { get; set; } = 100.0;
        public bool IgnoreDwgZ { get; set; } = false;
        public double OffsetMm { get; set; } = 0.0;
        public bool ConnectPolyLineCorners { get; set; } = true;
        public bool ConnectSharedEndpoints { get; set; } = true;
        public bool MergeCollinearSegments { get; set; } = true;
        public double ConnectToleranceMm { get; set; } = 20.0;
        public double MinSegmentLengthMm { get; set; } = 50.0;
    }

    public sealed class DwgToPipeResult
    {
        public int PipesCreated { get; set; }
        public int ElbowsCreated { get; set; }
        public int TeesCreated { get; set; }
        public int CrossesCreated { get; set; }
        public int SkippedShortSegments { get; set; }
        public int FailedSegments { get; set; }
        public int FailedConnections { get; set; }
    }

    public static class DwgToPipeService
    {
        private const double MmPerFoot = 304.8;
        private const double ZVariationToleranceFeet = 1.0 / MmPerFoot; // 1 mm
        private const double DuplicatePointToleranceFeet = 0.5 / MmPerFoot; // 0.5 mm

        public static List<DwgCurveChain> ExtractChains(Document doc, ImportInstance dwg)
        {
            var chains = new List<DwgCurveChain>();
            var options = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement? geomElem = dwg.get_Geometry(options);
            if (geomElem == null) return chains;

            CollectGeometry(doc, geomElem, Transform.Identity, chains);
            return chains;
        }

        public static List<DwgLayerSummary> SummarizeLayers(IReadOnlyList<DwgCurveChain> chains)
        {
            return chains
                .GroupBy(c => c.LayerName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new DwgLayerSummary
                {
                    LayerName = g.First().LayerName,
                    LineCount = g.Count(c => !c.IsPolyLine),
                    PolyLineCount = g.Count(c => c.IsPolyLine),
                    ThreeDCount = g.Count(c => c.Is3D),
                    TotalSegments = g.Sum(c => Math.Max(0, c.Points.Count - 1))
                })
                .OrderByDescending(x => x.TotalSegments)
                .ThenBy(x => x.LayerName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void CollectGeometry(
            Document doc,
            GeometryElement geomElem,
            Transform currentTransform,
            List<DwgCurveChain> output)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is GeometryInstance inst)
                {
                    GeometryElement? symbolGeom = inst.GetSymbolGeometry();
                    if (symbolGeom == null) continue;
                    Transform nextTransform = currentTransform.Multiply(inst.Transform);
                    CollectGeometry(doc, symbolGeom, nextTransform, output);
                    continue;
                }

                if (obj is Line line)
                {
                    if (!line.IsBound) continue;
                    XYZ p0 = currentTransform.OfPoint(line.GetEndPoint(0));
                    XYZ p1 = currentTransform.OfPoint(line.GetEndPoint(1));
                    if (p0.DistanceTo(p1) <= DuplicatePointToleranceFeet) continue;

                    output.Add(new DwgCurveChain
                    {
                        LayerName = ResolveLayerName(doc, obj),
                        IsPolyLine = false,
                        Is3D = Math.Abs(p1.Z - p0.Z) > ZVariationToleranceFeet,
                        Points = new List<XYZ> { p0, p1 }
                    });
                    continue;
                }

                if (obj is PolyLine poly)
                {
                    IList<XYZ> coords = poly.GetCoordinates();
                    if (coords == null || coords.Count < 2) continue;

                    var pts = new List<XYZ>(coords.Count);
                    foreach (XYZ raw in coords)
                    {
                        XYZ pt = currentTransform.OfPoint(raw);
                        if (pts.Count == 0 || pts[pts.Count - 1].DistanceTo(pt) > DuplicatePointToleranceFeet)
                            pts.Add(pt);
                    }
                    if (pts.Count < 2) continue;

                    double minZ = pts.Min(p => p.Z);
                    double maxZ = pts.Max(p => p.Z);
                    output.Add(new DwgCurveChain
                    {
                        LayerName = ResolveLayerName(doc, obj),
                        IsPolyLine = true,
                        Is3D = (maxZ - minZ) > ZVariationToleranceFeet,
                        Points = pts
                    });
                }
            }
        }

        private static string ResolveLayerName(Document doc, GeometryObject obj)
        {
            if (obj.GraphicsStyleId != null && obj.GraphicsStyleId != ElementId.InvalidElementId)
            {
                if (doc.GetElement(obj.GraphicsStyleId) is GraphicsStyle style &&
                    !string.IsNullOrWhiteSpace(style.GraphicsStyleCategory?.Name))
                {
                    return style.GraphicsStyleCategory.Name;
                }
            }
            return "(未分层)";
        }

        public static DwgToPipeResult Execute(
            Document doc,
            IReadOnlyList<DwgCurveChain> allChains,
            DwgToPipeRequest request)
        {
            var result = new DwgToPipeResult();
            if (request.SelectedLayers.Count == 0) return result;

            Level? level = doc.GetElement(request.LevelId) as Level;
            if (level == null)
                throw new InvalidOperationException("未找到指定的参照标高。");

            double diameterFeet = Math.Max(5.0, request.DiameterMm) / MmPerFoot;
            double offsetFeet = request.OffsetMm / MmPerFoot;
            double targetFlatZ = level.Elevation + offsetFeet;
            double minSegFeet = Math.Max(1.0, request.MinSegmentLengthMm) / MmPerFoot;
            double connectTolFeet = Math.Max(1.0, request.ConnectToleranceMm) / MmPerFoot;

            List<DwgCurveChain> selectedChains = allChains
                .Where(c => request.SelectedLayers.Contains(c.LayerName))
                .ToList();

            if (selectedChains.Count == 0) return result;

            using var tg = new TransactionGroup(doc, "DWG 转管道");
            if (tg.Start() != TransactionStatus.Started)
                throw new InvalidOperationException("无法启动事务组。");

            var createdPipes = new List<Pipe>();
            var polyPairs = new List<(Pipe First, Pipe Second, XYZ Vertex)>();

            using (var tx = new Transaction(doc, "创建管道与连接"))
            {
                FailureHandlingOptions fho = tx.GetFailureHandlingOptions();
                fho.SetFailuresPreprocessor(new SilentWarningPreprocessor());
                tx.SetFailureHandlingOptions(fho);

                if (tx.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动创建管道事务。");

                foreach (DwgCurveChain chain in selectedChains)
                {
                    List<XYZ> pts = PrepareChainPoints(chain, request, targetFlatZ, offsetFeet);
                    if (pts.Count < 2) continue;

                    Pipe? prevPipe = null;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        XYZ p0 = pts[i];
                        XYZ p1 = pts[i + 1];
                        if (p0.DistanceTo(p1) < minSegFeet)
                        {
                            result.SkippedShortSegments++;
                            prevPipe = null;
                            continue;
                        }

                        Pipe? pipe = TryCreatePipe(doc, request.PipingSystemTypeId, request.PipeTypeId, request.LevelId, p0, p1, diameterFeet);
                        if (pipe == null)
                        {
                            result.FailedSegments++;
                            prevPipe = null;
                            continue;
                        }

                        createdPipes.Add(pipe);
                        result.PipesCreated++;

                        if (chain.IsPolyLine && prevPipe != null)
                            polyPairs.Add((prevPipe, pipe, p0));

                        prevPipe = pipe;
                    }
                }

                // Pass 1: Connect consecutive segments of each PolyLine / 3D PolyLine with elbows.
                if (request.ConnectPolyLineCorners && polyPairs.Count > 0)
                {
                    foreach (var pair in polyPairs)
                    {
                        Connector? c1 = FindClosestOpenConnector(pair.First, pair.Vertex, connectTolFeet);
                        Connector? c2 = FindClosestOpenConnector(pair.Second, pair.Vertex, connectTolFeet);
                        if (c1 == null || c2 == null) continue;
                        if (AreNearlyCollinear(c1, c2)) continue;

                        if (TryCreateElbow(doc, c1, c2))
                            result.ElbowsCreated++;
                        else
                            result.FailedConnections++;
                    }
                }

                // Pass 2: Connect remaining shared endpoints across lines & polylines (2-way Elbow, 3-way Tee, 4-way Cross).
                if (request.ConnectSharedEndpoints && createdPipes.Count > 1)
                {
                    ConnectOpenEndpoints(doc, createdPipes, connectTolFeet, result);
                }

                tx.Commit();
            }

            tg.Assimilate();
            return result;
        }

        private static List<XYZ> PrepareChainPoints(
            DwgCurveChain chain,
            DwgToPipeRequest request,
            double targetFlatZ,
            double offsetFeet)
        {
            var raw = new List<XYZ>(chain.Points.Count);
            foreach (XYZ p in chain.Points)
            {
                XYZ mapped = request.IgnoreDwgZ
                    ? new XYZ(p.X, p.Y, targetFlatZ)
                    : (Math.Abs(offsetFeet) < 1e-9 ? p : new XYZ(p.X, p.Y, p.Z + offsetFeet));
                if (raw.Count == 0 || raw[raw.Count - 1].DistanceTo(mapped) > DuplicatePointToleranceFeet)
                    raw.Add(mapped);
            }

            if (!request.MergeCollinearSegments || raw.Count < 3)
                return raw;

            var merged = new List<XYZ> { raw[0] };
            for (int i = 1; i < raw.Count - 1; i++)
            {
                XYZ a = merged[merged.Count - 1];
                XYZ b = raw[i];
                XYZ c = raw[i + 1];
                XYZ d1 = (b - a).Normalize();
                XYZ d2 = (c - b).Normalize();
                if (d1.DotProduct(d2) > 0.9998)
                    continue; // collinear intermediate point
                merged.Add(b);
            }
            merged.Add(raw[raw.Count - 1]);
            return merged;
        }

        private static Pipe? TryCreatePipe(
            Document doc,
            ElementId systemTypeId,
            ElementId pipeTypeId,
            ElementId levelId,
            XYZ p0,
            XYZ p1,
            double diameterFeet)
        {
            using var sub = new SubTransaction(doc);
            if (sub.Start() != TransactionStatus.Started) return null;
            try
            {
                Pipe pipe = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, p0, p1);
                Parameter? diaParam = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (diaParam != null && !diaParam.IsReadOnly)
                    diaParam.Set(diameterFeet);
                sub.Commit();
                return pipe;
            }
            catch
            {
                sub.RollBack();
                return null;
            }
        }

        private static void ConnectOpenEndpoints(
            Document doc,
            List<Pipe> pipes,
            double toleranceFeet,
            DwgToPipeResult result)
        {
            var openConnectors = new List<Connector>();
            foreach (Pipe pipe in pipes)
            {
                if (!pipe.IsValidObject) continue;
                ConnectorSet? set = pipe.ConnectorManager?.Connectors;
                if (set == null) continue;
                foreach (Connector c in set)
                {
                    if (c.ConnectorType == ConnectorType.End && !c.IsConnected)
                        openConnectors.Add(c);
                }
            }

            var used = new bool[openConnectors.Count];
            for (int i = 0; i < openConnectors.Count; i++)
            {
                if (used[i] || openConnectors[i].IsConnected) continue;

                var cluster = new List<int> { i };
                XYZ anchor = openConnectors[i].Origin;
                for (int j = i + 1; j < openConnectors.Count; j++)
                {
                    if (used[j] || openConnectors[j].IsConnected) continue;
                    if (openConnectors[j].Owner.Id.Equals(openConnectors[i].Owner.Id)) continue;
                    if (openConnectors[j].Origin.DistanceTo(anchor) <= toleranceFeet)
                        cluster.Add(j);
                }

                if (cluster.Count < 2) continue;
                foreach (int idx in cluster) used[idx] = true;

                List<Connector> group = cluster.Select(idx => openConnectors[idx]).ToList();
                if (group.Count == 2)
                {
                    if (AreNearlyCollinear(group[0], group[1])) continue;
                    if (TryCreateElbow(doc, group[0], group[1]))
                        result.ElbowsCreated++;
                    else
                        result.FailedConnections++;
                }
                else if (group.Count == 3)
                {
                    if (TryCreateTee(doc, group[0], group[1], group[2]))
                        result.TeesCreated++;
                    else
                        result.FailedConnections++;
                }
                else if (group.Count == 4)
                {
                    if (TryCreateCross(doc, group))
                        result.CrossesCreated++;
                    else
                        result.FailedConnections++;
                }
            }
        }

        private static Connector? FindClosestOpenConnector(Pipe pipe, XYZ target, double maxDistFeet)
        {
            if (!pipe.IsValidObject) return null;
            ConnectorSet? set = pipe.ConnectorManager?.Connectors;
            if (set == null) return null;

            Connector? best = null;
            double bestDist = maxDistFeet;
            foreach (Connector c in set)
            {
                if (c.ConnectorType != ConnectorType.End || c.IsConnected) continue;
                double d = c.Origin.DistanceTo(target);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        private static bool AreNearlyCollinear(Connector c1, Connector c2)
        {
            XYZ z1 = c1.CoordinateSystem.BasisZ.Normalize();
            XYZ z2 = c2.CoordinateSystem.BasisZ.Normalize();
            return Math.Abs(z1.DotProduct(z2)) > 0.999;
        }

        private static bool TryCreateElbow(Document doc, Connector c1, Connector c2)
        {
            using var sub = new SubTransaction(doc);
            if (sub.Start() != TransactionStatus.Started) return false;
            try
            {
                doc.Create.NewElbowFitting(c1, c2);
                sub.Commit();
                return true;
            }
            catch
            {
                sub.RollBack();
                return false;
            }
        }

        private static bool TryCreateTee(Document doc, Connector a, Connector b, Connector c)
        {
            // Pair the two most collinear connectors as main run, third as branch.
            double dotAB = Math.Abs(a.CoordinateSystem.BasisZ.DotProduct(b.CoordinateSystem.BasisZ));
            double dotAC = Math.Abs(a.CoordinateSystem.BasisZ.DotProduct(c.CoordinateSystem.BasisZ));
            double dotBC = Math.Abs(b.CoordinateSystem.BasisZ.DotProduct(c.CoordinateSystem.BasisZ));

            Connector m1, m2, branch;
            if (dotAB >= dotAC && dotAB >= dotBC) { m1 = a; m2 = b; branch = c; }
            else if (dotAC >= dotAB && dotAC >= dotBC) { m1 = a; m2 = c; branch = b; }
            else { m1 = b; m2 = c; branch = a; }

            using var sub = new SubTransaction(doc);
            if (sub.Start() != TransactionStatus.Started) return false;
            try
            {
                doc.Create.NewTeeFitting(m1, m2, branch);
                sub.Commit();
                return true;
            }
            catch
            {
                sub.RollBack();
                return false;
            }
        }

        private static bool TryCreateCross(Document doc, List<Connector> g)
        {
            // Find the partner most collinear with g[0]
            int partner = 1;
            double bestDot = -1;
            for (int i = 1; i < 4; i++)
            {
                double d = Math.Abs(g[0].CoordinateSystem.BasisZ.DotProduct(g[i].CoordinateSystem.BasisZ));
                if (d > bestDot) { bestDot = d; partner = i; }
            }
            var others = Enumerable.Range(1, 3).Where(i => i != partner).ToList();

            using var sub = new SubTransaction(doc);
            if (sub.Start() != TransactionStatus.Started) return false;
            try
            {
                doc.Create.NewCrossFitting(g[0], g[partner], g[others[0]], g[others[1]]);
                sub.Commit();
                return true;
            }
            catch
            {
                sub.RollBack();
                return false;
            }
        }

        private sealed class SilentWarningPreprocessor : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (FailureMessageAccessor fma in failuresAccessor.GetFailureMessages())
                {
                    if (fma.GetSeverity() == FailureSeverity.Warning)
                        failuresAccessor.DeleteWarning(fma);
                }
                return FailureProcessingResult.Continue;
            }
        }
    }
}
