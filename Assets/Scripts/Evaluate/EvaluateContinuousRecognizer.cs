using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Jackknife;

namespace Machete
{
    public class Evaluation
    {

        public static void PrintMacheteStats(Machete machete)
        {
            Debug.Log(string.Format("Yeah: {0} templates", machete.templates.Count));            
        }

        public static Results EvaluateSession(DeviceType device, int subject_id)
        {
            configuartion_parameters_t parameneters = new configuartion_parameters_t(device);
            
            // Load subject dataset
            Dataset ds = Global.load_subject_dataset(device, subject_id);
            List<Sample> train_set = Global.GetTrainSet(ds, 1);            
            // Covert the dataset to format accepted by Jackknife
            List<Jackknife.Sample> jk_train_set = JackknifeConnector.GetJKTrainSet(train_set);

            // Load subject session
            List<Frame> frames = new List<Frame>();
            Global.load_session(device, subject_id, frames, ds);
            //Debug.Log("Frame_cnt = " + frames.Count());
            // Load ground truth
            List<GestureCommand> cmds = new List<GestureCommand>();
            GestureCommand.GetAllCommands(cmds, ds, device, subject_id);

            // Train the segmentor
            ContinuousResultOptions cr_options = new ContinuousResultOptions();
            
            //COREY FIX latency framecount
            cr_options.latencyFrameCount = 1;
            Machete yeah = new Machete(device, cr_options);
            foreach (Sample s in train_set)
            {
                yeah.AddSample(s, filtered: true);
            }

            //PrintYeahStats(yeah);

            // Train the recognizer
            JackknifeBlades blades = new JackknifeBlades();
            blades.SetIPDefaults();
            
            //COREY FIX RESAMPLECNT
            blades.ResampleCnt = 20;
            blades.LowerBound = false;

            Jackknife.Jackknife jk = new Jackknife.Jackknife(blades);
            foreach (Jackknife.Sample s in jk_train_set)
            {
               jk.AddTemplate(s);
            }

            // Set between 2.0 and 10.0 in steps of .25
            // to find the best result
            // Best at 7.5 with around 66% :/
            jk.SetRejectionThresholds(7.0);


            // Set up filter for session points
            ExponentialMovingAverage ema_filter = new ExponentialMovingAverage(frames[0].pt, 5.0);
            Vector pt;
            List<Vector> video = new List<Vector>();

            List<RecognitionResult> rresults = new List<RecognitionResult>();

            int triggered_count = 0;

            // Go through session
            for (int session_pt = 0; session_pt < frames.Count; session_pt++)
            {
                long ts1 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // at beginning

                List<ContinuousResult> continuous_results = new List<ContinuousResult>();

                pt = ema_filter.Filter(frames[session_pt].pt, 1 / (double)parameneters.fps);

                long ts2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // after filter

                video.Add(pt);

                //Debug.Log(string.Format("Pt: {0} {1} {2}", pt.Data[0], pt.Data[1], pt.Data[2]));

                yeah.ProcessFrame(pt, session_pt, continuous_results);

                long ts3 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // after processing frame

                bool cancel_if_better_score = false;
                ContinuousResult result = ContinuousResult.SelectResult(
                    continuous_results,
                    cancel_if_better_score);

                long ts4 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // after looking for result

                //Debug.Log(string.Format("{0} {1} {2}", ts2 - ts1, ts3 - ts2, ts4 - ts3));
                //Debug.Log(string.Format("FRAME NO: {0}", frame_no));

                if (result == null) { continue; }
                
                
                //COREY FIX For comparing against the original code
                //if (result.sample.GestureId == 0)
                //    Debug.Log(string.Format("start {0}, end {1}", result.startFrameNo, result.endFrameNo + 1));
                
                triggered_count += 1;

                //Debug.Log(string.Format("Frame: {3} Result: {0}, Sample: {1}, Score: {2}", result.gid, result.sample.GestureName, result.score, frame_no));
                //Debug.Log(string.Format("Best result as of: {0} {1}", ii, yeah.bestScore));

                // Run recognizer on segmented result
                double recognizer_d = 0.0f;
                bool match = false;

                // Save a buffer to pass to recognizer
                List<Jackknife.Vector> jkbuffer = JackknifeConnector.GetJKBufferFromVideo(
                    video,
                    result.startFrameNo,
                    result.endFrameNo);
                
                long ts5 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // before passing to recognizer
                
                match = jk.IsMatch(jkbuffer, result.sample.GestureId, out recognizer_d);

                //COREY Fix print out scores

                if (result.sample.GestureId == 0)
                {
                    //Debug.Log(string.Format("start {0}, end {1} ", result.startFrameNo, result.endFrameNo + 1) + string.Format("Is match = {0}, score = {1}",
                    //    match,
                    //    recognizer_d));
                }
                // Matched to template with this gid
                if (match == false)
                {
                    continue;
                }

                long ts6 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // after classifying

                // Gesture was accepted
                RecognitionResult rresult = new RecognitionResult();
                rresult.gid = result.gid;
                rresult.start = result.startFrameNo;
                rresult.end = result.endFrameNo;
                rresult.score = recognizer_d;
            
                match = false;

                for(int ii = 0; ii < rresults.Count; ii++)
                {
                    if (rresults[ii].Update(rresult) == true)
                    {
                        match = true;
                        break;
                    }
                }

                // if some result was updated for better, continue
                if (match == true)
                {
                    continue;
                }

                rresults.Add(rresult);
            }

            // Per gesture confusion matrix
            List<ConfisionMatrix> cm = new List<ConfisionMatrix>();
            for (int ii = 0; ii < ds.Gestures.Count; ii++)
            {
                cm.Add(new ConfisionMatrix());
            }

            for(int ii = 0; ii < rresults.Count; ii++)
            {
                RecognitionResult result = rresults[ii];

                bool found = false;
                int cidx = 0;

                for (cidx = 0; cidx < cmds.Count; cidx++)
                {
                    found = cmds[cidx].Hit(result);

                    if (found == true)
                    {
                        break;
                    }
                }

                if (found == true)
                {
                    // true positive
                    if (cmds[cidx].detected == false)
                    {
                        cmds[cidx].detected = true;
                        cm[result.gid].tp += 1.0f;
                    }
                }
                else
                {
                    bool bad = GestureCommand.IsBadCommand(
                        frames,
                        result);

                    if (bad == true)
                    {
                        continue;
                    }
                    // false positive
                    cm[result.gid].fp += 1.0f;
                }
            }

            // false negatives
            for(int cidx = 0; cidx < cmds.Count; cidx ++)
            {
                if (cmds[cidx].detected == true)
                {
                    continue;
                }

                cm[cmds[cidx].gid].fn += 1.0;
            }

            Results ret = new Results();
            for(int ii = 0; ii < cm.Count; ii++)
            {                
                ret.AppendResults(cm[ii]);
                //temp += string.Format("{5}:\t A: {0:N6}, E: {1:N6}, P: {2:N6}, R: {3:N6}, F1: {4:N6}\n", ret.accuracy/ret.total, ret.error / ret.total, ret.precision / ret.total, ret.recall / ret.total, ret.f1_0 / ret.total, ii);
            }
            //Debug.Log(temp);

            return ret;
        }

