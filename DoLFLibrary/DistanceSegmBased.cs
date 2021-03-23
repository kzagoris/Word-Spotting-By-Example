using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DoLFLibrary
{
    internal class DistanceSegmBased
    {

        private int L;

        #region Settings

        public float NearNeighborArea { get; set; }

        #endregion

        public float GetSimilarity([NotNull]float[] vector1, [NotNull]float[] vector2)
        {
            if (vector1 == null)
                throw new ArgumentNullException(nameof(vector1));
            
            if (vector2 == null)
                throw new ArgumentNullException(nameof(vector2));

            if ((vector1?.Length ?? 0) == 0 || (vector2?.Length ?? 0) == 0)
                return 100;

            L = (int)vector1[^1];
            float proximity = NearNeighborArea; //debug.minDistance;

            int nlp1 = (vector1.Length - 1) / L;
            int nlp2 = (vector2.Length - 1) / L;

            if (Math.Abs(nlp2 - nlp1) > nlp1 / 2)
                return L - 5;
            if (nlp1 == 0 || nlp2 == 0)
                return L - 5;

            var isUsed = new bool[nlp2];
            float x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            int t1 = 0;
            int nSelected = 0;

            Func<int, float> getDistance = pos =>
            {
                int t2 = pos * L;
                x2 = vector2[t2];
                y2 = vector2[t2 + 1];
                float r = float.NaN;
                float speedupx = Math.Abs(x1 - x2);
                float speedupy = Math.Abs(y1 - y2);
                if (!(speedupx <= proximity)) return r;
                if (!(speedupy <= 2 * proximity)) return r;
                r = 0;
                for (int q = 4; q < L; q++)
                    r += (vector1[t1 + q] - vector2[t2 + q]) * (vector1[t1 + q] - vector2[t2 + q]);
                r = (float)Math.Sqrt(r);

                return r;
            };

            float cost = 0;
            for (int i = 0; i < nlp1; i++)
            {
                t1 = i * L;
                x1 = vector1[t1];
                y1 = vector1[t1 + 1];
                int minPos = -1;
                var minD = float.MaxValue - 1;
                float foundMinD = float.MaxValue - 1;
                for (int j = 0; j < nlp2; j++)
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
                        }
                    }
                }

                if (minPos == -1) // not found any points
                {
                }
                else
                {
                    cost += minD;
                    nSelected++;

                }
            }

            if (nSelected == 0)
                cost = L - 5;
            else
            {
                cost /= nSelected;
            }




            return cost;
        }

        public float[] GetNormalizedDescriptor(DoLF.DsLPoints[] myDsLPs, int Width, int Height)
        {
            //DsLPsDetectorv2wFeatures.DsLP[]  = myDsLPDetector.myDsLPs.OrderBy(x => x.X).ToArray();
            var descriptor = new List<float>();
            // must find the centroid of points(origin) by averaging the X and Y coordinates
            double meanX = 0, meanY = 0;
            double dx = 0, dy = 0;
            double wToh = Width / (double)Height;

            foreach (var lp in myDsLPs)
            {
                meanX += lp.X;
                meanY += lp.Y;
            }

            meanX = meanX / myDsLPs.Length;
            meanY = meanY / myDsLPs.Length;

            foreach (var lp in myDsLPs)
            {
                dx += Math.Abs(lp.X - meanX);
                dy += Math.Abs(lp.Y - meanY);
            }
            dx /= myDsLPs.Count();
            dy /= myDsLPs.Count();
            //dy *= wToh;

            if (myDsLPs.Length > 0)
            {
                int length = myDsLPs[0].Descriptor.Length + 4;
                foreach (var lp in myDsLPs)
                {
                    descriptor.Add((float)((lp.X - meanX) / dx));
                    descriptor.Add((float)((lp.Y - meanY) / (dy * wToh)));
                    descriptor.Add(lp.Gradient);
                    descriptor.Add((float)wToh);
                    descriptor.AddRange(lp.Descriptor);
                }
                descriptor.Add(length);
            }
            return descriptor.ToArray();
        }
    }
}
