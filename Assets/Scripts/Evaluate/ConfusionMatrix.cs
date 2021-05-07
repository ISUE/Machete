using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Machete
{

    public class ConfisionMatrix
    {
        public double tp = 0.0;
        public double tn = 0.0;
        public double fp = 0.0;
        public double fn = 0.0;

        public void Reset()
        {
            tp = 0.0;
            tn = 0.0;
            fp = 0.0;
            fn = 0.0;
        }

        public double Accuracy()
        {
            double denom = tp + tn + fp + fn;
            if(denom == 0.0) return 0.0;
            return (tp + tn) / denom;
        }

        public double Error()
        {
            double denom = tp + tn + fp + fn;
            if(denom == 0.0) return 0.0;
            return (fn + fp) / denom;
        }

        public double Recall()
        {
            double denom = tp + fn;
            if(denom == 0.0) return 0.0;
            return (tp / denom);
        }

        public double Precision()
        {
            double denom = tp + fp;
            if(denom == 0.0) return 0.0;
            return (tp / denom);
        }

        public double FallOut()
        {
            double denom = fp + tn;
            if(denom == 0.0) return 0.0;
            return (fp / denom);
        }

        public double Specificity()
        {
            double denom = tn + fp;
            if(denom == 0.0) return 0.0;
            return (tn / denom);
        }

        public double Fscore(double beta)
        {
            double b2 = beta * beta;            
            double numer = (b2 + 1.0) * (Precision() * Recall());
            double denom = (b2 * Precision()) + Recall();
            if(denom == 0.0) return 0.0;
            return numer / denom;
        }
    }
}