        public static Results EvaluateSessionWindowed(DeviceType device, int subject_id)
        {
            configuartion_parameters_t parameneters = new configuartion_parameters_t(device);
            
            // Load subject dataset
            Dataset ds = Global.load_subject_dataset(device, subject_id);
            List<Sample> train_set = Global.GetTrainSet(ds, 1);

            // Covert the dataset to format accepted by Jackknife
            List<Jackknife.Sample> jk_train_set = JackknifeConnector.GetJKTrainSet(train_set);

            // Load subject session
            List<Frame> frames = new List<Frame>();
            Global.load_session(device, subject_id, frames, ds);

            // Load ground truth
            List<GestureCommand> cmds = new List<GestureCommand>();
            GestureCommand.GetAllCommands(cmds, ds, device, subject_id);

            // Train the recognizer
            JackknifeBlades blades = new JackknifeBlades();         
            blades.SetIPDefaults();

            blades.ResampleCnt = 20;

            Jackknife.Jackknife jk = new Jackknife.Jackknife(blades);
            foreach (Jackknife.Sample s in jk_train_set)
            {
               jk.AddTemplate(s);
            }

            // Set between 2.0 and 10.0 in steps of .25
            // to find the best result
            jk.SetRejectionThresholds(5.25f);

            // Set up filter for session points
            ExponentialMovingAverage ema_filter = new ExponentialMovingAverage(frames[0].pt);
            Vector pt;

            WindowSegmentor windowSegmentor = new WindowSegmentor(jk);

            //List<RecognitionResult> rresults = new List<RecognitionResult>();

            List<ContinuousResult> continuous_results = new List<ContinuousResult>();

            // Go through session
            for (int session_pt = 0; session_pt < frames.Count; session_pt++)
            {
                long ts1 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // at beginning

                pt = ema_filter.Filter(frames[session_pt].pt, 1 / (double)parameneters.fps);

                long ts2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // after filter

                Jackknife.Vector jkpt = JackknifeConnector.ToJKVector(pt);
                
                windowSegmentor.Update(jkpt);
                windowSegmentor.Segment(continuous_results);

                if (session_pt % 2000 == 0)
                    Debug.Log(string.Format("{0}% Done", (double)session_pt / (double) frames.Count * 100.0));
            }

            foreach (ContinuousResult cr in continuous_results)
            {
                Debug.Log(string.Format("st {0}, en {1}, gid {2}", cr.startFrameNo, cr.endFrameNo, cr.gid));

            }
            // Per gesture confusion matrix
            List<ConfisionMatrix> cm = new List<ConfisionMatrix>();
            for (int ii = 0; ii < ds.Gestures.Count; ii++)
            {
                cm.Add(new ConfisionMatrix());
            }

            for (int ii = 0; ii < continuous_results.Count; ii++)
            {
                ContinuousResult result = continuous_results[ii];

                bool found = false;
                int cidx = 0;

                for (cidx = 0; cidx < cmds.Count; cidx++)
                {
                    found = cmds[cidx].Hit(result);

                    if (found == true)
                    {
                        break;
                    }
                }

                if (found == true)
                {
                    // true positive
                    if (cmds[cidx].detected == false)
                    {
                        cmds[cidx].detected = true;
                        cm[result.gid].tp += 1.0f;
                    }
                }
                else
                {
                    bool bad = GestureCommand.IsBadCommand(
                        frames,
                        result);

                    if (bad == true)
                    {
                        continue;
                    }
                    // false positive
                    cm[result.gid].fp += 1.0f;
                }
            }

            // false negatives
            for (int cidx = 0; cidx < cmds.Count; cidx++)
            {
                if (cmds[cidx].detected == true)
                {
                    continue;
                }

                cm[cmds[cidx].gid].fn += 1.0;
            }

            Results ret = new Results();

            for (int ii = 0; ii < cm.Count; ii++)
            {
                ret.AppendResults(cm[ii]);
            }

            ret.PrintF();
            return ret;
        }

        public static Results EvaluateContinuousRecognizer(DeviceType device)
        {
            // Get the list of participants 15 through 24
            List<int> participants = new List<int> { 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 };

            Results all_results = new Results();
            all_results.Reset();

            long one = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // at beginning
            Results user_results = new Results();
            for (int pid = 0; pid < participants.Count; pid++)
            {
                Debug.Log("Running Yeah evaluation on Participant " + participants[pid]);
                user_results.Reset();
                for (int ii = 0; ii < 10; ii++)
                {
                    Results tempResults = EvaluateSession(device, participants[pid]);
                    tempResults.PrintF();
                    user_results.AppendResults(tempResults);
                }
                all_results.AppendResults(user_results);
                Debug.Log("Results for Participant " + participants[pid]);
                user_results.PrintF();
            }

            long two = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // at beginning
            
            Debug.Log(string.Format("Time elapsed {0} seconds", (two - one) / 1000));
            Debug.Log("Overall results");

            all_results.PrintF();
            return all_results;
        }
    }

}


