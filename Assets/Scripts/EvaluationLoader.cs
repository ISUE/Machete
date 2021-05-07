using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using Jackknife;
using System.IO;

namespace Machete
{
    public enum DeviceType
    {
        KINECT = 0,
        MOUSE = 8,
        VIVE_POSITION = 2,
        VIVE_QUATERNION = 3,
    };

    public enum SegmentorType
    {
        MACHETE = 0,
        WINDOW = 1,
    }

    public enum WindowMode
    {
        MIN = 0,
        MAX = 1,
        MIN_MAX = 2,
        MIN_MID_MAX = 3,
        MID = 4,
    }

    public class EvaluationLoader : MonoBehaviour
    {
        enum EvaluationStatus
        {
            LOADING = 0,
            TRAINING = 1,
            EVALUATING = 2,
            SUMMARIZING = 3,
            TRANSITION = 4,
            FINISHED = 5,
        }

        // Inspector Evaluation Settings
        [Range(1, 5)] public int trainCount = 1;
        [Range(1, 100)] public int iterationCount = 1;
        public DeviceType deviceType = DeviceType.KINECT;
        public SegmentorType segmentorType = SegmentorType.MACHETE;
        public WindowMode windowMode = WindowMode.MIN;

        // Dataset loading
        List<int> participants;
        List<Dataset> participantsDataset;
        List<List<Sample>> trainSets;
        List<List<Frame>> participantsFrames;
        List<List<GestureCommand>> participantsCommands;
        List<List<Jackknife.Sample>> jkTrainSets;
        configuartion_parameters_t parameters;

        ContinuousResultOptions cr_options;
        Results iteration_results;
        Results all_results;
        Results user_results;

        private double knownThreshold;

        // In-Update interation variables
        Machete machete;
        Window window;
        JackknifeBlades blades;
        Jackknife.Jackknife jackknife;
        int currentParticipantID;
        int currentParticipantIndex;
        int iteration;
        int frame_idx;
        EvaluationStatus eStatus;
        List<Frame> frames;
        ExponentialMovingAverage ema_filter;
        List<Vector> video;
        List<RecognitionResult> rresults;
        private Vector last_video_vector;

        double timer = 0.0;

        RunningStatistics timeStats_UserDependent;
        RunningStatistics timeStats_Overall;

        void Start()
        {
            eStatus = EvaluationStatus.LOADING;
            PrintStatus();
            Global.App_datapath = Application.dataPath;
            cr_options = new ContinuousResultOptions();
            cr_options.latencyFrameCount = 1;
            parameters = new configuartion_parameters_t(deviceType);
            knownThreshold = Global.GetDatasetThreshold(deviceType);

            iteration_results = new Results();
            all_results = new Results();
            user_results = new Results();

            //
            // Load all data
            //
            participants = Global.GetParticipantList(deviceType);
            participantsDataset = new List<Dataset>();
            trainSets = new List<List<Sample>>();
            participantsFrames = new List<List<Frame>>();
            participantsCommands = new List<List<GestureCommand>>();
            jkTrainSets = new List<List<Jackknife.Sample>>();

            long ts1 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // at beginning
            foreach (int external_PID in participants)
            {
                Dataset dataset = Global.load_subject_dataset(deviceType, external_PID);
                participantsDataset.Add(dataset);

                List<Frame> loadedFrames = new List<Frame>();
                Global.load_session(deviceType, external_PID, loadedFrames, dataset);
                participantsFrames.Add(loadedFrames);

                List<GestureCommand> commands = new List<GestureCommand>();
                GestureCommand.GetAllCommands(commands, dataset, deviceType, external_PID);
                participantsCommands.Add(commands);

                List<Sample> trainSet = Global.GetTrainSet(dataset, trainCount);
                trainSets.Add(trainSet);

                List<Jackknife.Sample> jkTrainSet = JackknifeConnector.GetJKTrainSet(trainSet);
                jkTrainSets.Add(jkTrainSet);
            }

            long ts2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond; // after loading
            Debug.Log(string.Format("Loaded participants data. Time elapsed: {0}s", (ts2 - ts1) / 1000.0));

            iteration = 0;
            currentParticipantIndex = 0;
            currentParticipantID = participants[currentParticipantIndex];
            eStatus = EvaluationStatus.TRAINING;

            timeStats_Overall = new RunningStatistics();
            timeStats_UserDependent = new RunningStatistics();
        }

        void ResetParticipantsCommands()
        {
            for (int p = 0; p < participantsCommands.Count; p++)
            {
                for (int c = 0; c < participantsCommands[p].Count; c++)
                {
                    participantsCommands[p][c].Reset();
                }
            }
        }

