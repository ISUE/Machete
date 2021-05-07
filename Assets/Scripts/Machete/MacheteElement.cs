using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete
{
    /**
     * A single element of the DTW matrix. Each element essentially
     * stores path information.
     */
    public class MacheteElement
    {
        public double score;

        public int startFrameNo;

        public int endFrameNo;

        public double column;

        public double runningScore;

        public double total;

        public MacheteElement() { }

        public MacheteElement(
            double column, 
            double startAngleDegrees
            )
        {
            this.column = column;
            runningScore = Mathf.Infinity;
            total = Mathf.Epsilon;

            if (column == 0)
            {
                double angle = startAngleDegrees * Mathf.PI / 180.0f;
                double threshold = 1.0f - Mathf.Cos((float)angle);
                score = threshold * threshold;

                runningScore = 0.0f;
                total = 0.0f;
            }
        }

        public double GetNormalizedWarpingPathCost()
        {
            if (column == 0)
                return score;

            return runningScore / total;
        }

        public void Update(
            MacheteElement extendThis,
            int frameNo,
            double cost,
            double length
            )
        {
            startFrameNo = extendThis.startFrameNo;
            endFrameNo = frameNo;
            cost *= length;

            this.runningScore = extendThis.runningScore + cost;
            this.total = extendThis.total + length;
        }
    }

}