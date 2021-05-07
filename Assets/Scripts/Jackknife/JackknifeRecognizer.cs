/**
 * Copyright 2017 the University of Central Florida Research Foundation, Inc.
 * All rights reserved.
 * 
 *     Eugene M. Taranta II <etaranta@gmail.com>
 *     Amirreza Samiei <samiei@knights.ucf.edu>
 *     Mehran Maghoumi <mehran@cs.ucf.edu>
 *     Pooya Khaloo <pooya@cs.ucf.edu>
 *     Corey R. Pittman <cpittman@knights.ucf.edu>
 *     Joseph J. LaViola Jr. <jjl@cs.ucf.edu>
 * 
 * Subject to the terms and conditions of the Florida Public Educational
 * Institution non-exclusive software license, this software is distributed 
 * under a non-exclusive, royalty-free, non-sublicensable, non-commercial, 
 * non-exclusive, academic research license, and is distributed without warranty
 * of any kind express or implied. 
 *
 * The Florida Public Educational Institution non-exclusive software license
 * is located at <https://github.com/ISUE/Jackknife/blob/master/LICENSE>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jackknife
{
    public class Jackknife
    {
        /**
         * The DTW cost matrix that is initialized once and reused multiple times.
         */
        private List<List<double>> cost = null;

        /**
         * Vector of all gesture templates.
         */
        private List<JackknifeTemplate> templates;

        /**
         * Per template longest length before resampling
         * (for windowed segmentation)
         * gid, len
         */
        private Dictionary<int, int> templateLenghts;

        /**
        * Length of the longest template
        */
        public int maxTemplateLen;

        /**
         * Length of the shortest template
         */
        public int minTemplateLen;

        /**
         * The set of measures and features used in this 
         * 
         * of Jackknife.
         */
        public JackknifeBlades Blades { get; set; }

        /**
         * Constructor.
         */
        public Jackknife(JackknifeBlades blades)
        {
            if (!(blades.InnerProduct ^ blades.EuclideanDistance))
            {
                throw new ArgumentException("!(blades.InnerProduct ^ blades.EuclideanDistance)");
            }

            this.Blades = blades;
            templates = new List<JackknifeTemplate>();
            templateLenghts = new Dictionary<int, int>();
            maxTemplateLen = 0;
            minTemplateLen = int.MaxValue;
        }

        /**
         * Turn sample into template.
         */
        public void AddTemplate(Sample sample)
        {
            JackknifeTemplate t = new JackknifeTemplate(Blades, sample);
            templates.Add(t);

            int newlen = sample.Trajectory.Count;

            if (!templateLenghts.ContainsKey(sample.GestureId))
            {
                templateLenghts.Add(sample.GestureId, newlen);
            }

            try {
                if (templateLenghts[sample.GestureId] < newlen)
                    templateLenghts[sample.GestureId] = newlen;
            } 
            catch (KeyNotFoundException) { }

            if (newlen > maxTemplateLen)
                maxTemplateLen = newlen;

            if (newlen < minTemplateLen)
                minTemplateLen = newlen;
        }

        /**
         * Learn a rejection threshold for each template.
         * Call after all templates have been added.
         *
         * See explanation in jackknife_train to understand
         * the input parameters.
         */
        public void Train(int gpsrN, int gpsrR, double beta)
        {
            int template_cnt = templates.Count;
            List<Distributions> distributions = new List<Distributions>();
            List<Vector> synthetic = new List<Vector>();

            double worst_score = 0.0;

            Random rand = new Random();

            //
            // Create negative samples.
            //
            for (int i = 0; i < 1000; i++)
            {
                synthetic.Clear();

                // Splice two samples together
                // to create one negative sample.
                for (int j = 0; j < 2; j++)
                {
                    int t = rand.Next(template_cnt);
                    Sample s = templates[t].Sample;

                    int len = s.Trajectory.Count;
                    int start = rand.Next(len / 2);

                    for (int kk = 0; kk < len / 2; kk++)
                    {
                        synthetic.Add(s.Trajectory[start + kk]);
                    }
                }

                JackknifeFeatures features = new JackknifeFeatures(Blades, synthetic);

                // and score it
                for (int t = 0; t < template_cnt; t++)
                {
                    double score = DTW(
                        features.Vecs,
                        templates[t].Features.Vecs);

                    if (worst_score < score)
                        worst_score = score;

                    if (i > 50)
                        distributions[t].AddNegativeScore(score);
                }

                // Generate a few samples to get an estimate
                // of worst possible score.
                if (i != 50)
                    continue;

                // allocate distributions
                for (int t = 0; t < template_cnt; t++)
                {
                    distributions.Add(new Distributions(worst_score, 1000));
                }
            }

            //
            // Create positive examples.
            //
            for (int t = 0; t < template_cnt; t++)
            {
                for (int i = 0; i < 1000; i++)
                {
                    synthetic.Clear();

                    // Create a synthetic variation of the sample.
                    synthetic = Mathematics.GPSR(templates[t].Sample.Trajectory, gpsrN, 0.25, gpsrR);

                    JackknifeFeatures features = new JackknifeFeatures(Blades, synthetic);

                    // and score it
                    double score = DTW(features.Vecs, templates[t].Features.Vecs);

                    distributions[t].AddPositiveScore(score);
                }
            }

            //
            // Now extract the rejection thresholds.
            //
            for (int t = 0; t < template_cnt; t++)
            {
                double threshold = distributions[t].RejectionThreshold(beta);
                JackknifeTemplate temp = templates[t];
                temp.RejectionThreshold = threshold;
                templates[t] = temp;
            }
        }

        /**
        * Hardcode the rejection thresholds
        */
        public void SetRejectionThresholds(double rt)
        {
            for (int t = 0; t < templates.Count; t++)
            {
                JackknifeTemplate temp = templates[t];
                temp.RejectionThreshold = rt;
                templates[t] = temp;
            }
        }
        
        /**
        * Check if buffer matches a certain template
        */
        public bool IsMatch(
            List<Vector> trajectory,
            int gid,
            out double score)
        {
            JackknifeFeatures features = new JackknifeFeatures(Blades, trajectory);
            double best_score = double.PositiveInfinity;
            bool ret = false;

            for (int tid = 0; tid < templates.Count; tid++)
            {
                if (templates[tid].Sample.GestureId != gid)
                {
                    continue;
                }

                double cf = 1;
                if (Blades.CFAbsDistance)
                {
                    cf *= 1.0 / Math.Max(0.01, features.Abs.Dot(templates[tid].Features.Abs));
                }

                if (Blades.CFBbWidths)
                {
                    cf *= 1.0 / Math.Max(0.01, features.Bb.Dot(templates[tid].Features.Bb));
                }

                JackknifeTemplate temp = templates[tid];
                temp.CF = cf;
                templates[tid] = temp;

                if (Blades.LowerBound)
                {
                    JackknifeTemplate tempLB = templates[tid];
                    tempLB.LB = cf * LowerBound(features.Vecs, templates[tid]);
                    templates[tid] = tempLB;
                }

                double d = templates[tid].CF;
                d *= DTW(features.Vecs, templates[tid].Features.Vecs);

                if (d < templates[tid].RejectionThreshold)
                {
                    ret = true;
                }

                if (d < best_score)
                {
                    best_score = d;
                }
            }

            score = best_score;
            return ret;
        }

        /**
      * Check if buffer matches a certain template
      */
        public List<Machete.ContinuousResult> IsMatch(
            List<Vector> trajectory, int start, int end)
        {
            List<Machete.ContinuousResult> res = new List<Machete.ContinuousResult>();
            JackknifeFeatures features = new JackknifeFeatures(Blades, trajectory);                        

            for (int tid = 0; tid < templates.Count; tid++)
            {
                double cf = 1;
                if (Blades.CFAbsDistance)
                {
                    cf *= 1.0 / Math.Max(0.01, features.Abs.Dot(templates[tid].Features.Abs));
                }

                if (Blades.CFBbWidths)
                {
                    cf *= 1.0 / Math.Max(0.01, features.Bb.Dot(templates[tid].Features.Bb));
                }

                //JackknifeTemplate temp = templates[tid];
                //temp.CF = cf;
                //templates[tid] = temp;
                templates[tid].CF = cf;
                if (Blades.LowerBound)
                {
                    JackknifeTemplate tempLB = templates[tid];
                    tempLB.LB = cf * LowerBound(features.Vecs, templates[tid]);
                    templates[tid] = tempLB;
                }

                double d = templates[tid].CF;
                d *= DTW(features.Vecs, templates[tid].Features.Vecs);

                if (d < templates[tid].RejectionThreshold)
                {                    
                    // Add to the Continuous Results
                    Machete.ContinuousResult cr = new Machete.ContinuousResult();
                    cr.startFrameNo = start;
                    cr.endFrameNo = end;
                    cr.score = d;
                    cr.gid = templates[tid].GestureId;
                    res.Add(cr);
                }


            }
            
            return res;
        }

        /**
        * For windowed segmentation, copy the buffer of longest
        * length of a particular sample, and check for a match
        */
        public int ClassForWinCustTemplLen(
            Machete.CircularBuffer<Vector> buffer, 
            out double bestScore,
            out int startFrameNo,
            out int endFrameNo)
        {
            List<Vector> trajectory;
            int templateCnt = templates.Count;
            bestScore = double.PositiveInfinity;
            int bestGid = -1;
            startFrameNo = -1;
            endFrameNo = -1;

            for (int t = 0; t < templateCnt; t++)
            {
                // Get the length required
                int len;
                try {
                    len = templateLenghts[templates[t].GestureId];
                }
                catch (KeyNotFoundException) {
                    // something went wrong
                    return -1;
                }

                if (len > buffer.Count())
                    continue;
                
                // Copy path of this lenght
                trajectory = new List<Vector>();
                int st = buffer.Count() - len;
                int en = buffer.Count();
                for (int i = st; i < en; i++)
                {
                    trajectory.Add(buffer[i]);
                }

                // Check with isMatch
                double score = double.PositiveInfinity;
                int gid = templates[t].GestureId;
                bool match = IsMatch(trajectory, templates[t].GestureId, out score);

                if (!match)
                {
                    continue;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestGid = gid;
                    startFrameNo = st;
                    endFrameNo = en;
                }
            }

            return bestGid;
        }


        //Return all of the matches for this window length 
        public void ClassForWinCustTemplLen(
            List<Vector> trajectory,
            List<Machete.ContinuousResult> crs,
            int start,
            int end)
        {          
            List<Machete.ContinuousResult> match = IsMatch(trajectory, start, end);

            if (match.Count == 0)
            {
                return;
            }

            else
            {
                foreach (var m in match)
                {
                    crs.Add(m);
                }
                
            }            
        }

        /**
        * Determine gesture class of the given sample.
        * The gesture id is returned.
        *
        * This function can be modified to return an n-best list, but
        * because of early rejection, such a list may not make sense.
        *
        */
        public int Classify(List<Vector> trajectory)
        {
            JackknifeFeatures features = new JackknifeFeatures(Blades, trajectory);
            int templateCnt = templates.Count;
            for (int t = 0; t < templateCnt; t++)
            {
                double cf = 1;
                if (Blades.CFAbsDistance)
                {
                    cf *= 1 / Math.Max(0.01, features.Abs.Dot(templates[t].Features.Abs));
                }

                if (Blades.CFBbWidths)
                {
                    cf *= 1 / Math.Max(0.01, features.Bb.Dot(templates[t].Features.Bb));
                }

                JackknifeTemplate temp = templates[t];
                temp.CF = cf;
                templates[t] = temp;

                if (Blades.LowerBound)
                {
                    JackknifeTemplate tempLB = templates[t];
                    tempLB.LB = cf * LowerBound(features.Vecs, templates[t]);
                    templates[t] = tempLB;
                }
            }

            templates.Sort(new SortTemplate());

            double best = double.PositiveInfinity;
            int ret = -1;
            for (int t = 0; t < templateCnt; t++)
            {
                if (templates[t].LB > templates[t].RejectionThreshold)
                    continue;

                if (templates[t].LB > best)
                    continue;

                double score = templates[t].CF;
                score *= DTW(features.Vecs, templates[t].Features.Vecs);
                if (score > templates[t].RejectionThreshold)
                    continue;
                if (score < best)
                {
                    best = score;
                    ret = templates[t].GestureId;
                }
            }

            return ret;

        }

        public int Classify(Sample sample)
        {
            return Classify(sample.Trajectory);
        }

        /**
         * Find the lower bound score for a candidate
         * against a given template.
         */
        private double LowerBound(List<Vector> vecs, JackknifeTemplate t)
        {
            double lb = 0.0; // lower bound
            int component_cnt = vecs[0].Size;

            for (int i = 0; i < vecs.Count; i++)
            {
                double cost = 0.0;

                for (int j = 0; j < component_cnt; j++)
                {
                    if (Blades.InnerProduct)
                    {
                        if (vecs[i][j] < 0.0)
                        {
                            cost += vecs[i][j] * t.Lower[i][j];
                        }
                        else
                        {
                            cost += vecs[i][j] * t.Upper[i][j];
                        }
                    }
                    else if (Blades.EuclideanDistance)
                    {
                        double diff = 0.0;

                        if (vecs[i][j] < t.Lower[i][j])
                        {
                            diff = vecs[i][j] - t.Lower[i][j];
                        }
                        else if (vecs[i][j] > t.Upper[i][j])
                        {
                            diff = vecs[i][j] - t.Upper[i][j];
                        }

                        cost += (diff * diff);
                    }
                    else
                        throw new Exception("Should not get here!");
                }

                // inner products are bounded
                if (Blades.InnerProduct)
                    cost = 1.0 - Math.Min(1.0, Math.Max(-1.0, cost));

                lb += cost;
            }

            return lb;
        }

        /**
         * The DTW algorithm. Awesome!
         */
        private double DTW(List<Vector> v1, List<Vector> v2)
        {
            // Initialize the cost matrix if not already initialize
            if (cost == null)
            {
                cost = new List<List<double>>();

                for (int i = 0; i < v1.Count + 1; i++)
                {
                    List<double> row = new List<double>();

                    for (int j = 0; j < v2.Count + 1; j++)
                        row.Add(double.PositiveInfinity);

                    cost.Add(row);
                }
            }
            else
            {
                for (int i = 0; i < v1.Count + 1; i++)
                    for (int j = 0; j < v2.Count + 1; j++)
                        cost[i][j] = double.PositiveInfinity;
            }


            cost[0][0] = 0;

            // using DP to find solution
            for (int i = 1; i <= v1.Count; i++)
            {
                for (int j = Math.Max(1, i - Blades.Radius); j <= Math.Min(v2.Count, i + Blades.Radius); j++)
                {
                    // pick minimum cost path (neighbor) to
                    // extend to this ii, jj element
                    double minimum = Math.Min(Math.Min(cost[i - 1][j],      // repeat v1 element
                                                       cost[i][j - 1]),     // repeat v2 element
                                                       cost[i - 1][j - 1]); // don't repeat either
                    cost[i][j] = minimum;
                    if (Blades.InnerProduct)
                    {
                        cost[i][j] += 1 - v1[i - 1].Dot(v2[j - 1]);
                    }
                    else if (Blades.EuclideanDistance)
                    {
                        cost[i][j] += v1[i - 1].L2Norm2(v2[j - 1]);
                    }
                    else
                        throw new Exception("Should not get here!");
                }
            }

            return cost[v1.Count][v2.Count];
        }
    }
}