        void GetNewParticipantSamples()
        {
            List<Sample> trainSet = Global.GetTrainSet(participantsDataset[currentParticipantIndex], trainCount);
            trainSets[currentParticipantIndex] = trainSet;

            List<Jackknife.Sample> jkTrainSet = JackknifeConnector.GetJKTrainSet(trainSet);
            jkTrainSets[currentParticipantIndex] = jkTrainSet;
        }

        void Update()
        {
            if (eStatus == EvaluationStatus.TRAINING)
            {
                if (segmentorType == SegmentorType.MACHETE)
                {
                    Prepare();
                }
                else if (segmentorType == SegmentorType.WINDOW)
                {
                    PrepareWindow();
                }

                eStatus = EvaluationStatus.EVALUATING;
            }

            if (eStatus == EvaluationStatus.EVALUATING)
            {
                if (frame_idx > 1)
                {
                    // Time since last frame
                    timer += Time.deltaTime; // Total time
                    timeStats_UserDependent.add(Time.deltaTime);
                    timeStats_Overall.add(Time.deltaTime);
                }

                if (segmentorType == SegmentorType.MACHETE)
                {
                    Step();
                }
                else if (segmentorType == SegmentorType.WINDOW)
                {
                    StepWindow();
                }

                frame_idx += 1;
                if (frame_idx == frames.Count)
                {
                    eStatus = EvaluationStatus.SUMMARIZING;
                }
            }

            if (eStatus == EvaluationStatus.SUMMARIZING)
            {
                Summarize();
                eStatus = EvaluationStatus.TRANSITION;
            }

            if (eStatus == EvaluationStatus.TRANSITION)
            {
                iteration += 1;

                Debug.Log(string.Format("Iteration {2} User# {0}, (Participant {1}) Completed.\n", 
                                            currentParticipantIndex, currentParticipantID, iteration));
                iteration_results.PrintF();
                user_results.AppendResults(iteration_results);
                // PrintTimes();
                iteration_results.Reset();

                // If all iterations are done, continue to next participant
                if (iteration == iterationCount)
                {
                    // Save results into a file and to all results
                    logResultsToFile("stats.csv", user_results, timeStats_UserDependent, "PID " + currentParticipantID);
                    all_results.AppendResults(user_results);

                    iteration = 0;
                    currentParticipantIndex += 1;

                    timeStats_UserDependent.reset();
                    user_results.Reset();
                }


                // If all participants done, we're finished
                if (currentParticipantIndex == participants.Count)
                {
                    Debug.Log("FINISHED");
                    eStatus = EvaluationStatus.FINISHED;
                    all_results.PrintF();

                    logResultsToFile("stats.csv", all_results, timeStats_Overall, "global");

                    timeStats_Overall.reset();
                    return;
                }

                eStatus = EvaluationStatus.TRAINING;
            }

            if (eStatus == EvaluationStatus.FINISHED)
            {
            }
        }

        /**
         * Calculate and display the results
         */
        void Summarize()
        {
            // Per gesture confusion matrix
            List<ConfisionMatrix> cm = new List<ConfisionMatrix>();
            for (int ii = 0; ii < participantsDataset[currentParticipantIndex].Gestures.Count; ii++)
            {
                cm.Add(new ConfisionMatrix());
            }

            for (int ii = 0; ii < rresults.Count; ii++)
            {
                RecognitionResult result = rresults[ii];

                bool found = false;
                int cidx;
                for (cidx = 0; cidx < participantsCommands[currentParticipantIndex].Count; cidx++)
                {
                    found = participantsCommands[currentParticipantIndex][cidx].Hit(result);

                    if (found)
                    {
                        break;
                    }
                }

                if (found)
                {
                    // true positive
                    if (participantsCommands[currentParticipantIndex][cidx].detected == false)
                    {
                        participantsCommands[currentParticipantIndex][cidx].detected = true;
                        cm[result.gid].tp += 1.0f;
                    }
                }
                else
                {
                    bool bad = GestureCommand.IsBadCommand(
                        frames,
                        result);

                    if (bad)
                    {
                        continue;
                    }

                    // false positive
                    cm[result.gid].fp += 1.0f;
                }
            }

            rresults.Clear();

            // false negatives
            for (int cidx = 0; cidx < participantsCommands[currentParticipantIndex].Count; cidx++)
            {
                if (participantsCommands[currentParticipantIndex][cidx].detected)
                {
                    continue;
                }

                cm[participantsCommands[currentParticipantIndex][cidx].gid].fn += 1.0;
            }

            for (int ii = 0; ii < cm.Count; ii++)
            {
                iteration_results.AppendResults(cm[ii]);
            }
        }

