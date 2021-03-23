using System;
using System.Collections.Generic;
using System.Linq;

namespace DoLFLibrary
{
    internal class DistanceSegmFree
    {
        #region Settings

        public float NearNeighborArea { get; set; }

        public float SimilarityCenterLocalPoints { get; set; }

        public int ClosestCenters { get; set; }

        #endregion

        private int L;


        public float[] GetNormalizedDescriptor(DoLF.DsLPoints[] myDsLPs)
        {
            var descriptor = new List<float>();
            if (myDsLPs.Length <= 0) return descriptor.ToArray();
            int length = myDsLPs[0].Descriptor.Length + 4;
            foreach (var lp in myDsLPs)
            {
                descriptor.Add(lp.X);
                descriptor.Add(lp.Y);
                descriptor.Add(lp.Gradient);
                descriptor.Add(0);
                descriptor.AddRange(lp.Descriptor);
            }
            descriptor.Add(length);
            return descriptor.ToArray();
        }

        public List<DoLF.Result> GetSimilarity(float[] query, int QueryWidth, int QueryHeight, float[] doc)
        {
            if (query.Length == 0)
                return new List<DoLF.Result>();
            L = (int)query[^1];
            int nLPquery = (query.Length - 1) / L;
            int nLPdoc = (doc.Length - 1) / L;
            if (nLPquery == 0 || nLPdoc == 0)
                return new List<DoLF.Result>();


            int[] qCenters = GetClosestsToMeanXY(query, ClosestCenters);

            var orderedDis3 = new List<PointDistance>();


            foreach (var qCenter in qCenters)
            {
                var qCenterX = (int)query[qCenter * L];
                var qCenterY = (int)query[qCenter * L + 1];
                float wToh = QueryWidth / (float)QueryHeight;
                Tuple<float, float> queryMeanDistance = GetMeanDistance(qCenter, query, wToh);



                var dis = new PointDistance[nLPdoc];


                //find the distance to all localpoints
                for (int i = 0; i < nLPdoc; i++)
                {
                    float distance = 0;
                    for (int j = 4; j < L; j++)
                    {
                        distance += Math.Abs(query[qCenter * L + j] - doc[i * L + j]);
                    }
                    distance = distance / (L - 4);
                    dis[i] = new PointDistance { Point = i, Distance = distance };
                }


                List<PointDistance> orderedDis =
                    dis.Where(x => x.Distance < this.SimilarityCenterLocalPoints).OrderBy(x => x.Distance)
                        .Take(500).ToList();
                if (orderedDis.Count() == 0)
                    orderedDis = dis.OrderBy(x => x.Distance).Take(250).ToList();
                List<int> queryPointsInUnit = GetPoints(qCenter, query, queryMeanDistance.Item1, queryMeanDistance.Item2);


                Dictionary<int, Tuple<float, float>> transformedQueryPointsInUnit = Transform(query, qCenter,
                                                                                        queryMeanDistance.Item1, queryMeanDistance.Item2, wToh, queryPointsInUnit);


                foreach (PointDistance p in orderedDis)
                {
                    Tuple<double, double, double> result = GetMeanDistanceForSpatialTexture(transformedQueryPointsInUnit,
                                                               queryMeanDistance.Item1, queryMeanDistance.Item2, p.Point, doc, query, doc, wToh,
                                                               this.NearNeighborArea);
                    float w1 = (qCenterX / (queryMeanDistance.Item1)) * (float)result.Item1;
                    float w2 = ((QueryWidth - qCenterX) / (queryMeanDistance.Item1)) * (float)result.Item1;
                    float h1 = (qCenterY / queryMeanDistance.Item2) * (float)result.Item2;
                    float h2 = ((QueryHeight - qCenterY) / queryMeanDistance.Item2) * (float)result.Item2;

                    p.MeanX = (float)result.Item1;
                    p.MeanY = (float)result.Item2;
                    p.CorrelateDistance = (float)result.Item3;
                    p.ContainingPoints = GetPointsNonUniform(p.Point, doc, w1, w2, h1, h2, out int minX, out int minY, out int maxX,
                        out int maxY);
                    p.Block = new [] { doc[p.Point * L] - w1, doc[p.Point * L + 1] - h1, w1 + w2, h1 + h2 };



                }




                var orderedDis2 =
                    orderedDis.OrderBy(x => x.CorrelateDistance).ToList();


                Dictionary<int, Tuple<float, float>> queryTransformPoints = Transform(query, qCenter,
                                                                                queryMeanDistance.Item1, queryMeanDistance.Item2, wToh);

                foreach (PointDistance p in orderedDis2)
                {
                    var myRFPNN = new RelativeFixedPointNearNeighbors();
                    Dictionary<int, Tuple<float, float>> wordPoints = Transform(doc, p.Point, p.MeanX, p.MeanY,
                                                                          p.Block[2] / p.Block[3], p.ContainingPoints);
                    Tuple<float, Dictionary<int, float>, DSLPointsSegmFree[], DSLPointsSegmFree[]>
                    results = myRFPNN.GetSimilarity(query, doc, queryTransformPoints, wordPoints, L,
                                  this.NearNeighborArea);
                    p.Similarity = results.Item1;
                }


                orderedDis3.AddRange(orderedDis2);

            }

            var orderedDis4 = orderedDis3.OrderBy(x => x.Similarity).ToArray();



            for (int i = 0; i < orderedDis4.Length; i++)
            {
                PointDistance cp = orderedDis4[i];
                if (cp == null)
                    continue;
                for (int j = i + 1; j < orderedDis4.Length; j++)
                {
                    PointDistance tempP = orderedDis4[j];
                    if (tempP == null)
                        continue;
                    float[] overlap = cp.Intersect(tempP.Block);
                    float[] union = cp.Union(tempP.Block);
                    float area = cp.Block[2] * cp.Block[3] > tempP.Block[2] * tempP.Block[3] ? tempP.Block[2] * tempP.Block[3] : cp.Block[2] * cp.Block[3];
                    if (!(overlap?[2] > 0) || !(overlap?[3] > 0) ||
                        !((overlap?[2] * overlap?[3]) / area > 0.5)) continue;
                    if (tempP.Similarity < cp.Similarity)
                    {
                        orderedDis4[i] = orderedDis4[j];
                        orderedDis4[j] = null;
                    }
                    else if (Math.Abs(tempP.Similarity - cp.Similarity) < float.Epsilon)
                    {
                        if (!(cp.Block[2] * cp.Block[3] >= tempP.Block[2] * tempP.Block[3])) continue;
                        orderedDis4[i] = orderedDis4[j];
                        orderedDis4[j] = null;
                    }
                    else
                    {
                        orderedDis4[j] = null;
                    }

                }
            }


            IEnumerable<DoLF.Result> finalResults =
                orderedDis4.Where(x => x != null).Select((x, index) => new DoLF.Result
                {
                    Similarity = x.Similarity,
                    Block = new[]
                        {
                            (int)(x.Block[0] < 0 ? 0 : x.Block[0]),
                            (int)(x.Block[0] < 0 ? 0 : x.Block[1]),
                            (int)x.Block[2],
                            (int)x.Block[3]
                        },
                    Position = index
                });




            return finalResults.ToList();
        }


