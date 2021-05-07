using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Machete
{
	public class ExponentialMovingAverage
	{
		public Vector Pt { get; set; }

		public double CutOffFrequencyHz { get; set; }

		public ExponentialMovingAverage(Vector initialPt, double cuttoff = 3.0)
		{
			Pt = initialPt.Clone() as Vector;
			CutOffFrequencyHz = cuttoff;
		}

		public Vector Filter(Vector pt, double durationS) 
		{
			double tau = 1.0 / (2.0 * Math.PI * CutOffFrequencyHz);            
			double alpha = 1.0 / (1.0 + tau / durationS);
            alpha = 0.489950;
            this.Pt = (pt * alpha) + (this.Pt * (1 - alpha));

			return this.Pt;
		}
	}

    public static class CentralMovingAverage
    {
        public static int GetCMAR(
            double fc,
            double fs)
        {
            double tmp = fs / (2.0 * Math.PI * fc);
            tmp = 3.0 / 2.0 * (tmp * tmp);
            return (int)Math.Round(tmp);
        }

        public static void CMAFilter(
            List<Vector> pts,
            out List<Vector> filtered,
            int window,
            int repeat_cnt = 1 // pass count (cma_r)
            )
        {
            List<Vector> signal;
            filtered = new List<Vector>();
            int pt_cnt = pts.Count;

            if (repeat_cnt == 0)
            { 
                filtered = pts;
                return;
            }

            signal = pts;

            for (int iteration = 1; iteration <= repeat_cnt; iteration++)
            {
                for (int ii = 0; ii < pt_cnt; ii++)
                {
                    int minimum = Math.Max(ii - window, 0);
                    int maximum = Math.Min(pt_cnt - 1, ii + window);

                    Vector pt = signal[minimum];

                    for (int jj = minimum + 1; jj <= maximum; jj++)
                    {
                        pt += signal[jj];
                    }

                    double cnt = maximum - minimum + 1.0;
                    filtered.Add(pt / cnt);
                }

                if (iteration < repeat_cnt)
                {
                    signal = filtered;
                    filtered = new List<Vector>();
                }
            }
        }

        public static Dataset FilterDataset(DeviceType device, Dataset ds)
        {
            // Set the cutoff depending on the device
            float cutoff = 3.0f;
            if (device == DeviceType.MOUSE)
                cutoff = 5.0f;

            // Filter the dataset using CMA
            int pass_cnt = GetCMAR(
                cutoff,  // cutoff is 3 for all except mouse (5.0)
                Global.GetFPS(device, ds.Samples));


            for (int ii = 0; ii < ds.Samples.Count; ii++)
            {

                CMAFilter(ds.Samples[ii].Trajectory,
                    out List<Vector> filtered,
                    1,
                    pass_cnt);

                ds.Samples[ii].FilteredTrajectory = filtered;
            }

            return ds;
        }
    }
}
