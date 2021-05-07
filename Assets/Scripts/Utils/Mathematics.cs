using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete {

    public static class Mathematics
    {
        public static void BoundingBox(
            List<Vector> trajectory,
            out Vector min_point,
            out Vector max_point
            )
        {
            min_point = (Vector)trajectory[0].Clone();
            max_point = (Vector)trajectory[0].Clone();

            for (int ii = 1; ii < trajectory.Count; ii++)
            {
                min_point.Minimum(trajectory[ii]);
                max_point.Maximum(trajectory[ii]);
            }
        }

        private static void DouglasPeuckerRDensity(
            List<Vector> points, 
            List<Tuple<int, double>> splits, 
            int start, 
            int end, 
            double threshold
            )
        {
            // base case
            if (start + 1 > end)
            {
                return;
            }

            Vector AB = points[end] - points[start];
            double denom = AB.Dot(AB);

            double largest = Double.NegativeInfinity;
            int selected = -1;

            for (int ii = start + 1;
                ii < end;
                ii++)
            {
                Vector AC = points[ii] - points[start];
                double numer = AC.Dot(AB);
                double d2 = AC.Dot(AC) - numer * numer / denom;

                if (denom == 0.0)
                {
                    d2 = AC.L2Norm();
                }

                Vector v1 = points[ii] - points[start];
                Vector v2 = points[end] - points[ii];

                double l1 = v1.L2Norm();
                double l2 = v2.L2Norm();

                double dot = v1.Dot(v2);
                // Protect against zero length vector
                dot /= (l1 * l2 > 0) ? (l1 * l2) : 1.0;
                dot = Math.Max(-1.0, Math.Min(1.0, dot));
                double angle = Math.Acos(dot);
                d2 *= angle / Math.PI;

                if (d2 > largest)
                {
                    largest = d2;
                    selected = ii;
                }
            }

            if (selected == -1)
            {
                //Debug.Log("DouglasPeuckerR: nothing selected");
                //Debug.Log(String.Format("start {0}, end {1}", start, end));
                // something went wrong: FIXME how do I exit?
            }

            largest = Math.Max(0.0, largest);
            largest = Math.Sqrt(largest);

            if (largest < threshold)
            {
                return;
            }

            DouglasPeuckerRDensity(
                points,
                splits,
                start,
                selected,
                threshold
                );

            DouglasPeuckerRDensity(
                points,
                splits,
                selected,
                end,
                threshold
                );

            splits[selected].Second = largest;
        }



        public static void DouglasPeuckerDensity(
            List<Vector> points,
            List<Tuple<int, double>> splits,
            double minimumThreshold
            )
        {
            // Create split entry for each point
            splits.Clear();

            for (int ii = 0; ii < points.Count; ii++)
            {
                splits.Add(new Tuple<int, double>(ii, 0));
            }

            // Modified tuples class in Dataset to make {set} not private
            splits[0].Second = double.MaxValue;
            splits[splits.Count - 1].Second = double.MaxValue;

            // Recursively evaluate all splits
            DouglasPeuckerRDensity(
                points,
                splits,
                0,
                points.Count - 1,
                minimumThreshold
                );

            // Sort in descending order by second value
            splits.Sort((a, b) => b.Second.CompareTo(a.Second));
        }

        public static double DouglasPeuckerDensity(
            List<Vector> trajectory,
            out List<Vector> output,
            double minimumThreshold
            )
        {
            List<Tuple<int, double>> splits = new List<Tuple<int, double>>();
            List<int> indicies = new List<int>();
            output = new List<Vector>();

            splits.Clear();
            output.Clear();
            indicies.Clear();

            DouglasPeuckerDensity(
                trajectory,
                splits,
                minimumThreshold
                );

            double ret = Double.NegativeInfinity;

            for (int ii = 0;
                ii < splits.Count;
                ii++)
            {
                int idx = splits[ii].First;
                double score = splits[ii].Second;

                if (score < minimumThreshold) { continue; }

                indicies.Add(idx);
            }

            indicies.Sort();

            for (int ii = 0;
                ii < indicies.Count;
                ii++)
            {
                output.Add(trajectory[indicies[ii]]);
            }

            return ret;
        }

        public static void Vectorize(
            List<Vector> trajectory,
            out List<Vector> vectors,
            bool normalize
            )
        {
            vectors = new List<Vector>();
            vectors.Clear();

            for (int ii = 1; ii < trajectory.Count; ii++)
            {
                Vector vec = trajectory[ii] - trajectory[ii - 1];

                if (normalize)
                    vec.Normalize();

                vectors.Add(vec);
            }
        }

        public static double PathLength(List<Vector> points)
        {
            double ret = 0;

            for (int i = 1; i < points.Count; i++)
                ret += points[i].L2Norm(points[i - 1]);

            return ret;
        }
    }
}