        public Tuple<double, double, double> GetMeanDistanceForSpatialTexture(
            Dictionary<int, Tuple<float, float>> queryPoints, float meanDistanceQX, float meanDistanceQY, int centerW,
            float[] W, float[] query, float[] doc, float wToh, float proximity)
        {
            float startx = meanDistanceQX - meanDistanceQX / 3;
            float starty = meanDistanceQY - meanDistanceQY / 3;

            float endx = meanDistanceQX + meanDistanceQX / 3;
            float endy = meanDistanceQY + meanDistanceQY / 3;

            float stepx = meanDistanceQX / 3;
            float stepy = meanDistanceQY / 3;

            double similarity = double.MaxValue;
            double meanDistanceWx = 0;
            double meanDistanceWy = 0;
            if (Math.Abs(startx - endx) < float.Epsilon)
            {
                endx = startx + 1;
                stepx = 1;
            }
            if (Math.Abs(starty - endy) < float.Epsilon)
            {
                endy = starty + 1;
                stepy = 1;
            }
            var myRFPNN = new RelativeFixedPointNearNeighbors();
            for (float dX = startx, dY = starty; dX <= endx && dY <= endy; dX += stepx, dY += stepy)
            {
                List<int> wpoints = GetPoints(centerW, W, dX, dY);
                Dictionary<int, Tuple<float, float>> transformedPoints = Transform(doc, centerW, dX, dY, wToh, wpoints);
                double currentC = myRFPNN.GetSimilarity(query, doc, queryPoints, transformedPoints, L, proximity).Item1;
                if (currentC < similarity)
                {
                    similarity = currentC;
                    meanDistanceWx = dX;
                    meanDistanceWy = dY;
                }
            }
            return new Tuple<double, double, double>(meanDistanceWx, meanDistanceWy, similarity);
        }


