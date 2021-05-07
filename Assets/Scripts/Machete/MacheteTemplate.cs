using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete
{
    public class MacheteTemplate
    {
        /**
         * Class for handling recognition of a single gesture,
         * aka, a template.
         */

        public Sample sample;

        // Place to store resampled points
        public List<Vector> points;

        // Direction vectors
        public List<Vector> vectors;

        DeviceType device_id;

        public int minimumFrameCount;
        public int maximumFrameCount;

        public double closedness;
        public Vector f2l_Vector;

        public double weightClosedness;
        public double weightF2l;

        public int vectorCount;

        public List<MacheteElement>[] dtw;

        public int currentIndex;

        public int sampleCount;

        public MacheteTrigger trigger;

        // result options
        ContinuousResultOptions cr_options;

        // Segmentation result
        public ContinuousResult result;

        public void Prepare(DeviceType deviceType, 
            List<Vector> resampled,
            bool filtered = true)
        {
            List<Vector> rotated;
            List<Vector> dpPoints;
            this.device_id = deviceType;

            if (filtered == true)
            {
                rotated = sample.FilteredTrajectory;
            }
            else
            {
                rotated = sample.Trajectory;
            }

            // Remove duplicate points
            resampled.Add(rotated[0]);

            for(int ii = 1; ii < rotated.Count; ii++)
            {
                int count = resampled.Count - 1;
                double length = resampled[count].L2Norm(rotated[ii]);

                if (length <= Double.Epsilon)
                {
                    continue;
                }

                resampled.Add(rotated[ii]);
            }
            
            /*
             * Determine Threshold
             */
            sampleCount = resampled.Count;

            Mathematics.BoundingBox(resampled, out Vector minimum, out Vector maximum);

            double diag = maximum.L2Norm(minimum);

            /*
             * Resample the input using DP
             */
            Mathematics.DouglasPeuckerDensity(resampled, out dpPoints, diag * 0.010f);
            
            /*
             * Heuristically dehook the trajectory
             */
            if (deviceType == DeviceType.MOUSE)
            {
                int ptCnt = dpPoints.Count;
                Vector v1 = dpPoints[1] - dpPoints[0];
                Vector v2 = dpPoints[2] - dpPoints[1];
                double ratio = v1.L2Norm() / v2.L2Norm();

                if (ratio < .2)
                {
                    dpPoints.RemoveAt(0);
                    ptCnt--;
                }

                v1 = dpPoints[ptCnt - 2] - dpPoints[ptCnt - 3];
                v2 = dpPoints[ptCnt - 1] - dpPoints[ptCnt - 2];

                ratio = v2.L2Norm() / v1.L2Norm();

                if (ratio < .2)
                {
                    dpPoints.RemoveAt(dpPoints.Count - 1);
                    ptCnt--;
                }
            }
            
            points = dpPoints;

            // Convert DP resampled points into vectors
            Mathematics.Vectorize(points, out vectors, true);

            // Determine correction factor information
            f2l_Vector = points[points.Count - 1] - points[0];
            double f2l_length = f2l_Vector.L2Norm();
            closedness = f2l_length;
            closedness /= Mathematics.PathLength(resampled);
            f2l_Vector.Normalize();

            weightClosedness = (1.0f - f2l_length / diag);
            weightF2l = Math.Min(1.0f, 2.0f * f2l_length / diag);
        }

        public MacheteTemplate(
            DeviceType device_id,
            ContinuousResultOptions cr_options,
            Sample sample,
            bool filtered = true
            )
        {
            trigger = new MacheteTrigger();
            dtw = new List<MacheteElement>[2] {
                new List<MacheteElement>(),
                new List<MacheteElement>()
            };
            List<Vector> resampled = new List<Vector>();
            

            this.sample = sample;

            if (filtered == true)
            {
                this.minimumFrameCount = sample.FilteredTrajectory.Count / 2;
                this.maximumFrameCount = sample.FilteredTrajectory.Count * 2;
            }
            else
            {
                this.minimumFrameCount = sample.Trajectory.Count / 2;
                this.maximumFrameCount = sample.Trajectory.Count * 2;
            }

            Prepare( device_id, resampled, filtered);

            this.vectorCount = this.vectors.Count;

            this.cr_options = cr_options;
            this.result = new ContinuousResult(
                cr_options,
                sample.GestureId,
                sample
                )
            {
                sample = sample
            };
        }

        public void ResetElements()
        {
            // Restart the DTW matrix
            for (int ridx = 0; ridx < 2; ridx++)
            {
                if (dtw[ridx] != null)
                   dtw[ridx].Clear();
            }

            // Reset idx, no real need except
            // but for completeness. :)
            currentIndex = 0;

            // Will be 20.0 for mouse
            double startAngleDegrees = 65.0f;

            if (DeviceType.MOUSE == device_id)
                startAngleDegrees = 20.0;

            for (int ridx = 0; ridx < 2; ridx++)
            {
                for (int cidx = 0; cidx <= vectorCount; cidx++)
                {
                    dtw[ridx].Add(new MacheteElement(cidx, startAngleDegrees));
                }
            }

            trigger.Reset();
        }

        public void Reset()
        {
            ResetElements();
        }

        public void Segmentation(
            ref int head,
            ref int tail
            )
        {
            List<MacheteElement> current = dtw[currentIndex];
            MacheteElement curr = current[current.Count - 1];

            head = curr.startFrameNo - 1;
            tail = curr.endFrameNo;
        }

        public void Update(
            CircularBuffer<Vector> buffer,
            Vector pt,
            Vector nvec,
            int frameNo,
            double length
            )
        {
            // Cache current row as prev
            List<MacheteElement> previous = dtw[currentIndex];

            // Update Circular Buffer Index
            currentIndex ++;
            currentIndex %= 2;

            // Cache reference to current row
            List<MacheteElement> current = dtw[currentIndex];

            // Update frame number
            current[0].startFrameNo = frameNo;

            for (int col = 1; col <= vectorCount; col++)
            {
                double dot = nvec.Dot(vectors[col - 1]);
                double cost = 1.0 - Math.Max(-1.0, Math.Min(1.0, dot));
                cost = cost * cost;

                // Pick the lowest cost neightbor to
                // extent it's warping path through
                // this (frame_no, col) element.
                MacheteElement n1 = current[col - 1];
                MacheteElement n2 = previous[col - 1];
                MacheteElement n3 = previous[col];

                MacheteElement extend = n1;
                double minimum = n1.GetNormalizedWarpingPathCost();

                if (n2.GetNormalizedWarpingPathCost() < minimum)
                {
                    extend = n2;
                    minimum = n2.GetNormalizedWarpingPathCost();
                }

                if (n3.GetNormalizedWarpingPathCost() < minimum)
                {
                    extend = n3;
                    minimum = n3.GetNormalizedWarpingPathCost();
                }

                // Update the miniumum cost warping path
                // Element to include this frame
                current[col].Update(
                    extend,
                    frameNo,
                    cost,
                    length);
            }

            MacheteElement curr = current[vectorCount];

            int startFrameNo = curr.startFrameNo;
            int endFrameNo = curr.endFrameNo;
            double durationFrameCount = endFrameNo - startFrameNo + 1;
            double cf = 1.0f;
            
            if (device_id == DeviceType.MOUSE)
            {
                double cf_closedness = 2.0;
                double cf_f2l = 2.0;

                if (durationFrameCount < buffer.Count() - 1)
                {
                    // Get first to last vector
                    Vector vec = buffer[-1] - buffer[-(int) durationFrameCount];
                    double total = current[vectorCount].total;
                    double vlength = vec.L2Norm();
                    double closedness1 = vlength / total;

                    vec /= vlength;
                    
                    // Closedness
                    cf_closedness = Math.Max(closedness1, closedness);
                    cf_closedness /= Math.Min(closedness1, closedness);
                    cf_closedness = 1 + weightClosedness * (cf_closedness - 1.0);
                    cf_closedness = Math.Min(2.0, cf_closedness);

                    // End direction
                    double f2l_dot = f2l_Vector.Dot(vec);
                    f2l_dot = 1.0 - Math.Max(-1.0, Math.Min(1.0, f2l_dot));
                    cf_f2l = 1.0 + (f2l_dot / 2.0) * weightF2l;
                    cf_f2l = Math.Min(2.0, cf_f2l);

                    cf = cf_closedness * cf_f2l;
                }
            }

            double ret = curr.GetNormalizedWarpingPathCost();

            if (durationFrameCount < minimumFrameCount)
            {
                cf *= 1000.0f;
            }

            trigger.Update(
                frameNo,
                ret,
                cf,
                curr.startFrameNo,
                curr.endFrameNo);

            double _t = trigger.GetThreshold();

            result.Update(
                ret * cf,
                _t,
                curr.startFrameNo,
                curr.endFrameNo,
                frameNo);
        }
    }
}