        /**
         * Step forward into calculations. Get next point, filter, segment, recognize
         */
        void Step()
        {
            List<ContinuousResult> continuousResults = new List<ContinuousResult>();
            
            Vector pt = ema_filter.Filter(frames[frame_idx].pt, 1 / (double) parameters.fps);
            video.Add(pt);

            if (frame_idx == 0)
            {
                last_video_vector = pt;
            }
            
            // Check if moved "far enough" if mouse
            if (frame_idx > 1 && deviceType == DeviceType.MOUSE)
            {
                Vector vec = pt - last_video_vector;
                double weight = vec.Length();
                
                if (weight <= 2.0) 
                    return;
            
                last_video_vector = pt;
            }
            

            machete.ProcessFrame(pt, frame_idx, continuousResults);

            bool cancel_if_better_score = false;
            ContinuousResult result = ContinuousResult.SelectResult(
                continuousResults,
                cancel_if_better_score);

            // No trigger, return
            if (result == null)
            {
                return;
            }

            List<Jackknife.Vector> jkbuffer = JackknifeConnector.GetJKBufferFromVideo(
                video,
                result.startFrameNo,
                result.endFrameNo);

            // Check if there was a match
            double recognizer_d = 0.0f;
            bool match;
            match = jackknife.IsMatch(jkbuffer, result.sample.GestureId, out recognizer_d);

            if (match == false)
            {
                return;
            }

            RecognitionResult rresult = new RecognitionResult();
            rresult.gid = result.gid;
            rresult.start = result.startFrameNo;
            rresult.end = result.endFrameNo;
            rresult.score = recognizer_d;

            match = false;

            for (int ii = 0; ii < rresults.Count; ii++)
            {
                if (rresults[ii].Update(rresult))
                {
                    match = true;
                    break;
                }
            }

            // if some result was updated for better, continue
            if (match)
            {
                return;
            }

            rresults.Add(rresult);
        }

