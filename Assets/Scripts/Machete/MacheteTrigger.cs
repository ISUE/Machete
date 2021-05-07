using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete
{
    public class MacheteTrigger
    {
        public double sum;
        public double count;

        public double s1;
        public double s2;
        public double s3;

        public int start;
        public int end;

        public bool check;

        public bool minimum;

        public MacheteTrigger()
        {
            this.Reset();
        }

        public void Reset()
        {
            start = -1;
            end = -1;
            s1 = Mathf.Infinity;
            s2 = Mathf.Infinity;
            s3 = Mathf.Infinity;
            minimum = false;
            sum = 0.0f;
            count = 0.0f;
        }

        public double GetThreshold()
        {
            double mu = sum / count;
            return mu / 2.0f;
        }

        public void Update(
            int frame,
            double score,
            double cf,
            int start,
            int end
            )
        {
            sum += score;
            count += 1.0f;
            score *= cf;

            s1 = s2;
            s2 = s3;
            s3 = score;

            double mu = sum / count;
            double threshold = mu / 2.0f;

            check = false;

            if (s3 < s2)
            {
                this.start = start;
                this.end = end;
                return;
            }

            if (s2 > threshold)
                return;

            if (s1 < s2)
                return;

            if (s3 < s2)
                return;

            check = true;
        }
    }

}