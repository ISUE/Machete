using System;
using System.Collections;
using System.Collections.Generic;


public class RunningStatistics
{   
    
    public double count;
    public double mean;
    public double M2;
    public double variance;
    public double std;
    public double maximum;
    public double minimum;

    public RunningStatistics()
    {
        reset();
    }
    public void reset()
    {
        count = 0.0;
        mean = 0.0;
        M2 = 0.0;
        variance = 0.0;
        maximum = double.NegativeInfinity;
        minimum = double.PositiveInfinity;
    }
    // 95% z = 1.96
    // 99% z = 2.576
    public double ci_upper(double z = 1.96)
    {
        return mean + z * Math.Sqrt(variance / count);
    }

    public double ci_lower(double z = 1.96)
    {
        return mean - z * Math.Sqrt(variance / count);
    }

    public double standard_error()
    {
        return Math.Sqrt(variance / count);
    }

    public void add(double val)
    {
        double delta = val - mean;
        count += 1.0;
        mean = mean + delta / count;
        M2 = M2 + delta * (val - mean);
        variance = M2 / (count - 1.0);
        std = Math.Sqrt(variance);
        maximum = Math.Max(maximum, val);
        minimum = Math.Min(minimum, val);
    }
}