        public List<int> GetPoints(int Point, float[] vector, float DistanceX, float DistanceY)
        {
            int pointPos = Point * L;
            int nVector = vector.Length / L;
            var points = new List<int>();
            for (int i = 0; i < nVector; i++)
            {
                int vPos = i * L;
                if (Math.Abs(vector[pointPos] - vector[vPos]) > DistanceX)
                    continue;
                if (Math.Abs(vector[pointPos + 1] - vector[vPos + 1]) > DistanceY)
                    continue;
                if ((((vector[pointPos] - vector[vPos]) * (vector[pointPos] - vector[vPos])) / (DistanceX * DistanceX) + ((vector[pointPos + 1] - vector[vPos + 1]) * (vector[pointPos + 1] - vector[vPos + 1])) / (DistanceY * DistanceY)) > 1)
                    continue;
                points.Add(i);
            }
            return points;
        }


        public List<int> GetPointsNonUniform(int Point, float[] vector, float DistanceX1, float DistanceX2,
                                             float DistanceY1, float DistanceY2, out int minX, out int minY, out int maxX, out int maxY)
        {
            int pointPos = Point * L;
            var pX = (int)vector[pointPos];
            var pY = (int)vector[pointPos + 1];
            int nVector = vector.Length / L;
            minX = minY = int.MaxValue - 1;
            maxX = maxY = int.MinValue + 1;
            var Points = new List<int>();
            for (int i = 0; i < nVector; i++)
            {
                int vPos = i * L;
                var vX = (int)vector[vPos];
                var vY = (int)vector[vPos + 1];
                if (vX < pX && ((pX - vX) > DistanceX1))
                    continue;
                if (vX > pX && (vX - pX) > DistanceX2)
                    continue;
                if (vY < pY && (pY - vY) > DistanceY1)
                    continue;
                if (vY > pY && (vY - pY) > DistanceY2)
                    continue;
                if ((((pX - vX) * (pX - vX)) / (DistanceX1 * DistanceX2) + ((pY - vY) * (pY - vY)) / (DistanceY1 * DistanceY2)) > 1)
                    continue;
                if (vX < minX)
                    minX = vX;
                if (vY < minY)
                    minY = vY;
                if (vX > maxX)
                    maxX = vX;
                if (vY > maxY)
                    maxY = vY;
                Points.Add(i);
            }
            return Points;
        }


        public Dictionary<int, Tuple<float, float>> Transform(float[] vector, int centerPoint, float MeanDistanceX,
                                                              float MeanDistanceY, float wToh, List<int> Points = null)
        {
            if (Points == null)
            {
                int n = vector.Length / L;
                Points = new List<int>();
                for (int i = 0; i < n; i++)
                    Points.Add(i);
            }

            float centerPointX = vector[centerPoint * L];
            float centerPointY = vector[centerPoint * L + 1];

            var newDims = new Dictionary<int, Tuple<float, float>>();

            foreach (int p in Points)
            {
                newDims.Add(p, new Tuple<float, float>((vector[p * L] - centerPointX) / MeanDistanceX,
                        (vector[p * L + 1] - centerPointY) / (wToh * MeanDistanceY)));
            }
            return newDims;
        }


        public Tuple<float, float> GetMeanDistance(int Point, float[] vector, float wToh)
        {
            double dX = 0, dY = 0;
            int nlp = vector.Length / L;
            int qPos = Point * L;
            for (int i = 0; i < nlp; i++)
            {
                //if (Point == i) continue;
                int vPos = i * L;
                dX += Math.Abs(vector[vPos] - vector[qPos]);
                dY += Math.Abs(vector[vPos + 1] - vector[qPos + 1]);
            }
            dX /= nlp;
            dY /= nlp;
            //dY *= wToh;
            return new Tuple<float, float>((float)dX, (float)dY);
        }


