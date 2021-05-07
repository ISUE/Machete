using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Jackknife;

public class WindowSegmentor
{
    Machete.CircularBuffer<Jackknife.Vector> buffer;

    /**
     * Undelying recognizer
     */
    private Jackknife.Jackknife jk;

    public WindowSegmentor(Jackknife.Jackknife jk)
    {
        this.jk = jk;
        buffer = new Machete.CircularBuffer<Jackknife.Vector>(jk.maxTemplateLen);
    }

    public void Update(Jackknife.Vector pt)
    {
        buffer.Insert(pt);
    }

    public int Segment(List<Machete.ContinuousResult> crs)
    {
        int ret = -1;

        ret = jk.ClassForWinCustTemplLen(
            buffer,
            out double score,
            out int st,
            out int end);

        if (ret != -1)
        {
            Machete.ContinuousResult cr = new Machete.ContinuousResult();
            cr.startFrameNo = st;
            cr.endFrameNo = end;
            cr.score = score;
            cr.gid = ret;
            crs.Add(cr);
        }

        return ret;
    }
}

public class Window
{
    public int minimum;
    public int maximum;
    public double width;
    public double step_size;
    Jackknife.Jackknife jackknife;
    public int mode; // 0 = min, 1 = max, 2 = min+max, 3 = min+mid+max, 4 = mid

    /**
     * Takes in a recognizer
     */
    public Window(Jackknife.Jackknife jk, int m)
    {
        mode = m;

        double numsteps = 0.0;

        if (m == 0) //min
        {
            numsteps = 0.5; // divided by this will step over the max
        }

        if (m == 1) //max
        {
            numsteps = 1.0; // step how much you want just start at max already
        }

        if (m == 2) //min+max
        {
            numsteps = 1.0;
        }

        if (m == 3) //min+mid+max
        {
            numsteps = 2.0;
        }

        if (m == 4) //mid
        {
            numsteps = 1.0;
        }

        jackknife = jk;
        minimum = jk.minTemplateLen;
        maximum = jk.maxTemplateLen;


        width = (double)maximum - minimum;
        step_size = width / (double)numsteps;

        if (m == 1)
        {
            minimum = maximum;
        }

        if (m == 4)
        {
            minimum += (maximum - minimum) / 2; // just start in the middle
        }
    }
}