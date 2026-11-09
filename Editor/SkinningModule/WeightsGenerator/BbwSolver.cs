using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace UnityEditor.U2D.Animation
{
    // Burst-compiled bounded-biharmonic-weights solve over a preprocessed mesh. The math lives in
    // BbwSolverKernel; this job makes it schedulable, so the asynchronous generation path can chain
    // preprocessing and solving as jobs without a native module.
    //
    // FloatMode.Deterministic: the solver is all double arithmetic, exactly the domain the mode
    // covers (64-bit only), and cross-platform bit-identity of the output is a measured, relied-upon
    // property - the flag makes that contract explicit to the compiler. The enforcement is
    // BbwSolverRegressionTest, whose goldens assert the exact output bits.
    [BurstCompile(FloatMode = FloatMode.Deterministic)]
    internal struct BbwSolveJob : IJob
    {
        public int maxIterations;
        public int originalVertexCount;
        public int supressCommonWarnings;
        // From the preprocessing job: [0] = solve vertex count, [1] = solve index count. The mesh
        // arrays are capacity-sized, so the real extents have to travel through native memory for
        // the jobs to chain without a sync point.
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<int> counts;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<float2> vertices;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<int> indices;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<float2> controlPoints;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<int2> boneEdges;
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<int> pinIndices;
        public NativeArray<BoneWeight> weights;
        [DeallocateOnJobCompletion]
        public NativeArray<int> result;

        public void Execute()
        {
            for (int i = 0; i < weights.Length; ++i)
                weights[i] = BoundedBiharmonicWeightsGenerator.defaultWeight;

            // The preprocessing early-outed before producing a mesh; the old pipeline returned
            // defaults without calling (or logging from) the native module in that case.
            if (counts[0] == 0)
            {
                result[0] = 0;
                return;
            }

            int r = BbwSolverKernel.Solve(maxIterations,
                vertices, counts[0], originalVertexCount,
                indices, counts[1],
                controlPoints, controlPoints.Length,
                boneEdges, boneEdges.Length,
                pinIndices, pinIndices.Length,
                weights);
            result[0] = r;

            switch (r)
            {
                case 1:
                case 2:
                    Debug.LogWarning($"Weight generation failure due to unexpected mesh input. Re-generate the mesh with a different Outline Detail value to resolve the issue. Error Code: {r}");
                    break;
                case 3:
                    if (0 == supressCommonWarnings)
                        Debug.LogWarning($"Internal weight generation error. Error Code: {r}");
                    break;
            }
        }
    }

    // Clean-room replacement for the retired native BoundedBiharmonicWeightsModule (vendored
    // libigl + Eigen), preserving its entry-point contract: fills `weights` (one BoneWeight per
    // original vertex, top four influences, normalized) and returns 0 NoError / 1 InputError /
    // 2 BoundaryConditionsError / 3 BBWError. On a non-zero return the weights array is left
    // untouched; callers pre-fill it with the default weight, as the old pipeline did.
    //
    // Implemented from the published math, not from the MPL-licensed native dependencies:
    // bounded biharmonic weights per Jacobson, Baran, Popovic & Sorkine (SIGGRAPH 2011) - each
    // handle's weight field minimizes the discrete biharmonic energy w' (L M^-1 L) w subject to
    // w = 1 on its own handle, w = 0 on the others, and 0 <= w <= 1 - solved with a primal
    // active-set method (Nocedal & Wright ch. 16) over an RCM-ordered envelope (skyline) LL'
    // factorization. Cotangent Laplacian and Voronoi vertex areas per Meyer et al. 2003.
    //
    // Everything is computed in double and cast to float only on output, like the native module.
    // No transcendental functions are used (cotangents come from dot/cross ratios), and every
    // traversal and ordering decision breaks ties on explicit keys, so results are bit-identical
    // across platforms and backends (Burst and Mono): measured on win, win-arm64, mac, mac-arm64
    // and linux when the module was replaced, and asserted since by BbwSolverRegressionTest's
    // exact-bit goldens.
    internal static class BbwSolverKernel
    {
        // Vertices this close (squared) to a handle become fixed boundary conditions. Bone samples
        // and control points are inserted into the mesh with bit-identical coordinates, so their
        // distance is exactly zero; the epsilon only decides borderline original vertices.
        const double k_OnHandleDistanceSq = 1e-8;

        const double k_BoundSlack = 1e-14;

        // Sparse symmetric matrix in flat CSR form, rows frozen to ascending-column order so every
        // traversal is deterministic.
        struct SparseMatrix
        {
            public int n;
            public NativeArray<int> rowPtr; // n + 1
            public NativeArray<int> cols;
            public NativeArray<double> vals;
            public NativeArray<double> diag;
        }

        internal static int Solve(int maxIterations,
            in NativeArray<float2> vertices, int vertexCount, int originalVertexCount,
            in NativeArray<int> indices, int indexCount,
            in NativeArray<float2> controlPoints, int controlPointCount,
            in NativeArray<int2> boneEdges, int boneEdgeCount,
            in NativeArray<int> pinIndices, int pinIndexCount,
            NativeArray<BoneWeight> weights)
        {
            int handleCount = pinIndexCount + boneEdgeCount;

            int triCount = indexCount / 3;
            if (triCount == 0 || vertexCount < 3)
                return 1;

            // Compact to referenced vertices, preserving order (native: igl::remove_unreferenced).
            NativeArray<bool> referenced = new NativeArray<bool>(vertexCount, Allocator.Temp);
            for (int i = 0; i < triCount * 3; ++i)
                referenced[indices[i]] = true;

            NativeArray<int> newIndex = new NativeArray<int>(vertexCount, Allocator.Temp);
            NativeArray<int> oldIndex = new NativeArray<int>(vertexCount, Allocator.Temp);
            int n = 0;
            for (int v = 0; v < vertexCount; ++v)
            {
                if (referenced[v])
                {
                    newIndex[v] = n;
                    oldIndex[n] = v;
                    ++n;
                }
                else
                {
                    newIndex[v] = -1;
                }
            }

            if (n < 3)
                return 1;

            // No handles: the native path validates the mesh first, then solves zero weight columns
            // and reports success, leaving the caller's pre-filled defaults in place.
            if (handleCount == 0)
                return 0;

            NativeArray<double> px = new NativeArray<double>(n, Allocator.Temp);
            NativeArray<double> py = new NativeArray<double>(n, Allocator.Temp);
            for (int i = 0; i < n; ++i)
            {
                px[i] = vertices[oldIndex[i]].x;
                py[i] = vertices[oldIndex[i]].y;
            }

            NativeArray<int> tri = new NativeArray<int>(triCount * 3, Allocator.Temp);
            for (int i = 0; i < triCount * 3; ++i)
                tri[i] = newIndex[indices[i]];

            // Boundary conditions: a vertex sitting on a handle is fixed to 1 for that handle and
            // 0 for the rest. Columns are pins first, then bones. A vertex on several handles
            // (a bone-chain joint) splits evenly so its fixed values still sum to 1.
            int fixedCount = 0;
            NativeArray<int> fixedVerts = new NativeArray<int>(n, Allocator.Temp);
            NativeArray<double> fixedRows = new NativeArray<double>(n * handleCount, Allocator.Temp);
            NativeArray<double> row = new NativeArray<double>(handleCount, Allocator.Temp);
            for (int v = 0; v < n; ++v)
            {
                bool onHandle = false;
                for (int c = 0; c < handleCount; ++c)
                    row[c] = 0.0;
                for (int p = 0; p < pinIndexCount; ++p)
                {
                    float2 c = controlPoints[pinIndices[p]];
                    double dx = px[v] - c.x;
                    double dy = py[v] - c.y;
                    if ((dx * dx) + (dy * dy) < k_OnHandleDistanceSq)
                    {
                        row[p] = 1.0;
                        onHandle = true;
                    }
                }

                for (int b = 0; b < boneEdgeCount; ++b)
                {
                    float2 e0 = controlPoints[boneEdges[b].x];
                    float2 e1 = controlPoints[boneEdges[b].y];
                    double ex = (double)e1.x - e0.x;
                    double ey = (double)e1.y - e0.y;
                    double vx = px[v] - e0.x;
                    double vy = py[v] - e0.y;
                    double len2 = (ex * ex) + (ey * ey);
                    double t = len2 > 0.0 ? System.Math.Max(0.0, System.Math.Min(1.0, ((vx * ex) + (vy * ey)) / len2)) : 0.0;
                    double dx = vx - (t * ex);
                    double dy = vy - (t * ey);
                    if ((dx * dx) + (dy * dy) < k_OnHandleDistanceSq)
                    {
                        row[pinIndexCount + b] = 1.0;
                        onHandle = true;
                    }
                }

                if (onHandle)
                {
                    double sum = 0.0;
                    for (int c = 0; c < handleCount; ++c)
                        sum += row[c];
                    for (int c = 0; c < handleCount; ++c)
                        fixedRows[(fixedCount * handleCount) + c] = row[c] / sum;
                    fixedVerts[fixedCount] = v;
                    ++fixedCount;
                }
            }

            // With nothing pinned the bilaplacian system is singular; the factorization below would
            // fail anyway (the native module failed the same way), but failing early keeps the
            // error readable.
            if (fixedCount == 0)
                return 3;

            // Connected components no handle touches (isolated mesh regions) have no boundary
            // conditions, so their bilaplacian block is singular and every constant field minimizes
            // it. The native module's LU landed on exactly zero for them - zero right-hand side
            // through a factorization that did not reject the block - and callers restore the
            // default weight for all-zero rows. Pin those components to zero explicitly: the SPD
            // factorization stays valid and the observable result is identical.
            NativeArray<bool> pinnedZero = FindComponentsWithoutHandles(tri, n, fixedVerts, fixedCount);

            SparseMatrix Q = BuildBilaplacian(px, py, tri, n);

            // One bounded QP per handle. The minimizer is unique (Q is positive definite on the
            // free set once boundary vertices are pinned), so any exactly-converged method lands on
            // the same weights the native solver produces, up to floating-point noise.
            NativeArray<double> W = new NativeArray<double>(handleCount * n, Allocator.Temp);
            NativeArray<double> x = new NativeArray<double>(n, Allocator.Temp);
            SolveScratch scratch = SolveScratch.Allocate(n, Q.rowPtr[n]);
            for (int c = 0; c < handleCount; ++c)
            {
                if (!SolveColumn(Q, n, fixedVerts, fixedCount, fixedRows, handleCount, pinnedZero, c, maxIterations, x, ref scratch))
                    return 3;
                for (int i = 0; i < n; ++i)
                    W[(c * n) + i] = x[i];
            }

            // Top-four selection + normalization. The native module sorted each row with igl::sort
            // descending, kept four columns and normalized them; its tie order depended on the row
            // width: rows of up to three handles went through sort2/sort3 specializations, which
            // emit equal values in ascending column order, while wider rows went through std::sort
            // ascending + reverse (stable insertion sort for <= 16 elements), which emits them in
            // descending column order. Ties only occur between zero weights in practice, so this is
            // cosmetic, but reproducing it keeps the output bit-comparable with the module this
            // solver replaced.
            bool tiesDescend = handleCount > 3;
            int numWeights = System.Math.Min(4, handleCount);
            NativeArray<bool> used = new NativeArray<bool>(handleCount, Allocator.Temp);
            NativeArray<int> pick = new NativeArray<int>(4, Allocator.Temp);
            for (int i = 0; i < n; ++i)
            {
                int old = oldIndex[i];
                if (old >= originalVertexCount)
                    continue;

                for (int c = 0; c < handleCount; ++c)
                    used[c] = false;

                double sum = 0.0;
                for (int s = 0; s < numWeights; ++s)
                {
                    int best = -1;
                    for (int c = 0; c < handleCount; ++c)
                    {
                        if (used[c])
                            continue;
                        if (best < 0 || W[(c * n) + i] > W[(best * n) + i] ||
                            (W[(c * n) + i] == W[(best * n) + i] && tiesDescend && c > best))
                            best = c;
                    }
                    used[best] = true;
                    pick[s] = best;
                    sum += W[(best * n) + i];
                }

                if (sum == 0.0)
                    continue;

                BoneWeight bw = default;
                for (int s = 0; s < numWeights; ++s)
                    SetSlot(ref bw, s, pick[s], (float)(W[(pick[s] * n) + i] / sum));
                weights[old] = bw;
            }

            return 0;
        }

        // Union-find over triangle connectivity; marks every vertex whose connected component
        // contains no boundary-condition vertex.
        static NativeArray<bool> FindComponentsWithoutHandles(in NativeArray<int> tri, int n, in NativeArray<int> fixedVerts, int fixedCount)
        {
            NativeArray<int> parent = new NativeArray<int>(n, Allocator.Temp);
            for (int i = 0; i < n; ++i)
                parent[i] = i;

            for (int t = 0; t < tri.Length; t += 3)
            {
                int a = Find(parent, tri[t]);
                int b = Find(parent, tri[t + 1]);
                int c = Find(parent, tri[t + 2]);
                if (b != a) parent[b] = a;
                if (c != a) parent[c] = a;
            }

            NativeArray<bool> componentHasHandle = new NativeArray<bool>(n, Allocator.Temp);
            for (int f = 0; f < fixedCount; ++f)
                componentHasHandle[Find(parent, fixedVerts[f])] = true;

            NativeArray<bool> pinnedZero = new NativeArray<bool>(n, Allocator.Temp);
            for (int i = 0; i < n; ++i)
                pinnedZero[i] = !componentHasHandle[Find(parent, i)];
            return pinnedZero;
        }

        static int Find(NativeArray<int> parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        static void SetSlot(ref BoneWeight bw, int slot, int boneIndex, float weight)
        {
            switch (slot)
            {
                case 0: bw.boneIndex0 = boneIndex; bw.weight0 = weight; break;
                case 1: bw.boneIndex1 = boneIndex; bw.weight1 = weight; break;
                case 2: bw.boneIndex2 = boneIndex; bw.weight2 = weight; break;
                default: bw.boneIndex3 = boneIndex; bw.weight3 = weight; break;
            }
        }

        // Bilaplacian Q = L M^-1 L from the cotangent Laplacian L and lumped vertex areas M.
        // Built as sum_k (1/m_k) l_k l_k' over the sparse rows l_k of L, which is O(n * degree^2)
        // and yields the two-ring stencil. Degenerate triangles produce infinite cotangents, as
        // they did in the native module's double pipeline; the resulting non-finite pivots surface
        // as a factorization failure (error 3).
        static SparseMatrix BuildBilaplacian(in NativeArray<double> px, in NativeArray<double> py, in NativeArray<int> tri, int n)
        {
            int triCount = tri.Length / 3;

            // Scratch CSR for L with capacity bounds: a triangle adds two off-diagonal partners per
            // corner, plus one diagonal slot per row. Rows accumulate in first-touch order, which
            // the Q build below relies on for reproducible summation.
            NativeArray<int> lStart = new NativeArray<int>(n + 1, Allocator.Temp);
            for (int t = 0; t < triCount * 3; ++t)
                lStart[tri[t] + 1] += 2;
            for (int i = 0; i < n; ++i)
                lStart[i + 1] += lStart[i] + 1;
            NativeArray<int> lCols = new NativeArray<int>(lStart[n], Allocator.Temp);
            NativeArray<double> lVals = new NativeArray<double>(lStart[n], Allocator.Temp);
            NativeArray<int> lCount = new NativeArray<int>(n, Allocator.Temp);
            NativeArray<double> mass = new NativeArray<double>(n, Allocator.Temp);

            for (int t = 0; t < triCount; ++t)
            {
                int a = tri[t * 3];
                int b = tri[(t * 3) + 1];
                int c = tri[(t * 3) + 2];

                double abx = px[b] - px[a], aby = py[b] - py[a];
                double acx = px[c] - px[a], acy = py[c] - py[a];
                double bcx = px[c] - px[b], bcy = py[c] - py[b];

                double cross = System.Math.Abs((abx * acy) - (aby * acx)); // 2 * area
                double da = (abx * acx) + (aby * acy);                     // angle at a
                double db = (-abx * bcx) + (-aby * bcy);                   // angle at b
                double dc = (acx * bcx) + (acy * bcy);                     // angle at c

                double cotA = da / cross;
                double cotB = db / cross;
                double cotC = dc / cross;

                AddLaplacianEdge(lCols, lVals, lCount, lStart, b, c, 0.5 * cotA);
                AddLaplacianEdge(lCols, lVals, lCount, lStart, a, c, 0.5 * cotB);
                AddLaplacianEdge(lCols, lVals, lCount, lStart, a, b, 0.5 * cotC);

                double area = 0.5 * cross;
                if (da >= 0.0 && db >= 0.0 && dc >= 0.0)
                {
                    // Non-obtuse: Voronoi areas, 1/8 sum over incident edges of |e|^2 cot(opposite).
                    // Voronoi (not barycentric) lumping is what the native module used - confirmed
                    // bit-level during the replacement.
                    double lab = (abx * abx) + (aby * aby);
                    double lac = (acx * acx) + (acy * acy);
                    double lbc = (bcx * bcx) + (bcy * bcy);
                    mass[a] += ((lab * cotC) + (lac * cotB)) / 8.0;
                    mass[b] += ((lab * cotC) + (lbc * cotA)) / 8.0;
                    mass[c] += ((lac * cotB) + (lbc * cotA)) / 8.0;
                }
                else
                {
                    // Obtuse: half the area to the obtuse corner, a quarter to the others.
                    mass[a] += da < 0.0 ? area / 2.0 : area / 4.0;
                    mass[b] += db < 0.0 ? area / 2.0 : area / 4.0;
                    mass[c] += dc < 0.0 ? area / 2.0 : area / 4.0;
                }
            }

            // Q row capacities: L is symmetric, so row i receives one contribution block from every
            // row k it appears in, i.e. from every k in row i, of at most that row's entry count.
            NativeArray<int> qStart = new NativeArray<int>(n + 1, Allocator.Temp);
            for (int i = 0; i < n; ++i)
            {
                int cap = 0;
                for (int e = 0; e < lCount[i]; ++e)
                    cap += lCount[lCols[lStart[i] + e]];
                qStart[i + 1] = qStart[i] + cap;
            }
            NativeArray<int> qCols = new NativeArray<int>(qStart[n], Allocator.Temp);
            NativeArray<double> qVals = new NativeArray<double>(qStart[n], Allocator.Temp);
            NativeArray<int> qCount = new NativeArray<int>(n, Allocator.Temp);

            for (int k = 0; k < n; ++k)
            {
                double invm = 1.0 / mass[k];
                int rowStart = lStart[k];
                int rowLen = lCount[k];
                for (int a = 0; a < rowLen; ++a)
                {
                    int i = lCols[rowStart + a];
                    double li = lVals[rowStart + a] * invm;
                    for (int b = 0; b < rowLen; ++b)
                        AddEntry(qCols, qVals, qCount, qStart, i, lCols[rowStart + b], li * lVals[rowStart + b]);
                }
            }

            SparseMatrix result = default;
            result.n = n;
            result.rowPtr = new NativeArray<int>(n + 1, Allocator.Temp);
            result.diag = new NativeArray<double>(n, Allocator.Temp);
            for (int i = 0; i < n; ++i)
                result.rowPtr[i + 1] = result.rowPtr[i] + qCount[i];
            result.cols = new NativeArray<int>(result.rowPtr[n], Allocator.Temp);
            result.vals = new NativeArray<double>(result.rowPtr[n], Allocator.Temp);
            for (int i = 0; i < n; ++i)
            {
                int src = qStart[i];
                int dst = result.rowPtr[i];
                int count = qCount[i];
                for (int e = 0; e < count; ++e)
                {
                    result.cols[dst + e] = qCols[src + e];
                    result.vals[dst + e] = qVals[src + e];
                }
                // Ascending-column insertion sort; sorting after accumulation leaves values intact.
                for (int a = 1; a < count; ++a)
                {
                    int c = result.cols[dst + a];
                    double v = result.vals[dst + a];
                    int b = a - 1;
                    while (b >= 0 && result.cols[dst + b] > c)
                    {
                        result.cols[dst + b + 1] = result.cols[dst + b];
                        result.vals[dst + b + 1] = result.vals[dst + b];
                        --b;
                    }
                    result.cols[dst + b + 1] = c;
                    result.vals[dst + b + 1] = v;
                }
                for (int e = 0; e < count; ++e)
                {
                    if (result.cols[dst + e] == i)
                    {
                        result.diag[i] = result.vals[dst + e];
                        break;
                    }
                }
            }
            return result;
        }

        // Accumulates into a bounded scratch CSR row with linear-scan dedup; appends keep
        // first-touch order, which reproducible summation depends on. Rows are small (one- or
        // two-ring stencils), so the scan is cheap.
        static void AddEntry(NativeArray<int> cols, NativeArray<double> vals, NativeArray<int> count, in NativeArray<int> start, int i, int j, double w)
        {
            int s = start[i];
            int c = count[i];
            for (int e = 0; e < c; ++e)
            {
                if (cols[s + e] == j)
                {
                    vals[s + e] += w;
                    return;
                }
            }
            cols[s + c] = j;
            vals[s + c] = w;
            count[i] = c + 1;
        }

        static void AddLaplacianEdge(NativeArray<int> cols, NativeArray<double> vals, NativeArray<int> count, in NativeArray<int> start, int i, int j, double w)
        {
            AddEntry(cols, vals, count, start, i, j, w);
            AddEntry(cols, vals, count, start, j, i, w);
            AddEntry(cols, vals, count, start, i, i, -w);
            AddEntry(cols, vals, count, start, j, j, -w);
        }

        // Primal active-set solve of min 1/2 x'Qx with x fixed on boundary vertices and bounded to
        // [0,1] elsewhere. Each iteration solves the equality-constrained problem on the free set,
        // clamps bound violations into the active set, and releases active bounds whose Lagrange
        // multiplier (the gradient component (Qx)_i) has the wrong sign. Terminates at the exact
        // KKT point; iteration cap mirrors the native solver's max_iter (failure -> error 3).
        // Scratch shared by every column solve and every active-set iteration. Allocator.Temp is
        // not reclaimed until the job ends, so allocating these per iteration made the job's peak
        // grow with columns x iterations.
        struct SolveScratch
        {
            public NativeArray<byte> state;
            public NativeArray<int> free;
            public NativeArray<int> localIndex;
            public NativeArray<double> rhs;
            public NativeArray<int> aStart;
            public NativeArray<int> aCols;
            public NativeArray<double> aVals;
            public NativeArray<int> degree;
            public NativeArray<int> order;
            public NativeArray<bool> visited;
            public NativeArray<int> neighbors;
            public NativeArray<int> perm;
            public NativeArray<int> pos;
            public NativeArray<int> first;
            public NativeArray<long> envPtr;
            public NativeArray<double> b2;
            public NativeArray<double> data; // envelope; the only size that is not bounded by n

            public static SolveScratch Allocate(int n, int nnz)
            {
                return new SolveScratch
                {
                    state = new NativeArray<byte>(n, Allocator.Temp),
                    free = new NativeArray<int>(n, Allocator.Temp),
                    localIndex = new NativeArray<int>(n, Allocator.Temp),
                    rhs = new NativeArray<double>(n, Allocator.Temp),
                    aStart = new NativeArray<int>(n + 1, Allocator.Temp),
                    aCols = new NativeArray<int>(nnz, Allocator.Temp),
                    aVals = new NativeArray<double>(nnz, Allocator.Temp),
                    degree = new NativeArray<int>(n, Allocator.Temp),
                    order = new NativeArray<int>(n, Allocator.Temp),
                    visited = new NativeArray<bool>(n, Allocator.Temp),
                    neighbors = new NativeArray<int>(n, Allocator.Temp),
                    perm = new NativeArray<int>(n, Allocator.Temp),
                    pos = new NativeArray<int>(n, Allocator.Temp),
                    first = new NativeArray<int>(n, Allocator.Temp),
                    envPtr = new NativeArray<long>(n + 1, Allocator.Temp),
                    b2 = new NativeArray<double>(n, Allocator.Temp),
                    data = default,
                };
            }

            // The envelope shrinks and grows as the free set changes, so size it on demand and
            // keep the largest seen. Doubling bounds the waste at roughly one extra copy.
            public void EnsureEnvelope(int size)
            {
                if (data.IsCreated && data.Length >= size)
                    return;
                int capacity = data.IsCreated ? data.Length : 1024;
                while (capacity < size)
                    capacity *= 2;
                data = new NativeArray<double>(capacity, Allocator.Temp);
            }
        }

        static bool SolveColumn(in SparseMatrix Q, int n, in NativeArray<int> fixedVerts, int fixedCount, in NativeArray<double> fixedRows, int handleCount, in NativeArray<bool> pinnedZero, int column, int maxIterations, NativeArray<double> x, ref SolveScratch scratch)
        {
            const byte kFree = 0, kFixed = 1, kAtLower = 2, kAtUpper = 3;
            NativeArray<byte> state = scratch.state;
            for (int i = 0; i < n; ++i)
            {
                x[i] = 0.0;
                state[i] = kFree; // reused across columns, so kFree cannot be left to zero-init
                if (pinnedZero[i])
                    state[i] = kFixed; // x stays 0
            }
            for (int f = 0; f < fixedCount; ++f)
            {
                state[fixedVerts[f]] = kFixed;
                x[fixedVerts[f]] = fixedRows[(f * handleCount) + column];
            }

            NativeArray<int> free = scratch.free;
            NativeArray<int> localIndex = scratch.localIndex;
            for (int it = 0; it < maxIterations; ++it)
            {
                int m = 0;
                for (int i = 0; i < n; ++i)
                {
                    localIndex[i] = -1;
                    if (state[i] == kFree)
                    {
                        localIndex[i] = m;
                        free[m++] = i;
                    }
                }

                if (m > 0)
                {
                    NativeArray<double> rhs = scratch.rhs;
                    for (int r = 0; r < m; ++r)
                    {
                        int i = free[r];
                        double s = 0.0;
                        for (int e = Q.rowPtr[i]; e < Q.rowPtr[i + 1]; ++e)
                        {
                            int k = Q.cols[e];
                            if (state[k] != kFree && x[k] != 0.0)
                                s += Q.vals[e] * x[k];
                        }
                        rhs[r] = -s;
                    }

                    if (!SkylineSolve(Q, free, localIndex, m, rhs, ref scratch))
                        return false;

                    for (int r = 0; r < m; ++r)
                        x[free[r]] = rhs[r];
                }

                int violations = 0;
                for (int r = 0; r < m; ++r)
                {
                    int i = free[r];
                    if (x[i] < -k_BoundSlack)
                    {
                        state[i] = kAtLower;
                        x[i] = 0.0;
                        ++violations;
                    }
                    else if (x[i] > 1.0 + k_BoundSlack)
                    {
                        state[i] = kAtUpper;
                        x[i] = 1.0;
                        ++violations;
                    }
                }
                if (violations > 0)
                    continue;

                int released = 0;
                for (int i = 0; i < n; ++i)
                {
                    if (state[i] != kAtLower && state[i] != kAtUpper)
                        continue;
                    double g = 0.0;
                    for (int e = Q.rowPtr[i]; e < Q.rowPtr[i + 1]; ++e)
                        g += Q.vals[e] * x[Q.cols[e]];
                    double slack = 1e-12 * System.Math.Abs(Q.diag[i]);
                    if (state[i] == kAtLower && g < -slack)
                    {
                        state[i] = kFree;
                        ++released;
                    }
                    else if (state[i] == kAtUpper && g > slack)
                    {
                        state[i] = kFree;
                        ++released;
                    }
                }
                if (released == 0)
                    return true;
            }

            // Out of iterations: keep the current iterate, which already honours the fixed values and
            // the 0..1 bounds. The native module's active set did the same and only reported it.
            return true;
        }

        // Direct solve of the free subsystem: reverse Cuthill-McKee ordering to shrink the envelope
        // (bilaplacian rows of a 2D mesh have O(sqrt n) bandwidth once reordered), then an envelope
        // (skyline) LL' factorization. Non-positive or non-finite pivots - singular or degenerate
        // systems - report failure, which Solve maps to error 3. Deterministic: every ordering
        // decision breaks ties on explicit (degree, index) keys.
        static bool SkylineSolve(in SparseMatrix Q, in NativeArray<int> free, in NativeArray<int> localIndex, int m, NativeArray<double> rhs, ref SolveScratch scratch)
        {
            // Local adjacency (free-to-free couplings only) as flat CSR, ascending because Q rows
            // are sorted and the free list is ascending.
            NativeArray<int> aStart = scratch.aStart;
            aStart[0] = 0; // reused, so the prefix sum cannot start from zero-init
            for (int r = 0; r < m; ++r)
            {
                int i = free[r];
                int count = 0;
                for (int e = Q.rowPtr[i]; e < Q.rowPtr[i + 1]; ++e)
                {
                    if (localIndex[Q.cols[e]] >= 0)
                        ++count;
                }
                aStart[r + 1] = aStart[r] + count;
            }
            NativeArray<int> aCols = scratch.aCols;
            NativeArray<double> aVals = scratch.aVals;
            NativeArray<int> degree = scratch.degree;
            for (int r = 0; r < m; ++r)
            {
                int i = free[r];
                int w = aStart[r];
                for (int e = Q.rowPtr[i]; e < Q.rowPtr[i + 1]; ++e)
                {
                    int lc = localIndex[Q.cols[e]];
                    if (lc >= 0)
                    {
                        aCols[w] = lc;
                        aVals[w] = Q.vals[e];
                        ++w;
                    }
                }
                degree[r] = w - aStart[r];
            }

            // Reverse Cuthill-McKee.
            NativeArray<int> order = scratch.order;
            NativeArray<bool> visited = scratch.visited;
            NativeArray<int> neighbors = scratch.neighbors;
            for (int r = 0; r < m; ++r)
                visited[r] = false; // reused, so the unvisited marker cannot be left to zero-init
            int ordered = 0;
            while (ordered < m)
            {
                int seed = -1;
                for (int r = 0; r < m; ++r)
                {
                    if (!visited[r] && (seed < 0 || degree[r] < degree[seed]))
                        seed = r;
                }
                visited[seed] = true;
                int head = ordered;
                order[ordered++] = seed;
                while (head < ordered)
                {
                    int u = order[head++];
                    int nb = 0;
                    for (int e = aStart[u]; e < aStart[u + 1]; ++e)
                    {
                        int v = aCols[e];
                        if (!visited[v])
                        {
                            visited[v] = true;
                            neighbors[nb++] = v;
                        }
                    }
                    // Insertion sort on (degree, index) - the same strict order the comparator-based
                    // sort produced, without a managed delegate.
                    for (int si = 1; si < nb; ++si)
                    {
                        int sv = neighbors[si];
                        int sj = si - 1;
                        while (sj >= 0 && (degree[neighbors[sj]] > degree[sv] || (degree[neighbors[sj]] == degree[sv] && neighbors[sj] > sv)))
                        {
                            neighbors[sj + 1] = neighbors[sj];
                            --sj;
                        }
                        neighbors[sj + 1] = sv;
                    }
                    for (int e = 0; e < nb; ++e)
                        order[ordered++] = neighbors[e];
                }
            }
            NativeArray<int> perm = scratch.perm; // permuted position -> local index
            NativeArray<int> pos = scratch.pos;   // local index -> permuted position
            for (int p = 0; p < m; ++p)
            {
                perm[p] = order[m - 1 - p];
                pos[perm[p]] = p;
            }

            // Envelope structure: row p spans columns first[p]..p in permuted numbering.
            NativeArray<int> first = scratch.first;
            for (int p = 0; p < m; ++p)
            {
                int lo = p;
                int li = perm[p];
                for (int e = aStart[li]; e < aStart[li + 1]; ++e)
                {
                    int q = pos[aCols[e]];
                    if (q < lo)
                        lo = q;
                }
                first[p] = lo;
            }
            NativeArray<long> envPtr = scratch.envPtr;
            envPtr[0] = 0; // reused, so the running offset cannot start from zero-init
            for (int p = 0; p < m; ++p)
                envPtr[p + 1] = envPtr[p] + (p - first[p] + 1);
            if (envPtr[m] > int.MaxValue)
                return false;
            int envSize = (int)envPtr[m];
            scratch.EnsureEnvelope(envSize);
            NativeArray<double> data = scratch.data;
            for (int e = 0; e < envSize; ++e)
                data[e] = 0.0; // the factorization reads entries no edge writes as zero
            for (int p = 0; p < m; ++p)
            {
                int li = perm[p];
                for (int e = aStart[li]; e < aStart[li + 1]; ++e)
                {
                    int q = pos[aCols[e]];
                    if (q <= p)
                        data[(int)envPtr[p] + (q - first[p])] = aVals[e];
                }
            }

            // Envelope LL' factorization, in place.
            for (int p = 0; p < m; ++p)
            {
                int rowP = (int)envPtr[p];
                int firstP = first[p];
                for (int q = firstP; q < p; ++q)
                {
                    int rowQ = (int)envPtr[q];
                    int lo = System.Math.Max(firstP, first[q]);
                    double s = data[rowP + (q - firstP)];
                    for (int k = lo; k < q; ++k)
                        s -= data[rowP + (k - firstP)] * data[rowQ + (k - first[q])];
                    data[rowP + (q - firstP)] = s / data[rowQ + (q - first[q])];
                }
                double d = data[rowP + (p - firstP)];
                for (int k = firstP; k < p; ++k)
                {
                    double v = data[rowP + (k - firstP)];
                    d -= v * v;
                }
                if (!(d > 0.0) || double.IsInfinity(d))
                    return false;
                data[rowP + (p - firstP)] = System.Math.Sqrt(d);
            }

            // Permute rhs, solve L y = b then L' x = y, permute back.
            NativeArray<double> b2 = scratch.b2;
            for (int p = 0; p < m; ++p)
                b2[p] = rhs[perm[p]];
            for (int p = 0; p < m; ++p)
            {
                double s = b2[p];
                int rowP = (int)envPtr[p];
                int firstP = first[p];
                for (int k = firstP; k < p; ++k)
                    s -= data[rowP + (k - firstP)] * b2[k];
                b2[p] = s / data[rowP + (p - firstP)];
            }
            for (int q = m - 1; q >= 0; --q)
            {
                int rowQ = (int)envPtr[q];
                int firstQ = first[q];
                double xq = b2[q] / data[rowQ + (q - firstQ)];
                b2[q] = xq;
                for (int k = firstQ; k < q; ++k)
                    b2[k] -= data[rowQ + (k - firstQ)] * xq;
            }
            for (int p = 0; p < m; ++p)
                rhs[perm[p]] = b2[p];

            return true;
        }
    }

}