        private void StepWindow()
        {
            List<ContinuousResult> continuous_results = new List<ContinuousResult>();
            
            Vector pt = ema_filter.Filter(frames[frame_idx].pt, 1 / (double) parameters.fps);
            video.Add(pt);

            // Get the trajectory and pass it to window
            for (double size = window.minimum; size <= window.maximum; size += window.step_size)
            {
                List<Jackknife.Vector> trajectory;
                int start = frame_idx - (int) size + 1;
                int end = frame_idx;

                if (start < 0)
                {
                    return;
                }

                trajectory = JackknifeConnector.GetJKBufferFromVideo(video, start, end);
                jackknife.ClassForWinCustTemplLen(trajectory, continuous_results, start, end);

                for (int rr = 0; rr < continuous_results.Count; rr++)
                {
                    ContinuousResult result = continuous_results[rr];
                    RecognitionResult rresult = new RecognitionResult();
                    rresult.gid = result.gid;
                    rresult.start = start;
                    rresult.end = end;
                    rresult.score = result.score;

                    bool match = false;

                    for (int ii = 0; ii < rresults.Count; ii++)
                    {
                        if (rresults[ii].Update(rresult))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (match)
                    {
                        continue;
                    }

                    rresults.Add(rresult);
                }
            }
        }

        /**
         * Train segmentor and recognizer with next participant's data
         */
        void Prepare()
        {
            ResetParticipantsCommands();
            GetNewParticipantSamples();

            machete = new Machete(deviceType, cr_options);
            foreach (Sample sample in trainSets[currentParticipantIndex])
            {
                machete.AddSample(sample, filtered: true);
            }

            blades = new JackknifeBlades();
            blades.SetIPDefaults();
            blades.ResampleCnt = Global.GetResampleCnt(deviceType);

            blades.Radius = (int) (blades.ResampleCnt * .1f);
            
            blades.LowerBound = false;
            jackknife = new Jackknife.Jackknife(blades);
            foreach (Jackknife.Sample sample in jkTrainSets[currentParticipantIndex])
            {
                jackknife.AddTemplate(sample);
            }

            jackknife.SetRejectionThresholds(knownThreshold);

            frames = participantsFrames[currentParticipantIndex];
            
            double cutoff = 3.0;
            if (deviceType == DeviceType.MOUSE)
                cutoff = 5.0;
            ema_filter = new ExponentialMovingAverage(frames[0].pt, cutoff);

            video = new List<Vector>();

            rresults = new List<RecognitionResult>();

            frame_idx = 0;

            currentParticipantID = participants[currentParticipantIndex];

            timer = 0.0;
        }

        void PrepareWindow()
        {
            ResetParticipantsCommands();

            // Set up Jackknife first because window will use it's values
            blades = new JackknifeBlades();
            blades.SetIPDefaults();
            blades.ResampleCnt = Global.GetResampleCnt(deviceType);
            
            blades.Radius = (int) (blades.ResampleCnt * .1f);
            
            blades.LowerBound = false;
            jackknife = new Jackknife.Jackknife(blades);
            foreach (Jackknife.Sample sample in jkTrainSets[currentParticipantIndex])
            {
                jackknife.AddTemplate(sample);
            }

            jackknife.SetRejectionThresholds(knownThreshold);

            frames = participantsFrames[currentParticipantIndex];
            
            double cutoff = 3.0;
            if (deviceType == DeviceType.MOUSE)
                cutoff = 5.0;
            ema_filter = new ExponentialMovingAverage(frames[0].pt, cutoff);

            video = new List<Vector>();

            rresults = new List<RecognitionResult>();

            frame_idx = 0;

            currentParticipantID = participants[currentParticipantIndex];

            window = new Window(jackknife, m: (int) windowMode);

            timer = 0.0;
        }

        /**
         * Print current evaluationStatus
         */
        void PrintStatus()
        {
            switch (eStatus)
            {
                case EvaluationStatus.LOADING:
                    Debug.Log("Loading all participants data");
                    break;
                case EvaluationStatus.TRAINING:
                    Debug.Log(string.Format("PREPARING PID {0}", currentParticipantID));
                    break;
                case EvaluationStatus.EVALUATING:
                    Debug.Log(string.Format("EVAULATING PID {0}", currentParticipantID));
                    break;
                case EvaluationStatus.SUMMARIZING:
                    Debug.Log(string.Format("SUMMARIZING PID {0}", currentParticipantID));
                    break;
                case EvaluationStatus.TRANSITION:
                    Debug.Log(string.Format("COMPLETED PID {0}", currentParticipantID));
                    break;
            }
        }

        string PrintTimes()
        {
            double f = 1000.0; // factor
            string s = string.Format("TIMES {0}, iter: {1}, PID: {2}\n",
                segmentorType == SegmentorType.MACHETE ? "MACHETE" : "WINDOW", iteration, currentParticipantID);
            s += string.Format("Avg frame time: {0} ms\n", timeStats_UserDependent.mean * f);
            s += string.Format("Total frame time: {0} ms\n", timer * f);
            s += string.Format("Min: {0} ms, Max: {1} ms\n", timeStats_UserDependent.minimum * f,
                timeStats_UserDependent.maximum * f);
            s += string.Format("std: {0} ms, var: {1} ms\n", timeStats_UserDependent.std * f,
                timeStats_UserDependent.variance * f);
            s += string.Format("95% ci: {0} ms, {1} ms\n", timeStats_UserDependent.ci_lower() * f,
                timeStats_UserDependent.ci_upper() * f);
            Debug.Log(s);
            return s;
        }

        string PrintGlobalTimes()
        {
            double f = 1000.0; // factor
            string s = string.Format("TIMES {0}, TRAIN COUNT {1},  GLOBAL\n",
                segmentorType == SegmentorType.MACHETE ? "MACHETE" : "WINDOW", trainCount);
            s += string.Format("Avg frame time: {0} ms\n", timeStats_Overall.mean * f);
            s += string.Format("Min: {0} ms, Max: {1} ms\n", timeStats_Overall.minimum * f,
                timeStats_Overall.maximum * f);
            s += string.Format("std: {0} ms, var: {1} ms\n", timeStats_Overall.std * f, timeStats_Overall.variance * f);
            s += string.Format("95% ci: {0} ms, {1} ms\n", timeStats_Overall.ci_lower() * f,
                timeStats_Overall.ci_upper() * f);
            Debug.Log(s);

            return s;
        }

        void logResultsToFile(string fname, Results results, RunningStatistics timeStats, string label)
        {
            double f = 1000.0;
            StreamWriter outfile = new StreamWriter(fname, true);
            outfile.Write((segmentorType == SegmentorType.MACHETE ? "MACHETE" : "WINDOW") + "," + label + "," +
                          trainCount + ",");
            outfile.Write(string.Format("{0:F4},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4}", (timeStats.mean * f),
                (timeStats.minimum * f), (timeStats.maximum * f), (timeStats.std * f), (timeStats.variance * f),
                (timeStats.ci_lower() * f), (timeStats.ci_upper() * f)));
            outfile.Write((segmentorType == SegmentorType.MACHETE ? ",-1" : string.Format(", {0}", window.mode)));
            outfile.WriteLine();

            outfile.Close();
        }
    }
}