        public int GetClosestToMeanXY(float[] v)
        {
            float meanX = 0, meanY = 0;
            int n = v.Length / L;
            for (int i = 0; i < n; i++)
            {
                int pos = i * L;
                meanX += v[pos];
                meanY += v[pos + 1];
            }
            meanX /= n;
            meanY /= n;
            //find the closest point
            int c = -1;
            float distance = float.MaxValue - 1;
            for (int i = 0; i < n; i++)
            {
                int pos = i * L;
                float d = (meanX - v[pos]) * (meanX - v[pos]) + (meanY - v[pos + 1]) * (meanY - v[pos + 1]);
                if (d < distance)
                {
                    distance = d;
                    c = i;
                }
            }

            return c;
        }

        public int[] GetClosestsToMeanXY(float[] v, int topPoints)
        {
            float meanX = 0, meanY = 0;
            int n = v.Length / L;
            for (int i = 0; i < n; i++)
            {
                int pos = i * L;
                meanX += v[pos];
                meanY += v[pos + 1];
            }
            meanX /= n;
            meanY /= n;
            //find the closest point
            var distances = new List<Tuple<int, float>>();
            for (int i = 0; i < n; i++)
            {
                int pos = i * L;
                float d = (meanX - v[pos]) * (meanX - v[pos]) + (meanY - v[pos + 1]) * (meanY - v[pos + 1]);
                distances.Add(new Tuple<int, float>(i, d));
            }
            var closestsPoints = distances.OrderBy(x => x.Item2).Take(topPoints).Select(x => x.Item1).ToArray();
            return closestsPoints;
        }

        #region Debug




        #endregion

        internal class PointDistance
        {
            public int Point { get; set; }

            public float Distance { get; set; }

            public float CorrelateDistance { get; set; }

            public float[] Block { get; set; }

            public List<int> ContainingPoints { get; set; }

            public float Similarity { get; set; }

            public float MeanX { get; set; }

            public float MeanY { get; set; }


            public float[] Intersect(float[] BlockB)
            {
                var x = Math.Max(this.Block[0], BlockB[0]);
                var num1 = Math.Min(this.Block[0] + this.Block[2], BlockB[0] + BlockB[2]);
                var y = Math.Max(this.Block[1], BlockB[1]);
                var num2 = Math.Min(this.Block[1] + this.Block[3], BlockB[1] + BlockB[3]);

                return (num1 >= x && num2 >= y)
                    ? new[] { x, y, num1 - x, num2 - y }
                    : null;
            }

            public float[] Union(float[] BlockB)
            {
                var x1 = Math.Min(Block[0], BlockB[0]);
                var x2 = Math.Max(Block[0] + Block[2], BlockB[0] + BlockB[2]);
                var y1 = Math.Min(Block[1], BlockB[1]);
                var y2 = Math.Max(Block[1] + Block[3], BlockB[1] + BlockB[3]);
                return new[] { x1, y1, x2 - x1, y2 - y1 };
            }


        }

        internal class DSLPointsSegmFree : DoLF.DsLPoints
        {
            public int GroupID { get; set; }

            public DSLPointsSegmFree(float X, float Y, int GroupID)
                : base(X, Y)
            {
                this.GroupID = GroupID;
            }
        }

