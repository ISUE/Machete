using UnityEngine;

namespace Machete
{
    public class Results
    {
        public double accuracy = 0.0;
        public double error = 0.0;
        public double precision = 0.0;
        public double recall = 0.0;
        public double f2_0 = 0.0;
        public double f1_0 = 0.0;
        public double f0_5 = 0.0;
        public double total = 0.0;

        public void Reset()
        {
            accuracy = 0.0;
            error = 0.0;
            precision = 0.0;
            recall = 0.0;
            f2_0 = 0.0;
            f1_0 = 0.0;
            f0_5 = 0.0;
            total = 0.0;
        }

        public double FScore(double beta)
        {
            double p = precision / total;
            double r = recall / total;
            double num = (beta * beta + 1.0) * (p * r);
            double denom = ((beta * beta) * p) + r;
            if (denom == 0.0) return 0.0;
            return num / denom;
        }

        public void AppendResults(Results rhs)
        {
            accuracy += rhs.accuracy / rhs.total;
            error += rhs.error / rhs.total;
            precision += rhs.precision / rhs.total;
            recall += rhs.recall / rhs.total;
            f2_0 += rhs.FScore(2.0);
            f1_0 += rhs.FScore(1.0);
            f0_5 += rhs.FScore(0.5);
            total += 1.0;
        }


        public void AppendResults(ConfisionMatrix cm)
        {
            accuracy += cm.Accuracy();
            error += cm.Error();
            precision += cm.Precision();
            recall += cm.Recall();
            f2_0 += cm.Fscore(2.0);
            f1_0 += cm.Fscore(1.0);
            f0_5 += cm.Fscore(0.5);
            total += 1.0;
        }

        public void PrintF()
        {
            string s = string.Format("A: {0:N6}, E: {1:N6}, P: {2:N6}, R: {3:N6}, F1: {4:N6}\n",
                accuracy / total,
                error / total,
                precision / total,
                recall / total,
                f1_0 / total);
            Debug.Log(s);
        }

    } // end class Results


    public class RecognitionResult
    {
        public int gid;
        public int start;
        public int end;
        public double score;

        public bool Update(RecognitionResult other)
        {
            if (gid != other.gid)
                return false;

            double middle = (double)(start + end) / 2.0;

            if (middle < other.start)
                return false;

            if (score < other.score)
            {
                start = other.start;
                end = other.end;
                score = other.score;
            }

            return true;
        }

        public void Print(Dataset ds)
        {
            string s = string.Format("gid {0}: {1}--{2} ({3})\n",
                    ds.Gestures[gid],
                    start,
                    end,
                    score);

            Debug.Log(s);
        }
    } // end class RecognitionResult
}