        internal class RelativeFixedPointNearNeighbors
        {
            public Tuple<float, Dictionary<int, float>, DSLPointsSegmFree[], DSLPointsSegmFree[]>
                GetSimilarity(float[] queryVector, float[] wordVector, Dictionary<int, Tuple<float, float>> queryPoints,
                              Dictionary<int, Tuple<float, float>> vectorPoints, int l, float Proximity)
            {
                int nlp1 = queryPoints.Count();
                int nlp2 = vectorPoints.Count();
                var qPoints = new List<DSLPointsSegmFree>();
                var vPoints = new List<DSLPointsSegmFree>();
                var Costs = new Dictionary<int, float>();

                if (Math.Abs(nlp2 - nlp1) > nlp1 / 2)
                    return
                        new Tuple
                            <float, Dictionary<int, float>, DSLPointsSegmFree[], DSLPointsSegmFree[]
                                >(l - 5, Costs, new DSLPointsSegmFree[] { },
                        new DSLPointsSegmFree[] { });

                if (nlp1 == 0 || nlp2 == 0)
                    return
                        new Tuple
                            <float, Dictionary<int, float>, DSLPointsSegmFree[], DSLPointsSegmFree[]
                                >(l - 5, Costs, new DSLPointsSegmFree[] { },
                        new DSLPointsSegmFree[] { });

                var isUsed = new bool[wordVector.Length / l];

                float x1 = 0, y1 = 0, x2 = 0, y2 = 0;
                int t1 = 0;
                int nSelected = 0;

                float getDistance(int pos)
                {
                    int t2 = pos * l;
                    x2 = vectorPoints[pos].Item1;
                    y2 = vectorPoints[pos].Item2;
                    float r = float.NaN;
                    float speedupx = Math.Abs(x1 - x2);
                    float speedupy = Math.Abs(y1 - y2);
                    if (speedupx <= Proximity)
                    {
                        if (speedupy <= 2 * Proximity)
                        {
                            r = 0;
                            for (int q = 4; q < l; q++)
                                r += (queryVector[t1 + q] - wordVector[t2 + q]) * (queryVector[t1 + q] - wordVector[t2 + q]);
                            r = (float)Math.Sqrt(r);
                        }
                    }

                    return r;
                }

                float minD = float.MaxValue - 1;


                float cost = 0;

                foreach (int i in queryPoints.Keys)
                {
                    t1 = i * l;
                    x1 = queryPoints[i].Item1;
                    y1 = queryPoints[i].Item2;
                    int minPos = -1;
                    minD = float.MaxValue - 1;
                    int foundMinPos = -1;
                    float foundMinD = float.MaxValue - 1;
                    foreach (int j in vectorPoints.Keys)
                    {
                        float f = getDistance(j);
                        if (!float.IsNaN(f))
                        {
                            if (f < minD && !isUsed[j]) // min distance and not used point
                            {
                                minD = f;
                                minPos = j;
                            }
                            else if (f < foundMinD && isUsed[j]) // min distance and used point
                            {
                                foundMinD = f;
                                foundMinPos = j;
                            }
                        }
                    }

                    if (minPos == -1) // not found any points
                    {
                        /*float r = 0;
                        for (int q = 4; q < l; q++)
                            r += query[t1 + q] * query[t1 + q];*/
                        //cost += 5.19f;
                    }
                    else
                    {
                        cost += minD;
                        //isUsed[minPos] = true; //  take the distance as cost and set as used point;
                        nSelected++;
                        qPoints.Add(new DSLPointsSegmFree(queryVector[t1], queryVector[t1 + 1], i));
                        vPoints.Add(new DSLPointsSegmFree(wordVector[minPos * l], wordVector[minPos * l + 1], minPos));
                        Costs.Add(i, minD);
                    }
                }

                if (nSelected == 0)
                    cost = l - 5;
                else
                {
                    cost /= nSelected;
                }


                PopulateDescriptor(ref qPoints, queryVector, l);
                PopulateDescriptor(ref vPoints, wordVector, l);


                return
                    new Tuple
                        <float, Dictionary<int, float>, DSLPointsSegmFree[], DSLPointsSegmFree[]>(
                    cost, Costs, qPoints.ToArray(), vPoints.ToArray());
            }


            public void PopulateDescriptor(ref List<DSLPointsSegmFree> points, float[] vector, int l)
            {
                for (int i = 0; i < points.Count(); i++)
                {
                    int pos = points[i].GroupID * l + 4;
                    points[i].Descriptor = new float[l - 4];
                    for (int q = 0; q < l - 4; q++)
                        points[i].Descriptor[q] = vector[pos + q];
                }
            }


            public static float GetMeanMinDistance(float[] vector, int l)
            {
                int np = vector.Length / l;
                float minD = 0;
                for (int i = 0; i < np - 1; i++)
                {
                    minD +=
                        (float)
                            Math.Sqrt(Math.Pow(vector[i * l] - vector[(i + 1) * l], 2) +
                        Math.Pow(vector[i * l + 1] - vector[(i + 1) * l + 1], 2));
                }
                return minD / ((np - 1) * 2);
            }
        }
    }
}
